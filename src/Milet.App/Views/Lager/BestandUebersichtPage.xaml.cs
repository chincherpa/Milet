using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class BestandUebersichtPage : Page
{
    public BestandUebersichtViewModel ViewModel { get; }
    public BestandUebersichtPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BestandUebersichtViewModel>();
        InitializeComponent();
    }
}
