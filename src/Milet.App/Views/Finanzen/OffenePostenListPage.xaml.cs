using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Finanzen;
using Milet.Application.Finanzen;

namespace Milet.App.Views.Finanzen;

public sealed partial class OffenePostenListPage : Page
{
    public OffenePostenListViewModel ViewModel { get; }

    public OffenePostenListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<OffenePostenListViewModel>();
        InitializeComponent();
    }

    private void PostenListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel.AusgewaehltePosten = ((ListView)sender).SelectedItems.Cast<OffenePostenDto>().ToList();
}
