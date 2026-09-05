using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Milet.App.Services;
using Milet.App.Shell;

namespace Milet.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Vor Activate() (s. LoginWindow.OnAngemeldetErfolgreich) — das Hauptfenster startet damit
        // direkt in der gewählten Darstellung.
        App.Host.Services.GetRequiredService<IThemeService>().RegistriereFenster(this);

        RootFrame.Navigate(typeof(ShellPage));
    }
}
