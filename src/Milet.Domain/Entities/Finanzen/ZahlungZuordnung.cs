namespace Milet.Domain.Entities.Finanzen;

/// <summary>Eine Zeile innerhalb einer Zahlung — ordnet einen Teilbetrag (+ ggf. gewährtes Skonto)
/// einem konkreten OffenerPosten zu. Eine Zahlung kann mehrere Zeilen (mehrere OPs) haben.</summary>
public class ZahlungZuordnung
{
    public int Id { get; set; }

    public int ZahlungId { get; set; }
    public Zahlung? Zahlung { get; set; }

    public int OffenerPostenId { get; set; }
    public OffenerPosten? OffenerPosten { get; set; }

    public decimal Betrag { get; set; }
    public decimal SkontoBetrag { get; set; }
}
