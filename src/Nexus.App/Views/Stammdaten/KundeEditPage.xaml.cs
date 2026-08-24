using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Nexus.App.ViewModels.Stammdaten;

namespace Nexus.App.Views.Stammdaten;

public sealed partial class KundeEditPage : Page
{
    public KundeEditViewModel ViewModel { get; }

    public KundeEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<KundeEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e);
    }
}
