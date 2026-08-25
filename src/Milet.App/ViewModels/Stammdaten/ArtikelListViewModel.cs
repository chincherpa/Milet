using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Stammdaten;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class ArtikelListViewModel : ObservableObject
{
    private readonly IArtikelService _artikelService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public ArtikelListViewModel(IArtikelService artikelService, INavigationService navigation, IDialogService dialogService)
    {
        _artikelService = artikelService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty]
    public partial string? Suchtext { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ArtikelDto> Artikel { get; set; } = [];

    [ObservableProperty]
    public partial ArtikelDto? Ausgewaehlt { get; set; }

    [ObservableProperty]
    public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            Artikel = await _artikelService.SucheAsync(Suchtext);
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
    private void Neu() => _navigation.Navigate<ArtikelEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } artikel)
        {
            _navigation.Navigate<ArtikelEditViewModel>(artikel.Id);
        }
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } artikel)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Artikel löschen", $"Artikel '{artikel.Bezeichnung}' ({artikel.Artikelnummer}) wirklich löschen?");

        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _artikelService.LoescheAsync(artikel.Id);
            Ausgewaehlt = null;
            await LadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
