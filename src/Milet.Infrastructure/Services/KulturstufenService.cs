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

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Kulturstufe), stufe.Id, ct);
        return stufe.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var stufe = await db.Kulturstufen.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new NotFoundException(nameof(Kulturstufe), id);

        db.Remove(stufe);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Kulturstufe wird noch von Bestand oder Bewegungen verwendet und kann nicht gelöscht werden — stattdessen auf 'inaktiv' setzen.", ex);
        }
    }
}
