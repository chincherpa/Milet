using Milet.Domain.Common;
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Verkauf;

public abstract class Beleg : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Leer bei Entwurf einer Rechnung — erst beim Buchen atomar vergeben.</summary>
    public string BelegNummer { get; set; } = string.Empty;

    public DateOnly BelegDatum { get; set; }

    public int KundeId { get; set; }
    public Domain.Entities.Stammdaten.Kunde? Kunde { get; set; }

    /// <summary>Eingefroren bei Erstellung — spätere Adressänderungen am Kunden wirken nicht rückwirkend.</summary>
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

    /// <summary>Nur Rechnung: gesetzt beim Buchen (BelegDatum + ZahlungsbedingungZielTage).</summary>
    public DateOnly? Faelligkeit { get; set; }

    public DateOnly? Leistungsdatum { get; set; }

    public string? Kopftext { get; set; }
    public string? Fusstext { get; set; }

    public List<BelegPosition> Positionen { get; set; } = [];
    public List<BelegSteuerSumme> Steuersummen { get; set; } = [];

    public byte[] RowVersion { get; set; } = [];
}
