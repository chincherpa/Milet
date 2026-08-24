using Nexus.Domain.Common;
using Nexus.Domain.ValueObjects;

namespace Nexus.Domain.Entities.Stammdaten;

public class Lieferant : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Lieferantennummer { get; set; } = string.Empty;

    public Adresse Adresse { get; set; } = new();

    public string? Ansprechpartner { get; set; }

    public string? Telefon { get; set; }

    public string? Email { get; set; }

    public string? UStIdNr { get; set; }

    public int? ZahlungsbedingungId { get; set; }
    public Zahlungsbedingung? Zahlungsbedingung { get; set; }

    /// <summary>DATEV-Kreditorenkonto (Konvention: 70000 + Id, überschreibbar).</summary>
    public int? KreditorenkontoNr { get; set; }

    public string? Notiz { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
