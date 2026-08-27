namespace Milet.Domain.Entities.Finanzen;

/// <summary>Config-Tabelle für das Mahnwesen — keine RowVersion/AuditableEntity, wie Zahlungsbedingung/Versandart.</summary>
public class Mahnstufe
{
    public int Id { get; set; }
    public int Stufe { get; set; }
    public int Karenztage { get; set; }
    public decimal Gebuehr { get; set; }
    public string? Mahntext { get; set; }
}
