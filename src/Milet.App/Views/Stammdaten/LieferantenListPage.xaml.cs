using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Stammdaten;

namespace Milet.App.Views.Stammdaten;

public sealed partial class LieferantenListPage : Page
{
    public LieferantenListViewModel ViewModel { get; }

    public LieferantenListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferantenListViewModel>();
        InitializeComponent();
    }
}
