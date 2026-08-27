using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Domain.Entities.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class RollenverwaltungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IRollenverwaltungService
{
    private static readonly RolleValidator Validator = new();

    public async Task<IReadOnlyList<RolleDto>> ListeAsync(CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rollen = await db.Rollen.AsNoTracking().Include(r => r.Rechte)
            .OrderBy(r => r.Name).ToListAsync(ct);

        return rollen.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<RechtDto>> AlleRechteAsync(CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rechte = await db.Rechte.AsNoTracking().OrderBy(r => r.Code).ToListAsync(ct);

        return rechte.Select(r => r.ToDto()).ToList();
    }

    public async Task<RolleDto> SpeichereAsync(RolleDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Rolle rolle;
        if (dto.Id == 0)
        {
            rolle = new Rolle { Name = dto.Name, Beschreibung = dto.Beschreibung };
            db.Rollen.Add(rolle);
        }
        else
        {
            rolle = await db.Rollen.Include(r => r.Rechte).FirstOrDefaultAsync(r => r.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Rolle), dto.Id);

            db.Entry(rolle).Property(r => r.RowVersion).OriginalValue = dto.RowVersion;
            rolle.Name = dto.Name;
            rolle.Beschreibung = dto.Beschreibung;
        }

        var alleRechte = await db.Rechte.ToDictionaryAsync(r => r.Code, ct);
        rolle.Rechte.Clear();
        foreach (var code in dto.RechteCodes)
        {
            if (alleRechte.TryGetValue(code, out var recht))
            {
                rolle.Rechte.Add(recht);
            }
        }

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Rolle), dto.Id, ct);
        return rolle.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rolle = await db.Rollen.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(Rolle), id);

        db.Rollen.Remove(rolle);
        await db.SaveChangesDeletingAsync(nameof(Rolle), id, ct);
    }
}
