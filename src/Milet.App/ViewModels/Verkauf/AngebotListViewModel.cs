using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AngebotListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public AngebotListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
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
        try { Belege = await _belegService.SucheAsync(BelegTyp.Angebot, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<AngebotEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } beleg) _navigation.Navigate<AngebotEditViewModel>(beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Angebot löschen", $"Angebot '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
