namespace Milet.Domain.Entities.Lager;

public enum LagerbewegungTyp
{
    Korrektur = 0,
    Lieferung = 1,
    InventurKorrektur = 2,

    /// <summary>Positiver Zugang durch Wareneingang aus einer Bestellung (Phase 4).</summary>
    Wareneingang = 3,
}
