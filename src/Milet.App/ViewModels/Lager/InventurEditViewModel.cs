using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class InventurPositionZeile : ObservableObject
{
    public int Id { get; }
    public string Artikelnummer { get; }
    public string ArtikelBezeichnung { get; }
    public decimal SollMenge { get; }

    [ObservableProperty]
    public partial decimal? IstMenge { get; set; }

    public InventurPositionZeile(InventurPositionDto dto)
    {
        Id = dto.Id;
        Artikelnummer = dto.Artikelnummer;
        ArtikelBezeichnung = dto.ArtikelBezeichnung;
        SollMenge = dto.SollMenge;
        IstMenge = dto.IstMenge;
    }
}

public sealed partial class InventurEditViewModel : ObservableObject, INavigationAware
{
    private readonly IInventurService _inventurService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;
    private int _id;

    public InventurEditViewModel(IInventurService inventurService, INavigationService navigation, IDialogService dialogService)
    {
        _inventurService = inventurService;
        _navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty] public partial string LagerortBezeichnung { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly Datum { get; set; }
    [ObservableProperty] public partial InventurStatus Status { get; set; }
    [ObservableProperty] public partial ObservableCollection<InventurPositionZeile> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstOffen { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = LadenAsync();
    }

    private async Task LadenAsync()
    {
        if (_id == 0) return;
        var inventur = await _inventurService.LadeAsync(_id);
        LagerortBezeichnung = inventur.LagerortBezeichnung;
        Datum = inventur.Datum;
        Status = inventur.Status;
        IstOffen = inventur.Status == InventurStatus.Offen;
        Positionen = new ObservableCollection<InventurPositionZeile>(inventur.Positionen.Select(p => new InventurPositionZeile(p)));
    }

    [RelayCommand]
    private async Task MengenSpeichernAsync()
    {
        Fehlermeldung = null;
        try
        {
            foreach (var zeile in Positionen.Where(z => z.IstMenge.HasValue))
                await _inventurService.ErfasseIstMengeAsync(zeile.Id, zeile.IstMenge!.Value);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AbschliessenAsync()
    {
        var bestaetigt = await _dialogService.BestaetigenAsync("Inventur abschließen", "Inventur abschließen und Korrekturbuchungen für alle erfassten Abweichungen anlegen?");
        if (!bestaetigt) return;

        try
        {
            var inventur = await _inventurService.AbschliessenAsync(_id);
            Status = inventur.Status;
            IstOffen = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<InventurListViewModel>();
}
