namespace Milet.Application.Admin;

/// <summary>
/// Fester Katalog der Rechte-Codes — deckungsgleich mit den Top-Level-Menüpunkten der
/// Navigation und dem per Seed angelegten <c>Recht</c>-Katalog (s. AdminSeed).
/// </summary>
public static class RechtCodes
{
    public const string Stammdaten = "Stammdaten";
    public const string Verkauf = "Verkauf";
    public const string Einkauf = "Einkauf";
    public const string Lager = "Lager";
    public const string Finanzen = "Finanzen";
    public const string Reporting = "Reporting";
    public const string Administration = "Administration";

    public static readonly IReadOnlyList<string> Alle =
    [
        Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Administration,
    ];
}
