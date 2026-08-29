using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BenutzerverwaltungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IBenutzerverwaltungService
{
    private static readonly BenutzerValidator Validator = new();

    public async Task<IReadOnlyList<BenutzerDto>> ListeAsync(CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var benutzer = await db.Benutzer.AsNoTracking().Include(b => b.Rolle)
            .OrderBy(b => b.Benutzername).ToListAsync(ct);

        return benutzer.Select(b => b.ToDto()).ToList();
    }

    public async Task<BenutzerDto> LadeAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var benutzer = await db.Benutzer.AsNoTracking().Include(b => b.Rolle)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Benutzer), id);

        return benutzer.ToDto();
    }

    public async Task<BenutzerDto> SpeichereAsync(BenutzerDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (dto.Id == 0)
        {
            var neu = new Benutzer
            {
                Benutzername = dto.Benutzername,
                Anzeigename = dto.Anzeigename,
                Email = dto.Email,
                PasswortHash = PasswortHasher.Hash(dto.NeuesPasswort!),
                RolleId = dto.RolleId,
                Aktiv = dto.Aktiv,
            };

            db.Benutzer.Add(neu);
            await db.SaveChangesTranslatingConcurrencyAsync(nameof(Benutzer), dto.Id, ct);
            return neu.ToDto();
        }

        var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == dto.Id, ct)
            ?? throw new NotFoundException(nameof(Benutzer), dto.Id);

        db.Entry(benutzer).Property(b => b.RowVersion).OriginalValue = dto.RowVersion;

        benutzer.Benutzername = dto.Benutzername;
        benutzer.Anzeigename = dto.Anzeigename;
        benutzer.Email = dto.Email;
        benutzer.RolleId = dto.RolleId;
        benutzer.Aktiv = dto.Aktiv;
        if (!string.IsNullOrEmpty(dto.NeuesPasswort))
        {
            benutzer.PasswortHash = PasswortHasher.Hash(dto.NeuesPasswort);
        }

        await StelleSicherDassEinAdminBleibtAsync(db, benutzer.Id, benutzer.Aktiv, benutzer.RolleId, ct);

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Benutzer), dto.Id, ct);
        return benutzer.ToDto();
    }

    /// <summary>
    /// Verhindert, dass sich der letzte Administrator selbst aussperrt — durch Deaktivieren, Löschen oder
    /// Zuweisen einer Rolle ohne Administrationsrecht. Danach wäre die Benutzer- und Rollenverwaltung für
    /// niemanden mehr erreichbar und nur noch per direktem SQL zu reparieren.
    ///
    /// Gibt es schon vor der Änderung keinen aktiven Administrator, greift die Sperre nicht: sie soll den
    /// letzten Administrator schützen, nicht eine bereits verfahrene Installation weiter blockieren.
    /// </summary>
    private static async Task StelleSicherDassEinAdminBleibtAsync(
        MiletDbContext db, int benutzerId, bool bleibtAktiv, int bleibtRolleId, CancellationToken ct)
    {
        var adminRollenIds = await db.Rollen
            .Where(r => r.Rechte.Any(recht => recht.Code == RechtCodes.Administration))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var adminsVorher = await db.Benutzer.CountAsync(b => b.Aktiv && adminRollenIds.Contains(b.RolleId), ct);
        if (adminsVorher == 0) return;

        var andereAdmins = await db.Benutzer.CountAsync(
            b => b.Id != benutzerId && b.Aktiv && adminRollenIds.Contains(b.RolleId), ct);
        if (andereAdmins > 0) return;

        if (bleibtAktiv && adminRollenIds.Contains(bleibtRolleId)) return;

        throw new InvalidOperationException(
            "Der letzte aktive Administrator kann nicht deaktiviert, gelöscht oder einer Rolle ohne "
            + "Administrationsrecht zugewiesen werden.");
    }

    public async Task PasswortZuruecksetzenAsync(int benutzerId, string neuesPasswort, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        if (string.IsNullOrWhiteSpace(neuesPasswort) || neuesPasswort.Length < 8)
        {
            throw new ValidationException(
                [new FluentValidation.Results.ValidationFailure(nameof(neuesPasswort), "Das Passwort muss mindestens 8 Zeichen lang sein.")]);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == benutzerId, ct)
            ?? throw new NotFoundException(nameof(Benutzer), benutzerId);

        benutzer.PasswortHash = PasswortHasher.Hash(neuesPasswort);
        await db.SaveChangesAsync(ct);
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Administration);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Benutzer), id);

        await StelleSicherDassEinAdminBleibtAsync(db, benutzer.Id, bleibtAktiv: false, bleibtRolleId: 0, ct);

        db.Benutzer.Remove(benutzer);
        await db.SaveChangesDeletingAsync(nameof(Benutzer), id, ct);
    }
}
