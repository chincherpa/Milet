namespace Milet.Domain.Services;

/// <summary>Mindestanforderungen an ein Passwort — reine Domain-Regel, ohne DB testbar (wie
/// <see cref="PasswortHasher"/>). Gebraucht an zwei Stellen: beim erzwungenen Wechsel nach dem Login und
/// beim administrativen Zurücksetzen.
///
/// Bewusst schlicht gehalten (Länge statt Zeichenklassen-Zwang): erzwungene Sonderzeichen führen empirisch
/// zu vorhersagbaren Mustern („Passwort1!"), Länge ist der Faktor, der tatsächlich zählt.</summary>
public static class PasswortRegeln
{
    public const int MindestLaenge = 10;

    /// <summary>Wirft mit sprechendem Text, wenn das Passwort die Mindestanforderungen nicht erfüllt.</summary>
    public static void Pruefe(string? passwort)
    {
        if (string.IsNullOrWhiteSpace(passwort))
        {
            throw new InvalidOperationException("Passwort darf nicht leer sein.");
        }

        if (passwort.Length < MindestLaenge)
        {
            throw new InvalidOperationException($"Passwort muss mindestens {MindestLaenge} Zeichen lang sein.");
        }

        if (passwort.Trim().Length != passwort.Length)
        {
            throw new InvalidOperationException("Passwort darf nicht mit einem Leerzeichen beginnen oder enden.");
        }
    }
}
