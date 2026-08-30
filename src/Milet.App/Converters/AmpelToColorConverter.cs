using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Milet.Application.Gaertnerei;

namespace Milet.App.Converters;

/// <summary>VerfuegbarkeitAmpel → Farbe für das Verfügbarkeits-Panel im Verkauf (Phase 8, E8).</summary>
public sealed class AmpelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        VerfuegbarkeitAmpel.Gruen => new SolidColorBrush(Colors.LimeGreen),
        VerfuegbarkeitAmpel.Gelb => new SolidColorBrush(Colors.Orange),
        VerfuegbarkeitAmpel.Rot => new SolidColorBrush(Colors.Red),
        _ => new SolidColorBrush(Colors.Gray),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
