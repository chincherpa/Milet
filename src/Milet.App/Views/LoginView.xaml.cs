using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels;

namespace Milet.App.Views;

public sealed partial class LoginView : UserControl
{
    public LoginViewModel ViewModel { get; }

    public LoginView()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<LoginViewModel>();
    }

    private void PasswortBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Passwort = PasswortBox.Password;
    }

    private void NeuesPasswortBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.NeuesPasswort = NeuesPasswortBox.Password;
    }

    private void NeuesPasswortWiederholungBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.NeuesPasswortWiederholung = NeuesPasswortWiederholungBox.Password;
    }
}
