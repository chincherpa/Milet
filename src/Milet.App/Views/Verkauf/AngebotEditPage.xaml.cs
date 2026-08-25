using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AngebotEditPage : Page
{
    public AngebotEditViewModel ViewModel { get; }
    public AngebotEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AngebotEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
