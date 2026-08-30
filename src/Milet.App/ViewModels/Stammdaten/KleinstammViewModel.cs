using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Admin;
using Milet.Application.Finanzen;
using Milet.Application.Gaertnerei;
using Milet.Application.Stammdaten;
using Milet.Domain.Entities.Admin;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class KleinstammViewModel : ObservableObject
{
    private readonly IEinheitenService _einheitenService;
    private readonly IMwStSaetzeService _mwStSaetzeService;
    private readonly IZahlungsbedingungenService _zahlungsbedingungenService;
    private readonly IVersandartenService _versandartenService;
    private readonly IPreislistenService _preislistenService;
    private readonly IArtikelPreiseService _artikelPreiseService;
    private readonly IArtikelService _artikelService;
    private readonly IDialogService _dialogService;
    private readonly Milet.Application.Lager.ILagerortService _lagerortService;
    private readonly IMahnwesenService _mahnwesenService;
    private readonly IFibuKonfigurationService _fibuKonfigurationService;
    private readonly IFirmenstammService _firmenstammService;
    private readonly IKulturstufenService _kulturstufenService;

    public KleinstammViewModel(
        IEinheitenService einheitenService,
        IMwStSaetzeService mwStSaetzeService,
        IZahlungsbedingungenService zahlungsbedingungenService,
        IVersandartenService versandartenService,
        IPreislistenService preislistenService,
        IArtikelPreiseService artikelPreiseService,
        IArtikelService artikelService,
        IDialogService dialogService,
        Milet.Application.Lager.ILagerortService lagerortService,
        IMahnwesenService mahnwesenService,
        IFibuKonfigurationService fibuKonfigurationService,
        IFirmenstammService firmenstammService,
        IKulturstufenService kulturstufenService)
    {
        _einheitenService = einheitenService;
        _mwStSaetzeService = mwStSaetzeService;
        _zahlungsbedingungenService = zahlungsbedingungenService;
        _versandartenService = versandartenService;
        _preislistenService = preislistenService;
        _artikelPreiseService = artikelPreiseService;
        _artikelService = artikelService;
        _dialogService = dialogService;
        _lagerortService = lagerortService;
        _mahnwesenService = mahnwesenService;
        _fibuKonfigurationService = fibuKonfigurationService;
        _firmenstammService = firmenstammService;
        _kulturstufenService = kulturstufenService;

        _ = EinheitenLadenAsync();
        _ = MwStSaetzeLadenAsync();
        _ = ZahlungsbedingungenLadenAsync();
        _ = VersandartenLadenAsync();
        _ = PreislistenLadenAsync();
        _ = ArtikelLookupsLadenAsync();
        _ = LagerortenLadenAsync();
        _ = MahnstufenLadenAsync();
        _ = FibuKonfigurationLadenAsync();
        _ = FirmenstammLadenAsync();
        _ = KulturstufenLadenAsync();
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
            EinheitNeu();
            await EinheitenLadenAsync();
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
    public partial int? MwStErloeskontoNr { get; set; }

    [ObservableProperty]
    public partial int? MwStAufwandskontoNr { get; set; }

    [ObservableProperty]
    public partial string? MwStFehler { get; set; }

    partial void OnMwStSatzAusgewaehltChanged(MwStSatzDto? value)
    {
        MwStFehler = null;
        MwStBezeichnung = value?.Bezeichnung ?? string.Empty;
        MwStSatzWert = value?.Satz ?? 0;
        MwStSteuerSchluessel = value?.SteuerSchluessel;
        MwStGueltigAb = value?.GueltigAb ?? DateOnly.FromDateTime(DateTime.Today);
        MwStErloeskontoNr = value?.ErloeskontoNr;
        MwStAufwandskontoNr = value?.AufwandskontoNr;
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
            ErloeskontoNr = MwStErloeskontoNr,
            AufwandskontoNr = MwStAufwandskontoNr,
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
            MwStSatzNeu();
            await MwStSaetzeLadenAsync();
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
            ZahlungsbedingungNeu();
            await ZahlungsbedingungenLadenAsync();
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
            VersandartNeu();
            await VersandartenLadenAsync();
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

        StaffelpreisNeu();
        StaffelpreiseListe = [];
        if (value is { Id: > 0 })
        {
            _ = StaffelpreiseLadenAsync(value.Id);
        }
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
            PreislisteNeu();
            await PreislistenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Staffelpreise (ArtikelPreis je Preisliste) ----

    [ObservableProperty]
    public partial IReadOnlyList<LookupDto> ArtikelLookups { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ArtikelPreisDto> StaffelpreiseListe { get; set; } = [];

    [ObservableProperty]
    public partial ArtikelPreisDto? StaffelpreisAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial int? StaffelpreisArtikelId { get; set; }

    [ObservableProperty]
    public partial decimal StaffelpreisAbMenge { get; set; } = 1;

    [ObservableProperty]
    public partial decimal StaffelpreisPreis { get; set; }

    [ObservableProperty]
    public partial string? StaffelpreisFehler { get; set; }

    partial void OnStaffelpreisAusgewaehltChanged(ArtikelPreisDto? value)
    {
        StaffelpreisFehler = null;
        StaffelpreisArtikelId = value?.ArtikelId;
        StaffelpreisAbMenge = value?.AbMenge ?? 1;
        StaffelpreisPreis = value?.Preis ?? 0;
    }

    [RelayCommand]
    private async Task ArtikelLookupsLadenAsync()
    {
        var artikel = await _artikelService.SucheAsync(null);
        ArtikelLookups = artikel.Select(a => new LookupDto(a.Id, $"{a.Artikelnummer} — {a.Bezeichnung}")).ToList();
    }

    [RelayCommand]
    private async Task StaffelpreiseLadenAsync(int preislisteId) => StaffelpreiseListe = await _artikelPreiseService.ListeAsync(preislisteId);

    [RelayCommand]
    private void StaffelpreisNeu() => StaffelpreisAusgewaehlt = null;

    [RelayCommand]
    private async Task StaffelpreisSpeichernAsync()
    {
        StaffelpreisFehler = null;
        if (PreislisteAusgewaehlt is not { Id: > 0 } preisliste)
        {
            StaffelpreisFehler = "Preisliste zuerst speichern.";
            return;
        }

        var dto = new ArtikelPreisDto
        {
            Id = StaffelpreisAusgewaehlt?.Id ?? 0,
            PreislisteId = preisliste.Id,
            ArtikelId = StaffelpreisArtikelId ?? 0,
            AbMenge = StaffelpreisAbMenge,
            Preis = StaffelpreisPreis,
        };

        try
        {
            await _artikelPreiseService.SpeichereAsync(dto);
            await StaffelpreiseLadenAsync(preisliste.Id);
            StaffelpreisNeu();
        }
        catch (ValidationException ex)
        {
            StaffelpreisFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            StaffelpreisFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StaffelpreisLoeschenAsync()
    {
        if (StaffelpreisAusgewaehlt is not { } staffelpreis || PreislisteAusgewaehlt is not { Id: > 0 } preisliste)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Staffelpreis löschen", $"Staffelpreis ab Menge {staffelpreis.AbMenge} wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _artikelPreiseService.LoescheAsync(staffelpreis.Id);
            StaffelpreisNeu();
            await StaffelpreiseLadenAsync(preisliste.Id);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Lagerorte ----

    [ObservableProperty]
    public partial IReadOnlyList<Milet.Application.Lager.LagerortDto> LagerorteListe { get; set; } = [];

    [ObservableProperty]
    public partial Milet.Application.Lager.LagerortDto? LagerortAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string LagerortCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LagerortBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LagerortAktiv { get; set; } = true;

    [ObservableProperty]
    public partial string? LagerortFehler { get; set; }

    partial void OnLagerortAusgewaehltChanged(Milet.Application.Lager.LagerortDto? value)
    {
        LagerortFehler = null;
        LagerortCode = value?.Code ?? string.Empty;
        LagerortBezeichnung = value?.Bezeichnung ?? string.Empty;
        LagerortAktiv = value?.Aktiv ?? true;
    }

    [RelayCommand]
    private async Task LagerortenLadenAsync() => LagerorteListe = await _lagerortService.SucheAsync(null);

    [RelayCommand]
    private void LagerortNeu() => LagerortAusgewaehlt = null;

    [RelayCommand]
    private async Task LagerortSpeichernAsync()
    {
        LagerortFehler = null;
        var dto = new Milet.Application.Lager.LagerortDto
        {
            Id = LagerortAusgewaehlt?.Id ?? 0,
            Code = LagerortCode,
            Bezeichnung = LagerortBezeichnung,
            Aktiv = LagerortAktiv,
            RowVersion = LagerortAusgewaehlt?.RowVersion ?? [],
        };

        try
        {
            await _lagerortService.SpeichereAsync(dto);
            await LagerortenLadenAsync();
            LagerortNeu();
        }
        catch (ValidationException ex)
        {
            LagerortFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            LagerortFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LagerortLoeschenAsync()
    {
        if (LagerortAusgewaehlt is not { } lagerort)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Lagerort löschen", $"Lagerort '{lagerort.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _lagerortService.LoescheAsync(lagerort.Id);
            LagerortNeu();
            await LagerortenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Mahnstufen ----

    [ObservableProperty]
    public partial IReadOnlyList<MahnstufeDto> MahnstufenListe { get; set; } = [];

    [ObservableProperty]
    public partial MahnstufeDto? MahnstufeAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial int MahnstufeStufe { get; set; }

    [ObservableProperty]
    public partial int MahnstufeKarenztage { get; set; }

    [ObservableProperty]
    public partial decimal MahnstufeGebuehr { get; set; }

    [ObservableProperty]
    public partial string? MahnstufeMahntext { get; set; }

    [ObservableProperty]
    public partial string? MahnstufeFehler { get; set; }

    partial void OnMahnstufeAusgewaehltChanged(MahnstufeDto? value)
    {
        MahnstufeFehler = null;
        MahnstufeStufe = value?.Stufe ?? 0;
        MahnstufeKarenztage = value?.Karenztage ?? 0;
        MahnstufeGebuehr = value?.Gebuehr ?? 0;
        MahnstufeMahntext = value?.Mahntext;
    }

    [RelayCommand]
    private async Task MahnstufenLadenAsync() => MahnstufenListe = await _mahnwesenService.ListeStufenAsync();

    [RelayCommand]
    private void MahnstufeNeu() => MahnstufeAusgewaehlt = null;

    [RelayCommand]
    private async Task MahnstufeSpeichernAsync()
    {
        MahnstufeFehler = null;
        var dto = new MahnstufeDto(
            MahnstufeAusgewaehlt?.Id ?? 0, MahnstufeStufe, MahnstufeKarenztage, MahnstufeGebuehr, MahnstufeMahntext);

        try
        {
            await _mahnwesenService.SpeichereStufeAsync(dto);
            await MahnstufenLadenAsync();
            MahnstufeNeu();
        }
        catch (ValidationException ex)
        {
            MahnstufeFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            MahnstufeFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MahnstufeLoeschenAsync()
    {
        if (MahnstufeAusgewaehlt is not { } stufe)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Mahnstufe löschen", $"Mahnstufe {stufe.Stufe} wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _mahnwesenService.LoescheStufeAsync(stufe.Id);
            MahnstufeNeu();
            await MahnstufenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- FibuKonten (DATEV-Export-Konfiguration, Singleton) ----

    [ObservableProperty]
    public partial int FibuKontenrahmenIndex { get; set; }

    [ObservableProperty]
    public partial int FibuBeraterNr { get; set; }

    [ObservableProperty]
    public partial int FibuMandantNr { get; set; }

    [ObservableProperty]
    public partial int FibuWirtschaftsjahrBeginnMonat { get; set; } = 1;

    [ObservableProperty]
    public partial int FibuSachkontenLaenge { get; set; } = 4;

    [ObservableProperty]
    public partial int FibuBankkontoNr { get; set; }

    [ObservableProperty]
    public partial string? FibuKonfigurationFehler { get; set; }

    [RelayCommand]
    private async Task FibuKonfigurationLadenAsync()
    {
        var dto = await _fibuKonfigurationService.LadeAsync();
        FibuKontenrahmenIndex = dto.Kontenrahmen == Kontenrahmen.Skr04 ? 1 : 0;
        FibuBeraterNr = dto.BeraterNr;
        FibuMandantNr = dto.MandantNr;
        FibuWirtschaftsjahrBeginnMonat = dto.WirtschaftsjahrBeginnMonat;
        FibuSachkontenLaenge = dto.SachkontenLaenge;
        FibuBankkontoNr = dto.BankkontoNr;
    }

    [RelayCommand]
    private async Task FibuKonfigurationSpeichernAsync()
    {
        FibuKonfigurationFehler = null;
        var dto = new FibuKonfigurationDto
        {
            Kontenrahmen = FibuKontenrahmenIndex == 1 ? Kontenrahmen.Skr04 : Kontenrahmen.Skr03,
            BeraterNr = FibuBeraterNr,
            MandantNr = FibuMandantNr,
            WirtschaftsjahrBeginnMonat = FibuWirtschaftsjahrBeginnMonat,
            SachkontenLaenge = FibuSachkontenLaenge,
            BankkontoNr = FibuBankkontoNr,
        };

        try
        {
            await _fibuKonfigurationService.SpeichereAsync(dto);
            await FibuKonfigurationLadenAsync();
        }
        catch (ValidationException ex)
        {
            FibuKonfigurationFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            FibuKonfigurationFehler = ex.Message;
        }
    }

    // ---- Firmenstamm (Briefkopf, Singleton) ----

    [ObservableProperty]
    public partial string FirmaFirmenname { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirmaStrasse { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirmaPlz { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirmaOrt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirmaLand { get; set; } = "DE";

    [ObservableProperty]
    public partial string? FirmaUStIdNr { get; set; }

    [ObservableProperty]
    public partial string? FirmaTelefon { get; set; }

    [ObservableProperty]
    public partial string? FirmaEmail { get; set; }

    [ObservableProperty]
    public partial string? FirmaIban { get; set; }

    [ObservableProperty]
    public partial string? FirmaBic { get; set; }

    [ObservableProperty]
    public partial string? FirmenstammFehler { get; set; }

    [RelayCommand]
    private async Task FirmenstammLadenAsync()
    {
        var dto = await _firmenstammService.LadeAsync();
        FirmaFirmenname = dto.Firmenname;
        FirmaStrasse = dto.Adresse.Strasse;
        FirmaPlz = dto.Adresse.Plz;
        FirmaOrt = dto.Adresse.Ort;
        FirmaLand = dto.Adresse.Land;
        FirmaUStIdNr = dto.UStIdNr;
        FirmaTelefon = dto.Telefon;
        FirmaEmail = dto.Email;
        FirmaIban = dto.Iban;
        FirmaBic = dto.Bic;
    }

    [RelayCommand]
    private async Task FirmenstammSpeichernAsync()
    {
        FirmenstammFehler = null;
        var dto = new FirmenstammDto
        {
            Firmenname = FirmaFirmenname,
            Adresse = new AdresseDto
            {
                Name1 = FirmaFirmenname,
                Strasse = FirmaStrasse,
                Plz = FirmaPlz,
                Ort = FirmaOrt,
                Land = FirmaLand,
            },
            UStIdNr = FirmaUStIdNr,
            Telefon = FirmaTelefon,
            Email = FirmaEmail,
            Iban = FirmaIban,
            Bic = FirmaBic,
        };

        try
        {
            await _firmenstammService.SpeichereAsync(dto);
            await FirmenstammLadenAsync();
        }
        catch (ValidationException ex)
        {
            FirmenstammFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            FirmenstammFehler = ex.Message;
        }
    }

    // ---- Kulturstufen (Phase 8) ----

    [ObservableProperty]
    public partial IReadOnlyList<KulturstufeDto> KulturstufenListe { get; set; } = [];

    [ObservableProperty]
    public partial KulturstufeDto? KulturstufeAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string KulturstufeCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KulturstufeBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int KulturstufeReihenfolge { get; set; }

    [ObservableProperty]
    public partial bool KulturstufeIstVerkaufsfaehig { get; set; }

    [ObservableProperty]
    public partial string KulturstufeFarbeHex { get; set; } = "#4CAF50";

    [ObservableProperty]
    public partial bool KulturstufeAktiv { get; set; } = true;

    [ObservableProperty]
    public partial string? KulturstufeFehler { get; set; }

    /// <summary>Ohne verkaufsfähige Stufe kann kein Lieferschein gebucht werden — Warnhinweis, kein Blocker (E5).</summary>
    public bool KeineAktiveVerkaufsfaehigeStufe =>
        KulturstufenListe.Count > 0 && !KulturstufenListe.Any(k => k.Aktiv && k.IstVerkaufsfaehig);

    partial void OnKulturstufeAusgewaehltChanged(KulturstufeDto? value)
    {
        KulturstufeFehler = null;
        KulturstufeCode = value?.Code ?? string.Empty;
        KulturstufeBezeichnung = value?.Bezeichnung ?? string.Empty;
        KulturstufeReihenfolge = value?.Reihenfolge ?? 0;
        KulturstufeIstVerkaufsfaehig = value?.IstVerkaufsfaehig ?? false;
        KulturstufeFarbeHex = value?.FarbeHex ?? "#4CAF50";
        KulturstufeAktiv = value?.Aktiv ?? true;
    }

    [RelayCommand]
    private async Task KulturstufenLadenAsync()
    {
        KulturstufenListe = await _kulturstufenService.ListeAsync();
        OnPropertyChanged(nameof(KeineAktiveVerkaufsfaehigeStufe));
    }

    [RelayCommand]
    private void KulturstufeNeu() => KulturstufeAusgewaehlt = null;

    [RelayCommand]
    private async Task KulturstufeSpeichernAsync()
    {
        KulturstufeFehler = null;
        var dto = new KulturstufeDto
        {
            Id = KulturstufeAusgewaehlt?.Id ?? 0,
            Code = KulturstufeCode,
            Bezeichnung = KulturstufeBezeichnung,
            Reihenfolge = KulturstufeReihenfolge,
            IstVerkaufsfaehig = KulturstufeIstVerkaufsfaehig,
            FarbeHex = KulturstufeFarbeHex,
            Aktiv = KulturstufeAktiv,
            RowVersion = KulturstufeAusgewaehlt?.RowVersion ?? [],
        };

        try
        {
            await _kulturstufenService.SpeichereAsync(dto);
            await KulturstufenLadenAsync();
            KulturstufeNeu();
        }
        catch (ValidationException ex)
        {
            KulturstufeFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            KulturstufeFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task KulturstufeLoeschenAsync()
    {
        if (KulturstufeAusgewaehlt is not { } stufe)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Kulturstufe löschen", $"Kulturstufe '{stufe.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _kulturstufenService.LoescheAsync(stufe.Id);
            KulturstufeNeu();
            await KulturstufenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
