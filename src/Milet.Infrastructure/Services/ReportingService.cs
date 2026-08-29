using Microsoft.EntityFrameworkCore;
using Milet.Application.Reporting;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class ReportingService(IDbContextFactory<MiletDbContext> dbContextFactory) : IReportingService
{
    public async Task<IReadOnlyList<UmsatzJeKundeDto>> UmsatzJeKundeAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rechnungen = await LadeGebuchteRechnungenAsync(db, von, bis, ct);

        return rechnungen
            .Where(r => r.KundeId != null)
            .GroupBy(r => r.KundeId!.Value)
            .Select(g => new UmsatzJeKundeDto(
                g.Key,
                g.First().Kunde?.Kundennummer ?? "",
                g.First().Kunde?.Adresse.Name1 ?? "",
                g.Count(),
                g.Sum(r => r.SummeNetto),
                g.Sum(r => r.SummeBrutto)))
            .OrderByDescending(d => d.SummeBrutto)
            .ToList();
    }

    public async Task<IReadOnlyList<UmsatzJeArtikelDto>> UmsatzJeArtikelAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var positionen = await LadeGebuchteRechnungspositionenAsync(von, bis, ct);
        return AggregiereArtikel(positionen).OrderByDescending(d => d.SummeNetto).ToList();
    }

    public async Task<IReadOnlyList<UmsatzJeMonatDto>> UmsatzJeMonatAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rechnungen = await LadeGebuchteRechnungenAsync(db, von, bis, ct);

        return rechnungen
            .GroupBy(r => (r.BelegDatum.Year, r.BelegDatum.Month))
            .Select(g => new UmsatzJeMonatDto(g.Key.Year, g.Key.Month, g.Sum(r => r.SummeNetto), g.Sum(r => r.SummeBrutto)))
            .OrderBy(d => d.Jahr).ThenBy(d => d.Monat)
            .ToList();
    }

    public async Task<IReadOnlyList<ArtikelbewegungDto>> ArtikelbewegungenAsync(int? artikelId, DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var vonZeitpunkt = von.ToDateTime(TimeOnly.MinValue);
        var bisZeitpunkt = bis.ToDateTime(TimeOnly.MaxValue);

        var query = db.Lagerbewegungen.AsNoTracking()
            .Include(l => l.Artikel)
            .Include(l => l.Lagerort)
            .Include(l => l.BelegPosition!).ThenInclude(p => p!.Beleg)
            .Where(l => l.Zeitpunkt >= vonZeitpunkt && l.Zeitpunkt <= bisZeitpunkt);

        if (artikelId is { } id) query = query.Where(l => l.ArtikelId == id);

        var bewegungen = await query.OrderByDescending(l => l.Zeitpunkt).ToListAsync(ct);

        return bewegungen.Select(l => new ArtikelbewegungDto(
            l.Zeitpunkt,
            l.Artikel?.Artikelnummer ?? "",
            l.Artikel?.Bezeichnung ?? "",
            l.Lagerort?.Code ?? "",
            l.Menge,
            l.Typ.ToString(),
            l.BelegPosition?.Beleg?.BelegNummer)).ToList();
    }

    public async Task<IReadOnlyList<TopArtikelDto>> TopArtikelAsync(DateOnly von, DateOnly bis, int anzahl = 10, CancellationToken ct = default)
    {
        var positionen = await LadeGebuchteRechnungspositionenAsync(von, bis, ct);
        return AggregiereArtikel(positionen)
            .OrderByDescending(d => d.SummeNetto)
            .Take(anzahl)
            .Select(d => new TopArtikelDto(d.ArtikelId, d.ArtikelNummer, d.Bezeichnung, d.Menge, d.SummeNetto))
            .ToList();
    }

    public async Task<IReadOnlyList<OffenerAuftragDto>> OffeneAuftraegeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var auftraege = await db.Auftraege.AsNoTracking()
            .Include(a => a.Positionen)
            .Include(a => a.Kunde)
            // Entwurf gehört dazu: es gibt keinen Codepfad, der einen Auftrag auf Gebucht setzt (Aufträge
            // entstehen als Entwurf und gehen über die Überleitung direkt auf Erledigt). Ein Filter allein
            // auf Gebucht lieferte deshalb unter allen Umständen eine leere Liste.
            .Where(a => a.Status == BelegStatus.Entwurf || a.Status == BelegStatus.Gebucht)
            .ToListAsync(ct);

        if (auftraege.Count == 0) return [];

        var quellPositionIds = auftraege.SelectMany(a => a.Positionen).Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var ergebnis = new List<OffenerAuftragDto>();
        foreach (var auftrag in auftraege)
        {
            var offeneMenge = auftrag.Positionen
                .Where(p => p.PositionsTyp == PositionsTyp.Artikel)
                .Sum(p => BelegPosition.OffeneMenge(p, folgepositionen));
            if (offeneMenge <= 0) continue;

            ergebnis.Add(new OffenerAuftragDto(
                auftrag.Id, auftrag.BelegNummer, auftrag.BelegDatum,
                auftrag.Kunde?.Adresse.Name1 ?? "", auftrag.SummeBrutto, offeneMenge));
        }

        return ergebnis.OrderBy(a => a.BelegDatum).ToList();
    }

    private static async Task<List<Rechnung>> LadeGebuchteRechnungenAsync(MiletDbContext db, DateOnly von, DateOnly bis, CancellationToken ct) =>
        await db.Rechnungen.AsNoTracking()
            .Include(r => r.Kunde)
            .Where(r => r.Status == BelegStatus.Gebucht && r.BelegDatum >= von && r.BelegDatum <= bis)
            .ToListAsync(ct);

    private async Task<List<BelegPosition>> LadeGebuchteRechnungspositionenAsync(DateOnly von, DateOnly bis, CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.BelegPositionen.AsNoTracking()
            .Include(p => p.Artikel)
            .Include(p => p.Beleg)
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel
                && p.Beleg is Rechnung
                && p.Beleg!.Status == BelegStatus.Gebucht
                && p.Beleg.BelegDatum >= von && p.Beleg.BelegDatum <= bis)
            .ToListAsync(ct);
    }

    private static IEnumerable<UmsatzJeArtikelDto> AggregiereArtikel(IEnumerable<BelegPosition> positionen) =>
        positionen
            .Where(p => p.ArtikelId != null)
            .GroupBy(p => p.ArtikelId!.Value)
            .Select(g => new UmsatzJeArtikelDto(
                g.Key,
                g.First().Artikel?.Artikelnummer ?? "",
                g.First().Bezeichnung,
                g.Sum(p => p.Menge),
                g.Sum(p => p.GesamtNetto)));
}
