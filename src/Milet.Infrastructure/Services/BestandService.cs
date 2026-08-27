using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
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
        var bestaende = await db.ArtikelBestaende.AsNoTracking().ToListAsync(ct);
        var bestaendeLookup = bestaende.ToDictionary(b => (b.ArtikelId, b.LagerortId), b => b.Menge);

        // Left-Join Artikel x Lagerorte gegen ArtikelBestaende (in-memory kombiniert, da nur kleine/mittlere Zeilenzahlen
        // erwartet werden): jede Kombination lagerfähiger Artikel x aktiver Lagerort erzeugt eine Zeile, auch ohne
        // existierenden ArtikelBestand-Datensatz (Menge = 0) — sonst ist der allererste Erstbestand über die UI nicht anlegbar.
        var ergebnis = new List<ArtikelBestandDto>();
        foreach (var a in artikel)
        {
            foreach (var l in lagerorte)
            {
                var menge = bestaendeLookup.GetValueOrDefault((a.Id, l.Id), 0m);
                ergebnis.Add(new ArtikelBestandDto(a.Id, a.Artikelnummer, a.Bezeichnung, a.HatSeriennummern, l.Id, l.Bezeichnung, menge, a.Mindestbestand));
            }
        }

        return ergebnis.OrderBy(b => b.Artikelnummer).ThenBy(b => b.LagerortBezeichnung).ToList();
    }

    public async Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BucheBewegungAsync(db, dto.ArtikelId, dto.LagerortId, dto.MengeDelta, LagerbewegungTyp.Korrektur, belegPositionId: null, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Einziger Schreibpfad auf Bestand — ein atomares UPDATE (kein Read-Modify-Write), Negativbestand ist hart gesperrt.
    /// Läuft innerhalb der Transaktion des Aufrufers (Aufrufer öffnet/committet); wiederverwendbar von Bestandskorrektur, Lieferschein-Buchen, Inventur-Abschluss.</summary>
    internal static async Task BucheBewegungAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal mengeDelta,
        LagerbewegungTyp typ, int? belegPositionId, CancellationToken ct)
    {
        var betroffeneZeilen = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE ArtikelBestaende SET Menge = Menge + {mengeDelta}
             WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId} AND Menge + {mengeDelta} >= 0
             """, ct);

        if (betroffeneZeilen == 0)
        {
            if (mengeDelta < 0)
                throw new InvalidOperationException("Nicht genügend Bestand vorhanden — Buchung würde negativen Bestand erzeugen.");

            db.ArtikelBestaende.Add(new ArtikelBestand { ArtikelId = artikelId, LagerortId = lagerortId, Menge = mengeDelta });
        }

        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            ArtikelId = artikelId,
            LagerortId = lagerortId,
            Typ = typ,
            Menge = mengeDelta,
            BelegPositionId = belegPositionId,
            Zeitpunkt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
