using Milet.Domain.Common;
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Stammdaten;

public class Kunde : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Kundennummer { get; set; } = string.Empty;

    public Adresse Adresse { get; set; } = new();

    public string? Ansprechpartner { get; set; }

    public string? Telefon { get; set; }

    public string? Email { get; set; }

    /// <summary>Abweichende Empfängeradresse für Rechnungsversand per E-Mail.</summary>
    public string? EmailRechnung { get; set; }

    public string? UStIdNr { get; set; }

    public int? ZahlungsbedingungId { get; set; }
    public Zahlungsbedingung? Zahlungsbedingung { get; set; }

    public int? PreislisteId { get; set; }
    public Preisliste? Preisliste { get; set; }

    /// <summary>Genereller Kundenrabatt in Prozent.</summary>
    public decimal RabattProzent { get; set; }

    public decimal? Kreditlimit { get; set; }

    public bool Liefersperre { get; set; }

    /// <summary>DATEV-Debitorenkonto (Konvention: 10000 + Id, überschreibbar).</summary>
    public int? DebitorenkontoNr { get; set; }

    public string? Notiz { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
