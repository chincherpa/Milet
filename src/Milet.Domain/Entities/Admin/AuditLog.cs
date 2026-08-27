namespace Milet.Domain.Entities.Admin;

/// <summary>
/// Append-only-Protokoll für Änderungen an <see cref="Common.AuditableEntity"/>-Entitäten
/// (Belege + Stammdaten, s. PLAN.md "Audit &amp; Concurrency"). Wird ausschließlich vom
/// AuditSaveChangesInterceptor geschrieben, nie über die UI bearbeitet.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTime Zeitpunkt { get; set; }

    public int? BenutzerId { get; set; }

    public string BenutzerName { get; set; } = "System";

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary>"Angelegt" | "Geändert" | "Gelöscht".</summary>
    public string Aktion { get; set; } = string.Empty;

    /// <summary>JSON-Objekt der geänderten Werte (Property -&gt; Wert), optional.</summary>
    public string? Aenderungen { get; set; }
}
