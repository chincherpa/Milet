using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Milet.App.ViewModels;

namespace Milet.App;

public sealed partial class LoginWindow : Window
{
    public LoginViewModel ViewModel { get; }

    public LoginWindow()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<LoginViewModel>();
        ViewModel.AngemeldetErfolgreich += OnAngemeldetErfolgreich;
    }

    private void PasswortBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Passwort = PasswortBox.Password;
    }

    private void OnAngemeldetErfolgreich()
    {
        ViewModel.AngemeldetErfolgreich -= OnAngemeldetErfolgreich;

        var mainWindow = new MainWindow();
        App.MainWindow = mainWindow;
        mainWindow.Activate();
        Close();
    }
}
