using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Milet.App.Converters;

/// <summary>Wandelt einen "#RRGGBB"-Hex-String (Kulturstufe.FarbeHex) in einen SolidColorBrush für die
/// Farbvorschau (Kulturstufen-Tab, Grundriss/Pflanzenübersicht-Highlighting).</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
        {
            return new SolidColorBrush(Colors.Gray);
        }

        try
        {
            var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
            var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
            var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
            return new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Colors.Gray);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
