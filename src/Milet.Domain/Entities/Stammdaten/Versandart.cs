namespace Nexus.Domain.Entities.Stammdaten;

public class Versandart
{
    public int Id { get; set; }

    public string Bezeichnung { get; set; } = string.Empty;

    public decimal? Kosten { get; set; }
}
