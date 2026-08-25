using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Stammdaten;

namespace Milet.App.Views.Stammdaten;

public sealed partial class ArtikelEditPage : Page
{
    public ArtikelEditViewModel ViewModel { get; }

    public ArtikelEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ArtikelEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e);
    }
}
