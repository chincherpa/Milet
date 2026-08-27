using Milet.Domain.Common;

namespace Milet.Domain.Entities.Admin;

public class Benutzer : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Benutzername { get; set; } = string.Empty;

    public string Anzeigename { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Format s. <see cref="Milet.Domain.Services.PasswortHasher"/> — niemals Klartext.</summary>
    public string PasswortHash { get; set; } = string.Empty;

    public int RolleId { get; set; }

    public Rolle Rolle { get; set; } = null!;

    public bool Aktiv { get; set; } = true;

    public byte[] RowVersion { get; set; } = [];
}
