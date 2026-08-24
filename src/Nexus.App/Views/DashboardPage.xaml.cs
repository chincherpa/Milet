using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Nexus.App.ViewModels;

namespace Nexus.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<DashboardViewModel>();
        InitializeComponent();
    }
}
