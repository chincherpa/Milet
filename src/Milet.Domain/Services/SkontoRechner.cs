namespace Milet.Domain.Services;

/// <summary>
/// Reine Skonto-Berechnung ohne Datenzugriff. Die Skontofrist läuft ab Rechnungsdatum
/// (deutsche Kaufmannspraxis), nicht ab Fälligkeit.
/// </summary>
public static class SkontoRechner
{
    public static decimal BerechneSkonto(
        DateOnly rechnungsdatum, DateOnly zahlungsdatum, int? skontoTage, decimal? skontoProzent, decimal betrag)
    {
        if (skontoTage is null || skontoProzent is null || skontoProzent <= 0m || betrag <= 0m)
        {
            return 0m;
        }

        if (zahlungsdatum > rechnungsdatum.AddDays(skontoTage.Value))
        {
            return 0m;
        }

        return Math.Round(betrag * skontoProzent.Value / 100m, 2, MidpointRounding.ToEven);
    }
}
