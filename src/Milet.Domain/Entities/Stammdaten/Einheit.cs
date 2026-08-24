namespace Milet.Domain.Entities.Stammdaten;

public class Einheit
{
    public int Id { get; set; }

    public string Kuerzel { get; set; } = string.Empty;

    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Erlaubte Nachkommastellen bei Mengenangaben (0 = nur ganze Stück).</summary>
    public int NachkommaStellen { get; set; }
}
