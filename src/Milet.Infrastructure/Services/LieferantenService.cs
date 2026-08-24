using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Abstractions;
using Nexus.Application.Common;
using Nexus.Application.Stammdaten;
using Nexus.Domain.Entities.Stammdaten;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Services.Mapping;

namespace Nexus.Infrastructure.Services;

public sealed class LieferantenService(
    IDbContextFactory<NexusDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : ILieferantenService
{
    private static readonly LieferantValidator Validator = new();

    public async Task<IReadOnlyList<LieferantDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.Lieferanten.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(l =>
                EF.Functions.Like(l.Lieferantennummer, $"%{s}%") ||
                EF.Functions.Like(l.Adresse.Name1, $"%{s}%"));
        }

        var lieferanten = await query.OrderBy(l => l.Lieferantennummer).Take(500).ToListAsync(ct);
        return lieferanten.Select(l => l.ToDto()).ToList();
    }

    public async Task<LieferantDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var lieferant = await db.Lieferanten.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException(nameof(Lieferant), id);

        return lieferant.ToDto();
    }

    public async Task<LieferantDto> SpeichereAsync(LieferantDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (dto.Id == 0)
        {
            var nummer = await numberRangeService.NaechsteNummerAsync("LF", ct);

            var neu = new Lieferant { Lieferantennummer = nummer };
            dto.ApplyTo(neu);

            db.Lieferanten.Add(neu);
            await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lieferant), dto.Id, ct);

            return neu.ToDto();
        }

        var lieferant = await db.Lieferanten.FirstOrDefaultAsync(l => l.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Lieferant), dto.Id);

        db.Entry(lieferant).Property(l => l.RowVersion).OriginalValue = dto.RowVersion;

        dto.ApplyTo(lieferant);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lieferant), dto.Id, ct);

        return lieferant.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var lieferant = await db.Lieferanten.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException(nameof(Lieferant), id);

        db.Lieferanten.Remove(lieferant);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lieferant), id, ct);
    }
}
