using Microsoft.UI.Xaml.Data;

namespace Milet.App.Converters;

public sealed class DateOnlyToDateTimeOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is DateOnly d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null!;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTimeOffset dateTimeOffset)
        {
            return DateOnly.FromDateTime(dateTimeOffset.DateTime);
        }

        return Nullable.GetUnderlyingType(targetType) is not null ? null! : DateOnly.FromDateTime(DateTime.Today);
    }
}
