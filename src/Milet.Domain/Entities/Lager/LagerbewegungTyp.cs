namespace Milet.Domain.Entities.Lager;

/// <summary>Wareneingang folgt erst in Phase 4 — hier bewusst noch nicht als Wert angelegt (analog LieferantId in Phase 2).</summary>
public enum LagerbewegungTyp
{
    Korrektur = 0,
    Lieferung = 1,
    InventurKorrektur = 2,
}
