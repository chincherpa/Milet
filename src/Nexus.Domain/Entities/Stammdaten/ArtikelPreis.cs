namespace Nexus.Domain.Entities.Stammdaten;

/// <summary>
/// Staffelpreis eines Artikels in einer Preisliste. AbMenge 1 = Grundpreis.
/// </summary>
public class ArtikelPreis
{
    public int Id { get; set; }

    public int PreislisteId { get; set; }
    public Preisliste? Preisliste { get; set; }

    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    public decimal AbMenge { get; set; } = 1;

    /// <summary>Netto-Einzelpreis ab dieser Menge.</summary>
    public decimal Preis { get; set; }
}
