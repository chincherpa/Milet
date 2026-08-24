using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Nexus.App.ViewModels.Stammdaten;

namespace Nexus.App.Views.Stammdaten;

public sealed partial class KundenListPage : Page
{
    public KundenListViewModel ViewModel { get; }

    public KundenListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<KundenListViewModel>();
        InitializeComponent();
    }
}
