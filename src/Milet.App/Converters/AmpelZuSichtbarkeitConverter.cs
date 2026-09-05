using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Milet.Application.Gaertnerei;

namespace Milet.App.Converters;

/// <summary>Blendet die Ampel-Ellipse ein, deren <c>ConverterParameter</c> zum Wert passt
/// ("Gruen"/"Gelb"/"Rot"/"Neutral", Neutral für einen fehlenden oder unbekannten Wert).
///
/// Ersetzt den früheren AmpelToColorConverter, der feste Farben lieferte: Ein IValueConverter kennt kein
/// Element und kann darum nicht theme-abhängig auflösen, und Bindungen werden beim Themewechsel nicht neu
/// ausgewertet — der Punkt hätte bis zur nächsten Navigation die alte Farbe behalten. Über die
/// Sichtbarkeit gesteuert, trägt jede Ellipse ihre Farbe als {ThemeResource}, die WinUI beim Umschalten
/// selbst neu auflöst.</summary>
public sealed class AmpelZuSichtbarkeitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var gefragt = parameter as string;
        var tatsaechlich = value is VerfuegbarkeitAmpel ampel ? ampel.ToString() : "Neutral";

        return string.Equals(gefragt, tatsaechlich, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
