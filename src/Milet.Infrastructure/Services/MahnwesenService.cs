using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class MahnwesenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IMahnwesenService
{
    private static readonly MahnstufeValidator Validator = new();

    public async Task<IReadOnlyList<MahnstufeDto>> ListeStufenAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Mahnstufen.AsNoTracking().OrderBy(m => m.Stufe)
            .Select(m => new MahnstufeDto(m.Id, m.Stufe, m.Karenztage, m.Gebuehr, m.Mahntext))
            .ToListAsync(ct);
    }

    public async Task<MahnstufeDto> SpeichereStufeAsync(MahnstufeDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new Mahnstufe() : await db.Mahnstufen.FirstOrDefaultAsync(m => m.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Mahnstufe), dto.Id);

        entity.Stufe = dto.Stufe;
        entity.Karenztage = dto.Karenztage;
        entity.Gebuehr = dto.Gebuehr;
        entity.Mahntext = dto.Mahntext;

        if (dto.Id == 0)
        {
            db.Mahnstufen.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheStufeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Mahnstufen.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(Mahnstufe), id);

        db.Mahnstufen.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(Mahnstufe), id, ct);
    }

    public async Task<IReadOnlyList<MahnlaufGruppeDto>> ErmittleFaelligeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var stufen = await db.Mahnstufen.AsNoTracking().ToListAsync(ct);
        var heute = DateOnly.FromDateTime(DateTime.Today);

        var offenePosten = await db.OffenePosten.AsNoTracking()
            .Where(o => o.Typ == OffenerPostenTyp.Debitor && !o.Mahnsperre && o.OffenerBetrag > 0m)
            .Include(o => o.Beleg).Include(o => o.Kunde)
            .ToListAsync(ct);

        var kandidaten = offenePosten
            .Select(o => (Op: o, Stufe: MahnSelektionService.ErmittleFaelligeStufe(o, heute, stufen)))
            .Where(x => x.Stufe is not null)
            .Select(x => new
            {
                x.Op.KundeId,
                KundenName = x.Op.Kunde?.Adresse.Name1 ?? string.Empty,
                Kandidat = new MahnKandidatDto(
                    x.Op.Id, x.Op.BelegId, x.Op.Beleg?.BelegNummer ?? string.Empty, x.Op.OffenerBetrag,
                    x.Op.Faelligkeit, x.Op.Mahnstufe, x.Stufe!.Value),
            })
            .ToList();

        return kandidaten
            .GroupBy(k => (k.KundeId, k.KundenName))
            .Select(g => new MahnlaufGruppeDto(g.Key.KundeId, g.Key.KundenName, g.Select(x => x.Kandidat).ToList()))
            .OrderBy(g => g.KundenName)
            .ToList();
    }

    public async Task<IReadOnlyList<MahnungDto>> MahnlaufDurchfuehrenAsync(IReadOnlyList<int> offenerPostenIds, CancellationToken ct = default)
    {
        if (offenerPostenIds.Count == 0)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var stufen = await db.Mahnstufen.ToListAsync(ct);
        var heute = DateOnly.FromDateTime(DateTime.Today);

        var offenePosten = await db.OffenePosten
            .Where(o => offenerPostenIds.Contains(o.Id))
            .Include(o => o.Beleg).Include(o => o.Kunde)
            .ToListAsync(ct);

        // Re-check zum Ausführungszeitpunkt — die vom Nutzer ausgewählten Kandidaten stammen aus einer
        // ggf. bereits etwas älteren ErmittleFaelligeAsync-Liste (zwischenzeitliche Zahlung/Mahnsperre möglich).
        var faellige = offenePosten
            .Select(o => (Op: o, Stufe: MahnSelektionService.ErmittleFaelligeStufe(o, heute, stufen)))
            .Where(x => x.Stufe is not null)
            .ToList();

        var mahnungen = new List<(Mahnung Entity, string KundenName)>();
        foreach (var gruppe in faellige.GroupBy(x => (x.Op.KundeId, x.Stufe)))
        {
            var stufeConfig = stufen.First(s => s.Stufe == gruppe.Key.Stufe);
            var positionen = gruppe.Select(x => new MahnungPosition
            {
                OffenerPostenId = x.Op.Id,
                BelegNummerSnapshot = x.Op.Beleg?.BelegNummer ?? string.Empty,
                OffenerBetragSnapshot = x.Op.OffenerBetrag,
            }).ToList();

            var mahnung = new Mahnung
            {
                KundeId = gruppe.Key.KundeId,
                MahnDatum = heute,
                Mahnstufe = gruppe.Key.Stufe!.Value,
                Gebuehr = stufeConfig.Gebuehr,
                Gesamtbetrag = positionen.Sum(p => p.OffenerBetragSnapshot) + stufeConfig.Gebuehr,
                Positionen = positionen,
            };
            db.Mahnungen.Add(mahnung);
            mahnungen.Add((mahnung, gruppe.First().Op.Kunde?.Adresse.Name1 ?? string.Empty));

            foreach (var (op, stufe) in gruppe)
            {
                op.Mahnstufe = stufe!.Value;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return mahnungen.Select(x => new MahnungDto(
            x.Entity.Id, x.Entity.KundeId, x.KundenName, x.Entity.MahnDatum, x.Entity.Mahnstufe, x.Entity.Gebuehr, x.Entity.Gesamtbetrag,
            x.Entity.Positionen.Select(p => new MahnungPositionDto(p.OffenerPostenId, p.BelegNummerSnapshot, p.OffenerBetragSnapshot)).ToList()))
            .ToList();
    }
}
