using Milet.Domain.Entities.Verkauf;

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

    /// <summary>Kulturbuchungen (Zugang/Stufenwechsel/Umsetzen/Ausfall) und Grundriss-Pflege — abgegrenzt
    /// von <see cref="Lager"/>, damit eine Aushilfe umtopfen darf, ohne Lieferscheine buchen zu dürfen.</summary>
    public const string Gaertnerei = "Gaertnerei";

    /// <summary>
    /// Recht, das ein Beleg dieses Typs verlangt. Einzige Quelle für alle Belegpfade (Anlegen/Ändern,
    /// Überleiten, Buchen) — die Zuordnung darf nicht je Service auseinanderlaufen.
    /// </summary>
    public static string FuerBelegTyp(BelegTyp typ) => typ switch
    {
        BelegTyp.Lieferschein => Lager,
        _ when typ.IstEinkaufsBeleg() => Einkauf,
        _ => Verkauf,
    };

    public static readonly IReadOnlyList<string> Alle =
    [
        Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Administration, Gaertnerei,
    ];
}
