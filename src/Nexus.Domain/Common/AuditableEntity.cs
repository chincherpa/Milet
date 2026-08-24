namespace Nexus.Domain.Common;

/// <summary>
/// Basisklasse für alle Entitäten mit Audit-Feldern.
/// Die Felder werden vom AuditSaveChangesInterceptor in der Infrastruktur gefüllt.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime ErstelltAm { get; set; }

    public int? ErstelltVonId { get; set; }

    public DateTime? GeaendertAm { get; set; }

    public int? GeaendertVonId { get; set; }
}
