using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.Views.Lager;
using Milet.Application.Abstractions;
using Milet.Application.Lager;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AuftragEditViewModel : BelegEditViewModelBase
{
    private readonly ILagerortService _lagerortService;

    public AuftragEditViewModel(
        IBelegService belegService, IVerkaufLookupService lookupService, IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService, INavigationService navigation, IDialogService dialogService, ILagerortService lagerortService)
        : base(BelegTyp.Auftrag, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService)
    {
        _lagerortService = lagerortService;
    }

    [RelayCommand]
    private async Task UeberleitenZuLieferscheinAsync()
    {
        if (Id == 0) { Fehlermeldung = "Auftrag muss erst gespeichert werden."; return; }

        var lagerorte = (await _lagerortService.SucheAsync(null)).Where(l => l.Aktiv).ToList();
        if (lagerorte.Count == 0) { Fehlermeldung = "Kein aktiver Lagerort angelegt."; return; }

        var offenePositionen = await UeberleitungService.LadeOffenePositionenAsync(Id);
        if (offenePositionen.Count == 0) { Fehlermeldung = "Keine offenen Positionen für eine Lieferung vorhanden."; return; }

        var dialog = new TeillieferungDialog(offenePositionen, lagerorte) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await UeberleitungService.UeberleitenMitAuswahlAsync(Id, BelegTyp.Lieferschein, dialog.GewaehlteMengen(), dialog.AusgewaehlterLagerortId);
            Navigation.Navigate<Lager.LieferscheinListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AuftragListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
