using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class AuthService(IDbContextFactory<MiletDbContext> dbContextFactory) : IAuthService
{
    /// <summary>Ab so vielen aufeinanderfolgenden Fehlversuchen wird der Benutzer gesperrt.</summary>
    private const int MaximaleFehlversuche = 5;

    /// <summary>Dauer der Sperre. Bewusst eine Zeitsperre und keine dauerhafte Deaktivierung: die häufigste
    /// Ursache ist ein Tippfehler oder ein vergessenes Passwort, nicht ein Angriff — eine dauerhafte Sperre
    /// bräuchte jedes Mal einen Administrator und lädt dazu ein, die Funktion wieder abzuschalten.</summary>
    private static readonly TimeSpan Sperrdauer = TimeSpan.FromMinutes(15);

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

    public async Task<AnmeldeErgebnisDto> AnmeldenAsync(string benutzername, string passwort, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(benutzername) || string.IsNullOrEmpty(passwort))
        {
            return AnmeldeErgebnisDto.Fehlgeschlagen();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Getrackt geladen (nicht AsNoTracking): der Fehlversuchszähler wird unten fortgeschrieben.
        var benutzer = await db.Benutzer
            .Include(b => b.Rolle).ThenInclude(r => r.Rechte)
            .FirstOrDefaultAsync(b => b.Benutzername == benutzername.Trim(), ct);

        // Bewusst keine unterschiedliche Fehlermeldung für "Benutzer existiert nicht" vs.
        // "Passwort falsch" vs. "Benutzer deaktiviert" — kein User-Enumeration-Leck. Verifiziert wird immer,
        // notfalls gegen den Dummy-Hash (s. o.), damit auch die Antwortzeit nichts verrät; das Ergebnis der
        // Dummy-Prüfung wird verworfen.
        var anmeldbar = benutzer is not null && benutzer.Aktiv;
        var passwortKorrekt = PasswortHasher.Verify(passwort, anmeldbar ? benutzer!.PasswortHash : DummyHash.Value);

        if (!anmeldbar)
        {
            return AnmeldeErgebnisDto.Fehlgeschlagen();
        }

        var jetzt = DateTime.Now;
        var gesperrt = benutzer!.GesperrtBis is { } bis && bis > jetzt;

        if (!passwortKorrekt)
        {
            // Auch während einer laufenden Sperre weiterzählen: sonst könnte ein Angreifer die Sperre
            // aussitzen und danach mit vollem Zähler-Budget weitermachen.
            benutzer.FehlversuchZaehler++;
            if (benutzer.FehlversuchZaehler >= MaximaleFehlversuche)
            {
                benutzer.GesperrtBis = jetzt.Add(Sperrdauer);
            }

            await db.SaveChangesAsync(ct);
            return AnmeldeErgebnisDto.Fehlgeschlagen();
        }

        if (gesperrt)
        {
            // Die Sperre wird NUR bei korrektem Passwort offengelegt. Andernfalls verriete die Meldung einem
            // Angreifer, dass der Benutzername existiert — und genau das soll der Dummy-Hash oben verhindern.
            // Wer sein Passwort kennt, hat dagegen ein Recht auf die Auskunft, warum er nicht hereinkommt.
            return AnmeldeErgebnisDto.Gesperrt(benutzer.GesperrtBis!.Value);
        }

        if (benutzer.FehlversuchZaehler != 0 || benutzer.GesperrtBis is not null)
        {
            benutzer.FehlversuchZaehler = 0;
            benutzer.GesperrtBis = null;
            await db.SaveChangesAsync(ct);
        }

        return AnmeldeErgebnisDto.Erfolgreich(
            new BenutzerSessionDto
            {
                BenutzerId = benutzer.Id,
                BenutzerName = benutzer.Anzeigename,
                RollenName = benutzer.Rolle.Name,
                Rechte = benutzer.Rolle.Rechte.Select(r => r.Code).ToList(),
            },
            benutzer.PasswortAenderungErforderlich);
    }

    public async Task PasswortAendernAsync(
        string benutzername, string altesPasswort, string neuesPasswort, CancellationToken ct = default)
    {
        // Kein Rechte-Guard: das ist der Selbstbedienungspfad direkt nach dem Login (der Benutzer hat zu
        // diesem Zeitpunkt noch keine Sitzung). Autorisiert wird über das alte Passwort.
        PasswortRegeln.Pruefe(neuesPasswort);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Benutzername == benutzername.Trim() && b.Aktiv, ct);

        if (benutzer is null || !PasswortHasher.Verify(altesPasswort, benutzer.PasswortHash))
        {
            throw new InvalidOperationException("Benutzername oder bisheriges Passwort falsch.");
        }

        if (PasswortHasher.Verify(neuesPasswort, benutzer.PasswortHash))
        {
            throw new InvalidOperationException("Das neue Passwort muss sich vom bisherigen unterscheiden.");
        }

        benutzer.PasswortHash = PasswortHasher.Hash(neuesPasswort);
        benutzer.PasswortAenderungErforderlich = false;
        benutzer.FehlversuchZaehler = 0;
        benutzer.GesperrtBis = null;
        await db.SaveChangesAsync(ct);
    }
}
