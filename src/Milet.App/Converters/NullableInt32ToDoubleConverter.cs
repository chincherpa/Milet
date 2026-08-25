using Microsoft.UI.Xaml.Data;

namespace Milet.App.Converters;

public sealed class NullableInt32ToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int i ? (double)i : double.NaN;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is double d && !double.IsNaN(d) ? (int)d : null!;
}
