using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Abstractions;
using Nexus.Application.Common;
using Nexus.Application.Stammdaten;
using Nexus.Domain.Entities.Stammdaten;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Services.Mapping;

namespace Nexus.Infrastructure.Services;

public sealed class ArtikelService(
    IDbContextFactory<NexusDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IArtikelService
{
    private static readonly ArtikelValidator Validator = new();

    public async Task<IReadOnlyList<ArtikelDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.Artikel.AsNoTracking().Include(a => a.Einheit).AsQueryable();

        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.Artikelnummer, $"%{s}%") ||
                EF.Functions.Like(a.Bezeichnung, $"%{s}%"));
        }

        var artikel = await query.OrderBy(a => a.Artikelnummer).Take(500).ToListAsync(ct);
        return artikel.Select(a => a.ToDto()).ToList();
    }

    public async Task<ArtikelDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.AsNoTracking().Include(a => a.Einheit)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Artikel), id);

        return artikel.ToDto();
    }

    public async Task<ArtikelDto> SpeichereAsync(ArtikelDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (dto.Id == 0)
        {
            var nummer = await numberRangeService.NaechsteNummerAsync("ART", ct);

            var neu = new Artikel { Artikelnummer = nummer };
            dto.ApplyTo(neu);

            db.Artikel.Add(neu);
            await db.SaveChangesTranslatingConcurrencyAsync(nameof(Artikel), dto.Id, ct);

            return neu.ToDto();
        }

        var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Artikel), dto.Id);

        db.Entry(artikel).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;

        dto.ApplyTo(artikel);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Artikel), dto.Id, ct);

        return artikel.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Artikel), id);

        db.Artikel.Remove(artikel);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Artikel), id, ct);
    }
}
