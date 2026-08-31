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

    /// <summary>Zähler seit dem letzten erfolgreichen Login — zurückgesetzt bei Erfolg und bei jedem
    /// Passwort-Reset durch einen Administrator. S. AuthService.</summary>
    public int FehlgeschlageneVersuche { get; set; }

    /// <summary>Gesetzt, sobald <see cref="FehlgeschlageneVersuche"/> die Schwelle erreicht — Login wird bis
    /// zu diesem Zeitpunkt abgelehnt (KontoGesperrtException), unabhängig vom Passwort.</summary>
    public DateTime? GesperrtBis { get; set; }

    /// <summary>Erzwingt einen Passwortwechsel beim nächsten Login — gesetzt vom AdminSeed für das
    /// Initialpasswort und bei jedem Passwort-Reset durch einen Administrator (der neue Wert wird
    /// typischerweise außerhalb der App an den Benutzer weitergegeben).</summary>
    public bool PasswortWechselErforderlich { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
