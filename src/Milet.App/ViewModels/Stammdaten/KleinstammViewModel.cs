using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Stammdaten;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class KleinstammViewModel : ObservableObject
{
    private readonly IEinheitenService _einheitenService;
    private readonly IMwStSaetzeService _mwStSaetzeService;
    private readonly IZahlungsbedingungenService _zahlungsbedingungenService;
    private readonly IVersandartenService _versandartenService;
    private readonly IPreislistenService _preislistenService;
    private readonly IDialogService _dialogService;

    public KleinstammViewModel(
        IEinheitenService einheitenService,
        IMwStSaetzeService mwStSaetzeService,
        IZahlungsbedingungenService zahlungsbedingungenService,
        IVersandartenService versandartenService,
        IPreislistenService preislistenService,
        IDialogService dialogService)
    {
        _einheitenService = einheitenService;
        _mwStSaetzeService = mwStSaetzeService;
        _zahlungsbedingungenService = zahlungsbedingungenService;
        _versandartenService = versandartenService;
        _preislistenService = preislistenService;
        _dialogService = dialogService;

        _ = EinheitenLadenAsync();
        _ = MwStSaetzeLadenAsync();
        _ = ZahlungsbedingungenLadenAsync();
        _ = VersandartenLadenAsync();
        _ = PreislistenLadenAsync();
    }

    // ---- Einheiten ----

    [ObservableProperty]
    public partial IReadOnlyList<EinheitDto> EinheitenListe { get; set; } = [];

    [ObservableProperty]
    public partial EinheitDto? EinheitAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string EinheitKuerzel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EinheitBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int EinheitNachkommaStellen { get; set; }

    [ObservableProperty]
    public partial string? EinheitFehler { get; set; }

    partial void OnEinheitAusgewaehltChanged(EinheitDto? value)
    {
        EinheitFehler = null;
        EinheitKuerzel = value?.Kuerzel ?? string.Empty;
        EinheitBezeichnung = value?.Bezeichnung ?? string.Empty;
        EinheitNachkommaStellen = value?.NachkommaStellen ?? 0;
    }

    [RelayCommand]
    private async Task EinheitenLadenAsync() => EinheitenListe = await _einheitenService.ListeAsync();

    [RelayCommand]
    private void EinheitNeu() => EinheitAusgewaehlt = null;

    [RelayCommand]
    private async Task EinheitSpeichernAsync()
    {
        EinheitFehler = null;
        var dto = new EinheitDto
        {
            Id = EinheitAusgewaehlt?.Id ?? 0,
            Kuerzel = EinheitKuerzel,
            Bezeichnung = EinheitBezeichnung,
            NachkommaStellen = EinheitNachkommaStellen,
        };

        try
        {
            await _einheitenService.SpeichereAsync(dto);
            await EinheitenLadenAsync();
            EinheitNeu();
        }
        catch (ValidationException ex)
        {
            EinheitFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            EinheitFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task EinheitLoeschenAsync()
    {
        if (EinheitAusgewaehlt is not { } einheit)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Einheit löschen", $"Einheit '{einheit.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _einheitenService.LoescheAsync(einheit.Id);
            await EinheitenLadenAsync();
            EinheitNeu();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- MwSt-Sätze ----

    [ObservableProperty]
    public partial IReadOnlyList<MwStSatzDto> MwStSaetzeListe { get; set; } = [];

    [ObservableProperty]
    public partial MwStSatzDto? MwStSatzAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string MwStBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal MwStSatzWert { get; set; }

    [ObservableProperty]
    public partial int? MwStSteuerSchluessel { get; set; }

    [ObservableProperty]
    public partial DateOnly MwStGueltigAb { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    public partial string? MwStFehler { get; set; }

    partial void OnMwStSatzAusgewaehltChanged(MwStSatzDto? value)
    {
        MwStFehler = null;
        MwStBezeichnung = value?.Bezeichnung ?? string.Empty;
        MwStSatzWert = value?.Satz ?? 0;
        MwStSteuerSchluessel = value?.SteuerSchluessel;
        MwStGueltigAb = value?.GueltigAb ?? DateOnly.FromDateTime(DateTime.Today);
    }

    [RelayCommand]
    private async Task MwStSaetzeLadenAsync() => MwStSaetzeListe = await _mwStSaetzeService.ListeAsync();

    [RelayCommand]
    private void MwStSatzNeu() => MwStSatzAusgewaehlt = null;

    [RelayCommand]
    private async Task MwStSatzSpeichernAsync()
    {
        MwStFehler = null;
        var dto = new MwStSatzDto
        {
            Id = MwStSatzAusgewaehlt?.Id ?? 0,
            Bezeichnung = MwStBezeichnung,
            Satz = MwStSatzWert,
            SteuerSchluessel = MwStSteuerSchluessel,
            GueltigAb = MwStGueltigAb,
        };

        try
        {
            await _mwStSaetzeService.SpeichereAsync(dto);
            await MwStSaetzeLadenAsync();
            MwStSatzNeu();
        }
        catch (ValidationException ex)
        {
            MwStFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            MwStFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MwStSatzLoeschenAsync()
    {
        if (MwStSatzAusgewaehlt is not { } satz)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("MwSt-Satz löschen", $"MwSt-Satz '{satz.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _mwStSaetzeService.LoescheAsync(satz.Id);
            await MwStSaetzeLadenAsync();
            MwStSatzNeu();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Zahlungsbedingungen ----

    [ObservableProperty]
    public partial IReadOnlyList<ZahlungsbedingungDto> ZahlungsbedingungenListe { get; set; } = [];

    [ObservableProperty]
    public partial ZahlungsbedingungDto? ZahlungsbedingungAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string ZahlungsbedingungBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ZahlungsbedingungZielTage { get; set; }

    [ObservableProperty]
    public partial int? ZahlungsbedingungSkontoTage { get; set; }

    [ObservableProperty]
    public partial decimal? ZahlungsbedingungSkontoProzent { get; set; }

    [ObservableProperty]
    public partial string? ZahlungsbedingungFehler { get; set; }

    partial void OnZahlungsbedingungAusgewaehltChanged(ZahlungsbedingungDto? value)
    {
        ZahlungsbedingungFehler = null;
        ZahlungsbedingungBezeichnung = value?.Bezeichnung ?? string.Empty;
        ZahlungsbedingungZielTage = value?.ZielTage ?? 0;
        ZahlungsbedingungSkontoTage = value?.SkontoTage;
        ZahlungsbedingungSkontoProzent = value?.SkontoProzent;
    }

    [RelayCommand]
    private async Task ZahlungsbedingungenLadenAsync() => ZahlungsbedingungenListe = await _zahlungsbedingungenService.ListeAsync();

    [RelayCommand]
    private void ZahlungsbedingungNeu() => ZahlungsbedingungAusgewaehlt = null;

    [RelayCommand]
    private async Task ZahlungsbedingungSpeichernAsync()
    {
        ZahlungsbedingungFehler = null;
        var dto = new ZahlungsbedingungDto
        {
            Id = ZahlungsbedingungAusgewaehlt?.Id ?? 0,
            Bezeichnung = ZahlungsbedingungBezeichnung,
            ZielTage = ZahlungsbedingungZielTage,
            SkontoTage = ZahlungsbedingungSkontoTage,
            SkontoProzent = ZahlungsbedingungSkontoProzent,
        };

        try
        {
            await _zahlungsbedingungenService.SpeichereAsync(dto);
            await ZahlungsbedingungenLadenAsync();
            ZahlungsbedingungNeu();
        }
        catch (ValidationException ex)
        {
            ZahlungsbedingungFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            ZahlungsbedingungFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ZahlungsbedingungLoeschenAsync()
    {
        if (ZahlungsbedingungAusgewaehlt is not { } zb)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Zahlungsbedingung löschen", $"Zahlungsbedingung '{zb.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _zahlungsbedingungenService.LoescheAsync(zb.Id);
            await ZahlungsbedingungenLadenAsync();
            ZahlungsbedingungNeu();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Versandarten ----

    [ObservableProperty]
    public partial IReadOnlyList<VersandartDto> VersandartenListe { get; set; } = [];

    [ObservableProperty]
    public partial VersandartDto? VersandartAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string VersandartBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal? VersandartKosten { get; set; }

    [ObservableProperty]
    public partial string? VersandartFehler { get; set; }

    partial void OnVersandartAusgewaehltChanged(VersandartDto? value)
    {
        VersandartFehler = null;
        VersandartBezeichnung = value?.Bezeichnung ?? string.Empty;
        VersandartKosten = value?.Kosten;
    }

    [RelayCommand]
    private async Task VersandartenLadenAsync() => VersandartenListe = await _versandartenService.ListeAsync();

    [RelayCommand]
    private void VersandartNeu() => VersandartAusgewaehlt = null;

    [RelayCommand]
    private async Task VersandartSpeichernAsync()
    {
        VersandartFehler = null;
        var dto = new VersandartDto
        {
            Id = VersandartAusgewaehlt?.Id ?? 0,
            Bezeichnung = VersandartBezeichnung,
            Kosten = VersandartKosten,
        };

        try
        {
            await _versandartenService.SpeichereAsync(dto);
            await VersandartenLadenAsync();
            VersandartNeu();
        }
        catch (ValidationException ex)
        {
            VersandartFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            VersandartFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task VersandartLoeschenAsync()
    {
        if (VersandartAusgewaehlt is not { } versandart)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Versandart löschen", $"Versandart '{versandart.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _versandartenService.LoescheAsync(versandart.Id);
            await VersandartenLadenAsync();
            VersandartNeu();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Preislisten ----

    [ObservableProperty]
    public partial IReadOnlyList<PreislisteDto> PreislistenListe { get; set; } = [];

    [ObservableProperty]
    public partial PreislisteDto? PreislisteAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string PreislisteName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateOnly? PreislisteGueltigVon { get; set; }

    [ObservableProperty]
    public partial DateOnly? PreislisteGueltigBis { get; set; }

    [ObservableProperty]
    public partial string? PreislisteFehler { get; set; }

    partial void OnPreislisteAusgewaehltChanged(PreislisteDto? value)
    {
        PreislisteFehler = null;
        PreislisteName = value?.Name ?? string.Empty;
        PreislisteGueltigVon = value?.GueltigVon;
        PreislisteGueltigBis = value?.GueltigBis;
    }

    [RelayCommand]
    private async Task PreislistenLadenAsync() => PreislistenListe = await _preislistenService.ListeAsync();

    [RelayCommand]
    private void PreislisteNeu() => PreislisteAusgewaehlt = null;

    [RelayCommand]
    private async Task PreislisteSpeichernAsync()
    {
        PreislisteFehler = null;
        var dto = new PreislisteDto
        {
            Id = PreislisteAusgewaehlt?.Id ?? 0,
            Name = PreislisteName,
            GueltigVon = PreislisteGueltigVon,
            GueltigBis = PreislisteGueltigBis,
        };

        try
        {
            await _preislistenService.SpeichereAsync(dto);
            await PreislistenLadenAsync();
            PreislisteNeu();
        }
        catch (ValidationException ex)
        {
            PreislisteFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            PreislisteFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PreislisteLoeschenAsync()
    {
        if (PreislisteAusgewaehlt is not { } preisliste)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Preisliste löschen", $"Preisliste '{preisliste.Name}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _preislistenService.LoescheAsync(preisliste.Id);
            await PreislistenLadenAsync();
            PreislisteNeu();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
