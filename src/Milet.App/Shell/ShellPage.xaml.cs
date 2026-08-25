using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.ViewModels;
using Milet.App.ViewModels.Stammdaten;
using Milet.App.ViewModels.Verkauf;
using Milet.App.Views;
using Milet.App.Views.Stammdaten;
using Milet.App.Views.Verkauf;

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
        _navigation.Register<KundenListViewModel, KundenListPage>();
        _navigation.Register<KundeEditViewModel, KundeEditPage>();
        _navigation.Register<LieferantenListViewModel, LieferantenListPage>();
        _navigation.Register<LieferantEditViewModel, LieferantEditPage>();
        _navigation.Register<ArtikelListViewModel, ArtikelListPage>();
        _navigation.Register<ArtikelEditViewModel, ArtikelEditPage>();
        _navigation.Register<KleinstammViewModel, KleinstammPage>();

        _navigation.Register<AngebotListViewModel, AngebotListPage>();
        _navigation.Register<AuftragListViewModel, AuftragListPage>();
        _navigation.Register<RechnungListViewModel, RechnungListPage>();

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
            case "kunden":
                _navigation.Navigate<KundenListViewModel>();
                break;
            case "lieferanten":
                _navigation.Navigate<LieferantenListViewModel>();
                break;
            case "artikel":
                _navigation.Navigate<ArtikelListViewModel>();
                break;
            case "einstellungen":
                _navigation.Navigate<KleinstammViewModel>();
                break;
            case "angebote":
                _navigation.Navigate<AngebotListViewModel>();
                break;
            case "auftraege":
                _navigation.Navigate<AuftragListViewModel>();
                break;
            case "rechnungen":
                _navigation.Navigate<RechnungListViewModel>();
                break;
        }
    }
}
