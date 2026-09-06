using Microsoft.EntityFrameworkCore;
using Milet.Application.Gaertnerei;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>Lesende Sicht auf die Kulturführung — Pflanzenliste, Fundstellen, Historie. Kein Schreibpfad;
/// alle Bestandsänderungen laufen über <see cref="KulturBuchungService"/>/<see cref="BestandService"/>.</summary>
public sealed class KulturBestandService(IDbContextFactory<MiletDbContext> dbContextFactory) : IKulturBestandService
{
    public async Task<IReadOnlyList<PflanzeUebersichtDto>> LadePflanzenAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.Artikel.AsNoTracking().Where(a => a.IstKulturpflanze && !a.Gesperrt);
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.Bezeichnung, $"%{s}%") ||
                EF.Functions.Like(a.Artikelnummer, $"%{s}%") ||
                (a.BotanischerName != null && EF.Functions.Like(a.BotanischerName, $"%{s}%")));
        }

        // Artikel ohne Bestand erscheinen mit Menge 0 — der Nutzer will "alle Pflanzen der Gärtnerei" sehen,
        // nicht nur die vorrätigen. Deshalb zwei getrennte Abfragen statt eines inneren Joins (kein N+1: die
        // zweite Abfrage lädt alle Mengen auf einmal, nicht je Artikel).
        var artikel = await query.OrderBy(a => a.Bezeichnung).ToListAsync(ct);
        var artikelIds = artikel.Select(a => a.Id).ToList();

        var mengenJeStufe = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => artikelIds.Contains(b.ArtikelId) && b.KulturstufeId != null)
            .GroupBy(b => new { b.ArtikelId, b.KulturstufeId, b.Kulturstufe!.Bezeichnung, b.Kulturstufe.FarbeHex, b.Kulturstufe.Reihenfolge })
            .Select(g => new
            {
                g.Key.ArtikelId,
                g.Key.KulturstufeId,
                g.Key.Bezeichnung,
                g.Key.FarbeHex,
                g.Key.Reihenfolge,
                Menge = g.Sum(x => x.Menge),
            })
            .ToListAsync(ct);

        var jeArtikel = mengenJeStufe.ToLookup(m => m.ArtikelId);

        return artikel.Select(a =>
        {
            var stufen = jeArtikel[a.Id]
                .OrderBy(m => m.Reihenfolge)
                .Select(m => new MengeJeStufeDto(m.KulturstufeId!.Value, m.Bezeichnung, m.FarbeHex, m.Menge))
                .ToList();
            return new PflanzeUebersichtDto(a.Id, a.Artikelnummer, a.Bezeichnung, a.BotanischerName, stufen.Sum(s => s.Menge), stufen);
        }).ToList();
    }

    public async Task<IReadOnlyList<PflanzenVorkommenDto>> LadeVorkommenAsync(int artikelId, CancellationToken ct = default)
    {
        var jeArtikel = await LadeVorkommenAsync([artikelId], ct);
        return jeArtikel[artikelId].ToList();
    }

    /// <summary>Eine Abfrage für beliebig viele Artikel — die Einzelvariante delegiert hierher, damit es nur
    /// eine Projektion und eine Sortierregel gibt.</summary>
    public async Task<ILookup<int, PflanzenVorkommenDto>> LadeVorkommenAsync(IReadOnlyList<int> artikelIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artikelIds);
        if (artikelIds.Count == 0) return Enumerable.Empty<PflanzenVorkommenDto>().ToLookup(_ => 0);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var fundstellen = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => artikelIds.Contains(b.ArtikelId) && b.SektionId != null && b.KulturstufeId != null && b.Menge > 0)
            .Select(b => new
            {
                b.ArtikelId,
                FeldId = b.LagerortId,
                FeldBezeichnung = b.Lagerort!.Bezeichnung,
                SektionId = b.SektionId!.Value,
                SektionBezeichnung = b.Sektion!.Bezeichnung,
                KulturstufeId = b.KulturstufeId!.Value,
                StufeBezeichnung = b.Kulturstufe!.Bezeichnung,
                b.Kulturstufe.FarbeHex,
                b.Kulturstufe.Reihenfolge,
                b.Menge,
            })
            .ToListAsync(ct);

        return fundstellen
            .OrderBy(f => f.Reihenfolge).ThenBy(f => f.FeldBezeichnung).ThenBy(f => f.SektionBezeichnung)
            .ToLookup(
                f => f.ArtikelId,
                f => new PflanzenVorkommenDto(f.FeldId, f.FeldBezeichnung, f.SektionId, f.SektionBezeichnung, f.KulturstufeId, f.StufeBezeichnung, f.FarbeHex, f.Menge));
    }

    public async Task<IReadOnlyList<KulturHistorieZeileDto>> LadeHistorieAsync(int artikelId, int? sektionId, DateOnly? von, DateOnly? bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.Lagerbewegungen.AsNoTracking().Where(l => l.ArtikelId == artikelId);
        if (sektionId is not null)
            query = query.Where(l => l.SektionId == sektionId);
        if (von is not null)
        {
            var vonDatum = von.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(l => l.Zeitpunkt >= vonDatum);
        }
        if (bis is not null)
        {
            var bisDatum = bis.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(l => l.Zeitpunkt <= bisDatum);
        }

        var bewegungen = await query
            .OrderByDescending(l => l.Zeitpunkt)
            .Take(500)
            .Select(l => new
            {
                l.Zeitpunkt,
                l.Typ,
                l.Menge,
                FeldBezeichnung = l.Lagerort!.Bezeichnung,
                SektionBezeichnung = l.Sektion != null ? l.Sektion.Bezeichnung : null,
                StufeBezeichnung = l.Kulturstufe != null ? l.Kulturstufe.Bezeichnung : null,
                BelegNummer = l.BelegPosition != null ? l.BelegPosition.Beleg!.BelegNummer : null,
                l.Bemerkung,
            })
            .ToListAsync(ct);

        return bewegungen
            .Select(b => new KulturHistorieZeileDto(
                b.Zeitpunkt, b.Typ.ToString(), b.Menge, b.FeldBezeichnung, b.SektionBezeichnung, b.StufeBezeichnung,
                b.BelegNummer, b.Bemerkung))
            .ToList();
    }
}
