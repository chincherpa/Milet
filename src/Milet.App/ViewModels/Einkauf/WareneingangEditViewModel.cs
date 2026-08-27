using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class WareneingangEditViewModel : ObservableObject, INavigationAware
{
    private readonly IBelegService _belegService;
    private readonly IWareneingangBuchenService _buchenService;
    private readonly IEinkaufLookupService _lookupService;
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly INavigationService _navigation;

    private int _id;
    private IReadOnlyList<ArtikelEinkaufLookupDto> _artikelLookups = [];

    public WareneingangEditViewModel(
        IBelegService belegService, IWareneingangBuchenService buchenService, IEinkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService, INavigationService navigation)
    {
        _belegService = belegService;
        _buchenService = buchenService;
        _lookupService = lookupService;
        _ueberleitungService = ueberleitungService;
        _navigation = navigation;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly BelegDatum { get; set; }
    [ObservableProperty] public partial string LieferantAnzeige { get; set; } = string.Empty;
    [ObservableProperty] public partial BelegStatus Status { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; }
    [ObservableProperty] public partial bool KannUeberleiten { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = LadenAsync();
    }

    private async Task LadenAsync()
    {
        if (_id == 0) return;
        var lookups = await _lookupService.LadeLookupsAsync();
        _artikelLookups = lookups.Artikel;

        var beleg = await _belegService.LadeAsync(_id);
        BelegNummer = beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum;
        LieferantAnzeige = beleg.LieferantAnzeige;
        Status = beleg.Status;
        Positionen = beleg.Positionen;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
        KannUeberleiten = beleg.Status == BelegStatus.Gebucht;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        var neueSeriennummernJePosition = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var position in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            var artikel = _artikelLookups.FirstOrDefault(a => a.Id == position.ArtikelId);
            if (artikel is not { HatSeriennummern: true }) continue;

            var dialog = new Milet.App.Views.Einkauf.SeriennummernErfassungDialog(position) { XamlRoot = App.MainWindow.Content.XamlRoot };
            var ergebnis = await dialog.ShowAsync();
            if (ergebnis != ContentDialogResult.Primary) return;
            neueSeriennummernJePosition[position.Id] = dialog.ErfassteNummern();
        }

        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id, neueSeriennummernJePosition);
            Status = gebucht.Status;
            IstBearbeitbar = false;
            KannUeberleiten = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UeberleitenZuEingangsrechnungAsync()
    {
        if (_id == 0 || Status != BelegStatus.Gebucht) return;
        try
        {
            await _ueberleitungService.UeberleitenAsync(_id, BelegTyp.Eingangsrechnung);
            _navigation.Navigate<EingangsrechnungListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<WareneingangListViewModel>();
}
