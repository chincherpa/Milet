using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Admin;

namespace Milet.App.Views.Admin;

public sealed partial class AdministrationPage : Page
{
    public AdministrationViewModel ViewModel { get; }

    public AdministrationPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AdministrationViewModel>();
        InitializeComponent();

        // PasswordBox unterstützt kein x:Bind TwoWay (wie im Rest der App bei anderen Feldern) — die
        // Box muss zusätzlich manuell geleert werden, wenn das ViewModel das Passwort selbst zurücksetzt
        // (Auswahlwechsel, "Neu", erfolgreiches Speichern), sonst bliebe der zuletzt getippte Text sichtbar
        // stehen, obwohl das ViewModel schon wieder null trägt.
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.BenutzerNeuesPasswort) && string.IsNullOrEmpty(ViewModel.BenutzerNeuesPasswort))
            {
                BenutzerNeuesPasswortBox.Password = string.Empty;
            }
        };
    }

    private void BenutzerNeuesPasswortBox_PasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.BenutzerNeuesPasswort = BenutzerNeuesPasswortBox.Password;
    }
}
