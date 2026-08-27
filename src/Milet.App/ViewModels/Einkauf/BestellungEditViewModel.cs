using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Lager;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class BestellungEditViewModel : EinkaufBelegEditViewModelBase
{
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly ILagerortService _lagerortService;

    public BestellungEditViewModel(
        IBelegService belegService, IEinkaufLookupService lookupService, IBelegUeberleitungService ueberleitungService,
        ILagerortService lagerortService, INavigationService navigation, IDialogService dialogService)
        : base(BelegTyp.Bestellung, belegService, lookupService, navigation, dialogService)
    {
        _ueberleitungService = ueberleitungService;
        _lagerortService = lagerortService;
    }

    [RelayCommand]
    private async Task UeberleitenZuWareneingangAsync()
    {
        if (Id == 0) { Fehlermeldung = "Bestellung muss erst gespeichert werden."; return; }

        var lagerorte = (await _lagerortService.SucheAsync(null)).Where(l => l.Aktiv).ToList();
        if (lagerorte.Count == 0) { Fehlermeldung = "Kein aktiver Lagerort angelegt."; return; }

        var offenePositionen = await _ueberleitungService.LadeOffenePositionenAsync(Id);
        if (offenePositionen.Count == 0) { Fehlermeldung = "Keine offenen Positionen für einen Wareneingang vorhanden."; return; }

        var dialog = new Milet.App.Views.Einkauf.WareneingangMengenDialog(offenePositionen, lagerorte) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await _ueberleitungService.UeberleitenMitAuswahlAsync(Id, BelegTyp.Wareneingang, dialog.GewaehlteMengen(), dialog.AusgewaehlterLagerortId);
            Navigation.Navigate<WareneingangListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<BestellungListViewModel>();
}
