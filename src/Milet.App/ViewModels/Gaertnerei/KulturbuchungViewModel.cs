using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Gaertnerei;
using Milet.Domain.Services;

namespace Milet.App.ViewModels.Gaertnerei;

/// <summary>Vier Buchungsarten (Zugang/Stufenwechsel/Umsetzen/Ausfall) in einem Pivot. Für alle außer Zugang
/// wird die Quelle aus den tatsächlichen Fundstellen der gewählten Pflanze gewählt (nie frei eingetippt) —
/// das verhindert die meisten Negativsperren-Fehler, bevor sie entstehen.</summary>
public sealed partial class KulturbuchungViewModel : ObservableObject
{
    private readonly IKulturBestandService _kulturBestandService;
    private readonly IKulturBuchungService _kulturBuchungService;
    private readonly IGaertnereiplanService _planService;
    private readonly IKulturstufenService _kulturstufenService;
    private readonly IDialogService _dialogService;

    public KulturbuchungViewModel(
        IKulturBestandService kulturBestandService,
        IKulturBuchungService kulturBuchungService,
        IGaertnereiplanService planService,
        IKulturstufenService kulturstufenService,
        IDialogService dialogService)
    {
        _kulturBestandService = kulturBestandService;
        _kulturBuchungService = kulturBuchungService;
        _planService = planService;
        _kulturstufenService = kulturstufenService;
        _dialogService = dialogService;
        _ = InitAsync();
    }

    [ObservableProperty]
    public partial IReadOnlyList<PflanzeUebersichtDto> Pflanzen { get; set; } = [];

