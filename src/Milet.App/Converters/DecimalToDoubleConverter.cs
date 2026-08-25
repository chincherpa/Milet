using Microsoft.UI.Xaml.Data;

namespace Milet.App.Converters;

public sealed class DecimalToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is decimal d ? (double)d : double.NaN;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is not double d || double.IsNaN(d))
        {
            return Nullable.GetUnderlyingType(targetType) is not null ? null! : 0m;
        }

        return (decimal)d;
    }
}
