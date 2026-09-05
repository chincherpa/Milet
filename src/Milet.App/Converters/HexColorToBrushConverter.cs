using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Milet.App.Converters;

/// <summary>Wandelt einen "#RRGGBB"-Hex-String (Kulturstufe.FarbeHex) in einen SolidColorBrush für die
/// Farbvorschau (Kulturstufen-Tab, Grundriss/Pflanzenübersicht-Highlighting).</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => FarbHelfer.VersucheHexZuFarbe(value as string, alpha: 255, out var farbe)
            ? new SolidColorBrush(farbe)
            : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