    [ObservableProperty]
    public partial PflanzeUebersichtDto? AusgewaehltePflanze { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FeldDto> Felder { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<KulturstufeDto> Kulturstufen { get; set; } = [];

    /// <summary>Bestehende Fundstellen der gewählten Pflanze — die einzige Quelle für Stufenwechsel/Umsetzen/Ausfall.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PflanzenVorkommenDto> Vorkommen { get; set; } = [];

    [ObservableProperty]
    public partial PflanzenVorkommenDto? AusgewaehlteQuelle { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<KulturHistorieZeileDto> Historie { get; set; } = [];

    [ObservableProperty]
    public partial string? Fehler { get; set; }

    [ObservableProperty]
    public partial string? Erfolg { get; set; }

    // ---- Zugang ----
    [ObservableProperty] public partial int? ZugangFeldId { get; set; }
    [ObservableProperty] public partial int? ZugangSektionId { get; set; }
    [ObservableProperty] public partial int? ZugangKulturstufeId { get; set; }
    [ObservableProperty] public partial decimal ZugangMenge { get; set; }
    [ObservableProperty] public partial DateOnly ZugangDatum { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] public partial string? ZugangBemerkung { get; set; }
    public IReadOnlyList<SektionDto> ZugangSektionen => SektionenFuerFeld(ZugangFeldId);

    // ---- Stufenwechsel ----
    [ObservableProperty] public partial int? NachFeldIdStufenwechsel { get; set; }
    [ObservableProperty] public partial int? NachSektionIdStufenwechsel { get; set; }
    [ObservableProperty] public partial int? NachKulturstufeId { get; set; }
    [ObservableProperty] public partial decimal MengeStufenwechsel { get; set; }
    [ObservableProperty] public partial DateOnly DatumStufenwechsel { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] public partial string? BemerkungStufenwechsel { get; set; }
    public IReadOnlyList<SektionDto> NachSektionenStufenwechsel => SektionenFuerFeld(NachFeldIdStufenwechsel);

    // ---- Umsetzen ----
    [ObservableProperty] public partial int? NachFeldIdUmsetzen { get; set; }
    [ObservableProperty] public partial int? NachSektionIdUmsetzen { get; set; }
    [ObservableProperty] public partial decimal MengeUmsetzen { get; set; }
    [ObservableProperty] public partial DateOnly DatumUmsetzen { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] public partial string? BemerkungUmsetzen { get; set; }
    public IReadOnlyList<SektionDto> NachSektionenUmsetzen => SektionenFuerFeld(NachFeldIdUmsetzen);

    // ---- Ausfall ----
    [ObservableProperty] public partial decimal MengeAusfall { get; set; }
    [ObservableProperty] public partial DateOnly DatumAusfall { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] public partial string? BemerkungAusfall { get; set; }

    private IReadOnlyList<SektionDto> SektionenFuerFeld(int? feldId)
        => feldId is { } id ? Felder.FirstOrDefault(f => f.Id == id)?.Sektionen ?? [] : [];

    partial void OnZugangFeldIdChanged(int? value)
    {
        ZugangSektionId = null;
        OnPropertyChanged(nameof(ZugangSektionen));
    }

    partial void OnNachFeldIdStufenwechselChanged(int? value)
    {
        NachSektionIdStufenwechsel = null;
        OnPropertyChanged(nameof(NachSektionenStufenwechsel));
    }

    partial void OnNachFeldIdUmsetzenChanged(int? value)
    {
        NachSektionIdUmsetzen = null;
        OnPropertyChanged(nameof(NachSektionenUmsetzen));
    }

    private async Task InitAsync()
    {
        Kulturstufen = await _kulturstufenService.ListeAsync();
        var plan = await _planService.LadePlanAsync();
        Felder = plan?.Felder ?? [];
        await PflanzenLadenAsync();
    }

    [RelayCommand]
    private async Task PflanzenLadenAsync() => Pflanzen = await _kulturBestandService.LadePflanzenAsync(null);

    partial void OnAusgewaehltePflanzeChanged(PflanzeUebersichtDto? value)
    {
        AusgewaehlteQuelle = null;
        _ = VorkommenUndHistorieLadenAsync(value);
    }

    private async Task VorkommenUndHistorieLadenAsync(PflanzeUebersichtDto? pflanze)
    {
        if (pflanze is null)
        {
            Vorkommen = [];
            Historie = [];
            return;
        }

        Vorkommen = await _kulturBestandService.LadeVorkommenAsync(pflanze.ArtikelId);
        Historie = await _kulturBestandService.LadeHistorieAsync(pflanze.ArtikelId, null, null, null);
    }

    /// <summary>Vorbelegung beim Wählen einer Quelle: Menge = voller Bestand, Ziel-Sektion = Quell-Sektion
    /// (Umtopfen bleibt oft am Ort), Ziel-Stufe = nächsthöhere aktive Stufe (Rückstufung bleibt über die
    /// ComboBox weiterhin möglich).</summary>
    partial void OnAusgewaehlteQuelleChanged(PflanzenVorkommenDto? value)
    {
        if (value is null) return;

        MengeStufenwechsel = value.Menge;
        NachFeldIdStufenwechsel = value.FeldId;
        NachSektionIdStufenwechsel = value.SektionId;
        var naechsteStufe = KulturRegeln.NaechsteStufe(
            Kulturstufen.Select(k => new Milet.Domain.Entities.Gaertnerei.Kulturstufe
            {
                Id = k.Id,
                Code = k.Code,
                Bezeichnung = k.Bezeichnung,
                Reihenfolge = k.Reihenfolge,
                IstVerkaufsfaehig = k.IstVerkaufsfaehig,
                FarbeHex = k.FarbeHex,
                Aktiv = k.Aktiv,
            }).ToList(),
            value.KulturstufeId);
        NachKulturstufeId = naechsteStufe?.Id ?? value.KulturstufeId;

        MengeUmsetzen = value.Menge;
        NachFeldIdUmsetzen = value.FeldId;
        NachSektionIdUmsetzen = value.SektionId;

        MengeAusfall = value.Menge;
    }

    [RelayCommand]
    private async Task ZugangBuchenAsync()
    {
        Fehler = null;
        Erfolg = null;
        if (AusgewaehltePflanze is not { } pflanze || ZugangFeldId is not { } feldId || ZugangKulturstufeId is not { } stufeId)
        {
            Fehler = "Pflanze, Feld und Kulturstufe sind erforderlich.";
            return;
        }

        try
        {
            await _kulturBuchungService.ZugangAsync(new KulturZugangDto
            {
                ArtikelId = pflanze.ArtikelId,
                FeldId = feldId,
                SektionId = ZugangSektionId,
                KulturstufeId = stufeId,
                Menge = ZugangMenge,
                Datum = ZugangDatum,
                Bemerkung = ZugangBemerkung,
            });
            Erfolg = $"Zugang gebucht: {ZugangMenge:0.###} Stück.";
            await VorkommenUndHistorieLadenAsync(AusgewaehltePflanze);
        }
        catch (ValidationException ex)
        {
            Fehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            Fehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StufenwechselBuchenAsync()
    {
        Fehler = null;
        Erfolg = null;
        if (AusgewaehltePflanze is not { } pflanze || AusgewaehlteQuelle is not { } quelle
            || NachFeldIdStufenwechsel is not { } nachFeldId || NachKulturstufeId is not { } nachStufeId)
        {
            Fehler = "Quelle, Ziel-Feld und Ziel-Stufe sind erforderlich.";
            return;
        }

        try
        {
            await _kulturBuchungService.StufenwechselAsync(new StufenwechselDto
            {
                ArtikelId = pflanze.ArtikelId,
                VonFeldId = quelle.FeldId,
                VonSektionId = quelle.SektionId,
                VonKulturstufeId = quelle.KulturstufeId,
                NachFeldId = nachFeldId,
                NachSektionId = NachSektionIdStufenwechsel,
                NachKulturstufeId = nachStufeId,
                Menge = MengeStufenwechsel,
                Datum = DatumStufenwechsel,
                Bemerkung = BemerkungStufenwechsel,
            });
            Erfolg = $"Stufenwechsel gebucht: {MengeStufenwechsel:0.###} Stück.";
            await VorkommenUndHistorieLadenAsync(AusgewaehltePflanze);
        }
        catch (ValidationException ex)
        {
            Fehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            Fehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UmsetzenBuchenAsync()
    {
        Fehler = null;
        Erfolg = null;
        if (AusgewaehltePflanze is not { } pflanze || AusgewaehlteQuelle is not { } quelle || NachFeldIdUmsetzen is not { } nachFeldId)
        {
            Fehler = "Quelle und Ziel-Feld sind erforderlich.";
            return;
        }

        try
        {
            await _kulturBuchungService.UmsetzenAsync(new UmsetzenDto
            {
                ArtikelId = pflanze.ArtikelId,
                VonFeldId = quelle.FeldId,
                VonSektionId = quelle.SektionId,
                NachFeldId = nachFeldId,
                NachSektionId = NachSektionIdUmsetzen,
                KulturstufeId = quelle.KulturstufeId,
                Menge = MengeUmsetzen,
                Datum = DatumUmsetzen,
                Bemerkung = BemerkungUmsetzen,
            });
            Erfolg = $"Umgesetzt: {MengeUmsetzen:0.###} Stück.";
            await VorkommenUndHistorieLadenAsync(AusgewaehltePflanze);
        }
        catch (ValidationException ex)
        {
            Fehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            Fehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AusfallBuchenAsync()
    {
        Fehler = null;
        Erfolg = null;
        if (AusgewaehltePflanze is not { } pflanze || AusgewaehlteQuelle is not { } quelle)
        {
            Fehler = "Bitte zuerst die betroffene Fundstelle auswählen.";
            return;
        }

        try
        {
            await _kulturBuchungService.AusfallAsync(new AusfallDto
            {
                ArtikelId = pflanze.ArtikelId,
                FeldId = quelle.FeldId,
                SektionId = quelle.SektionId,
                KulturstufeId = quelle.KulturstufeId,
                Menge = MengeAusfall,
                Datum = DatumAusfall,
                Bemerkung = BemerkungAusfall,
            });
            Erfolg = $"Ausfall gebucht: {MengeAusfall:0.###} Stück.";
            await VorkommenUndHistorieLadenAsync(AusgewaehltePflanze);
        }
        catch (ValidationException ex)
        {
            Fehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            Fehler = ex.Message;
        }
    }
}
