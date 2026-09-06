using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Milet.App.Services;

public interface IDialogService
{
    Task ZeigeFehlerAsync(string titel, string nachricht);

    Task<bool> BestaetigenAsync(string titel, string nachricht);
}

public sealed class DialogService : IDialogService
{
    public async Task ZeigeFehlerAsync(string titel, string nachricht)
    {
        var dialog = ErzeugeDialog(titel, nachricht);
        dialog.CloseButtonText = "OK";

        await dialog.ShowAsync();
    }

    public async Task<bool> BestaetigenAsync(string titel, string nachricht)
    {
        var dialog = ErzeugeDialog(titel, nachricht);
        dialog.PrimaryButtonText = "Ja";
        dialog.CloseButtonText = "Abbrechen";
        dialog.DefaultButton = ContentDialogButton.Close;

        var ergebnis = await dialog.ShowAsync();
        return ergebnis == ContentDialogResult.Primary;
    }

    private static ContentDialog ErzeugeDialog(string titel, string nachricht) => new()
    {
        Title = titel,
        Content = nachricht,
        XamlRoot = App.MainWindow.Content.XamlRoot,

        // Ein ContentDialog hängt im Popup-Baum des XamlRoot, nicht im Fensterbaum — das auf
        // Window.Content gesetzte RequestedTheme erbt er deshalb nicht. Ohne diese Zeile erscheinen
        // Fehler- und Bestätigungsdialoge im Systemtheme statt in der gewählten Darstellung.
        RequestedTheme = App.MainWindow.Content is FrameworkElement wurzel
            ? wurzel.RequestedTheme
            : ElementTheme.Default,
    };
}
