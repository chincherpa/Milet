namespace Milet.Domain.Entities.Lager;

public enum LagerbewegungTyp
{
    Korrektur = 0,
    Lieferung = 1,
    InventurKorrektur = 2,

    /// <summary>Positiver Zugang durch Wareneingang aus einer Bestellung (Phase 4).</summary>
    Wareneingang = 3,

    /// <summary>Erstzugang einer Kultur auf eine Stufe (Anzucht/Aussaat), Phase 8.</summary>
    Kulturzugang = 5,

    /// <summary>Umbuchung auf eine andere Kulturstufe — zwei Ledger-Zeilen (Abgang alte, Zugang neue Stufe/Sektion), Phase 8.</summary>
    Stufenwechsel = 6,

    /// <summary>Ortswechsel ohne Stufenwechsel — zwei Ledger-Zeilen, Phase 8.</summary>
    Umsetzen = 7,

    /// <summary>Realer Verlust (Frost, Trockenheit, Pilz) — eigener Typ statt „Korrektur", damit die Ausfallquote je Sorte/Stufe auswertbar bleibt (E7), Phase 8.</summary>
    Ausfall = 8,

    /// <summary>Gegenbuchung eines Storno (Lieferschein- oder Wareneingang-Rückgängigmachung, s. StornoService) —
    /// eigener Typ statt „Korrektur", damit ein Storno im Ledger von einer manuellen Korrektur unterscheidbar bleibt.</summary>
    StornoRueckgabe = 9,
}
