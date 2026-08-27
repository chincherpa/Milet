using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Finanzen;

namespace Milet.App.Views.Finanzen;

public sealed partial class MahnlaufPage : Page
{
    public MahnlaufViewModel ViewModel { get; }

    public MahnlaufPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<MahnlaufViewModel>();
        InitializeComponent();
    }
}
