using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Milet.App.Themes;

/// <summary>Zugriff auf die semantischen Farben aus <c>Themes/Farben.xaml</c> für Code-Behind.
///
/// Nötig, weil die Gärtnerei-Pläne ihre Elemente imperativ in den Canvas hängen und deshalb kein
/// <c>{ThemeResource}</c> in XAML nutzen können. <c>Application.Current.Resources[key]</c> löst gegen das
/// Applikations-Theme auf, nicht gegen das Element-Theme — bei umgeschaltetem <c>RequestedTheme</c> käme
/// damit die falsche Farbe. Deshalb wird das ThemeDictionary hier anhand von
/// <see cref="FrameworkElement.ActualTheme"/> selbst ausgewählt.
///
/// Einschränkung: <see cref="ElementTheme"/> kennt nur Default/Light/Dark. Im Windows-Modus „hoher
/// Kontrast" greift für XAML weiterhin der HighContrast-Zweig, der Code-Behind-Pfad bleibt beim
/// Dunkel-Zweig. Betroffen ist nur die grafische Plandarstellung, deren Aussage ohnehin an den
/// Beschriftungen hängt.</summary>
internal static class ThemeRessourcen
{
    private static ResourceDictionary? _farben;

    /// <summary>Liefert den Brush zum Schlüssel passend zum aktuellen Theme von <paramref name="element"/>.
    /// Die Instanz stammt direkt aus dem ResourceDictionary und darf geteilt, aber nicht verändert werden.</summary>
    public static Brush Brush(FrameworkElement element, string schluessel)
    {
        var zweigName = element.ActualTheme == ElementTheme.Light ? "Light" : "Default";

        if (FarbenDictionary().ThemeDictionaries.TryGetValue(zweigName, out var zweigWert)
            && zweigWert is ResourceDictionary zweig
            && ((IDictionary<object, object>)zweig).TryGetValue(schluessel, out var brushWert)
            && brushWert is Brush brush)
        {
            return brush;
        }

        // Kann nur passieren, wenn ein Schlüssel in Farben.xaml umbenannt und eine Aufrufstelle vergessen
        // wurde. Bewusst laut: ein still zurückgegebener Graustich wäre im fertigen Plan kaum zu finden.
        throw new KeyNotFoundException(
            $"Farb-Ressource '{schluessel}' fehlt im Theme-Zweig '{zweigName}' von Themes/Farben.xaml.");
    }

    private static ResourceDictionary FarbenDictionary()
    {
        // Nicht nur nach "hat einen Light-Zweig" suchen — App.xaml mergt XamlControlsResources VOR
        // Farben.xaml, und das bringt selbst ThemeDictionaries mit einem "Light"-Zweig mit. Ohne die
        // Probe auf einen Milet-eigenen Schlüssel landet man auf dem WinUI-Dictionary statt auf Farben.xaml
        // und jede Abfrage wirft KeyNotFoundException (genau das ließ Grundriss/Pflanzenübersicht beim
        // ersten Zeichnen abstürzen).
        return _farben ??= Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.ThemeDictionaries.TryGetValue("Light", out var zweig)
                && zweig is ResourceDictionary rd
                && rd.ContainsKey("MiletAmpelGruenBrush"))
            ?? throw new InvalidOperationException(
                "Themes/Farben.xaml ist nicht in App.xaml gemergt — Theme-Farben sind nicht auflösbar.");
    }
}
