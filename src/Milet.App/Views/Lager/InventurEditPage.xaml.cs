using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class InventurEditPage : Page
{
    public InventurEditViewModel ViewModel { get; }
    public InventurEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<InventurEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
