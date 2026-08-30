using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

public class InventurPosition
{
    public int Id { get; set; }
    public int InventurId { get; set; }
    public Inventur? Inventur { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    /// <summary>NULL bei Hauptlager-Inventur (kein Feld) — sonst je vorhandener Bestandszeile dieses Feldes (E10).</summary>
    public int? SektionId { get; set; }
    public Sektion? Sektion { get; set; }
    public int? KulturstufeId { get; set; }
    public Kulturstufe? Kulturstufe { get; set; }

    /// <summary>Eingefroren beim Anlegen der Inventur (aktueller ArtikelBestand.Menge zu diesem Zeitpunkt).</summary>
    public decimal SollMenge { get; set; }

    /// <summary>Null solange nicht gezählt.</summary>
    public decimal? IstMenge { get; set; }
}
