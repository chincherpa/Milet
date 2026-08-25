using Milet.Domain.Common;

namespace Milet.Domain.Entities.Finanzen;

public class OffenerPosten : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int BelegId { get; set; }
    public Entities.Verkauf.Beleg? Beleg { get; set; }
    public int KundeId { get; set; }
    public OffenerPostenTyp Typ { get; set; } = OffenerPostenTyp.Debitor;
    public decimal Betrag { get; set; }
    public decimal OffenerBetrag { get; set; }
    public DateOnly Faelligkeit { get; set; }
    public int Mahnstufe { get; set; }
    public bool Mahnsperre { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
