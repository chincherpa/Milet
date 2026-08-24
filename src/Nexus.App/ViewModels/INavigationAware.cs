using Microsoft.UI.Xaml.Navigation;

namespace Nexus.App.ViewModels;

/// <summary>
/// ViewModels, die den Navigationsparameter (z. B. eine Id) auswerten müssen, implementieren dies.
/// Die zugehörige Page ruft es aus ihrem eigenen OnNavigatedTo-Override auf.
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo(NavigationEventArgs args);
}
