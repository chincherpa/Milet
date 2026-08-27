namespace Milet.Domain.Entities.Admin;

/// <summary>
/// Fester Rechte-Katalog, ein Eintrag je Modul (deckungsgleich mit den Top-Level-Menüpunkten
/// in der Navigation). Wird nur per Seed angelegt, nicht über die UI verwaltet.
/// </summary>
public class Recht
{
    public int Id { get; set; }

    /// <summary>Z. B. "Stammdaten", "Verkauf", "Administration" — siehe RechtCodes in Milet.Application.Admin.</summary>
    public string Code { get; set; } = string.Empty;

    public string Bezeichnung { get; set; } = string.Empty;

    public ICollection<Rolle> Rollen { get; set; } = new List<Rolle>();
}
