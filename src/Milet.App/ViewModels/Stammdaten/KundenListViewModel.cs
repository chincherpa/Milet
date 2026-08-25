using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Stammdaten;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class KundenListViewModel : ObservableObject
{
    private readonly IKundenService _kundenService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public KundenListViewModel(IKundenService kundenService, INavigationService navigation, IDialogService dialogService)
    {
        _kundenService = kundenService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty]
    public partial string? Suchtext { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<KundeDto> Kunden { get; set; } = [];

    [ObservableProperty]
    public partial KundeDto? Ausgewaehlt { get; set; }

    [ObservableProperty]
    public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            Kunden = await _kundenService.SucheAsync(Suchtext);
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
    private void Neu() => _navigation.Navigate<KundeEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } kunde)
        {
            _navigation.Navigate<KundeEditViewModel>(kunde.Id);
        }
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } kunde)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Kunde löschen", $"Kunde '{kunde.Adresse.Name1}' ({kunde.Kundennummer}) wirklich löschen?");

        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _kundenService.LoescheAsync(kunde.Id);
            Ausgewaehlt = null;
            await LadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
