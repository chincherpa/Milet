namespace Milet.Application.Abstractions;

/// <summary>
/// Liefert den angemeldeten Benutzer für Audit-Felder und Rechteprüfung.
/// Bis Phase 7 (Login) liefert die Implementierung null/"System".
/// </summary>
public interface ICurrentUserService
{
    int? BenutzerId { get; }

    string BenutzerName { get; }
}
