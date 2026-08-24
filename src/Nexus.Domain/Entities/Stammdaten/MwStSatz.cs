namespace Nexus.Domain.Entities.Stammdaten;

public class MwStSatz
{
    public int Id { get; set; }

    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Prozentsatz, z. B. 19.00.</summary>
    public decimal Satz { get; set; }

    /// <summary>DATEV-Steuerschlüssel (z. B. 3 = 19 % USt).</summary>
    public int? SteuerSchluessel { get; set; }

    public DateOnly GueltigAb { get; set; }
}
