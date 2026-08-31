namespace Milet.Application.Common;

/// <summary>
/// Login abgelehnt, weil das Konto nach zu vielen Fehlversuchen vorübergehend gesperrt ist (s.
/// AuthService). Anders als die übrigen Login-Fehler bewusst mit eigener Meldung/Zeitpunkt: der
/// Angreifer kennt den Benutzernamen bereits (sonst hätte er keine Fehlversuche gegen genau dieses
/// Konto sammeln können) — kein zusätzliches User-Enumeration-Leck.
/// </summary>
public sealed class KontoGesperrtException(DateTime gesperrtBis)
    : Exception($"Konto ist wegen zu vieler Fehlversuche gesperrt bis {gesperrtBis:HH:mm} Uhr.")
{
    public DateTime GesperrtBis { get; } = gesperrtBis;
}
