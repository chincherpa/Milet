using Milet.Domain.Entities.Gaertnerei;
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

    /// <summary>NULL bei Handelsware ohne Kulturführung — dann verhält sich die Zeile exakt wie vor Phase 8.</summary>
    public int? SektionId { get; set; }
    public Sektion? Sektion { get; set; }
    public int? KulturstufeId { get; set; }
    public Kulturstufe? Kulturstufe { get; set; }

    public LagerbewegungTyp Typ { get; set; }

    /// <summary>Signiert: positiv = Zugang, negativ = Abgang.</summary>
    public decimal Menge { get; set; }

    public int? BelegPositionId { get; set; }
    public BelegPosition? BelegPosition { get; set; }

    public int? SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }

    public DateTime Zeitpunkt { get; set; }
    public int? BenutzerId { get; set; }

    /// <summary>Freitext-Grund — bei manueller Bestandskorrektur/Ausfall der vom Nutzer erfasste Grund, bei
    /// automatischen Buchungen (Lieferschein/Wareneingang-Buchen, Inventurabschluss, Storno) ein Verweis auf
    /// den auslösenden Beleg. NULL bei Zeilen, die vor Einführung dieses Felds entstanden sind (Phase 9,
    /// Task 13) oder deren Aufrufer keinen Grund mitgibt.</summary>
    public string? Bemerkung { get; set; }
}
