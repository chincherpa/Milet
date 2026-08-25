using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class RechnungListPage : Page
{
    public RechnungListViewModel ViewModel { get; }
    public RechnungListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<RechnungListViewModel>();
        InitializeComponent();
    }
}
