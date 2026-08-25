using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Stammdaten;

namespace Milet.App.Views.Stammdaten;

public sealed partial class ArtikelListPage : Page
{
    public ArtikelListViewModel ViewModel { get; }

    public ArtikelListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ArtikelListViewModel>();
        InitializeComponent();
    }
}
