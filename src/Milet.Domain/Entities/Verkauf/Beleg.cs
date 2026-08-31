using Milet.Domain.Common;
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Verkauf;

public abstract class Beleg : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Leer bei Entwurf einer Rechnung — erst beim Buchen atomar vergeben. Bei allen anderen Belegtypen
    /// (inkl. Eingangsrechnung, siehe Architektur-Plan Phase 4) beim ersten Speichern vergeben.</summary>
    public string BelegNummer { get; set; } = string.Empty;

    public DateOnly BelegDatum { get; set; }

    /// <summary>Genau eines von KundeId/LieferantId ist gesetzt (DB-CHECK-Constraint, siehe BelegConfiguration) —
    /// abhängig vom Belegtyp: Verkaufsbelege (Angebot/Auftrag/Rechnung/Lieferschein) tragen KundeId,
    /// Einkaufsbelege (Bestellung/Wareneingang/Eingangsrechnung) tragen LieferantId.
    /// Siehe BelegTypErweiterung.IstEinkaufsBeleg.</summary>
    public int? KundeId { get; set; }
    public Domain.Entities.Stammdaten.Kunde? Kunde { get; set; }

    public int? LieferantId { get; set; }
    public Domain.Entities.Stammdaten.Lieferant? Lieferant { get; set; }

    /// <summary>Eingefroren bei Erstellung — spätere Adressänderungen wirken nicht rückwirkend.
    /// Bei Einkaufsbelegen invertierte Semantik: RechnungsadresseSnapshot = Anschrift des Lieferanten
    /// (Geschäftspartner-Anschrift für den Druck), LieferadresseSnapshot = eigene Firma (wohin die Ware geht).</summary>
    public Adresse RechnungsadresseSnapshot { get; set; } = new();
    public Adresse LieferadresseSnapshot { get; set; } = new();

    /// <summary>Snapshot aus Zahlungsbedingung bei Erstellung.</summary>
    public int ZahlungsbedingungZielTage { get; set; }
    public int? ZahlungsbedingungSkontoTage { get; set; }
    public decimal? ZahlungsbedingungSkontoProzent { get; set; }

    public BelegStatus Status { get; set; } = BelegStatus.Entwurf;

    public decimal SummeNetto { get; set; }
    public decimal SummeMwSt { get; set; }
    public decimal SummeBrutto { get; set; }

    /// <summary>Nur Rechnung: gesetzt beim Buchen (BelegDatum + ZahlungsbedingungZielTage). Bei Eingangsrechnung
    /// ebenfalls beim Buchen gesetzt (für die Fälligkeitsberechnung des Kreditor-OP), s. EingangsrechnungBuchenService.</summary>
    public DateOnly? Faelligkeit { get; set; }

    public DateOnly? Leistungsdatum { get; set; }

    public string? Kopftext { get; set; }
    public string? Fusstext { get; set; }

    /// <summary>Nur Eingangsrechnung: die Rechnungsnummer des Lieferanten. Die eigene BelegNummer (Nummernkreis
    /// "ER-...") ist nur eine interne Referenz ohne GoBD-Lückenlosigkeitspflicht — GoBD-relevant ist das
    /// Originaldokument des Lieferanten, dessen Nummer hier zusätzlich erfasst wird.</summary>
    public string? ExterneReferenz { get; set; }

    public List<BelegPosition> Positionen { get; set; } = [];
    public List<BelegSteuerSumme> Steuersummen { get; set; } = [];

    /// <summary>Gesetzt beim DATEV-Export (Doppelexport-Marker, s. DatevExportService) — nur relevant
    /// für gebuchte Rechnungen/Eingangsrechnungen.</summary>
    public DateTime? ExportiertAm { get; set; }

    /// <summary>Nur Gutschrift: Selbstreferenz auf die Rechnung, die diese Gutschrift storniert
    /// (s. StornoService). NULL bei jeder anderen Belegart und bei einer fachlichen Gutschrift ohne
    /// Storno-Bezug.</summary>
    public int? StorniertenBelegId { get; set; }
    public Beleg? StorniertenBeleg { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
