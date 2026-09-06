using Milet.Domain.Common;

namespace Milet.Domain.Entities.Finanzen;

/// <summary>Ergebnis eines Mahnlaufs für einen Kunden — kein Beleg-Subtyp (PLAN.md). Insert-only nach
/// Erzeugung, keine RowVersion nötig (wird nie nachträglich editiert, nur storniert wäre eine spätere Phase).</summary>
public class Mahnung : AuditableEntity
{
    public int Id { get; set; }

    public int KundeId { get; set; }
    public Entities.Stammdaten.Kunde? Kunde { get; set; }

    public DateOnly MahnDatum { get; set; }
    public int Mahnstufe { get; set; }

    /// <summary>Gebühr der Mahnstufe. ACHTUNG: sie ist ein Wert des Anschreibens, KEINE Forderung — es
    /// entsteht kein OffenerPosten dafür, und der DATEV-Export bucht sie nicht. Geht sie ein, gibt es nichts,
    /// wogegen sie ausgeglichen werden könnte.
    ///
    /// Das als echte Forderung zu führen ist bewusst nicht gemacht worden, weil es kein Bugfix, sondern ein
    /// Feature mit Buchhaltungsentscheidung wäre: OffenerPosten.BelegId ist nicht nullable, eine Gebühren-
    /// forderung ohne Beleg ist im Modell nicht vorgesehen. Nötig wären: Spalte nullable, Null-Behandlung in
    /// DatevExportService/ZahlungService/OffenePostenService und ein Erlöskonto für Mahngebühren in der
    /// FibuKonfiguration. Bis dahin sind die Standardgebühren des Seeds (Stufe 1 = 0,00) die sichere
    /// Einstellung. S. REVIEW_2026-09-06.md Befund 18.</summary>
    public decimal Gebuehr { get; set; }

    /// <summary>Summe der gemahnten offenen Beträge PLUS <see cref="Gebuehr"/> — der Betrag, der im
    /// Anschreiben steht, nicht die buchhalterische Forderung (s. dort).</summary>
    public decimal Gesamtbetrag { get; set; }

    public List<MahnungPosition> Positionen { get; set; } = [];
}
