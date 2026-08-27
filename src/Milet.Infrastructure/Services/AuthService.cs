using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class AuthService(IDbContextFactory<MiletDbContext> dbContextFactory) : IAuthService
{
    public async Task<BenutzerSessionDto?> AnmeldenAsync(string benutzername, string passwort, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(benutzername) || string.IsNullOrEmpty(passwort))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var benutzer = await db.Benutzer.AsNoTracking()
            .Include(b => b.Rolle).ThenInclude(r => r.Rechte)
            .FirstOrDefaultAsync(b => b.Benutzername == benutzername.Trim(), ct);

        // Bewusst keine unterschiedliche Fehlermeldung für "Benutzer existiert nicht" vs.
        // "Passwort falsch" vs. "Benutzer deaktiviert" — kein User-Enumeration-Leck.
        if (benutzer is null || !benutzer.Aktiv || !PasswortHasher.Verify(passwort, benutzer.PasswortHash))
        {
            return null;
        }

        return new BenutzerSessionDto
        {
            BenutzerId = benutzer.Id,
            BenutzerName = benutzer.Anzeigename,
            RollenName = benutzer.Rolle.Name,
            Rechte = benutzer.Rolle.Rechte.Select(r => r.Code).ToList(),
        };
    }
}
