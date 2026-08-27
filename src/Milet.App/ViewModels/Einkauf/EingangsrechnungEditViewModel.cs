using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class EingangsrechnungEditViewModel : EinkaufBelegEditViewModelBase
{
    private readonly IEingangsrechnungBuchenService _buchenService;

    public EingangsrechnungEditViewModel(
        IBelegService belegService, IEinkaufLookupService lookupService,
        IEingangsrechnungBuchenService buchenService, INavigationService navigation, IDialogService dialogService)
        : base(BelegTyp.Eingangsrechnung, belegService, lookupService, navigation, dialogService)
    {
        _buchenService = buchenService;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (Id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        try
        {
            var ergebnis = await _buchenService.BuchenAsync(Id);
            Status = ergebnis.Beleg.Status;
            IstBearbeitbar = false;

            if (ergebnis.BetragWeichtAb)
            {
                // Der Kreditor-OP ist zu diesem Zeitpunkt bereits angelegt (Soft-Warnung, siehe
                // Architektur-Entscheidung 7) — der Dialog informiert nur, blockiert nichts mehr.
                await DialogService.ZeigeFehlerAsync(
                    "Betragsabweichung zum Wareneingang",
                    $"Rechnungsbetrag ({ergebnis.Beleg.SummeBrutto:C}) weicht vom Wareneingang ({ergebnis.ErwarteterBetrag:C}) um {ergebnis.AbweichungBetrag:C} ab. Der Offene Posten wurde trotzdem angelegt.");
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<EingangsrechnungListViewModel>();
}
