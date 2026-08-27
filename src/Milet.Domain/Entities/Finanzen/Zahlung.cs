using Milet.Domain.Common;

namespace Milet.Domain.Entities.Finanzen;

/// <summary>Erfasste Zahlung — kann mehrere OffenePosten gleichzeitig ausgleichen (s. ZahlungZuordnung).
/// Kein Beleg-Subtyp: keine GoBD-Nummernkreis-Pflicht, keine Immutability-Sperre (append-only in der Praxis,
/// Korrektur = neue Gegenzahlung, nie Edit/Delete einer bestehenden Zahlung).</summary>
public class Zahlung : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Genau eines gesetzt, je Typ — analog Beleg.KundeId/LieferantId.</summary>
    public int? KundeId { get; set; }
    public Entities.Stammdaten.Kunde? Kunde { get; set; }

    public int? LieferantId { get; set; }
    public Entities.Stammdaten.Lieferant? Lieferant { get; set; }

    public OffenerPostenTyp Typ { get; set; } = OffenerPostenTyp.Debitor;
    public DateOnly Zahlungsdatum { get; set; }
    public decimal Gesamtbetrag { get; set; }
    public string? Zahlungsart { get; set; }
    public string? Referenz { get; set; }

    public List<ZahlungZuordnung> Zuordnungen { get; set; } = [];

    /// <summary>Gesetzt beim DATEV-Export (Doppelexport-Marker, s. DatevExportService).</summary>
    public DateTime? ExportiertAm { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
