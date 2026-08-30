using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BestandService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IBestandService
{
    private static readonly BestandskorrekturValidator Validator = new();

    public async Task<IReadOnlyList<ArtikelBestandDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikelQuery = db.Artikel.AsNoTracking().Where(a => a.IstLagerartikel && !a.Gesperrt).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            artikelQuery = artikelQuery.Where(a => EF.Functions.Like(a.Bezeichnung, $"%{s}%") || EF.Functions.Like(a.Artikelnummer, $"%{s}%"));
        }

        var artikel = await artikelQuery.ToListAsync(ct);
        var lagerorte = await db.Lagerorte.AsNoTracking().Where(l => l.Aktiv).ToListAsync(ct);
        var bestaende = await db.ArtikelBestaende.AsNoTracking()
            .Include(b => b.Sektion).Include(b => b.Kulturstufe)
            .ToListAsync(ct);
        var bestaendeJeArtikelUndLagerort = bestaende.ToLookup(b => (b.ArtikelId, b.LagerortId));

        // Left-Join Artikel x Lagerorte gegen ArtikelBestaende (in-memory kombiniert, da nur kleine/mittlere Zeilenzahlen
        // erwartet werden): jede Kombination lagerfähiger Artikel x aktiver Lagerort erzeugt eine Zeile, auch ohne
        // existierenden ArtikelBestand-Datensatz (Menge = 0) — sonst ist der allererste Erstbestand über die UI nicht anlegbar.
        // Ein Feld kann je Artikel MEHRERE Bestandszeilen haben (Sektion × Kulturstufe, Phase 8) — dann entsteht
        // eine Zeile je tatsächlich existierender Kombination statt einer aggregierten Summe.
        var ergebnis = new List<ArtikelBestandDto>();
        foreach (var a in artikel)
        {
            foreach (var l in lagerorte)
            {
                var zeilen = bestaendeJeArtikelUndLagerort[(a.Id, l.Id)].ToList();
                if (zeilen.Count == 0)
                {
                    ergebnis.Add(new ArtikelBestandDto(a.Id, a.Artikelnummer, a.Bezeichnung, a.HatSeriennummern, l.Id, l.Bezeichnung, 0m, a.Mindestbestand, IstKulturpflanze: a.IstKulturpflanze));
                    continue;
                }

                foreach (var b in zeilen)
                {
                    ergebnis.Add(new ArtikelBestandDto(
                        a.Id, a.Artikelnummer, a.Bezeichnung, a.HatSeriennummern, l.Id, l.Bezeichnung, b.Menge, a.Mindestbestand,
                        b.SektionId, b.Sektion?.Bezeichnung, b.KulturstufeId, b.Kulturstufe?.Bezeichnung, a.IstKulturpflanze));
                }
            }
        }

        return ergebnis.OrderBy(b => b.Artikelnummer).ThenBy(b => b.LagerortBezeichnung).ThenBy(b => b.SektionBezeichnung).ToList();
    }

    public async Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BucheBewegungAsync(db, dto.ArtikelId, dto.LagerortId, dto.MengeDelta, LagerbewegungTyp.Korrektur, belegPositionId: null, ct, dto.SektionId, dto.KulturstufeId);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Einziger Schreibpfad auf Bestand — ein atomares UPDATE (kein Read-Modify-Write), Negativbestand ist hart gesperrt.
    /// Läuft innerhalb der Transaktion des Aufrufers (Aufrufer öffnet/committet); wiederverwendbar von Bestandskorrektur,
    /// Lieferschein-Buchen, Inventur-Abschluss, Kulturbuchungen (Phase 8). sektionId/kulturstufeId bleiben bei Default (null)
    /// für jeden Aufrufer, der die beiden Dimensionen nicht kennt — Handelsware verhält sich exakt wie vor Phase 8.</summary>
    internal static async Task BucheBewegungAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal mengeDelta,
        LagerbewegungTyp typ, int? belegPositionId, CancellationToken ct,
        int? sektionId = null, int? kulturstufeId = null)
    {
        // Eine vorgelagerte Abfrage (per Subquery-Projektion ein einziger Round-Trip) lädt Artikel.IstKulturpflanze
        // und ob der Lagerort aktive Sektionen hat — Grundlage für die zentralen Dimensionsregeln (KulturRegeln).
        // Das macht die Regeln unumgehbar: es gibt keinen zweiten Schreibpfad auf ArtikelBestaende.
        var kontext = await db.Artikel
            .Where(a => a.Id == artikelId)
            .Select(a => new
            {
                a.IstKulturpflanze,
                LagerortHatSektionen = db.Sektionen.Any(s => s.LagerortId == lagerortId && s.Aktiv),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Artikel {artikelId} nicht gefunden.");

        KulturRegeln.PruefeDimensionen(kontext.IstKulturpflanze, kontext.LagerortHatSektionen, sektionId, kulturstufeId);

        var betroffeneZeilen = await AusfuehrenUpdateAsync(db, artikelId, lagerortId, sektionId, kulturstufeId, mengeDelta, ct);

        if (betroffeneZeilen == 0)
        {
            if (mengeDelta < 0)
                throw new InvalidOperationException("Nicht genügend Bestand vorhanden — Buchung würde negativen Bestand erzeugen.");

            // E4: Standard-Upsert-Muster statt db.ArtikelBestaende.Add(...) — zwei parallele Erstbuchungen auf
            // dieselbe Kombination sähen sonst beide 0 betroffene Zeilen, fügten beide ein, und die zweite
            // Transaktion stürbe am Unique-Index (unverständliche DbUpdateException für den Nutzer). UPDLOCK,
            // HOLDLOCK erzeugt eine Key-Range-Sperre auf dem Unique-Index und verhindert das Phantom-Insert der
            // Konkurrenztransaktion — der Unique-Index aus E3/Task 4 (HasFilter(null)) ist Voraussetzung dafür,
            // dass die Range-Sperre wirklich über NULL-Dimensionen greift.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO ArtikelBestaende (ArtikelId, LagerortId, SektionId, KulturstufeId, Menge)
                 SELECT {artikelId}, {lagerortId}, {sektionId}, {kulturstufeId}, 0
                 WHERE NOT EXISTS (
                     SELECT 1 FROM ArtikelBestaende WITH (UPDLOCK, HOLDLOCK)
                     WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId}
                       AND ((SektionId IS NULL AND {sektionId} IS NULL) OR SektionId = {sektionId})
                       AND ((KulturstufeId IS NULL AND {kulturstufeId} IS NULL) OR KulturstufeId = {kulturstufeId}))
                 """, ct);

            betroffeneZeilen = await AusfuehrenUpdateAsync(db, artikelId, lagerortId, sektionId, kulturstufeId, mengeDelta, ct);

            if (betroffeneZeilen == 0)
                throw new InvalidOperationException("Nicht genügend Bestand vorhanden — Buchung würde negativen Bestand erzeugen.");
        }

        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            ArtikelId = artikelId,
            LagerortId = lagerortId,
            SektionId = sektionId,
            KulturstufeId = kulturstufeId,
            Typ = typ,
            Menge = mengeDelta,
            BelegPositionId = belegPositionId,
            Zeitpunkt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Der atomare UPDATE aus E4 — NULL-sicher in beiden Dimensionen, damit eine dimensionslose
    /// Handelsware-Buchung (sektionId/kulturstufeId beide null) exakt die eine bestehende NULL/NULL-Zeile
    /// trifft statt stillschweigend eine neue anzulegen.</summary>
    private static Task<int> AusfuehrenUpdateAsync(
        MiletDbContext db, int artikelId, int lagerortId, int? sektionId, int? kulturstufeId, decimal mengeDelta, CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE ArtikelBestaende SET Menge = Menge + {mengeDelta}
             WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId}
               AND ((SektionId IS NULL AND {sektionId} IS NULL) OR SektionId = {sektionId})
               AND ((KulturstufeId IS NULL AND {kulturstufeId} IS NULL) OR KulturstufeId = {kulturstufeId})
               AND Menge + {mengeDelta} >= 0
             """, ct);
}
