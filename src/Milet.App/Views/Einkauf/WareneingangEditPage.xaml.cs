using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class WareneingangEditPage : Page
{
    public WareneingangEditViewModel ViewModel { get; }
    public WareneingangEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<WareneingangEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
