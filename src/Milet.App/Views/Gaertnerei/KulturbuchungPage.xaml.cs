using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Gaertnerei;

namespace Milet.App.Views.Gaertnerei;

public sealed partial class KulturbuchungPage : Page
{
    public KulturbuchungViewModel ViewModel { get; }

    public KulturbuchungPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<KulturbuchungViewModel>();
        InitializeComponent();
    }
}
