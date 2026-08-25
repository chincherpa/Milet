using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class RechnungEditPage : Page
{
    public RechnungEditViewModel ViewModel { get; }
    public RechnungEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<RechnungEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
