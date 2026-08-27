using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

/// <summary>Analog zu Verkauf.BelegEditViewModelBase, aber Lieferant- statt Kunde-basiert und ohne
/// Preisfindung (EK-Preis kommt direkt aus Artikel.Einkaufspreis). Bewusst eine eigene Basisklasse statt
/// Erweiterung von BelegEditViewModelBase — siehe Architektur-Entscheidung 2 im Phase-4-Plan.</summary>
public abstract partial class EinkaufBelegEditViewModelBase : ObservableObject, INavigationAware
{
    private readonly BelegTyp _typ;
    private readonly IBelegService _belegService;
    private readonly IEinkaufLookupService _lookupService;
    protected readonly INavigationService Navigation;
    protected readonly IDialogService DialogService;

    protected int Id;
    private byte[] _rowVersion = [];
    private int _naechstePositionsNr = 1;

    protected EinkaufBelegEditViewModelBase(
        BelegTyp typ, IBelegService belegService, IEinkaufLookupService lookupService,
        INavigationService navigation, IDialogService dialogService)
    {
        _typ = typ;
        _belegService = belegService;
        _lookupService = lookupService;
        Navigation = navigation;
        DialogService = dialogService;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = "(automatisch)";
    [ObservableProperty] public partial DateTimeOffset? BelegDatum { get; set; } = DateTimeOffset.Now;
    [ObservableProperty] public partial IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten { get; set; } = [];
    [ObservableProperty] public partial int LieferantId { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelEinkaufLookupDto> ArtikelLookups { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial BelegPositionDto? PositionAusgewaehlt { get; set; }
    [ObservableProperty] public partial int? PositionArtikelId { get; set; }
    [ObservableProperty] public partial decimal PositionMenge { get; set; } = 1;
    [ObservableProperty] public partial decimal PositionEinzelpreis { get; set; }

    [ObservableProperty] public partial decimal SummeNetto { get; set; }
    [ObservableProperty] public partial decimal SummeMwSt { get; set; }
    [ObservableProperty] public partial decimal SummeBrutto { get; set; }

    [ObservableProperty] public partial BelegStatus Status { get; set; } = BelegStatus.Entwurf;
    [ObservableProperty] public partial string? Kopftext { get; set; }
    [ObservableProperty] public partial string? Fusstext { get; set; }
    /// <summary>Nur auf der Eingangsrechnung-Seite als Eingabefeld sichtbar ("Rechnungsnummer des Lieferanten") —
    /// siehe Architektur-Entscheidung 6. Für Bestellung/Wareneingang bleibt das Feld leer/ungenutzt.</summary>
    [ObservableProperty] public partial string? ExterneReferenz { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; } = true;

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        Id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Lieferanten = lookups.Lieferanten;
        ArtikelLookups = lookups.Artikel;

        if (Id == 0) { IstBearbeitbar = true; return; }

        var beleg = await _belegService.LadeAsync(Id);
        _rowVersion = beleg.RowVersion;
        BelegNummer = beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum.ToDateTime(TimeOnly.MinValue);
        LieferantId = beleg.LieferantId ?? 0;
        Positionen = new ObservableCollection<BelegPositionDto>(beleg.Positionen);
        _naechstePositionsNr = Positionen.Count == 0 ? 1 : Positionen.Max(p => p.PositionsNr) + 1;
        SummeNetto = beleg.SummeNetto;
        SummeMwSt = beleg.SummeMwSt;
        SummeBrutto = beleg.SummeBrutto;
        Status = beleg.Status;
        Kopftext = beleg.Kopftext;
        Fusstext = beleg.Fusstext;
        ExterneReferenz = beleg.ExterneReferenz;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private void EkPreisUebernehmen()
    {
        if (PositionArtikelId is not { } artikelId) return;
        var artikel = ArtikelLookups.FirstOrDefault(a => a.Id == artikelId);
        if (artikel is not null) PositionEinzelpreis = artikel.Einkaufspreis;
    }

    [RelayCommand]
    private void PositionHinzufuegen()
    {
        if (PositionArtikelId is not { } artikelId) { Fehlermeldung = "Artikel auswählen."; return; }
        if (PositionMenge <= 0) { Fehlermeldung = "Menge muss größer 0 sein."; return; }
        var artikel = ArtikelLookups.FirstOrDefault(a => a.Id == artikelId);
        if (artikel is null) return;

        Positionen.Add(new BelegPositionDto
        {
            PositionsNr = _naechstePositionsNr++,
            PositionsTyp = PositionsTyp.Artikel,
            ArtikelId = artikel.Id,
            Bezeichnung = artikel.Bezeichnung,
            EinheitKuerzel = artikel.EinheitKuerzel,
            Menge = PositionMenge,
            Einzelpreis = PositionEinzelpreis,
            MwStSatzId = artikel.MwStSatzId,
            MwStSatzWert = artikel.MwStSatzWert,
            SteuerSchluessel = artikel.SteuerSchluessel,
            GesamtNetto = Math.Round(PositionMenge * PositionEinzelpreis, 2, MidpointRounding.ToEven),
        });

        PositionArtikelId = null;
        PositionMenge = 1;
        PositionEinzelpreis = 0;
        Fehlermeldung = null;
        AktualisiereSummen();
    }

    [RelayCommand]
    private void PositionEntfernen()
    {
        if (PositionAusgewaehlt is not { } position) return;
        Positionen.Remove(position);
        PositionAusgewaehlt = null;
        AktualisiereSummen();
    }

    private void AktualisiereSummen()
    {
        decimal netto = 0, mwst = 0;
        foreach (var gruppe in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel).GroupBy(p => p.MwStSatzWert))
        {
            var gruppenNetto = Math.Round(gruppe.Sum(p => p.GesamtNetto), 2, MidpointRounding.ToEven);
            netto += gruppenNetto;
            mwst += Math.Round(gruppenNetto * gruppe.Key / 100m, 2, MidpointRounding.ToEven);
        }
        SummeNetto = netto;
        SummeMwSt = mwst;
        SummeBrutto = netto + mwst;
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;
        var dto = new BelegDto
        {
            Id = Id,
            BelegTyp = _typ,
            BelegDatum = DateOnly.FromDateTime((BelegDatum ?? DateTimeOffset.Now).DateTime),
            LieferantId = LieferantId,
            Kopftext = Kopftext,
            Fusstext = Fusstext,
            ExterneReferenz = ExterneReferenz,
            Positionen = Positionen.ToList(),
            RowVersion = _rowVersion,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            Id = gespeichert.Id;
            _rowVersion = gespeichert.RowVersion;
            BelegNummer = gespeichert.BelegNummer;
            SummeNetto = gespeichert.SummeNetto;
            SummeMwSt = gespeichert.SummeMwSt;
            SummeBrutto = gespeichert.SummeBrutto;
        }
        catch (ValidationException ex)
        {
            Fehlermeldung = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (ConcurrencyConflictException)
        {
            var neuLaden = await DialogService.BestaetigenAsync(
                "Datensatz geändert", "Dieser Beleg wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");
            if (neuLaden) await InitAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => NavigiereZurListe();

    protected abstract void NavigiereZurListe();
}
