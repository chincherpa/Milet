using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Inventur : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }
    public DateOnly Datum { get; set; }
    public InventurStatus Status { get; set; } = InventurStatus.Offen;
    public List<InventurPosition> Positionen { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
