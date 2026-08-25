using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class FirmenstammService(IDbContextFactory<MiletDbContext> dbContextFactory) : IFirmenstammService
{
    public async Task<FirmenstammDto> LadeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var firma = await db.Firmenstamm.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);
        return firma?.ToDto() ?? new FirmenstammDto();
    }

    public async Task SpeichereAsync(FirmenstammDto dto, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var firma = await db.Firmenstamm.FirstOrDefaultAsync(f => f.Id == 1, ct);
        if (firma is null)
        {
            firma = new Firmenstamm { Id = 1 };
            db.Add(firma);
        }
        dto.ApplyTo(firma);
        await db.SaveChangesAsync(ct);
    }
}
