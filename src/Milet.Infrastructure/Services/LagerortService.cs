using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class LagerortService(IDbContextFactory<MiletDbContext> dbContextFactory) : ILagerortService
{
    private static readonly LagerortValidator Validator = new();

    public async Task<IReadOnlyList<LagerortDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Lagerorte.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(l => EF.Functions.Like(l.Code, $"%{s}%") || EF.Functions.Like(l.Bezeichnung, $"%{s}%"));
        }
        var liste = await query.OrderBy(l => l.Code).ToListAsync(ct);
        return liste.Select(l => l.ToDto()).ToList();
    }

    public async Task<LagerortDto> SpeichereAsync(LagerortDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Lagerort lagerort;
        if (dto.Id == 0)
        {
            lagerort = new Lagerort();
            db.Add(lagerort);
        }
        else
        {
            lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == dto.Id, ct)
                ?? throw new Application.Common.NotFoundException(nameof(Lagerort), dto.Id);
            db.Entry(lagerort).Property(l => l.RowVersion).OriginalValue = dto.RowVersion;
        }

        lagerort.Code = dto.Code;
        lagerort.Bezeichnung = dto.Bezeichnung;
        lagerort.Aktiv = dto.Aktiv;

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lagerort), lagerort.Id, ct);
        return lagerort.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Lagerort), id);
        db.Remove(lagerort);
        await db.SaveChangesDeletingAsync(nameof(Lagerort), id, ct);
    }
}
