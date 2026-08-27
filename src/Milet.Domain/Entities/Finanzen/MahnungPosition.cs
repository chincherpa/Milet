namespace Milet.Domain.Entities.Finanzen;

/// <summary>Snapshot-Zeile eines gemahnten OffenerPosten zum Zeitpunkt des Mahnlaufs.</summary>
public class MahnungPosition
{
    public int Id { get; set; }

    public int MahnungId { get; set; }
    public Mahnung? Mahnung { get; set; }

    public int OffenerPostenId { get; set; }
    public OffenerPosten? OffenerPosten { get; set; }

    public string BelegNummerSnapshot { get; set; } = string.Empty;
    public decimal OffenerBetragSnapshot { get; set; }
}
