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

        if (dto.Id != 0)
        {
            await StelleSicherDassEinAdminBleibtAsync(
                db, dto.Id, behaeltAdminRecht: dto.RechteCodes.Contains(RechtCodes.Administration), ct);
        }

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

        await StelleSicherDassEinAdminBleibtAsync(db, id, behaeltAdminRecht: false, ct);

        db.Rollen.Remove(rolle);
        await db.SaveChangesDeletingAsync(nameof(Rolle), id, ct);
    }

    /// <summary>
    /// Gegenstück zur gleichnamigen Prüfung in <c>BenutzerverwaltungService</c>, von der anderen Seite her:
    /// wird der Administrator-Rolle das Recht entzogen oder die Rolle gelöscht, darf danach nicht die
    /// gesamte Installation ohne erreichbare Administration dastehen.
    /// </summary>
    private static async Task StelleSicherDassEinAdminBleibtAsync(
        MiletDbContext db, int rolleId, bool behaeltAdminRecht, CancellationToken ct)
    {
        if (behaeltAdminRecht) return;

        var adminRollenIds = await db.Rollen
            .Where(r => r.Rechte.Any(recht => recht.Code == RechtCodes.Administration))
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (!adminRollenIds.Contains(rolleId)) return;

        var adminsVorher = await db.Benutzer.CountAsync(b => b.Aktiv && adminRollenIds.Contains(b.RolleId), ct);
        if (adminsVorher == 0) return;

        var verbleibendeAdminRollen = adminRollenIds.Where(id => id != rolleId).ToList();
        var verbleibendeAdmins = await db.Benutzer.CountAsync(
            b => b.Aktiv && verbleibendeAdminRollen.Contains(b.RolleId), ct);
        if (verbleibendeAdmins > 0) return;

        throw new InvalidOperationException(
            "Der Rolle kann das Administrationsrecht nicht entzogen werden (und sie kann nicht gelöscht "
            + "werden): danach hätte kein aktiver Benutzer mehr Zugriff auf die Administration.");
    }
}
