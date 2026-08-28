using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Entities.Lager;

/// <summary>Append-only Ledger — wird nie geändert, nur eingefügt. Quelle der Wahrheit für Bestand.</summary>
public class Lagerbewegung
{
    public int Id { get; set; }

    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }

    public LagerbewegungTyp Typ { get; set; }

    /// <summary>Signiert: positiv = Zugang, negativ = Abgang.</summary>
    public decimal Menge { get; set; }

    public int? BelegPositionId { get; set; }
    public BelegPosition? BelegPosition { get; set; }

    public int? SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }

    public DateTime Zeitpunkt { get; set; }
    public int? BenutzerId { get; set; }

    /// <summary>Freitext-Begründung, bislang nur bei manueller Korrektur (<see cref="LagerbewegungTyp.Korrektur"/>) und
    /// Inventur-Abschluss (<see cref="LagerbewegungTyp.InventurKorrektur"/>) befüllt — bei Lieferschein-/Wareneingang-Buchungen
    /// erklärt sich die Bewegung bereits über <see cref="BelegPositionId"/>.</summary>
    public string? Grund { get; set; }
}
