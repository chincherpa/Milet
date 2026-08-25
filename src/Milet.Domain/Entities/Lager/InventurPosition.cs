using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

public class InventurPosition
{
    public int Id { get; set; }
    public int InventurId { get; set; }
    public Inventur? Inventur { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    /// <summary>Eingefroren beim Anlegen der Inventur (aktueller ArtikelBestand.Menge zu diesem Zeitpunkt).</summary>
    public decimal SollMenge { get; set; }

    /// <summary>Null solange nicht gezählt.</summary>
    public decimal? IstMenge { get; set; }
}
