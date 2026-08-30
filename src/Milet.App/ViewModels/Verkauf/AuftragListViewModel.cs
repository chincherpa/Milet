using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Gaertnerei;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

/// <summary>Ein Auftrag mit seiner (asynchron nachgeladenen) Verfügbarkeitsampel — "welche Aufträge kann ich
/// heute ausliefern?" ohne jeden einzeln zu öffnen (Phase 8, E8).</summary>
public sealed partial class AuftragZeile(BelegDto beleg) : ObservableObject
{
    public BelegDto Beleg { get; } = beleg;

    [ObservableProperty]
    public partial VerfuegbarkeitAmpel? Ampel { get; set; }
}

public sealed partial class AuftragListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly IVerfuegbarkeitService _verfuegbarkeitService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public AuftragListViewModel(
        IBelegService belegService, IVerfuegbarkeitService verfuegbarkeitService,
        INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService;
        _verfuegbarkeitService = verfuegbarkeitService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<AuftragZeile> Zeilen { get; set; } = [];
    [ObservableProperty] public partial AuftragZeile? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            var belege = await _belegService.SucheAsync(BelegTyp.Auftrag, Suchtext);
            Zeilen = belege.Select(b => new AuftragZeile(b)).ToList();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message);
            return;
        }
        finally { LaedtGerade = false; }

        // Bewusst NICHT awaited im try-Block oben — die Liste soll sofort stehen, die Ampeln trudeln nach.
        _ = AmpelnLadenAsync(Zeilen);
    }

    private async Task AmpelnLadenAsync(IReadOnlyList<AuftragZeile> zeilen)
    {
        foreach (var zeile in zeilen)
        {
            try
            {
                var ergebnis = await _verfuegbarkeitService.LadeFuerBelegAsync(zeile.Beleg.Id);
                zeile.Ampel = ergebnis.GesamtAmpel;
            }
            catch
            {
                // Ampel bleibt null (keine Anzeige) — ein einzelner Fehler soll die restliche Liste nicht blockieren.
            }
        }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<AuftragEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } zeile) _navigation.Navigate<AuftragEditViewModel>(zeile.Beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } zeile) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Auftrag löschen", $"Auftrag '{zeile.Beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(zeile.Beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
