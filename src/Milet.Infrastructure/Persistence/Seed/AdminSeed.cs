using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Services;

namespace Milet.Infrastructure.Persistence.Seed;

/// <summary>
/// RBAC-Grunddaten (Phase 7): fester Rechte-Katalog, eine "Administrator"-Rolle mit allen
/// Rechten, ein Erstbenutzer, damit sich überhaupt jemand anmelden kann. Idempotent, "je
/// fehlendem Eintrag ergänzen"-Muster wie StammdatenSeed (s. dort für die Begründung).
/// </summary>
public static class AdminSeed
{
    /// <summary>Nur für die Erstanlage — muss nach dem ersten Login umgehend geändert werden
    /// (Benutzerverwaltung → Passwort zurücksetzen). S. docs/deployment.md.</summary>
    public const string StandardAdminBenutzername = "admin";
    public const string StandardAdminPasswort = "Milet!Admin1";

    public static async Task ApplyAsync(MiletDbContext db, CancellationToken ct = default)
    {
        var benoetigteRechte = RechtCodes.Alle
            .Select(code => new Recht { Code = code, Bezeichnung = code })
            .ToList();
        var vorhandeneRechteCodes = await db.Rechte.Select(r => r.Code).ToListAsync(ct);
        foreach (var recht in benoetigteRechte)
        {
            if (!vorhandeneRechteCodes.Contains(recht.Code))
            {
                db.Rechte.Add(recht);
            }
        }
        await db.SaveChangesAsync(ct);

        var administratorRolle = await db.Rollen
            .Include(r => r.Rechte)
            .FirstOrDefaultAsync(r => r.Name == "Administrator", ct);

        if (administratorRolle is null)
        {
            administratorRolle = new Rolle { Name = "Administrator", Beschreibung = "Voller Zugriff auf alle Module." };
            db.Rollen.Add(administratorRolle);
        }

        var alleRechte = await db.Rechte.ToListAsync(ct);
        foreach (var recht in alleRechte)
        {
            if (!administratorRolle.Rechte.Any(r => r.Code == recht.Code))
            {
                administratorRolle.Rechte.Add(recht);
            }
        }
        await db.SaveChangesAsync(ct);

        if (!await db.Benutzer.AnyAsync(ct))
        {
            db.Benutzer.Add(new Benutzer
            {
                Benutzername = StandardAdminBenutzername,
                Anzeigename = "Administrator",
                PasswortHash = PasswortHasher.Hash(StandardAdminPasswort),
                RolleId = administratorRolle.Id,
                Aktiv = true,
                PasswortWechselErforderlich = true,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
