using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Milet.App.Converters;

/// <summary>ConverterParameter="invers" kehrt die Zuordnung um (true → Collapsed).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var istWahr = value is true;
        if (parameter is string p && p.Equals("invers", StringComparison.OrdinalIgnoreCase))
        {
            istWahr = !istWahr;
        }

        return istWahr ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}
