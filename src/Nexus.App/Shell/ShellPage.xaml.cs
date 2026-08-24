using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Nexus.App.Services;
using Nexus.App.ViewModels;
using Nexus.App.Views;

namespace Nexus.App.Shell;

public sealed partial class ShellPage : Page
{
    private readonly INavigationService _navigation;

    public ShellPage()
    {
        InitializeComponent();

        _navigation = App.Host.Services.GetRequiredService<INavigationService>();
        _navigation.Initialize(ContentFrame);
        _navigation.Register<DashboardViewModel, DashboardPage>();

        _navigation.Navigate<DashboardViewModel>();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        switch (item.Tag as string)
        {
            case "dashboard":
                _navigation.Navigate<DashboardViewModel>();
                break;
        }
    }
}
