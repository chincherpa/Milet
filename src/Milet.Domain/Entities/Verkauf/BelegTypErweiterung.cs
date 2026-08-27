namespace Milet.Domain.Entities.Verkauf;

/// <summary>Unterscheidet Verkaufs- von Einkaufsbelegen — bestimmt, ob ein Beleg über Kunde oder Lieferant
/// läuft (siehe Beleg.KundeId/LieferantId) und ob beim Buchen ein Debitor- oder Kreditor-OP entsteht.</summary>
public static class BelegTypErweiterung
{
    private static readonly HashSet<BelegTyp> EinkaufsTypen =
        [BelegTyp.Bestellung, BelegTyp.Wareneingang, BelegTyp.Eingangsrechnung];

    public static bool IstEinkaufsBeleg(this BelegTyp typ) => EinkaufsTypen.Contains(typ);
}
