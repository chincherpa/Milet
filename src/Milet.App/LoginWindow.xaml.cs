using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Milet.App.Services;

namespace Milet.App;

public sealed partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        // Vor Activate() — sonst erscheint das Fenster kurz im Systemtheme und springt dann um.
        App.Host.Services.GetRequiredService<IThemeService>().RegistriereFenster(this);

        LoginView.ViewModel.AngemeldetErfolgreich += OnAngemeldetErfolgreich;
    }

    private void OnAngemeldetErfolgreich()
    {
        LoginView.ViewModel.AngemeldetErfolgreich -= OnAngemeldetErfolgreich;

        var mainWindow = new MainWindow();
        App.MainWindow = mainWindow;
        mainWindow.Activate();
        Close();
    }
}
