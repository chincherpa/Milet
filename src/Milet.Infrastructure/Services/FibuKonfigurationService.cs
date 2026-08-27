using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class FibuKonfigurationService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IFibuKonfigurationService
{
    public async Task<FibuKonfigurationDto> LadeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var konfiguration = await db.FibuKonfiguration.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);
        return konfiguration?.ToDto() ?? new FibuKonfigurationDto();
    }

    public async Task SpeichereAsync(FibuKonfigurationDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var konfiguration = await db.FibuKonfiguration.FirstOrDefaultAsync(f => f.Id == 1, ct);
        if (konfiguration is null)
        {
            konfiguration = new FibuKonfiguration { Id = 1 };
            db.Add(konfiguration);
        }
        dto.ApplyTo(konfiguration);
        await db.SaveChangesAsync(ct);
    }
}
