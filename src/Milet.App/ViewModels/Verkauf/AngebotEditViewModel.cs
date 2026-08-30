using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Gaertnerei;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class AngebotEditViewModel : BelegEditViewModelBase
{
    public AngebotEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService,
        IVerfuegbarkeitService verfuegbarkeitService)
        : base(BelegTyp.Angebot, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService, verfuegbarkeitService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AngebotListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<AuftragListViewModel>();
}
