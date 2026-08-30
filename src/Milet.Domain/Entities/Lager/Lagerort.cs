using Milet.Domain.Common;
using Milet.Domain.Entities.Gaertnerei;

namespace Milet.Domain.Entities.Lager;

public class Lagerort : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    public bool Aktiv { get; set; } = true;

    /// <summary>Ein Feld ist ein Lagerort mit Geometrie (E2) — kein eigener Entitätstyp.
    /// Geometrie bleibt NULL bei reinen Warenlagern (z. B. Hauptlager HL), die keine Sektionen kennen.</summary>
    public bool IstFeld { get; set; }
    public int? GaertnereiplanId { get; set; }
    public Gaertnereiplan? Gaertnereiplan { get; set; }
    public decimal? PosXMeter { get; set; }
    public decimal? PosYMeter { get; set; }
    public decimal? BreiteMeter { get; set; }
    public decimal? HoeheMeter { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
