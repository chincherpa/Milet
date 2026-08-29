namespace Milet.Domain.Entities.Verkauf;

/// <summary>Unterscheidet Verkaufs- von Einkaufsbelegen — bestimmt, ob ein Beleg über Kunde oder Lieferant
/// läuft (siehe Beleg.KundeId/LieferantId) und ob beim Buchen ein Debitor- oder Kreditor-OP entsteht.</summary>
public static class BelegTypErweiterung
{
    private static readonly HashSet<BelegTyp> EinkaufsTypen =
        [BelegTyp.Bestellung, BelegTyp.Wareneingang, BelegTyp.Eingangsrechnung];

    public static bool IstEinkaufsBeleg(this BelegTyp typ) => EinkaufsTypen.Contains(typ);

    /// <summary>Ermittelt den Belegtyp aus dem tatsächlichen TPH-Subtyp. Einzige Stelle, an der die
    /// Zuordnung Subtyp → <see cref="BelegTyp"/> steht — Rechteprüfung, Mapping und Überleitung leiten
    /// den Typ damit alle aus dem geladenen Objekt ab und nicht aus einer Angabe des Aufrufers.</summary>
    public static BelegTyp TypVon(Beleg beleg) => beleg switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        Lieferschein => BelegTyp.Lieferschein,
        Bestellung => BelegTyp.Bestellung,
        Wareneingang => BelegTyp.Wareneingang,
        Eingangsrechnung => BelegTyp.Eingangsrechnung,
        null => throw new ArgumentNullException(nameof(beleg)),
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {beleg.GetType().Name}."),
    };
}
