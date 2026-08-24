using Microsoft.EntityFrameworkCore;
using Nexus.Application.Stammdaten;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

public sealed class StammdatenLookupService(IDbContextFactory<NexusDbContext> dbContextFactory) : IStammdatenLookupService
{
    public async Task<StammdatenLookups> LadeLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var einheiten = await db.Einheiten.AsNoTracking()
            .OrderBy(e => e.Bezeichnung)
            .Select(e => new LookupDto(e.Id, $"{e.Kuerzel} — {e.Bezeichnung}"))
            .ToListAsync(ct);

        var mwst = await db.MwStSaetze.AsNoTracking()
            .OrderBy(m => m.Satz)
            .Select(m => new LookupDto(m.Id, $"{m.Satz:0.##} % — {m.Bezeichnung}"))
            .ToListAsync(ct);

        var zahlungsbedingungen = await db.Zahlungsbedingungen.AsNoTracking()
            .OrderBy(z => z.Bezeichnung)
            .Select(z => new LookupDto(z.Id, z.Bezeichnung))
            .ToListAsync(ct);

        var versandarten = await db.Versandarten.AsNoTracking()
            .OrderBy(v => v.Bezeichnung)
            .Select(v => new LookupDto(v.Id, v.Bezeichnung))
            .ToListAsync(ct);

        var preislisten = await db.Preislisten.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new LookupDto(p.Id, p.Name))
            .ToListAsync(ct);

        return new StammdatenLookups(einheiten, mwst, zahlungsbedingungen, versandarten, preislisten);
    }
}
