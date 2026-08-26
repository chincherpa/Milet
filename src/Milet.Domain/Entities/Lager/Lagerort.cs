using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Lagerort : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
