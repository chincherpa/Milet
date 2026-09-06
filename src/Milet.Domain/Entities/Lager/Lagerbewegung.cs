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

    /// <summary>Fachlicher Zeitpunkt der Bewegung — bei Kulturbuchungen das vom Benutzer erfasste Datum
    /// (nachgetragener Zugang, Ausfall von vorgestern), sonst der Buchungszeitpunkt. LOKALE Zeit, nicht UTC:
    /// sämtliche Auswertungen darüber (Kulturhistorie, Reporting) bilden ihre Filtergrenzen aus lokalen
    /// DateOnly-Werten der Oberfläche. Mit UtcNow fiel in Sommerzeit jede Buchung vor 02:00 Ortszeit in den
    /// Bericht des Vortags. Gleiche Quelle wie Belegdatum und NumberRangeService (s. Kommentar dort).</summary>
    public DateTime Zeitpunkt { get; set; }
    public int? BenutzerId { get; set; }

    /// <summary>Freitext des Benutzers zur Bewegung — bei Ausfall die Ursache ("Frostschaden Nacht 12./13."),
    /// bei Zugang/Umsetzen optionale Notiz. Nur die Kulturbuchungen füllen das Feld; Lieferschein- und
    /// Wareneingangsbuchungen tragen ihre Herkunft über BelegPositionId.</summary>
    public string? Bemerkung { get; set; }
}
