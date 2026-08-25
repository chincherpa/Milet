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

    public decimal GesamtNetto { get; set; }

    /// <summary>Trägt Teillieferung/Teilfakturierung/Sammelrechnung: offene Menge = Menge − Σ referenzierender Folgepositionen.</summary>
    public int? UrsprungsPositionId { get; set; }

    /// <summary>Berechnet die noch nicht überführte Menge dieser Position anhand aller Positionen im System, die auf sie verweisen.</summary>
    public static decimal OffeneMenge(BelegPosition position, IEnumerable<BelegPosition> alle)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(alle);
        var uebernommen = alle.Where(p => p.UrsprungsPositionId == position.Id).Sum(p => p.Menge);
        return position.Menge - uebernommen;
    }
}
