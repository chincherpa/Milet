using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class EingangsrechnungEditPage : Page
{
    public EingangsrechnungEditViewModel ViewModel { get; }
    public EingangsrechnungEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<EingangsrechnungEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
