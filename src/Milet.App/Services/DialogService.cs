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
        var dialog = new ContentDialog
        {
            Title = titel,
            Content = nachricht,
            CloseButtonText = "OK",
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        await dialog.ShowAsync();
    }

    public async Task<bool> BestaetigenAsync(string titel, string nachricht)
    {
        var dialog = new ContentDialog
        {
            Title = titel,
            Content = nachricht,
            PrimaryButtonText = "Ja",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        var ergebnis = await dialog.ShowAsync();
        return ergebnis == ContentDialogResult.Primary;
    }
}
