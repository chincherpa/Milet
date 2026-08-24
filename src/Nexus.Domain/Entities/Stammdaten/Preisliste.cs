namespace Nexus.Domain.Entities.Stammdaten;

public class Preisliste
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly? GueltigVon { get; set; }

    public DateOnly? GueltigBis { get; set; }

    public List<ArtikelPreis> Preise { get; set; } = [];
}
