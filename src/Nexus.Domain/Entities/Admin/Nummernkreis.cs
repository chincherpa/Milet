namespace Nexus.Domain.Entities.Admin;

/// <summary>
/// Nummernkreis für Stammdaten- und Belegnummern.
/// Vergabe ausschließlich über INumberRangeService (atomares UPDATE ... OUTPUT).
/// </summary>
public class Nummernkreis
{
    public int Id { get; set; }

    /// <summary>Z. B. "KD", "LF", "ART", "RE", "AN".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Null = fortlaufend ohne Jahresbezug.</summary>
    public int? Jahr { get; set; }

    /// <summary>Nummer, die bei der nächsten Vergabe zurückgegeben wird.</summary>
    public int NaechsteNummer { get; set; }

    /// <summary>.NET-Formatstring, z. B. "KD-{0:00000}".</summary>
    public string Format { get; set; } = "{0}";
}
