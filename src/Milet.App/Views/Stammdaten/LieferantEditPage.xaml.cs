using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Stammdaten;

namespace Milet.App.Views.Stammdaten;

public sealed partial class LieferantEditPage : Page
{
    public LieferantEditViewModel ViewModel { get; }

    public LieferantEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferantEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e);
    }
}
