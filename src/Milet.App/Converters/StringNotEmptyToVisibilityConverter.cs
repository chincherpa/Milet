using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Milet.App.Converters;

/// <summary>Blendet ein Element aus, solange der gebundene Text leer ist — für optionale Freitexte, die in
/// einer Liste sonst eine leere Zeile Höhe kosten würden (Kulturhistorie: Bemerkung).
///
/// Eigener Konverter statt einer Verkettung von <see cref="StringNotEmptyToBoolConverter"/> und
/// <see cref="BoolToVisibilityConverter"/>: XAML kann Konverter nicht verketten.</summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
