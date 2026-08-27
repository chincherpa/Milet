using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.Views.Finanzen;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;

namespace Milet.App.ViewModels.Finanzen;

public sealed partial class OffenePostenListViewModel : ObservableObject
{
    private readonly IOffenePostenService _offenePostenService;
    private readonly IZahlungService _zahlungService;
    private readonly IDialogService _dialogService;

    public OffenePostenListViewModel(
        IOffenePostenService offenePostenService, IZahlungService zahlungService, IDialogService dialogService)
    {
        _offenePostenService = offenePostenService;
        _zahlungService = zahlungService;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<OffenePostenDto> Posten { get; set; } = [];
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    /// <summary>0 = Alle, 1 = Debitor, 2 = Kreditor.</summary>
    [ObservableProperty] public partial int TypFilterIndex { get; set; }

    /// <summary>0 = Alle, 1 = Offen, 2 = TeilweiseBezahlt, 3 = Ausgeglichen.</summary>
    [ObservableProperty] public partial int StatusFilterIndex { get; set; }

    [ObservableProperty] public partial bool NurUeberfaellige { get; set; }

    /// <summary>Wird per Code-Behind aus `ListView.SelectionChanged` befüllt (Mehrfachauswahl für eine
    /// Sammelzahlung über mehrere offene Posten desselben Partners) — siehe OffenePostenListPage.xaml.cs.</summary>
    public List<OffenePostenDto> AusgewaehltePosten { get; set; } = [];

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            var filter = new OffenePostenFilterDto(
                Typ: TypFilterIndex switch { 1 => OffenerPostenTyp.Debitor, 2 => OffenerPostenTyp.Kreditor, _ => null },
                Status: StatusFilterIndex switch
                {
                    1 => OffenerPostenStatus.Offen,
                    2 => OffenerPostenStatus.TeilweiseBezahlt,
                    3 => OffenerPostenStatus.Ausgeglichen,
                    _ => null,
                },
                NurUeberfaellige: NurUeberfaellige);
            Posten = await _offenePostenService.ListeAsync(filter);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message);
        }
        finally
        {
            LaedtGerade = false;
        }
    }

    [RelayCommand]
    private async Task ZahlungErfassenAsync()
    {
        if (AusgewaehltePosten.Count == 0)
        {
            await _dialogService.ZeigeFehlerAsync("Zahlung erfassen", "Mindestens einen offenen Posten auswählen.");
            return;
        }

        var typ = AusgewaehltePosten[0].Typ;
        var kundeId = AusgewaehltePosten[0].KundeId;
        var lieferantId = AusgewaehltePosten[0].LieferantId;
        if (AusgewaehltePosten.Any(p => p.Typ != typ || p.KundeId != kundeId || p.LieferantId != lieferantId))
        {
            await _dialogService.ZeigeFehlerAsync("Zahlung erfassen", "Alle ausgewählten Posten müssen zum selben Kunden/Lieferanten gehören.");
            return;
        }

        var heute = DateOnly.FromDateTime(DateTime.Today);
        var skontoVorschlaege = new Dictionary<int, decimal>();
        foreach (var op in AusgewaehltePosten)
        {
            var vorschlag = await _zahlungService.SkontoVorschlagAsync(op.Id, heute);
            skontoVorschlaege[op.Id] = vorschlag.SkontoBetrag;
        }

        var dialog = new ZahlungDialog(AusgewaehltePosten, skontoVorschlaege) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await _zahlungService.ErfasseZahlungAsync(new ZahlungDto(
                Id: 0,
                KundeId: kundeId,
                LieferantId: lieferantId,
                Typ: typ,
                Zahlungsdatum: DateOnly.FromDateTime(dialog.Zahlungsdatum.Date),
                Zahlungsart: dialog.Zahlungsart,
                Referenz: dialog.Referenz,
                Zuordnungen: dialog.Zuordnungen()));

            AusgewaehltePosten = [];
            await LadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Zahlung fehlgeschlagen", ex.Message);
        }
    }
}
