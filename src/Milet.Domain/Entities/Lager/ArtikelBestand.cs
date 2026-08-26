using Milet.Domain.Common;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

/// <summary>Snapshot je Artikel+Lagerort — wird ausschließlich über ein atomares SQL-UPDATE fortgeschrieben, nie per Read-Modify-Write.</summary>
public class ArtikelBestand : IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }
    public decimal Menge { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
