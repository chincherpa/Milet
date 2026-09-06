namespace Milet.Domain.Entities.Verkauf;

public class BelegPosition
{
    public int Id { get; set; }

    public int BelegId { get; set; }
    public Beleg? Beleg { get; set; }

    public int PositionsNr { get; set; }
    public PositionsTyp PositionsTyp { get; set; } = PositionsTyp.Artikel;

    public int? ArtikelId { get; set; }
    public Domain.Entities.Stammdaten.Artikel? Artikel { get; set; }

    /// <summary>Snapshot — spätere Änderungen am Artikelstamm wirken nicht auf gespeicherte Belege.</summary>
    public string Bezeichnung { get; set; } = string.Empty;
    public string? EinheitKuerzel { get; set; }

    public decimal Menge { get; set; }
    public decimal Einzelpreis { get; set; }
    public decimal RabattProzent { get; set; }

    /// <summary>MwSt-Snapshot je Zeile — Satzänderungen wirken nicht rückwirkend.</summary>
    public int? MwStSatzId { get; set; }
    public decimal MwStSatzWert { get; set; }
    public int? SteuerSchluessel { get; set; }

    /// <summary>Nur bei Lieferschein-Positionen gesetzt — Ziel-Lagerort für die Bestandsabbuchung beim Buchen.</summary>
    public int? LagerortId { get; set; }
    public Domain.Entities.Lager.Lagerort? Lagerort { get; set; }

    /// <summary>Nur Lieferschein/Wareneingang — bestimmt, gegen welche Bestandszeile gebucht wird (Phase 8, E9).</summary>
    public int? SektionId { get; set; }
    public Domain.Entities.Gaertnerei.Sektion? Sektion { get; set; }
    public int? KulturstufeId { get; set; }
    public Domain.Entities.Gaertnerei.Kulturstufe? Kulturstufe { get; set; }

    public decimal GesamtNetto { get; set; }

    /// <summary>Trägt Teillieferung/Teilfakturierung/Sammelrechnung: offene Menge = Menge − Σ referenzierender Folgepositionen.</summary>
    public int? UrsprungsPositionId { get; set; }

    /// <summary>Berechnet die noch nicht überführte Menge dieser Position anhand aller Positionen im System,
    /// die auf sie verweisen.
    ///
    /// WICHTIG für Aufrufer: <paramref name="alle"/> darf keine Positionen STORNIERTER Belege enthalten —
    /// eine stornierte Lieferung hat die Menge nicht verbraucht und muss wieder überleitbar sein. Die Methode
    /// kann das nicht selbst filtern, weil sie den Belegstatus nicht kennt (Beleg-Navigation ist in den
    /// AsNoTracking-Abfragen der Aufrufer nicht geladen); gefiltert wird deshalb an der Abfrage. Alle
    /// heutigen Aufrufer tun das (BelegService, BelegUeberleitungService, VerfuegbarkeitService,
    /// ReportingService).</summary>
    public static decimal OffeneMenge(BelegPosition position, IEnumerable<BelegPosition> alle)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(alle);
        var uebernommen = alle.Where(p => p.UrsprungsPositionId == position.Id).Sum(p => p.Menge);
        return position.Menge - uebernommen;
    }
}
