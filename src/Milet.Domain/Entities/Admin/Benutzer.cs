using Milet.Domain.Common;

namespace Milet.Domain.Entities.Admin;

public class Benutzer : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Benutzername { get; set; } = string.Empty;

    public string Anzeigename { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Format s. <see cref="Milet.Domain.Services.PasswortHasher"/> — niemals Klartext.</summary>
    public string PasswortHash { get; set; } = string.Empty;

    public int RolleId { get; set; }

    public Rolle Rolle { get; set; } = null!;

    public bool Aktiv { get; set; } = true;

    /// <summary>Aufeinanderfolgende Fehlanmeldungen. Wird bei jeder erfolgreichen Anmeldung auf 0 gesetzt.</summary>
    public int FehlversuchZaehler { get; set; }

    /// <summary>Gesetzt, sobald <see cref="FehlversuchZaehler"/> die Schwelle erreicht; bis dahin ist die
    /// Anmeldung auch mit korrektem Passwort abgelehnt. Null = nicht gesperrt. Lokale Zeit (wie alles
    /// Zeitbezogene im Projekt, s. Lagerbewegung.Zeitpunkt).</summary>
    public DateTime? GesperrtBis { get; set; }

    /// <summary>Erzwingt einen Passwortwechsel direkt nach der Anmeldung. Gesetzt für den Erstbenutzer aus
    /// <c>AdminSeed</c> (dessen Passwort im öffentlichen Quellcode steht) und nach jedem administrativen
    /// Zurücksetzen — der Administrator kennt das gesetzte Passwort sonst dauerhaft mit.</summary>
    public bool PasswortAenderungErforderlich { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
