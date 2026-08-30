using Milet.Domain.Common;

namespace Milet.Domain.Entities.Gaertnerei;

/// <summary>Zeichenfläche für den Grundriss der Gärtnerei. Als Tabelle (nicht Singleton-Zeile) angelegt,
/// damit „mehrere Standorte" später ohne Schemabruch nachrüstbar ist — v1 zeigt genau einen Plan.</summary>
public class Gaertnereiplan : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;
    public decimal BreiteMeter { get; set; }
    public decimal HoeheMeter { get; set; }
    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
