using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AngebotListPage : Page
{
    public AngebotListViewModel ViewModel { get; }
    public AngebotListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AngebotListViewModel>();
        InitializeComponent();
    }
}
