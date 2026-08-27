using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Einkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class BestellVorschlagPage : Page
{
    public BestellVorschlagViewModel ViewModel { get; }
    public BestellVorschlagPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BestellVorschlagViewModel>();
        InitializeComponent();
    }
}
