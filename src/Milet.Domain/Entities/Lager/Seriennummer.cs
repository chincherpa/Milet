using Milet.Domain.Common;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

public class Seriennummer : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public string Nummer { get; set; } = string.Empty;
    public SeriennummerStatus Status { get; set; } = SeriennummerStatus.AufLager;

    /// <summary>Nur gesetzt, solange Status == AufLager.</summary>
    public int? LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
