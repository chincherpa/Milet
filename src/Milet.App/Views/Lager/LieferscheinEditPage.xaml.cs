using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class LieferscheinEditPage : Page
{
    public LieferscheinEditViewModel ViewModel { get; }
    public LieferscheinEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferscheinEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
