using Milet.Domain.Entities.Finanzen;

namespace Milet.Domain.Services;

/// <summary>
/// Reine Selektionslogik ohne Datenzugriff: ermittelt, ob und für welche Mahnstufe ein
/// OffenerPosten an einem gegebenen Datum fällig ist.
/// </summary>
public static class MahnSelektionService
{
    /// <summary>Liefert die nächste fällige Mahnstufe für den OP, oder null wenn (noch) keine Mahnung fällig ist.</summary>
    public static int? ErmittleFaelligeStufe(OffenerPosten op, DateOnly heute, IReadOnlyCollection<Mahnstufe> stufen)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(stufen);

        if (op.Mahnsperre || op.OffenerBetrag <= 0m || op.Status == OffenerPostenStatus.Ausgeglichen)
        {
            return null;
        }

        var naechsteStufe = op.Mahnstufe + 1;
        var config = stufen.FirstOrDefault(s => s.Stufe == naechsteStufe);
        if (config is null)
        {
            // Keine Config für die nächste Stufe hinterlegt (z. B. Stufe 2 fehlt) — bewusst nicht auf eine
            // höhere Stufe überspringen, sondern gar keine Eskalation vorschlagen.
            return null;
        }

        return heute >= op.Faelligkeit.AddDays(config.Karenztage) ? naechsteStufe : null;
    }
}
