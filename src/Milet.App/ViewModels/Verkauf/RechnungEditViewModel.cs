using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Finanzen;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class RechnungEditViewModel : BelegEditViewModelBase
{
    private readonly IEmailVersandService _emailVersandService;
    private readonly IKundenService _kundenService;

    public RechnungEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IRechnungBuchenService buchenService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService,
        IEmailVersandService emailVersandService,
        IKundenService kundenService)
        : base(BelegTyp.Rechnung, belegService, lookupService, ueberleitungService, buchenService, pdfService, navigation, dialogService)
    {
        _emailVersandService = emailVersandService;
        _kundenService = kundenService;
    }

    [ObservableProperty] public partial string? EmailStatus { get; set; }

    /// <summary>Nur gebuchte Rechnungen können versendet werden (Status wird von der Basisklasse gepflegt,
    /// eine partial-void-Hook auf deren Status-Änderung ist von hier aus nicht erreichbar — daher Prüfung
    /// erst beim Klick statt per Button-Sichtbarkeit).</summary>
    [RelayCommand]
    private async Task EmailSendenAsync()
    {
        EmailStatus = null;
        if (Status != BelegStatus.Gebucht)
        {
            EmailStatus = "Nur gebuchte Rechnungen können per E-Mail versendet werden.";
            return;
        }

        try
        {
            var kunde = await _kundenService.LadeAsync(KundeId);
            var empfaenger = kunde.EmailRechnung ?? kunde.Email;
            if (string.IsNullOrWhiteSpace(empfaenger))
            {
                EmailStatus = "Kunde hat keine E-Mail-Adresse hinterlegt.";
                return;
            }

            var ergebnis = await _emailVersandService.SendeBelegPdfAsync(
                Id, empfaenger, $"Rechnung {BelegNummer}", "Anbei erhalten Sie Ihre Rechnung als PDF.");

            EmailStatus = ergebnis.Erfolgreich ? $"E-Mail an {empfaenger} versendet." : $"Versand fehlgeschlagen: {ergebnis.Fehlermeldung}";
        }
        catch (Exception ex)
        {
            EmailStatus = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<RechnungListViewModel>();

    // Wird nie aufgerufen — ZeigtUeberleitenButton ist bei Rechnung false (kein weiterer Belegtyp in Phase 2).
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
