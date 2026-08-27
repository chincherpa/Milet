using Microsoft.EntityFrameworkCore;
using Milet.Application.Einkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class BestellVorschlagService(IDbContextFactory<MiletDbContext> dbContextFactory) : IBestellVorschlagService
{
    public async Task<IReadOnlyList<BestellVorschlagPositionDto>> ErmittleVorschlaegeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.IstLagerartikel && !a.Gesperrt && a.Mindestbestand != null)
            .Include(a => a.Einheit)
            .Include(a => a.MwStSatz)
            .ToListAsync(ct);
        if (artikel.Count == 0) return [];

        var artikelIds = artikel.Select(a => a.Id).ToList();
        var bestaendeJeArtikel = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => artikelIds.Contains(b.ArtikelId))
            .GroupBy(b => b.ArtikelId)
            .Select(g => new { ArtikelId = g.Key, Summe = g.Sum(b => b.Menge) })
            .ToDictionaryAsync(x => x.ArtikelId, x => x.Summe, ct);

        var ergebnis = new List<BestellVorschlagPositionDto>();
        foreach (var a in artikel)
        {
            var bestand = bestaendeJeArtikel.GetValueOrDefault(a.Id, 0m);
            var mindestbestand = a.Mindestbestand!.Value;
            if (bestand >= mindestbestand) continue;

            ergebnis.Add(new BestellVorschlagPositionDto(
                a.Id, a.Artikelnummer, a.Bezeichnung, bestand, mindestbestand,
                VorschlagsMenge: mindestbestand - bestand,
                a.Einkaufspreis, a.MwStSatzId, a.MwStSatz!.Satz, a.MwStSatz.SteuerSchluessel, a.Einheit?.Kuerzel));
        }

        return ergebnis.OrderBy(v => v.Artikelnummer).ToList();
    }
}
