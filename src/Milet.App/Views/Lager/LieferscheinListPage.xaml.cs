using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class LieferscheinListPage : Page
{
    public LieferscheinListViewModel ViewModel { get; }
    public LieferscheinListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferscheinListViewModel>();
        InitializeComponent();
    }

    private void LieferscheineListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel.AusgewaehlteIds = ((ListView)sender).SelectedItems.Cast<BelegDto>().Select(b => b.Id).ToList();
}
