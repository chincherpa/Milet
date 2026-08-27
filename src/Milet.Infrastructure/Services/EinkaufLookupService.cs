using Microsoft.EntityFrameworkCore;
using Milet.Application.Einkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class EinkaufLookupService(IDbContextFactory<MiletDbContext> dbContextFactory) : IEinkaufLookupService
{
    public async Task<EinkaufLookups> LadeLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var lieferanten = await db.Lieferanten.AsNoTracking()
            .OrderBy(l => l.Lieferantennummer)
            .Select(l => new LieferantEinkaufLookupDto(l.Id, $"{l.Lieferantennummer} — {l.Adresse.Name1}", l.ZahlungsbedingungId))
            .ToListAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => !a.Gesperrt)
            .OrderBy(a => a.Artikelnummer)
            .Select(a => new ArtikelEinkaufLookupDto(
                a.Id,
                $"{a.Artikelnummer} — {a.Bezeichnung}",
                a.Bezeichnung,
                a.Einkaufspreis,
                a.MwStSatzId,
                a.MwStSatz!.Satz,
                a.MwStSatz.SteuerSchluessel,
                a.Einheit!.Kuerzel,
                a.HatSeriennummern))
            .ToListAsync(ct);

        return new EinkaufLookups(lieferanten, artikel);
    }
}
