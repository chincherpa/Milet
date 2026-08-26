using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Lager;

public sealed partial class LieferscheinListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public LieferscheinListViewModel(
        IBelegService belegService, IBelegUeberleitungService ueberleitungService,
        INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService;
        _ueberleitungService = ueberleitungService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    /// <summary>Wird per Code-Behind aus `ListView.SelectionChanged` befüllt (Mehrfachauswahl für Sammelrechnung) — siehe `LieferscheinListPage.xaml.cs`.</summary>
    public List<int> AusgewaehlteIds { get; set; } = [];

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Lieferschein, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private void Bearbeiten() { if (Ausgewaehlt is { } beleg) _navigation.Navigate<LieferscheinEditViewModel>(beleg.Id); }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Lieferschein löschen", $"Lieferschein '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }

    [RelayCommand]
    private async Task SammelrechnungAsync()
    {
        if (AusgewaehlteIds.Count == 0)
        {
            await _dialogService.ZeigeFehlerAsync("Sammelrechnung", "Mindestens einen Lieferschein auswählen.");
            return;
        }

        try
        {
            await _ueberleitungService.UeberleitenMehrereAsync(AusgewaehlteIds, BelegTyp.Rechnung);
            _navigation.Navigate<Verkauf.RechnungListViewModel>();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Sammelrechnung fehlgeschlagen", ex.Message);
        }
    }
}
