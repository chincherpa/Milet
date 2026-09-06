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
                // Das Initialpasswort steht im öffentlichen Quellcode — der erste Login erzwingt den Wechsel,
                // bevor irgendetwas anderes möglich ist.
                PasswortAenderungErforderlich = true,
            });
            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Nachtrag für bereits migrierte Datenbanken: die Spalte kommt mit Default false, ein dort
            // existierender Erstbenutzer würde also nie zum Wechsel gezwungen. Gesetzt wird das Flag nur,
            // wenn das Passwort tatsächlich noch das dokumentierte ist — wer es längst geändert hat, wird
            // nicht grundlos zu einem weiteren Wechsel genötigt.
            var standardAdmin = await db.Benutzer
                .FirstOrDefaultAsync(b => b.Benutzername == StandardAdminBenutzername, ct);
            if (standardAdmin is not null
                && !standardAdmin.PasswortAenderungErforderlich
                && PasswortHasher.Verify(StandardAdminPasswort, standardAdmin.PasswortHash))
            {
                standardAdmin.PasswortAenderungErforderlich = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
