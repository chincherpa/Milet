using Milet.Domain.Common;
using Milet.Domain.Entities.Lager;

namespace Milet.Domain.Entities.Gaertnerei;

/// <summary>Klassische Lagerplatz-Ebene unterhalb eines Feldes (`Lagerort` mit `IstFeld = true`).
/// Koordinaten sind relativ zum Feld, nicht zum Plan.</summary>
public class Sektion : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;

    public decimal PosXMeter { get; set; }
    public decimal PosYMeter { get; set; }
    public decimal BreiteMeter { get; set; }
    public decimal HoeheMeter { get; set; }

    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public decimal FlaecheQm => BreiteMeter * HoeheMeter;
}
