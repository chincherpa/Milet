namespace Milet.Domain.Entities.Stammdaten;

public class MwStSatz
{
    public int Id { get; set; }

    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Prozentsatz, z. B. 19.00.</summary>
    public decimal Satz { get; set; }

    /// <summary>DATEV-Steuerschlüssel (z. B. 3 = 19 % USt).</summary>
    public int? SteuerSchluessel { get; set; }

    public DateOnly GueltigAb { get; set; }

    /// <summary>Gegenkonto für den DATEV-Export gebuchter Ausgangsrechnungen dieses Steuersatzes
    /// (Umsatzerlöse, z. B. SKR03 8400 bei 19 %).</summary>
    public int? ErloeskontoNr { get; set; }

    /// <summary>Gegenkonto für den DATEV-Export gebuchter Eingangsrechnungen dieses Steuersatzes
    /// (Wareneingang/Aufwand, z. B. SKR03 3400 bei 19 %).</summary>
    public int? AufwandskontoNr { get; set; }
}
