using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class RechnungEditViewModel : BelegEditViewModelBase
{
    public RechnungEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IRechnungBuchenService buchenService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
        : base(BelegTyp.Rechnung, belegService, lookupService, ueberleitungService, buchenService, pdfService, navigation, dialogService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<RechnungListViewModel>();

    // Wird nie aufgerufen — ZeigtUeberleitenButton ist bei Rechnung false (kein weiterer Belegtyp in Phase 2).
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
