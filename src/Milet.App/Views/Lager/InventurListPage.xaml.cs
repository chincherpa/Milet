using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class InventurListPage : Page
{
    public InventurListViewModel ViewModel { get; }
    public InventurListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<InventurListViewModel>();
        InitializeComponent();
    }
}
