using Milet.Domain.Common;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

/// <summary>Snapshot je Artikel+Lagerort(+Sektion+Kulturstufe) — wird ausschließlich über ein atomares SQL-UPDATE fortgeschrieben, nie per Read-Modify-Write.</summary>
public class ArtikelBestand : IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }
    public decimal Menge { get; set; }

    /// <summary>NULL bei Handelsware ohne Kulturführung — dann verhält sich die Zeile exakt wie vor Phase 8.</summary>
    public int? SektionId { get; set; }
    public Sektion? Sektion { get; set; }
    public int? KulturstufeId { get; set; }
    public Kulturstufe? Kulturstufe { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
