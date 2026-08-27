using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Common;
using Milet.Application.Reporting;

namespace Milet.App.ViewModels.Reporting;

public sealed partial class ReportingViewModel : ObservableObject
{
    private readonly IReportingService _reportingService;
    private readonly IDialogService _dialogService;

    public ReportingViewModel(IReportingService reportingService, IDialogService dialogService)
    {
        _reportingService = reportingService;
        _dialogService = dialogService;
        Bis = DateOnly.FromDateTime(DateTime.Today);
        Von = Bis.AddMonths(-1);
        _ = OffeneAuftraegeLadenAsync();
    }

    [ObservableProperty] public partial DateOnly Von { get; set; }
    [ObservableProperty] public partial DateOnly Bis { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }

    [ObservableProperty] public partial IReadOnlyList<UmsatzJeKundeDto> UmsatzJeKundeListe { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<UmsatzJeArtikelDto> UmsatzJeArtikelListe { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<UmsatzJeMonatDto> UmsatzJeMonatListe { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<ArtikelbewegungDto> ArtikelbewegungenListe { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<TopArtikelDto> TopArtikelListe { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<OffenerAuftragDto> OffeneAuftraegeListe { get; set; } = [];

    private async Task<T> MitFehlerbehandlungAsync<T>(Func<Task<T>> aktion, T standardwert)
    {
        Fehlermeldung = null;
        try
        {
            return await aktion();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return standardwert;
        }
    }

    [RelayCommand]
    private async Task UmsatzJeKundeLadenAsync() =>
        UmsatzJeKundeListe = await MitFehlerbehandlungAsync(() => _reportingService.UmsatzJeKundeAsync(Von, Bis), (IReadOnlyList<UmsatzJeKundeDto>)[]);

    [RelayCommand]
    private async Task UmsatzJeArtikelLadenAsync() =>
        UmsatzJeArtikelListe = await MitFehlerbehandlungAsync(() => _reportingService.UmsatzJeArtikelAsync(Von, Bis), (IReadOnlyList<UmsatzJeArtikelDto>)[]);

    [RelayCommand]
    private async Task UmsatzJeMonatLadenAsync() =>
        UmsatzJeMonatListe = await MitFehlerbehandlungAsync(() => _reportingService.UmsatzJeMonatAsync(Von, Bis), (IReadOnlyList<UmsatzJeMonatDto>)[]);

    [RelayCommand]
    private async Task ArtikelbewegungenLadenAsync() =>
        ArtikelbewegungenListe = await MitFehlerbehandlungAsync(() => _reportingService.ArtikelbewegungenAsync(null, Von, Bis), (IReadOnlyList<ArtikelbewegungDto>)[]);

    [RelayCommand]
    private async Task TopArtikelLadenAsync() =>
        TopArtikelListe = await MitFehlerbehandlungAsync(() => _reportingService.TopArtikelAsync(Von, Bis, 10), (IReadOnlyList<TopArtikelDto>)[]);

    [RelayCommand]
    private async Task OffeneAuftraegeLadenAsync() =>
        OffeneAuftraegeListe = await MitFehlerbehandlungAsync(() => _reportingService.OffeneAuftraegeAsync(), (IReadOnlyList<OffenerAuftragDto>)[]);

    [RelayCommand]
    private Task UmsatzJeKundeExportierenAsync() => ExportierenAsync("UmsatzJeKunde.csv",
        ["Kundennummer", "Kunde", "Anzahl Rechnungen", "Netto", "Brutto"],
        UmsatzJeKundeListe.Select(d => (IReadOnlyList<object?>)[d.KundeNummer, d.KundeName, d.AnzahlRechnungen, d.SummeNetto, d.SummeBrutto]));

    [RelayCommand]
    private Task UmsatzJeArtikelExportierenAsync() => ExportierenAsync("UmsatzJeArtikel.csv",
        ["Artikelnummer", "Bezeichnung", "Menge", "Netto"],
        UmsatzJeArtikelListe.Select(d => (IReadOnlyList<object?>)[d.ArtikelNummer, d.Bezeichnung, d.Menge, d.SummeNetto]));

    [RelayCommand]
    private Task UmsatzJeMonatExportierenAsync() => ExportierenAsync("UmsatzJeMonat.csv",
        ["Jahr", "Monat", "Netto", "Brutto"],
        UmsatzJeMonatListe.Select(d => (IReadOnlyList<object?>)[d.Jahr, d.Monat, d.SummeNetto, d.SummeBrutto]));

    [RelayCommand]
    private Task ArtikelbewegungenExportierenAsync() => ExportierenAsync("Artikelbewegungen.csv",
        ["Zeitpunkt", "Artikelnummer", "Bezeichnung", "Lagerort", "Menge", "Typ", "Beleg"],
        ArtikelbewegungenListe.Select(d => (IReadOnlyList<object?>)[d.Zeitpunkt, d.ArtikelNummer, d.ArtikelBezeichnung, d.LagerortCode, d.Menge, d.Typ, d.BelegNummer]));

    [RelayCommand]
    private Task TopArtikelExportierenAsync() => ExportierenAsync("TopArtikel.csv",
        ["Artikelnummer", "Bezeichnung", "Menge", "Netto"],
        TopArtikelListe.Select(d => (IReadOnlyList<object?>)[d.ArtikelNummer, d.Bezeichnung, d.Menge, d.SummeNetto]));

    [RelayCommand]
    private Task OffeneAuftraegeExportierenAsync() => ExportierenAsync("OffeneAuftraege.csv",
        ["Belegnummer", "Datum", "Kunde", "Brutto", "Offene Menge"],
        OffeneAuftraegeListe.Select(d => (IReadOnlyList<object?>)[d.BelegNummer, d.BelegDatum, d.KundeName, d.SummeBrutto, d.OffeneMenge]));

    private async Task ExportierenAsync(string dateiname, IReadOnlyList<string> spalten, IEnumerable<IReadOnlyList<object?>> zeilen)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.SuggestedFileName = dateiname.Replace(".csv", "");
            picker.FileTypeChoices.Add("CSV-Datei", [".csv"]);
            var datei = await picker.PickSaveFileAsync();
            if (datei is null) return;

            var bytes = CsvWriter.Schreiben(spalten, zeilen);
            await Windows.Storage.FileIO.WriteBytesAsync(datei, bytes);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("CSV-Export fehlgeschlagen", ex.Message);
        }
    }
}
