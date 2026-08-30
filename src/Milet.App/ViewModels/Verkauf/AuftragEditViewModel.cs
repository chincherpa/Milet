using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.Views.Lager;
using Milet.Application.Abstractions;
using Milet.Application.Gaertnerei;
using Milet.Application.Lager;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AuftragEditViewModel : BelegEditViewModelBase
{
    private readonly ILagerortService _lagerortService;
    private readonly IKulturBestandService _kulturBestandService;
    private readonly IKulturstufenService _kulturstufenService;

    public AuftragEditViewModel(
        IBelegService belegService, IVerkaufLookupService lookupService, IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService, INavigationService navigation, IDialogService dialogService, ILagerortService lagerortService,
        IKulturBestandService kulturBestandService, IKulturstufenService kulturstufenService, IVerfuegbarkeitService verfuegbarkeitService)
        : base(BelegTyp.Auftrag, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService, verfuegbarkeitService)
    {
        _lagerortService = lagerortService;
        _kulturBestandService = kulturBestandService;
        _kulturstufenService = kulturstufenService;
    }

    [RelayCommand]
    private async Task UeberleitenZuLieferscheinAsync()
    {
        if (Id == 0) { Fehlermeldung = "Auftrag muss erst gespeichert werden."; return; }

        var lagerorte = (await _lagerortService.SucheAsync(null)).Where(l => l.Aktiv).ToList();
        if (lagerorte.Count == 0) { Fehlermeldung = "Kein aktiver Lagerort angelegt."; return; }

        var offenePositionen = await UeberleitungService.LadeOffenePositionenAsync(Id);
        if (offenePositionen.Count == 0) { Fehlermeldung = "Keine offenen Positionen für eine Lieferung vorhanden."; return; }

        // E9: Vorbelegung im Dialog nur mit verkaufsfähigen Fundstellen — aus einer nicht-verkaufsfähigen
        // Stufe würde LieferscheinBuchenService die Buchung ohnehin ablehnen (E8).
        var kulturstufen = await _kulturstufenService.ListeAsync();
        var verkaufsfaehigeStufenIds = kulturstufen.Where(k => k.IstVerkaufsfaehig).Select(k => k.Id).ToHashSet();
        var fundstellenJeArtikel = new Dictionary<int, IReadOnlyList<PflanzenVorkommenDto>>();
        foreach (var artikelId in offenePositionen.Where(p => p.ArtikelId is not null).Select(p => p.ArtikelId!.Value).Distinct())
        {
            var vorkommen = await _kulturBestandService.LadeVorkommenAsync(artikelId);
            fundstellenJeArtikel[artikelId] = vorkommen.Where(v => verkaufsfaehigeStufenIds.Contains(v.KulturstufeId)).ToList();
        }

        var dialog = new TeillieferungDialog(offenePositionen, lagerorte, fundstellenJeArtikel) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await UeberleitungService.UeberleitenMitAuswahlAsync(Id, BelegTyp.Lieferschein, dialog.GewaehlteMengen(), dialog.AusgewaehlterLagerortId, dialog.DimensionenJePosition());
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
