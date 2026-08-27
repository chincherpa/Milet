using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class BestellungListPage : Page
{
    public BestellungListViewModel ViewModel { get; }
    public BestellungListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BestellungListViewModel>();
        InitializeComponent();
    }
}
