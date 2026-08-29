using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class AuthService(IDbContextFactory<MiletDbContext> dbContextFactory) : IAuthService
{
    /// <summary>
    /// Hash eines Passworts, das nie vergeben wird. Gegen ihn wird verifiziert, wenn der Benutzer nicht
    /// existiert oder deaktiviert ist — damit die Antwortzeit in allen Fällen dieselbe ist. Ohne das läuft
    /// PBKDF2 (210 000 Iterationen) nur für existierende, aktive Benutzer: der Laufzeitunterschied liegt im
    /// dreistelligen Millisekundenbereich und ist über das Netz eindeutig messbar — die einheitliche
    /// Fehlermeldung allein verhindert die User-Enumeration also nicht.
    ///
    /// Lazy, damit die Kosten beim ersten Anmeldeversuch anfallen und nicht beim Start.
    /// </summary>
    private static readonly Lazy<string> DummyHash =
        new(() => PasswortHasher.Hash("nie-vergebenes-Passwort-fuer-konstante-Antwortzeit"));

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
        // "Passwort falsch" vs. "Benutzer deaktiviert" — kein User-Enumeration-Leck. Verifiziert wird immer,
        // notfalls gegen den Dummy-Hash (s. o.), damit auch die Antwortzeit nichts verrät; das Ergebnis der
        // Dummy-Prüfung wird verworfen.
        var anmeldbar = benutzer is not null && benutzer.Aktiv;
        var passwortKorrekt = PasswortHasher.Verify(passwort, anmeldbar ? benutzer!.PasswortHash : DummyHash.Value);

        if (!anmeldbar || !passwortKorrekt)
        {
            return null;
        }

        return new BenutzerSessionDto
        {
            BenutzerId = benutzer!.Id,
            BenutzerName = benutzer.Anzeigename,
            RollenName = benutzer.Rolle.Name,
            Rechte = benutzer.Rolle.Rechte.Select(r => r.Code).ToList(),
        };
    }
}
