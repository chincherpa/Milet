using Microsoft.UI.Xaml.Controls;

namespace Milet.App.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<Type, Type> _viewModelToPage = [];
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public void Register<TViewModel, TPage>()
        where TViewModel : class
        where TPage : Page
        => _viewModelToPage[typeof(TViewModel)] = typeof(TPage);

    public bool Navigate<TViewModel>(object? parameter = null)
        where TViewModel : class
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("NavigationService wurde nicht initialisiert.");
        }

        if (!_viewModelToPage.TryGetValue(typeof(TViewModel), out var pageType))
        {
            throw new InvalidOperationException($"Keine Seite für ViewModel '{typeof(TViewModel).Name}' registriert.");
        }

        if (_frame.CurrentSourcePageType == pageType && parameter is null)
        {
            return false;
        }

        return _frame.Navigate(pageType, parameter);
    }
}
