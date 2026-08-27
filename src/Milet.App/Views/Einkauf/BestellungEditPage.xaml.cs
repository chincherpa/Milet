using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class BestellungEditPage : Page
{
    public BestellungEditViewModel ViewModel { get; }
    public BestellungEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BestellungEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
