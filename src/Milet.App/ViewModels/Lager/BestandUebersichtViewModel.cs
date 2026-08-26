using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class BestandUebersichtViewModel : ObservableObject
{
    private readonly IBestandService _bestandService;
    private readonly ISeriennummernService _seriennummernService;
    private readonly ILagerortService _lagerortService;
    private readonly IDialogService _dialogService;

    public BestandUebersichtViewModel(
        IBestandService bestandService, ISeriennummernService seriennummernService,
        ILagerortService lagerortService, IDialogService dialogService)
    {
        _bestandService = bestandService;
        _seriennummernService = seriennummernService;
        _lagerortService = lagerortService;
        _dialogService = dialogService;
        _ = LadenAsync();
        _ = LagerorteLadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelBestandDto> Bestaende { get; set; } = [];
    [ObservableProperty] public partial ArtikelBestandDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LagerortDto> Lagerorte { get; set; } = [];

    [ObservableProperty] public partial decimal KorrekturMengeDelta { get; set; }
    [ObservableProperty] public partial string KorrekturGrund { get; set; } = string.Empty;
    [ObservableProperty] public partial string? KorrekturFehler { get; set; }

    [ObservableProperty] public partial IReadOnlyList<SeriennummerDto> SeriennummernAufLager { get; set; } = [];
    [ObservableProperty] public partial string NeueSeriennummer { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SeriennummerFehler { get; set; }

    public Microsoft.UI.Xaml.Visibility ZeigtSeriennummernPanel =>
        Ausgewaehlt is { HatSeriennummern: true } ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility ZeigtKorrekturPanel =>
        Ausgewaehlt is { HatSeriennummern: false } ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    partial void OnAusgewaehltChanged(ArtikelBestandDto? value)
    {
        KorrekturFehler = null;
        SeriennummerFehler = null;
        NeueSeriennummer = string.Empty;
        KorrekturMengeDelta = 0;
        KorrekturGrund = string.Empty;
        SeriennummernAufLager = [];
        OnPropertyChanged(nameof(ZeigtSeriennummernPanel));
        OnPropertyChanged(nameof(ZeigtKorrekturPanel));
        if (value is { HatSeriennummern: true }) _ = SeriennummernLadenAsync(value.ArtikelId);
    }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Bestaende = await _bestandService.SucheAsync(Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private async Task LagerorteLadenAsync() => Lagerorte = await _lagerortService.SucheAsync(null);

    private async Task SeriennummernLadenAsync(int artikelId) => SeriennummernAufLager = await _seriennummernService.AufLagerAsync(artikelId);

    [RelayCommand]
    private async Task KorrekturBuchenAsync()
    {
        if (Ausgewaehlt is not { } bestand) return;
        KorrekturFehler = null;
        try
        {
            await _bestandService.KorrigiereAsync(new BestandskorrekturDto
            {
                ArtikelId = bestand.ArtikelId,
                LagerortId = bestand.LagerortId,
                MengeDelta = KorrekturMengeDelta,
                Grund = KorrekturGrund,
            });
            KorrekturMengeDelta = 0;
            KorrekturGrund = string.Empty;
            await LadenAsync();
        }
        catch (ValidationException ex)
        {
            KorrekturFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            KorrekturFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SeriennummerErfassenAsync()
    {
        if (Ausgewaehlt is not { } bestand) return;
        SeriennummerFehler = null;
        try
        {
            await _seriennummernService.ErfasseAsync(bestand.ArtikelId, bestand.LagerortId, NeueSeriennummer);
            NeueSeriennummer = string.Empty;
            await SeriennummernLadenAsync(bestand.ArtikelId);
            await LadenAsync();
        }
        catch (Exception ex)
        {
            SeriennummerFehler = ex.Message;
        }
    }
}
