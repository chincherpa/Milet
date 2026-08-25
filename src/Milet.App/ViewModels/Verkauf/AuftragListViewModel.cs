using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AuftragListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public AuftragListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService; _navigation = navigation; _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Auftrag, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<AuftragEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } beleg) _navigation.Navigate<AuftragEditViewModel>(beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Auftrag löschen", $"Auftrag '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
