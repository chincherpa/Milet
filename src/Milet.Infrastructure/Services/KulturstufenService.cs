using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

/// <summary>Kleinstamm-Muster (wie KleinstammServices.cs), mit RowVersion-Concurrency-Schutz (E5).</summary>
public sealed class KulturstufenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IKulturstufenService
{
    private static readonly KulturstufeValidator Validator = new();

    public async Task<IReadOnlyList<KulturstufeDto>> ListeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var stufen = await db.Kulturstufen.AsNoTracking().OrderBy(k => k.Reihenfolge).ToListAsync(ct);
        return stufen.Select(k => k.ToDto()).ToList();
    }

    public async Task<KulturstufeDto> SpeichereAsync(KulturstufeDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Kulturstufe stufe;
        if (dto.Id == 0)
        {
            stufe = new Kulturstufe();
            db.Add(stufe);
        }
        else
        {
            stufe = await db.Kulturstufen.FirstOrDefaultAsync(k => k.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Kulturstufe), dto.Id);
            db.Entry(stufe).Property(k => k.RowVersion).OriginalValue = dto.RowVersion;
        }

        stufe.Code = dto.Code;
        stufe.Bezeichnung = dto.Bezeichnung;
        stufe.Reihenfolge = dto.Reihenfolge;
        stufe.IstVerkaufsfaehig = dto.IstVerkaufsfaehig;
        stufe.FarbeHex = dto.FarbeHex;
        stufe.Aktiv = dto.Aktiv;

        await db.SaveChangesTranslatingConcurrencyAsync(
            nameof(Kulturstufe), stufe.Id,
            $"Code '{dto.Code}' oder Reihenfolge {dto.Reihenfolge} ist bereits an eine andere Kulturstufe vergeben. "
            + "Zum Umsortieren die Pfeiltasten verwenden (Reihenfolge ist eindeutig).", ct);
        return stufe.ToDto();
    }

    /// <summary>
    /// Tauscht die Reihenfolge mit der nächsten aktiven Stufe in der gewünschten Richtung — in EINER
    /// Transaktion, weil Reihenfolge eindeutig indiziert ist: nacheinander gespeichert verletzt schon der
    /// Zwischenstand den Index, ein Umsortieren über SpeichereAsync ist also gar nicht möglich.
    ///
    /// Der Tausch läuft über einen freien Zwischenwert, weil SQL Server den Unique-Index je Anweisung prüft,
    /// nicht erst beim Commit: A→B, B→A in einem SaveChanges wäre zwischenzeitlich doppelt belegt.
    /// </summary>
    public async Task VerschiebeAsync(int id, bool nachOben, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        var stufe = await db.Kulturstufen.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException(nameof(Kulturstufe), id);

        var nachbar = nachOben
            ? await db.Kulturstufen.Where(k => k.Reihenfolge < stufe.Reihenfolge).OrderByDescending(k => k.Reihenfolge).FirstOrDefaultAsync(ct)
            : await db.Kulturstufen.Where(k => k.Reihenfolge > stufe.Reihenfolge).OrderBy(k => k.Reihenfolge).FirstOrDefaultAsync(ct);
        if (nachbar is null) return;

        var eigene = stufe.Reihenfolge;
        var fremde = nachbar.Reihenfolge;
        var freierWert = (await db.Kulturstufen.MaxAsync(k => (int?)k.Reihenfolge, ct) ?? 0) + 1;

        // Drei Schritte statt zwei: die eigene Zeile zuerst auf einen garantiert freien Wert parken, dann
        // ist der Zielwert für den Nachbarn frei, dann die geparkte Zeile auf den Wert des Nachbarn setzen.
        stufe.Reihenfolge = freierWert;
        await db.SaveChangesAsync(ct);

        nachbar.Reihenfolge = eigene;
        await db.SaveChangesAsync(ct);

        stufe.Reihenfolge = fremde;
        await db.SaveChangesAsync(ct);

        await transaktion.CommitAsync(ct);
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var stufe = await db.Kulturstufen.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException(nameof(Kulturstufe), id);

        db.Remove(stufe);
        await db.SaveChangesDeletingAsync(
            "Kulturstufe wird noch von Bestand oder Bewegungen verwendet und kann nicht gelöscht werden — "
            + "stattdessen auf 'inaktiv' setzen.", ct);
    }
}
