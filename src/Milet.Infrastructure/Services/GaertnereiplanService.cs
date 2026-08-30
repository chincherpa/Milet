using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class GaertnereiplanService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IGaertnereiplanService
{
    private static readonly GaertnereiplanValidator PlanValidator = new();
    private static readonly FeldValidator FeldValidator = new();
    private static readonly SektionValidator SektionValidator = new();

    public async Task<GaertnereiplanDto?> LadePlanAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var plan = await db.Gaertnereiplaene.AsNoTracking().FirstOrDefaultAsync(ct);
        if (plan is null) return null;

        // Eine Abfrage-Gruppe (drei Queries, keine N+1-Schleife) — Datenquelle für Grundriss-Editor UND Pflanzenübersicht.
        var felder = await db.Lagerorte.AsNoTracking()
            .Where(l => l.IstFeld && l.GaertnereiplanId == plan.Id)
            .OrderBy(l => l.Code)
            .ToListAsync(ct);
        var feldIds = felder.Select(f => f.Id).ToList();
        var sektionen = await db.Sektionen.AsNoTracking()
            .Where(s => feldIds.Contains(s.LagerortId))
            .ToListAsync(ct);
        var sektionenJeFeld = sektionen.ToLookup(s => s.LagerortId);

        var felderDto = felder.Select(f => f.ToDto(sektionenJeFeld[f.Id])).ToList();
        return plan.ToDto(felderDto);
    }

    public async Task<GaertnereiplanDto> SpeicherePlanAsync(GaertnereiplanDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await PlanValidator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Gaertnereiplan plan;
        if (dto.Id == 0)
        {
            plan = new Gaertnereiplan();
            db.Add(plan);
        }
        else
        {
            plan = await db.Gaertnereiplaene.FirstOrDefaultAsync(p => p.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Gaertnereiplan), dto.Id);
            db.Entry(plan).Property(p => p.RowVersion).OriginalValue = dto.RowVersion;
        }

        plan.Bezeichnung = dto.Bezeichnung;
        plan.BreiteMeter = dto.BreiteMeter;
        plan.HoeheMeter = dto.HoeheMeter;
        plan.Aktiv = dto.Aktiv;

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Gaertnereiplan), plan.Id, ct);
        return plan.ToDto(dto.Felder);
    }

    public async Task<FeldDto> SpeichereFeldAsync(int gaertnereiplanId, FeldDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await FeldValidator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Lagerort feld;
        if (dto.Id == 0)
        {
            feld = new Lagerort { IstFeld = true, GaertnereiplanId = gaertnereiplanId };
            db.Add(feld);
        }
        else
        {
            feld = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == dto.Id && l.IstFeld, ct)
                ?? throw new NotFoundException(nameof(Lagerort), dto.Id);
            db.Entry(feld).Property(l => l.RowVersion).OriginalValue = dto.RowVersion;
        }

        feld.Code = dto.Code;
        feld.Bezeichnung = dto.Bezeichnung;
        feld.PosXMeter = dto.PosXMeter;
        feld.PosYMeter = dto.PosYMeter;
        feld.BreiteMeter = dto.BreiteMeter;
        feld.HoeheMeter = dto.HoeheMeter;
        feld.Aktiv = dto.Aktiv;

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lagerort), feld.Id, ct);
        return feld.ToDto([]);
    }

    public async Task LoescheFeldAsync(int feldId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var feld = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == feldId && l.IstFeld, ct)
            ?? throw new NotFoundException(nameof(Lagerort), feldId);

        db.Remove(feld);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Feld '{feld.Bezeichnung}' enthält noch Sektionen oder Bestand und kann nicht gelöscht werden.", ex);
        }
    }

    public async Task<SektionSpeichernErgebnisDto> SpeichereSektionAsync(SektionDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await SektionValidator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var feld = await db.Lagerorte.AsNoTracking().FirstOrDefaultAsync(l => l.Id == dto.LagerortId && l.IstFeld, ct)
            ?? throw new NotFoundException(nameof(Lagerort), dto.LagerortId);

        Sektion sektion;
        if (dto.Id == 0)
        {
            sektion = new Sektion { LagerortId = dto.LagerortId };
            db.Add(sektion);
        }
        else
        {
            sektion = await db.Sektionen.FirstOrDefaultAsync(s => s.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Sektion), dto.Id);
            db.Entry(sektion).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;
        }

        sektion.Code = dto.Code;
        sektion.Bezeichnung = dto.Bezeichnung;
        sektion.PosXMeter = dto.PosXMeter;
        sektion.PosYMeter = dto.PosYMeter;
        sektion.BreiteMeter = dto.BreiteMeter;
        sektion.HoeheMeter = dto.HoeheMeter;
        sektion.Aktiv = dto.Aktiv;

        if (!KulturRegeln.LiegtInnerhalb(sektion, feld))
        {
            throw new InvalidOperationException($"Sektion '{dto.Bezeichnung}' liegt nicht vollständig innerhalb des Feldes '{feld.Bezeichnung}'.");
        }

        // Überlappung ist bewusst eine Warnung, kein Abbruch (E11) — zweistöckige Stellagen/Frühbeete über
        // Beeten gibt es real.
        var andereSektionen = await db.Sektionen.AsNoTracking()
            .Where(s => s.LagerortId == dto.LagerortId && s.Aktiv && s.Id != sektion.Id)
            .ToListAsync(ct);
        var warnungen = andereSektionen
            .Where(andere => KulturRegeln.Ueberlappt(sektion, andere))
            .Select(andere => $"Überlappt mit Sektion '{andere.Bezeichnung}'.")
            .ToList();

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Sektion), sektion.Id, ct);
        return new SektionSpeichernErgebnisDto(sektion.ToDto(), warnungen);
    }

    public async Task LoescheSektionAsync(int sektionId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var sektion = await db.Sektionen.FirstOrDefaultAsync(s => s.Id == sektionId, ct)
            ?? throw new NotFoundException(nameof(Sektion), sektionId);

        db.Remove(sektion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Sektion '{sektion.Bezeichnung}' enthält noch Bestand und kann nicht gelöscht werden.", ex);
        }
    }
}
