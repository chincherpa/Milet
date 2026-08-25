using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public abstract partial class BelegEditViewModelBase : ObservableObject, INavigationAware
{
    private readonly BelegTyp _typ;
    private readonly IBelegService _belegService;
    private readonly IVerkaufLookupService _lookupService;
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly IRechnungBuchenService? _buchenService;
    private readonly IPdfService _pdfService;
    protected readonly INavigationService Navigation;
    private readonly IDialogService _dialogService;

    private int _id;
    private byte[] _rowVersion = [];
    private int _naechstePositionsNr = 1;

    protected BelegEditViewModelBase(
        BelegTyp typ,
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IRechnungBuchenService? buchenService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
    {
        _typ = typ;
        _belegService = belegService;
        _lookupService = lookupService;
        _ueberleitungService = ueberleitungService;
        _buchenService = buchenService;
        _pdfService = pdfService;
        Navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = "(automatisch)";
    /// <summary>Nullable, exakt der Typ von <c>CalendarDatePicker.Date</c> — vermeidet einen Konverter/Absturz beim x:Bind TwoWay, falls der Nutzer das Datum leert.</summary>
    [ObservableProperty] public partial DateTimeOffset? BelegDatum { get; set; } = DateTimeOffset.Now;
    [ObservableProperty] public partial IReadOnlyList<KundeVerkaufLookupDto> Kunden { get; set; } = [];
    [ObservableProperty] public partial int KundeId { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelVerkaufLookupDto> ArtikelLookups { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial BelegPositionDto? PositionAusgewaehlt { get; set; }
    [ObservableProperty] public partial int? PositionArtikelId { get; set; }
    [ObservableProperty] public partial decimal PositionMenge { get; set; } = 1;
    [ObservableProperty] public partial decimal PositionEinzelpreis { get; set; }
    [ObservableProperty] public partial decimal PositionRabattProzent { get; set; }

    [ObservableProperty] public partial decimal SummeNetto { get; set; }
    [ObservableProperty] public partial decimal SummeMwSt { get; set; }
    [ObservableProperty] public partial decimal SummeBrutto { get; set; }

    [ObservableProperty] public partial BelegStatus Status { get; set; } = BelegStatus.Entwurf;
    [ObservableProperty] public partial DateTimeOffset? Faelligkeit { get; set; }
    [ObservableProperty] public partial string? Kopftext { get; set; }
    [ObservableProperty] public partial string? Fusstext { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; } = true;

    /// <summary>Als <see cref="Microsoft.UI.Xaml.Visibility"/> statt <c>bool</c> — x:Bind konvertiert bool nicht automatisch in Visibility, ein eigener Converter wäre hierfür Overhead.</summary>
    public Microsoft.UI.Xaml.Visibility ZeigtBuchenButton =>
        _buchenService is not null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility ZeigtUeberleitenButton =>
        _typ != BelegTyp.Rechnung ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string UeberleitenButtonText => _typ switch
    {
        BelegTyp.Angebot => "→ Auftrag",
        BelegTyp.Auftrag => "→ Rechnung",
        _ => string.Empty,
    };

    /// <summary>x:Bind-Funktionsbindung für die schreibgeschützte Fälligkeits-Anzeige (nur `RechnungEditPage`).</summary>
    public string FormatiereDatum(DateTimeOffset? wert) => wert?.ToString("dd.MM.yyyy") ?? "–";

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Kunden = lookups.Kunden;
        ArtikelLookups = lookups.Artikel;

        if (_id == 0)
        {
            IstBearbeitbar = true;
            return;
        }

        var beleg = await _belegService.LadeAsync(_id);
        _rowVersion = beleg.RowVersion;
        BelegNummer = string.IsNullOrEmpty(beleg.BelegNummer) ? "(wird beim Buchen vergeben)" : beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum.ToDateTime(TimeOnly.MinValue);
        KundeId = beleg.KundeId;
        Positionen = new ObservableCollection<BelegPositionDto>(beleg.Positionen);
        _naechstePositionsNr = Positionen.Count == 0 ? 1 : Positionen.Max(p => p.PositionsNr) + 1;
        SummeNetto = beleg.SummeNetto;
        SummeMwSt = beleg.SummeMwSt;
        SummeBrutto = beleg.SummeBrutto;
        Status = beleg.Status;
        Faelligkeit = beleg.Faelligkeit?.ToDateTime(TimeOnly.MinValue);
        Kopftext = beleg.Kopftext;
        Fusstext = beleg.Fusstext;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private async Task PreisVorschlagAsync()
    {
        if (PositionArtikelId is not { } artikelId || KundeId == 0) return;
        var menge = PositionMenge <= 0 ? 1 : PositionMenge;
        var ergebnis = await _lookupService.ErmittlePreisAsync(artikelId, menge, KundeId);
        PositionEinzelpreis = ergebnis.Einzelpreis;
        PositionRabattProzent = ergebnis.RabattProzent;
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
            RabattProzent = PositionRabattProzent,
            MwStSatzId = artikel.MwStSatzId,
            MwStSatzWert = artikel.MwStSatzWert,
            SteuerSchluessel = artikel.SteuerSchluessel,
            GesamtNetto = BerechnePositionsNetto(PositionMenge, PositionEinzelpreis, PositionRabattProzent),
        });

        PositionArtikelId = null;
        PositionMenge = 1;
        PositionEinzelpreis = 0;
        PositionRabattProzent = 0;
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

    /// <summary>UI-Vorschau — Server berechnet autoritativ neu (siehe Klassenkopf-Kommentar).</summary>
    private static decimal BerechnePositionsNetto(decimal menge, decimal einzelpreis, decimal rabattProzent)
    {
        var brutto = menge * einzelpreis;
        var nachRabatt = brutto * (1 - rabattProzent / 100m);
        return Math.Round(nachRabatt, 2, MidpointRounding.ToEven);
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;
        var dto = new BelegDto
        {
            Id = _id,
            BelegTyp = _typ,
            BelegDatum = DateOnly.FromDateTime((BelegDatum ?? DateTimeOffset.Now).DateTime),
            KundeId = KundeId,
            Kopftext = Kopftext,
            Fusstext = Fusstext,
            Positionen = Positionen.ToList(),
            RowVersion = _rowVersion,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            _id = gespeichert.Id;
            _rowVersion = gespeichert.RowVersion;
            BelegNummer = string.IsNullOrEmpty(gespeichert.BelegNummer) ? "(wird beim Buchen vergeben)" : gespeichert.BelegNummer;
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
            var neuLaden = await _dialogService.BestaetigenAsync(
                "Datensatz geändert", "Dieser Beleg wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");
            if (neuLaden) await InitAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_buchenService is null || _id == 0) return;
        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id);
            BelegNummer = gebucht.BelegNummer;
            Status = gebucht.Status;
            Faelligkeit = gebucht.Faelligkeit?.ToDateTime(TimeOnly.MinValue);
            IstBearbeitbar = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PdfAsync()
    {
        if (_id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        try
        {
            var pdfBytes = await _pdfService.GeneriereBelegPdfAsync(_id);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.SuggestedFileName = string.IsNullOrEmpty(BelegNummer) ? "Beleg" : BelegNummer.Replace('/', '-');
            picker.FileTypeChoices.Add("PDF-Dokument", [".pdf"]);
            var datei = await picker.PickSaveFileAsync();
            if (datei is null) return;
            await Windows.Storage.FileIO.WriteBytesAsync(datei, pdfBytes);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UeberleitenAsync()
    {
        if (_id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        var zielTyp = _typ switch
        {
            BelegTyp.Angebot => BelegTyp.Auftrag,
            BelegTyp.Auftrag => BelegTyp.Rechnung,
            _ => (BelegTyp?)null,
        };
        if (zielTyp is null) return;

        try
        {
            await _ueberleitungService.UeberleitenAsync(_id, zielTyp.Value);
            NavigiereNachUeberleitung(zielTyp.Value);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => NavigiereZurListe();

    protected abstract void NavigiereZurListe();
    protected abstract void NavigiereNachUeberleitung(BelegTyp zielTyp);
}
