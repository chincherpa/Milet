using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class BestellVorschlagZeile : ObservableObject
{
    public int ArtikelId { get; }
    public string Artikelnummer { get; }
    public string Bezeichnung { get; }
    public decimal AktuellerBestand { get; }
    public decimal Mindestbestand { get; }
    public decimal Einkaufspreis { get; }
    public int MwStSatzId { get; }
    public decimal MwStSatzWert { get; }
    public int? SteuerSchluessel { get; }
    public string? EinheitKuerzel { get; }

    [ObservableProperty] public partial bool Ausgewaehlt { get; set; } = true;
    [ObservableProperty] public partial decimal Menge { get; set; }

    public BestellVorschlagZeile(BestellVorschlagPositionDto dto)
    {
        ArtikelId = dto.ArtikelId;
        Artikelnummer = dto.Artikelnummer;
        Bezeichnung = dto.Bezeichnung;
        AktuellerBestand = dto.AktuellerBestand;
        Mindestbestand = dto.Mindestbestand;
        Einkaufspreis = dto.Einkaufspreis;
        MwStSatzId = dto.MwStSatzId;
        MwStSatzWert = dto.MwStSatzWert;
        SteuerSchluessel = dto.SteuerSchluessel;
        EinheitKuerzel = dto.EinheitKuerzel;
        Menge = dto.VorschlagsMenge;
    }
}

public sealed partial class BestellVorschlagViewModel : ObservableObject
{
    private readonly IBestellVorschlagService _vorschlagService;
    private readonly IEinkaufLookupService _lookupService;
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;

    public BestellVorschlagViewModel(
        IBestellVorschlagService vorschlagService, IEinkaufLookupService lookupService, IBelegService belegService,
        INavigationService navigation)
    {
        _vorschlagService = vorschlagService;
        _lookupService = lookupService;
        _belegService = belegService;
        _navigation = navigation;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten { get; set; } = [];
    [ObservableProperty] public partial int LieferantId { get; set; }
    [ObservableProperty] public partial ObservableCollection<BestellVorschlagZeile> Zeilen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            var lookups = await _lookupService.LadeLookupsAsync();
            Lieferanten = lookups.Lieferanten;
            var vorschlaege = await _vorschlagService.ErmittleVorschlaegeAsync();
            Zeilen = new ObservableCollection<BestellVorschlagZeile>(vorschlaege.Select(v => new BestellVorschlagZeile(v)));
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            LaedtGerade = false;
        }
    }

    [RelayCommand]
    private async Task BestellungErzeugenAsync()
    {
        Fehlermeldung = null;
        if (LieferantId == 0)
        {
            // Kein Hauptlieferant am Artikel hinterlegt (siehe Phase-4-Plan, Architektur-Entscheidung 3) —
            // manuelle Auswahl je Bestellvorschlag-Lauf ist die bewusste v1-Vereinfachung.
            Fehlermeldung = "Lieferant auswählen.";
            return;
        }

        var ausgewaehlt = Zeilen.Where(z => z.Ausgewaehlt && z.Menge > 0).ToList();
        if (ausgewaehlt.Count == 0) { Fehlermeldung = "Mindestens eine Position auswählen."; return; }

        var positionen = ausgewaehlt.Select((z, i) => new BelegPositionDto
        {
            PositionsNr = i + 1,
            PositionsTyp = PositionsTyp.Artikel,
            ArtikelId = z.ArtikelId,
            Bezeichnung = z.Bezeichnung,
            EinheitKuerzel = z.EinheitKuerzel,
            Menge = z.Menge,
            Einzelpreis = z.Einkaufspreis,
            MwStSatzId = z.MwStSatzId,
            MwStSatzWert = z.MwStSatzWert,
            SteuerSchluessel = z.SteuerSchluessel,
            GesamtNetto = Math.Round(z.Menge * z.Einkaufspreis, 2, MidpointRounding.ToEven),
        }).ToList();

        var dto = new BelegDto
        {
            BelegTyp = BelegTyp.Bestellung,
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = LieferantId,
            Positionen = positionen,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            _navigation.Navigate<BestellungEditViewModel>(gespeichert.Id);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
}
