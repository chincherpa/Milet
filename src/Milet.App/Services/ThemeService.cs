using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Milet.App.Services;

/// <summary>Umschaltung zwischen Systemvorgabe, Hell und Dunkel zur Laufzeit.</summary>
public interface IThemeService
{
    ElementTheme Aktuell { get; }

    /// <summary>Meldet ein Fenster an und setzt sofort das aktuelle Theme darauf. Im Konstruktor des
    /// Fensters aufrufen — also vor <c>Activate()</c>, sonst flackert der Start kurz im Systemtheme.</summary>
    void RegistriereFenster(Window fenster);

    /// <summary>Setzt das Theme auf allen angemeldeten Fenstern und merkt sich die Wahl.</summary>
    void Anwenden(ElementTheme theme);
}

/// <summary>Setzt <see cref="FrameworkElement.RequestedTheme"/> auf dem Wurzelelement jedes Fensters.
///
/// Nicht über <c>Application.Current.RequestedTheme</c>: das ist nur vor der Erzeugung des ersten Fensters
/// setzbar und kennt keine „Systemvorgabe" — <see cref="ElementTheme.Default"/> liefert genau die
/// gewünschte Dreiwertigkeit.
///
/// Die App hat zwei Fenster (<c>LoginWindow</c>, danach <c>MainWindow</c>), und <c>MainWindow</c>
/// existiert beim Start noch nicht. Deshalb die Anmeldeliste statt eines festen Verweises.</summary>
public sealed class ThemeService : IThemeService
{
    private readonly IAppEinstellungenService _einstellungen;
    private readonly List<WeakReference<Window>> _fenster = [];

    public ThemeService(IAppEinstellungenService einstellungen)
    {
        _einstellungen = einstellungen;
        Aktuell = Enum.TryParse<ElementTheme>(einstellungen.Laden().Theme, out var gespeichert)
            ? gespeichert
            : ElementTheme.Default;
    }

    public ElementTheme Aktuell { get; private set; }

    public void RegistriereFenster(Window fenster)
    {
        _fenster.Add(new WeakReference<Window>(fenster));
        SetzeAufFenster(fenster, Aktuell);
    }

    public void Anwenden(ElementTheme theme)
    {
        Aktuell = theme;
        _einstellungen.Speichern(new AppEinstellungen { Theme = theme.ToString() });

        // Geschlossene Fenster (das LoginWindow nach der Anmeldung) fallen hier heraus, statt vom Dienst
        // am Leben gehalten zu werden.
        _fenster.RemoveAll(verweis => !verweis.TryGetTarget(out _));
        foreach (var verweis in _fenster)
        {
            if (verweis.TryGetTarget(out var fenster))
            {
                SetzeAufFenster(fenster, theme);
            }
        }
    }

    private static void SetzeAufFenster(Window fenster, ElementTheme theme)
    {
        if (fenster.Content is FrameworkElement wurzel)
        {
            wurzel.RequestedTheme = theme;
        }

        SetzeTitelleiste(fenster, theme);
    }

    /// <summary>Die Standard-Titelleiste folgt dem System-, nicht dem Element-Theme: bei „Hell" unter einem
    /// dunklen Windows bliebe sie sonst dunkel. Die Farbeigenschaften greifen erst ab Windows 11
    /// (<see cref="AppWindowTitleBar.IsCustomizationSupported"/>); unter Windows 10 bleibt es beim
    /// Systemverhalten.</summary>
    private static void SetzeTitelleiste(Window fenster, ElementTheme theme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titelleiste = fenster.AppWindow.TitleBar;

        if (theme == ElementTheme.Default)
        {
            // null heißt „Windows entscheidet" — genau das ist bei Systemvorgabe gewollt.
            titelleiste.BackgroundColor = null;
            titelleiste.ForegroundColor = null;
            titelleiste.InactiveBackgroundColor = null;
            titelleiste.InactiveForegroundColor = null;
            titelleiste.ButtonBackgroundColor = null;
            titelleiste.ButtonForegroundColor = null;
            titelleiste.ButtonInactiveBackgroundColor = null;
            titelleiste.ButtonInactiveForegroundColor = null;
            titelleiste.ButtonHoverBackgroundColor = null;
            titelleiste.ButtonHoverForegroundColor = null;
            titelleiste.ButtonPressedBackgroundColor = null;
            titelleiste.ButtonPressedForegroundColor = null;
            return;
        }

        var hell = theme == ElementTheme.Light;
        var hintergrund = hell ? Color.FromArgb(255, 243, 243, 243) : Color.FromArgb(255, 32, 32, 32);
        var vordergrund = hell ? Colors.Black : Colors.White;
        var inaktiverText = hell ? Color.FromArgb(255, 118, 118, 118) : Color.FromArgb(255, 150, 150, 150);
        var ueberfahren = hell ? Color.FromArgb(255, 233, 233, 233) : Color.FromArgb(255, 45, 45, 45);
        var gedrueckt = hell ? Color.FromArgb(255, 237, 237, 237) : Color.FromArgb(255, 41, 41, 41);

        titelleiste.BackgroundColor = hintergrund;
        titelleiste.ForegroundColor = vordergrund;
        titelleiste.InactiveBackgroundColor = hintergrund;
        titelleiste.InactiveForegroundColor = inaktiverText;
        titelleiste.ButtonBackgroundColor = hintergrund;
        titelleiste.ButtonForegroundColor = vordergrund;
        titelleiste.ButtonInactiveBackgroundColor = hintergrund;
        titelleiste.ButtonInactiveForegroundColor = inaktiverText;
        titelleiste.ButtonHoverBackgroundColor = ueberfahren;
        titelleiste.ButtonHoverForegroundColor = vordergrund;
        titelleiste.ButtonPressedBackgroundColor = gedrueckt;
        titelleiste.ButtonPressedForegroundColor = vordergrund;
    }
}
