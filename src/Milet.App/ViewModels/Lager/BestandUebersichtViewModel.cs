using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Gaertnerei;
using Milet.Application.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class BestandUebersichtViewModel : ObservableObject
{
    private readonly IBestandService _bestandService;
    private readonly ISeriennummernService _seriennummernService;
    private readonly ILagerortService _lagerortService;
    private readonly IGaertnereiplanService _gaertnereiplanService;
    private readonly IKulturstufenService _kulturstufenService;
    private readonly IDialogService _dialogService;

    public BestandUebersichtViewModel(
        IBestandService bestandService, ISeriennummernService seriennummernService,
        ILagerortService lagerortService, IGaertnereiplanService gaertnereiplanService,
        IKulturstufenService kulturstufenService, IDialogService dialogService)
    {
        _bestandService = bestandService;
        _seriennummernService = seriennummernService;
        _lagerortService = lagerortService;
        _gaertnereiplanService = gaertnereiplanService;
        _kulturstufenService = kulturstufenService;
        _dialogService = dialogService;
        _ = LadenAsync();
        _ = LagerorteLadenAsync();
        _ = KulturstufenLadenAsync();
        _ = FelderLadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelBestandDto> Bestaende { get; set; } = [];
    [ObservableProperty] public partial ArtikelBestandDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LagerortDto> Lagerorte { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<KulturstufeDto> Kulturstufen { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<FeldDto> Felder { get; set; } = [];

    // ---- Filter (Feld/Lagerort, Kulturstufe) ----
    [ObservableProperty] public partial int? FilterLagerortId { get; set; }
    [ObservableProperty] public partial int? FilterKulturstufeId { get; set; }

    public IReadOnlyList<ArtikelBestandDto> GefilterteBestaende => Bestaende
        .Where(b => FilterLagerortId is null || b.LagerortId == FilterLagerortId)
        .Where(b => FilterKulturstufeId is null || b.KulturstufeId == FilterKulturstufeId)
        .ToList();

    partial void OnFilterLagerortIdChanged(int? value) => OnPropertyChanged(nameof(GefilterteBestaende));
    partial void OnFilterKulturstufeIdChanged(int? value) => OnPropertyChanged(nameof(GefilterteBestaende));

    [ObservableProperty] public partial decimal KorrekturMengeDelta { get; set; }
    [ObservableProperty] public partial string KorrekturGrund { get; set; } = string.Empty;
    [ObservableProperty] public partial string? KorrekturFehler { get; set; }
    [ObservableProperty] public partial int? KorrekturSektionId { get; set; }
    [ObservableProperty] public partial int? KorrekturKulturstufeId { get; set; }

    public IReadOnlyList<SektionDto> KorrekturSektionen =>
        Ausgewaehlt is { } b ? Felder.FirstOrDefault(f => f.Id == b.LagerortId)?.Sektionen ?? [] : [];

    /// <summary>Bei Kulturpflanzen sind Sektion/Kulturstufe Pflichtfelder (E1) — die Regel selbst prüft
    /// KulturRegeln.PruefeDimensionen in BestandService.BucheBewegungAsync, hier nur die Sichtbarkeit.</summary>
    public bool ZeigtKulturDimensionen => Ausgewaehlt is { IstKulturpflanze: true };

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
        KorrekturSektionId = value?.SektionId;
        KorrekturKulturstufeId = value?.KulturstufeId;
        SeriennummernAufLager = [];
        OnPropertyChanged(nameof(ZeigtSeriennummernPanel));
        OnPropertyChanged(nameof(ZeigtKorrekturPanel));
        OnPropertyChanged(nameof(ZeigtKulturDimensionen));
        OnPropertyChanged(nameof(KorrekturSektionen));
        if (value is { HatSeriennummern: true }) _ = SeriennummernLadenAsync(value.ArtikelId);
    }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Bestaende = await _bestandService.SucheAsync(Suchtext); OnPropertyChanged(nameof(GefilterteBestaende)); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private async Task LagerorteLadenAsync() => Lagerorte = await _lagerortService.SucheAsync(null);

    [RelayCommand]
    private async Task KulturstufenLadenAsync() => Kulturstufen = await _kulturstufenService.ListeAsync();

    [RelayCommand]
    private async Task FelderLadenAsync()
    {
        var plan = await _gaertnereiplanService.LadePlanAsync();
        Felder = plan?.Felder ?? [];
    }

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
                SektionId = KorrekturSektionId,
                KulturstufeId = KorrekturKulturstufeId,
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
