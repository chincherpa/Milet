using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Stammdaten;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class EinheitenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IEinheitenService
{
    private static readonly EinheitValidator Validator = new();

    public async Task<IReadOnlyList<EinheitDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Einheiten.AsNoTracking().OrderBy(e => e.Bezeichnung)
            .Select(e => new EinheitDto { Id = e.Id, Kuerzel = e.Kuerzel, Bezeichnung = e.Bezeichnung, NachkommaStellen = e.NachkommaStellen })
            .ToListAsync(ct);
    }

    public async Task<EinheitDto> SpeichereAsync(EinheitDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new Einheit() : await db.Einheiten.FirstOrDefaultAsync(e => e.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Einheit), dto.Id);

        entity.Kuerzel = dto.Kuerzel;
        entity.Bezeichnung = dto.Bezeichnung;
        entity.NachkommaStellen = dto.NachkommaStellen;

        if (dto.Id == 0)
        {
            db.Einheiten.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Einheiten.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(Einheit), id);

        db.Einheiten.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(Einheit), id, ct);
    }
}

public sealed class MwStSaetzeService(IDbContextFactory<MiletDbContext> dbContextFactory) : IMwStSaetzeService
{
    private static readonly MwStSatzValidator Validator = new();

    public async Task<IReadOnlyList<MwStSatzDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.MwStSaetze.AsNoTracking().OrderBy(m => m.Satz)
            .Select(m => new MwStSatzDto { Id = m.Id, Bezeichnung = m.Bezeichnung, Satz = m.Satz, SteuerSchluessel = m.SteuerSchluessel, GueltigAb = m.GueltigAb })
            .ToListAsync(ct);
    }

    public async Task<MwStSatzDto> SpeichereAsync(MwStSatzDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new MwStSatz() : await db.MwStSaetze.FirstOrDefaultAsync(m => m.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(MwStSatz), dto.Id);

        entity.Bezeichnung = dto.Bezeichnung;
        entity.Satz = dto.Satz;
        entity.SteuerSchluessel = dto.SteuerSchluessel;
        entity.GueltigAb = dto.GueltigAb;

        if (dto.Id == 0)
        {
            db.MwStSaetze.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.MwStSaetze.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(MwStSatz), id);

        db.MwStSaetze.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(MwStSatz), id, ct);
    }
}

public sealed class ZahlungsbedingungenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IZahlungsbedingungenService
{
    private static readonly ZahlungsbedingungValidator Validator = new();

    public async Task<IReadOnlyList<ZahlungsbedingungDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Zahlungsbedingungen.AsNoTracking().OrderBy(z => z.Bezeichnung)
            .Select(z => new ZahlungsbedingungDto { Id = z.Id, Bezeichnung = z.Bezeichnung, ZielTage = z.ZielTage, SkontoTage = z.SkontoTage, SkontoProzent = z.SkontoProzent })
            .ToListAsync(ct);
    }

    public async Task<ZahlungsbedingungDto> SpeichereAsync(ZahlungsbedingungDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new Zahlungsbedingung() : await db.Zahlungsbedingungen.FirstOrDefaultAsync(z => z.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Zahlungsbedingung), dto.Id);

        entity.Bezeichnung = dto.Bezeichnung;
        entity.ZielTage = dto.ZielTage;
        entity.SkontoTage = dto.SkontoTage;
        entity.SkontoProzent = dto.SkontoProzent;

        if (dto.Id == 0)
        {
            db.Zahlungsbedingungen.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Zahlungsbedingungen.FirstOrDefaultAsync(z => z.Id == id, ct)
            ?? throw new NotFoundException(nameof(Zahlungsbedingung), id);

        db.Zahlungsbedingungen.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(Zahlungsbedingung), id, ct);
    }
}

public sealed class VersandartenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IVersandartenService
{
    private static readonly VersandartValidator Validator = new();

    public async Task<IReadOnlyList<VersandartDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Versandarten.AsNoTracking().OrderBy(v => v.Bezeichnung)
            .Select(v => new VersandartDto { Id = v.Id, Bezeichnung = v.Bezeichnung, Kosten = v.Kosten })
            .ToListAsync(ct);
    }

    public async Task<VersandartDto> SpeichereAsync(VersandartDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new Versandart() : await db.Versandarten.FirstOrDefaultAsync(v => v.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Versandart), dto.Id);

        entity.Bezeichnung = dto.Bezeichnung;
        entity.Kosten = dto.Kosten;

        if (dto.Id == 0)
        {
            db.Versandarten.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Versandarten.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Versandart), id);

        db.Versandarten.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(Versandart), id, ct);
    }
}

public sealed class PreislistenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IPreislistenService
{
    private static readonly PreislisteValidator Validator = new();

    public async Task<IReadOnlyList<PreislisteDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Preislisten.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new PreislisteDto { Id = p.Id, Name = p.Name, GueltigVon = p.GueltigVon, GueltigBis = p.GueltigBis })
            .ToListAsync(ct);
    }

    public async Task<PreislisteDto> SpeichereAsync(PreislisteDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = dto.Id == 0 ? new Preisliste() : await db.Preislisten.FirstOrDefaultAsync(p => p.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Preisliste), dto.Id);

        entity.Name = dto.Name;
        entity.GueltigVon = dto.GueltigVon;
        entity.GueltigBis = dto.GueltigBis;

        if (dto.Id == 0)
        {
            db.Preislisten.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return dto with { Id = entity.Id };
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Preislisten.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Preisliste), id);

        db.Preislisten.Remove(entity);
        await db.SaveChangesDeletingAsync(nameof(Preisliste), id, ct);
    }
}
