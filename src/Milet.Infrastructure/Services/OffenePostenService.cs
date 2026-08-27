using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class OffenePostenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IOffenePostenService
{
    public async Task<IReadOnlyList<OffenePostenDto>> ListeAsync(OffenePostenFilterDto? filter = null, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.OffenePosten.AsNoTracking()
            .Include(o => o.Beleg)
            .Include(o => o.Kunde)
            .Include(o => o.Lieferant)
            .AsQueryable();

        if (filter?.Typ is { } typ) query = query.Where(o => o.Typ == typ);
        if (filter?.Status is { } status) query = query.Where(o => o.Status == status);

        var heute = DateOnly.FromDateTime(DateTime.Today);
        if (filter?.NurUeberfaellige == true) query = query.Where(o => o.Faelligkeit < heute && o.OffenerBetrag > 0);

        var posten = await query.OrderBy(o => o.Faelligkeit).ToListAsync(ct);
        return posten.Select(o => ZuDto(o, heute)).ToList();
    }

    public async Task<OffenePostenDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var op = await db.OffenePosten.AsNoTracking()
            .Include(o => o.Beleg).Include(o => o.Kunde).Include(o => o.Lieferant)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(OffenerPosten), id);

        return ZuDto(op, DateOnly.FromDateTime(DateTime.Today));
    }

    private static OffenePostenDto ZuDto(OffenerPosten o, DateOnly heute) => new(
        o.Id,
        o.BelegId,
        o.Beleg?.BelegNummer ?? string.Empty,
        o.KundeId,
        o.LieferantId,
        o.Kunde?.Adresse.Name1 ?? o.Lieferant?.Adresse.Name1 ?? string.Empty,
        o.Typ,
        o.Betrag,
        o.OffenerBetrag,
        o.Faelligkeit,
        heute.DayNumber - o.Faelligkeit.DayNumber,
        o.Mahnstufe,
        o.Mahnsperre,
        o.Status,
        o.RowVersion);
}
