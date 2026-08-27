using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class AuditLogService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IAuditLogService
{
    public async Task<IReadOnlyList<AuditLogDto>> ListeAsync(AuditLogFilterDto? filter = null, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.AuditLog.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter?.EntityName))
        {
            query = query.Where(a => a.EntityName == filter.EntityName);
        }

        if (filter?.Von is { } von)
        {
            query = query.Where(a => a.Zeitpunkt >= von);
        }

        if (filter?.Bis is { } bis)
        {
            query = query.Where(a => a.Zeitpunkt <= bis);
        }

        var logs = await query.OrderByDescending(a => a.Zeitpunkt).Take(1000).ToListAsync(ct);
        return logs.Select(a => a.ToDto()).ToList();
    }
}
