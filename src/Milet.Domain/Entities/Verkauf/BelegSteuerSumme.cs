namespace Milet.Domain.Entities.Verkauf;

public class BelegSteuerSumme
{
    public int Id { get; set; }

    public int BelegId { get; set; }
    public Beleg? Beleg { get; set; }

    public decimal MwStSatzWert { get; set; }
    public int? SteuerSchluessel { get; set; }
    public decimal NettoSumme { get; set; }
    public decimal MwStBetrag { get; set; }
}
