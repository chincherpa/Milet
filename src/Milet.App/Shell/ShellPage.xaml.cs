using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.ViewModels;
using Milet.App.ViewModels.Admin;
using Milet.App.ViewModels.Einkauf;
using Milet.App.ViewModels.Finanzen;
using Milet.App.ViewModels.Lager;
using Milet.App.ViewModels.Reporting;
using Milet.App.ViewModels.Stammdaten;
using Milet.App.ViewModels.Verkauf;
using Milet.App.Views;
using Milet.App.Views.Admin;
using Milet.App.Views.Einkauf;
using Milet.App.Views.Finanzen;
using Milet.App.Views.Lager;
using Milet.App.Views.Reporting;
using Milet.App.Views.Stammdaten;
using Milet.App.Views.Verkauf;
using Milet.Application.Abstractions;
using Milet.Application.Admin;

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
        _navigation.Register<AngebotEditViewModel, AngebotEditPage>();
        _navigation.Register<AuftragEditViewModel, AuftragEditPage>();
        _navigation.Register<RechnungEditViewModel, RechnungEditPage>();

        _navigation.Register<LieferscheinListViewModel, LieferscheinListPage>();
        _navigation.Register<LieferscheinEditViewModel, LieferscheinEditPage>();
        _navigation.Register<BestandUebersichtViewModel, BestandUebersichtPage>();
        _navigation.Register<InventurListViewModel, InventurListPage>();
        _navigation.Register<InventurEditViewModel, InventurEditPage>();

        _navigation.Register<BestellVorschlagViewModel, BestellVorschlagPage>();
        _navigation.Register<BestellungListViewModel, BestellungListPage>();
        _navigation.Register<BestellungEditViewModel, BestellungEditPage>();
        _navigation.Register<WareneingangListViewModel, WareneingangListPage>();
        _navigation.Register<WareneingangEditViewModel, WareneingangEditPage>();
        _navigation.Register<EingangsrechnungListViewModel, EingangsrechnungListPage>();
        _navigation.Register<EingangsrechnungEditViewModel, EingangsrechnungEditPage>();

        _navigation.Register<OffenePostenListViewModel, OffenePostenListPage>();
        _navigation.Register<MahnlaufViewModel, MahnlaufPage>();
        _navigation.Register<DatevExportViewModel, DatevExportPage>();

        _navigation.Register<ReportingViewModel, ReportingPage>();

        _navigation.Register<AdministrationViewModel, AdministrationPage>();

        _navigation.Navigate<DashboardViewModel>();
        AktualisiereMenueSichtbarkeit();
    }

    /// <summary>UI-Sichtbarkeit gemäß den Rechten des angemeldeten Benutzers (s. PLAN.md "RBAC":
    /// "Rechte-Guard in Services UND UI-Sichtbarkeit") — die Service-Guards (PruefeRecht) bleiben
    /// die eigentliche Durchsetzung, das hier blendet nur unerreichbare Menüpunkte aus.</summary>
    private void AktualisiereMenueSichtbarkeit()
    {
        var session = App.Host.Services.GetRequiredService<ICurrentSessionService>();

        SetzeMenuePunktSichtbarkeit("stammdaten", RechtCodes.Stammdaten, session);
        SetzeMenuePunktSichtbarkeit("verkauf", RechtCodes.Verkauf, session);
        SetzeMenuePunktSichtbarkeit("einkauf", RechtCodes.Einkauf, session);
        SetzeMenuePunktSichtbarkeit("lager", RechtCodes.Lager, session);
        SetzeMenuePunktSichtbarkeit("finanzen", RechtCodes.Finanzen, session);
        SetzeMenuePunktSichtbarkeit("reporting", RechtCodes.Reporting, session);
        SetzeMenuePunktSichtbarkeit("admin", RechtCodes.Administration, session);
    }

    private void SetzeMenuePunktSichtbarkeit(string tag, string rechtCode, ICurrentSessionService session)
    {
        var item = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag as string == tag);
        if (item is not null)
        {
            item.IsEnabled = session.HatRecht(rechtCode);
        }
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
            case "lieferscheine":
                _navigation.Navigate<LieferscheinListViewModel>();
                break;
            case "bestand":
                _navigation.Navigate<BestandUebersichtViewModel>();
                break;
            case "inventur":
                _navigation.Navigate<InventurListViewModel>();
                break;
            case "bestellvorschlag":
                _navigation.Navigate<BestellVorschlagViewModel>();
                break;
            case "bestellungen":
                _navigation.Navigate<BestellungListViewModel>();
                break;
            case "wareneingaenge":
                _navigation.Navigate<WareneingangListViewModel>();
                break;
            case "eingangsrechnungen":
                _navigation.Navigate<EingangsrechnungListViewModel>();
                break;
            case "offeneposten":
                _navigation.Navigate<OffenePostenListViewModel>();
                break;
            case "mahnlauf":
                _navigation.Navigate<MahnlaufViewModel>();
                break;
            case "datevexport":
                _navigation.Navigate<DatevExportViewModel>();
                break;
            case "reporting":
                _navigation.Navigate<ReportingViewModel>();
                break;
            case "admin":
                _navigation.Navigate<AdministrationViewModel>();
                break;
        }
    }
}
