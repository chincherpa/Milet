using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AuftragEditPage : Page
{
    public AuftragEditViewModel ViewModel { get; }
    public AuftragEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AuftragEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
