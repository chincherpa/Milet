using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Milet.App.Converters;

/// <summary>Liefert zu einer benutzerdefinierten Hintergrundfarbe ("#RRGGBB") die lesbare Textfarbe —
/// Schwarz auf hellem, Weiß auf dunklem Grund.
///
/// Ersetzt das feste <c>Foreground="White"</c> auf den Kulturstufen-Chips: Weiß auf einer hellen
/// Stufenfarbe (z. B. dem hellgrünen "#8BC34A" aus dem Seed) ist unlesbar, und zwar unabhängig davon,
/// welches App-Theme eingestellt ist — die Fläche trägt ja die Stammdatenfarbe, nicht die Themefarbe.</summary>
public sealed class KontrastFarbeConverter : IValueConverter
{
    /// <summary>Schwelle nach WCAG-Praxis: ab dieser relativen Luminanz trägt schwarzer Text besser.</summary>
    private const double Schwelle = 0.179;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Unlesbare Farbe: der HexColorToBrushConverter setzt für denselben Fall Grau als Fläche,
        // darauf ist Weiß die bessere Wahl.
        if (!FarbHelfer.VersucheHexZuFarbe(value as string, alpha: 255, out var farbe))
        {
            return new SolidColorBrush(Colors.White);
        }

        return new SolidColorBrush(FarbHelfer.RelativeLuminanz(farbe) > Schwelle ? Colors.Black : Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
