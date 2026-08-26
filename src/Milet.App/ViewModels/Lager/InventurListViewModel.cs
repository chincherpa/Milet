using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class InventurListViewModel : ObservableObject
{
    private readonly IInventurService _inventurService;
    private readonly ILagerortService _lagerortService;
    private readonly INavigationService _navigation;

    public InventurListViewModel(IInventurService inventurService, ILagerortService lagerortService, INavigationService navigation)
    {
        _inventurService = inventurService;
        _lagerortService = lagerortService;
        _navigation = navigation;
        _ = LadenAsync();
        _ = LagerorteLadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<InventurDto> Inventuren { get; set; } = [];
    [ObservableProperty] public partial InventurDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LagerortDto> Lagerorte { get; set; } = [];
    [ObservableProperty] public partial int NeueInventurLagerortId { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Inventuren = await _inventurService.SucheAsync(); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private async Task LagerorteLadenAsync() => Lagerorte = await _lagerortService.SucheAsync(null);

    [RelayCommand]
    private async Task NeueInventurAsync()
    {
        Fehlermeldung = null;
        if (NeueInventurLagerortId == 0) { Fehlermeldung = "Lagerort wählen."; return; }
        try
        {
            var inventur = await _inventurService.NeueInventurAsync(NeueInventurLagerortId);
            _navigation.Navigate<InventurEditViewModel>(inventur.Id);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Bearbeiten() { if (Ausgewaehlt is { } inventur) _navigation.Navigate<InventurEditViewModel>(inventur.Id); }
}
