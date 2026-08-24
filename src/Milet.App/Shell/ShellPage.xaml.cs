using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.ViewModels;
using Milet.App.Views;

namespace Milet.App.Shell;

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
