using Milet.Domain.Common;

namespace Milet.Domain.Entities.Admin;

public class Rolle : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Beschreibung { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<Recht> Rechte { get; set; } = new List<Recht>();

    public ICollection<Benutzer> Benutzer { get; set; } = new List<Benutzer>();
}
