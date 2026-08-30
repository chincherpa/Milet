using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.Application.Gaertnerei;

namespace Milet.App.ViewModels.Gaertnerei;

/// <summary>Links alle Kulturpflanzen, rechts derselbe Grundriss wie GrundrissViewModel (schreibgeschützt) —
/// die Auswahl einer Pflanze färbt ihre Sektionen nach Kulturstufe ein, alle übrigen werden ausgegraut.
/// Das ist die Kernanforderung aus dem Plan: "dieselbe Pflanze liegt in mehreren Sektionen, weil sie in
/// mehreren Stufen steht" auf einen Blick.</summary>
public sealed partial class PflanzenUebersichtViewModel : ObservableObject
{
    private readonly IGaertnereiplanService _planService;
    private readonly IKulturBestandService _kulturBestandService;
    private readonly IKulturstufenService _kulturstufenService;
    private decimal _planBreiteMeter;
    private decimal _planHoeheMeter;

    public PflanzenUebersichtViewModel(
        IGaertnereiplanService planService,
        IKulturBestandService kulturBestandService,
        IKulturstufenService kulturstufenService)
    {
        _planService = planService;
        _kulturBestandService = kulturBestandService;
        _kulturstufenService = kulturstufenService;
        _ = InitAsync();
    }

    [ObservableProperty]
    public partial ObservableCollection<PlanElementViewModel> Elemente { get; set; } = [];

    [ObservableProperty]
    public partial double Zoom { get; set; } = 20;

    [ObservableProperty]
    public partial double PlanPixelBreite { get; set; }

    [ObservableProperty]
    public partial double PlanPixelHoehe { get; set; }

    [ObservableProperty]
    public partial string? Suchtext { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PflanzeUebersichtDto> Pflanzen { get; set; } = [];

    [ObservableProperty]
    public partial PflanzeUebersichtDto? AusgewaehltePflanze { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PflanzenVorkommenDto> Fundstellen { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<KulturstufeDto> Kulturstufen { get; set; } = [];

    [ObservableProperty]
    public partial bool NurVerkaufsfaehigeAnzeigen { get; set; }

    /// <summary>null = alle Stufen; sonst nur Fundstellen dieser Kulturstufe hervorheben.</summary>
    [ObservableProperty]
    public partial int? StufenFilterId { get; set; }

    private async Task InitAsync()
    {
        Kulturstufen = await _kulturstufenService.ListeAsync();

        var plan = await _planService.LadePlanAsync();
        if (plan is not null)
        {
            _planBreiteMeter = plan.BreiteMeter;
            _planHoeheMeter = plan.HoeheMeter;
            AktualisierePlanPixelGroesse();

            var neu = new ObservableCollection<PlanElementViewModel>();
            foreach (var feld in plan.Felder)
            {
                neu.Add(new PlanElementViewModel
                {
                    Id = feld.Id,
                    IstFeld = true,
                    Code = feld.Code,
                    Bezeichnung = feld.Bezeichnung,
                    PosXMeter = feld.PosXMeter,
                    PosYMeter = feld.PosYMeter,
                    BreiteMeter = feld.BreiteMeter,
                    HoeheMeter = feld.HoeheMeter,
                    Zoom = Zoom,
                });

                foreach (var sektion in feld.Sektionen)
                {
                    neu.Add(new PlanElementViewModel
                    {
                        Id = sektion.Id,
                        IstFeld = false,
                        LagerortId = feld.Id,
                        Code = sektion.Code,
                        Bezeichnung = sektion.Bezeichnung,
                        PosXMeter = sektion.PosXMeter,
                        PosYMeter = sektion.PosYMeter,
                        BreiteMeter = sektion.BreiteMeter,
                        HoeheMeter = sektion.HoeheMeter,
                        FeldOffsetXMeter = feld.PosXMeter,
                        FeldOffsetYMeter = feld.PosYMeter,
                        Zoom = Zoom,
                    });
                }
            }

            Elemente = neu;
        }

        await PflanzenLadenAsync();
    }

    partial void OnZoomChanged(double value)
    {
        foreach (var element in Elemente)
        {
            element.Zoom = value;
        }

        AktualisierePlanPixelGroesse();
    }

    private void AktualisierePlanPixelGroesse()
    {
        PlanPixelBreite = (double)_planBreiteMeter * Zoom;
        PlanPixelHoehe = (double)_planHoeheMeter * Zoom;
    }

    partial void OnSuchtextChanged(string? value) => _ = PflanzenLadenAsync();

    [RelayCommand]
    private async Task PflanzenLadenAsync()
    {
        Pflanzen = await _kulturBestandService.LadePflanzenAsync(Suchtext);
    }

    partial void OnAusgewaehltePflanzeChanged(PflanzeUebersichtDto? value) => _ = VorkommenLadenAsync(value);

    partial void OnNurVerkaufsfaehigeAnzeigenChanged(bool value) => AktualisiereHighlighting();

    partial void OnStufenFilterIdChanged(int? value) => AktualisiereHighlighting();

    private async Task VorkommenLadenAsync(PflanzeUebersichtDto? pflanze)
    {
        Fundstellen = pflanze is null ? [] : await _kulturBestandService.LadeVorkommenAsync(pflanze.ArtikelId);
        AktualisiereHighlighting();
    }

    /// <summary>Färbt die Sektionen der aktuellen Fundstellen in der Farbe ihrer Kulturstufe ein und graut den
    /// Rest aus (Deckkraft ~0,25, s. GrundrissPage/PflanzenUebersichtPage-Rendering).</summary>
    private void AktualisiereHighlighting()
    {
        var sichtbar = Fundstellen.AsEnumerable();
        if (NurVerkaufsfaehigeAnzeigen)
        {
            var verkaufsfaehigeStufenIds = Kulturstufen.Where(k => k.IstVerkaufsfaehig).Select(k => k.Id).ToHashSet();
            sichtbar = sichtbar.Where(f => verkaufsfaehigeStufenIds.Contains(f.KulturstufeId));
        }

        if (StufenFilterId is { } stufenId)
        {
            sichtbar = sichtbar.Where(f => f.KulturstufeId == stufenId);
        }

        var fundstelleJeSektion = sichtbar.ToDictionary(f => f.SektionId);
        var pflanzeGewaehlt = AusgewaehltePflanze is not null;

        foreach (var element in Elemente.Where(e => !e.IstFeld))
        {
            if (fundstelleJeSektion.TryGetValue(element.Id, out var fund))
            {
                element.HighlightFarbeHex = fund.FarbeHex;
                element.HighlightMenge = fund.Menge;
                element.IstAusgegraut = false;
            }
            else
            {
                element.HighlightFarbeHex = null;
                element.HighlightMenge = null;
                element.IstAusgegraut = pflanzeGewaehlt;
            }
        }
    }
}
