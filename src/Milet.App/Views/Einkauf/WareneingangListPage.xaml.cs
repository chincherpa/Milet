using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class WareneingangListPage : Page
{
    public WareneingangListViewModel ViewModel { get; }
    public WareneingangListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<WareneingangListViewModel>();
        InitializeComponent();
    }
}
