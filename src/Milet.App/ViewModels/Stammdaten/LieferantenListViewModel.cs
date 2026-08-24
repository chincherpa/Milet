using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.App.Services;
using Nexus.Application.Stammdaten;

namespace Nexus.App.ViewModels.Stammdaten;

public sealed partial class LieferantenListViewModel : ObservableObject
{
    private readonly ILieferantenService _lieferantenService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public LieferantenListViewModel(ILieferantenService lieferantenService, INavigationService navigation, IDialogService dialogService)
    {
        _lieferantenService = lieferantenService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty]
    public partial string? Suchtext { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LieferantDto> Lieferanten { get; set; } = [];

    [ObservableProperty]
    public partial LieferantDto? Ausgewaehlt { get; set; }

    [ObservableProperty]
    public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            Lieferanten = await _lieferantenService.SucheAsync(Suchtext);
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
    private void Neu() => _navigation.Navigate<LieferantEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } lieferant)
        {
            _navigation.Navigate<LieferantEditViewModel>(lieferant.Id);
        }
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } lieferant)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Lieferant löschen", $"Lieferant '{lieferant.Adresse.Name1}' ({lieferant.Lieferantennummer}) wirklich löschen?");

        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _lieferantenService.LoescheAsync(lieferant.Id);
            await LadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
