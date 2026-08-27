using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class EingangsrechnungListPage : Page
{
    public EingangsrechnungListViewModel ViewModel { get; }
    public EingangsrechnungListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<EingangsrechnungListViewModel>();
        InitializeComponent();
    }
}
