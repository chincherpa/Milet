using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class AuftragEditViewModel : BelegEditViewModelBase
{
    public AuftragEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
        : base(BelegTyp.Auftrag, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AuftragListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
