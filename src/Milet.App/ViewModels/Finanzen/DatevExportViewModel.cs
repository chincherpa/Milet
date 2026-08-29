using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Finanzen;

namespace Milet.App.ViewModels.Finanzen;

public sealed partial class DatevExportViewModel : ObservableObject
{
    private readonly IDatevExportService _datevExportService;
    private readonly IDialogService _dialogService;

    public DatevExportViewModel(IDatevExportService datevExportService, IDialogService dialogService)
    {
        _datevExportService = datevExportService;
        _dialogService = dialogService;
        Bis = DateOnly.FromDateTime(DateTime.Today);
        Von = new DateOnly(Bis.Year, Bis.Month, 1);
    }

    [ObservableProperty] public partial DateOnly Von { get; set; }
    [ObservableProperty] public partial DateOnly Bis { get; set; }
    [ObservableProperty] public partial DatevExportVorschauDto? Vorschau { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial string? Erfolgsmeldung { get; set; }

    [RelayCommand]
    private async Task VorschauLadenAsync()
    {
        LaedtGerade = true;
        Fehlermeldung = null;
        Erfolgsmeldung = null;
        try
        {
            Vorschau = await _datevExportService.VorschauAsync(Von, Bis);
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
    private async Task ExportierenAsync()
    {
        Fehlermeldung = null;
        Erfolgsmeldung = null;
        try
        {
            var ergebnis = await _datevExportService.ExportierenAsync(Von, Bis);
            if (ergebnis.AnzahlBuchungszeilen == 0)
            {
                Erfolgsmeldung = "Keine exportierbaren Buchungen im gewählten Zeitraum (geprüft: Kunde/Lieferant hat Debitoren-/Kreditorenkonto, MwSt-Satz hat Erlös-/Aufwandskonto, Bankkonto ist konfiguriert).";
                await VorschauLadenAsync();
                return;
            }

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.SuggestedFileName = ergebnis.Dateiname.Replace(".csv", "");
            picker.FileTypeChoices.Add("DATEV-Buchungsstapel (CSV)", [".csv"]);
            var datei = await picker.PickSaveFileAsync();
            if (datei is null)
            {
                Erfolgsmeldung = "Export abgebrochen — es wurde nichts als exportiert markiert, der Zeitraum lässt sich unverändert erneut exportieren.";
                return;
            }

            // Reihenfolge ist der eigentliche Punkt: erst schreiben, dann markieren. Scheitert das Schreiben
            // (volle Platte, Netzlaufwerk weg, Datei gesperrt), landet das im catch unten — und die Belege
            // gelten weiterhin als nicht exportiert, tauchen also im nächsten Lauf wieder auf.
            await Windows.Storage.FileIO.WriteBytesAsync(datei, ergebnis.CsvBytes);
            await _datevExportService.MarkiereAlsExportiertAsync(ergebnis.BelegIds, ergebnis.ZahlungIds);
            Erfolgsmeldung = $"{ergebnis.AnzahlBuchungszeilen} Buchungszeilen exportiert und gespeichert.";
            await VorschauLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("DATEV-Export fehlgeschlagen", ex.Message);
        }
    }
}
