using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Stammdaten;

namespace Milet.App.Views.Stammdaten;

public sealed partial class KleinstammPage : Page
{
    public KleinstammViewModel ViewModel { get; }

    public KleinstammPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<KleinstammViewModel>();
        InitializeComponent();
    }
}
