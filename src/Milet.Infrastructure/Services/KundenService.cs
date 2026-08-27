using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Stammdaten;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class KundenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService,
    IBerechtigungsService berechtigung) : IKundenService
{
    private static readonly KundeValidator Validator = new();

    public async Task<IReadOnlyList<KundeDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = db.Kunden.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(k =>
                EF.Functions.Like(k.Kundennummer, $"%{s}%") ||
                EF.Functions.Like(k.Adresse.Name1, $"%{s}%"));
        }

        var kunden = await query.OrderBy(k => k.Kundennummer).Take(500).ToListAsync(ct);
        return kunden.Select(k => k.ToDto()).ToList();
    }

    public async Task<KundeDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var kunde = await db.Kunden.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException(nameof(Kunde), id);

        return kunde.ToDto();
    }

    public async Task<KundeDto> SpeichereAsync(KundeDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Stammdaten);
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (dto.Id == 0)
        {
            var nummer = await numberRangeService.NaechsteNummerAsync("KD", ct);

            var neu = new Kunde { Kundennummer = nummer };
            dto.ApplyTo(neu);

            db.Kunden.Add(neu);
            await db.SaveChangesTranslatingConcurrencyAsync(nameof(Kunde), dto.Id, ct);

            return neu.ToDto();
        }

        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Kunde), dto.Id);

        db.Entry(kunde).Property(k => k.RowVersion).OriginalValue = dto.RowVersion;

        dto.ApplyTo(kunde);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Kunde), dto.Id, ct);

        return kunde.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Stammdaten);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException(nameof(Kunde), id);

        db.Kunden.Remove(kunde);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Kunde), id, ct);
    }
}
