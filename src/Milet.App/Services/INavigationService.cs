using Microsoft.UI.Xaml.Controls;

namespace Milet.App.Services;

public interface INavigationService
{
    void Initialize(Frame frame);

    void Register<TViewModel, TPage>()
        where TViewModel : class
        where TPage : Page;

    bool Navigate<TViewModel>(object? parameter = null)
        where TViewModel : class;
}
