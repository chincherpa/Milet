using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Finanzen;

namespace Milet.App.ViewModels.Finanzen;

public sealed partial class MahnlaufKandidatZeile : ObservableObject
{
    public int OffenerPostenId { get; }
    public string KundenName { get; }
    public string BelegNummer { get; }
    public decimal OffenerBetrag { get; }
    public DateOnly Faelligkeit { get; }
    public int NaechsteMahnstufe { get; }

    [ObservableProperty] public partial bool Ausgewaehlt { get; set; } = true;

    public MahnlaufKandidatZeile(string kundenName, MahnKandidatDto dto)
    {
        KundenName = kundenName;
        OffenerPostenId = dto.OffenerPostenId;
        BelegNummer = dto.BelegNummer;
        OffenerBetrag = dto.OffenerBetrag;
        Faelligkeit = dto.Faelligkeit;
        NaechsteMahnstufe = dto.NaechsteMahnstufe;
    }
}

public sealed partial class MahnlaufViewModel : ObservableObject
{
    private readonly IMahnwesenService _mahnwesenService;
    private readonly IPdfService _pdfService;
    private readonly IDialogService _dialogService;

    public MahnlaufViewModel(IMahnwesenService mahnwesenService, IPdfService pdfService, IDialogService dialogService)
    {
        _mahnwesenService = mahnwesenService;
        _pdfService = pdfService;
        _dialogService = dialogService;
        _ = FaelligeErmittelnAsync();
    }

    [ObservableProperty] public partial ObservableCollection<MahnlaufKandidatZeile> Kandidaten { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<MahnungDto> ErzeugteMahnungen { get; set; } = [];
    [ObservableProperty] public partial MahnungDto? MahnungAusgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }

    [RelayCommand]
    private async Task FaelligeErmittelnAsync()
    {
        LaedtGerade = true;
        Fehlermeldung = null;
        try
        {
            var gruppen = await _mahnwesenService.ErmittleFaelligeAsync();
            Kandidaten = new ObservableCollection<MahnlaufKandidatZeile>(
                gruppen.SelectMany(g => g.Kandidaten.Select(k => new MahnlaufKandidatZeile(g.KundenName, k))));
            ErzeugteMahnungen = [];
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
    private async Task MahnlaufDurchfuehrenAsync()
    {
        var ausgewaehlteIds = Kandidaten.Where(k => k.Ausgewaehlt).Select(k => k.OffenerPostenId).ToList();
        if (ausgewaehlteIds.Count == 0)
        {
            Fehlermeldung = "Mindestens einen Kandidaten auswählen.";
            return;
        }

        Fehlermeldung = null;
        try
        {
            ErzeugteMahnungen = await _mahnwesenService.MahnlaufDurchfuehrenAsync(ausgewaehlteIds);
            await FaelligeErmittelnAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MahnungPdfAsync()
    {
        if (MahnungAusgewaehlt is not { } mahnung) return;
        try
        {
            var pdfBytes = await _pdfService.GeneriereMahnungPdfAsync(mahnung.Id);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.SuggestedFileName = $"Mahnung-{mahnung.Id}";
            picker.FileTypeChoices.Add("PDF-Dokument", [".pdf"]);
            var datei = await picker.PickSaveFileAsync();
            if (datei is null) return;
            await Windows.Storage.FileIO.WriteBytesAsync(datei, pdfBytes);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("PDF-Erstellung fehlgeschlagen", ex.Message);
        }
    }
}
