namespace Nexus.Domain.Entities.Stammdaten;

public class Zahlungsbedingung
{
    public int Id { get; set; }

    public string Bezeichnung { get; set; } = string.Empty;

    public int ZielTage { get; set; }

    public int? SkontoTage { get; set; }

    public decimal? SkontoProzent { get; set; }
}
