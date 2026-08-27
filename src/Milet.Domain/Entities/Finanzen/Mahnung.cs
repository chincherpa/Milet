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
    public decimal Gebuehr { get; set; }
    public decimal Gesamtbetrag { get; set; }

    public List<MahnungPosition> Positionen { get; set; } = [];
}
