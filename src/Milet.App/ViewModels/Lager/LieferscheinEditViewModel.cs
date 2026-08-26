using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Lager;

public sealed partial class LieferscheinEditViewModel : ObservableObject, INavigationAware
{
    private readonly IBelegService _belegService;
    private readonly IVerkaufLookupService _lookupService;
    private readonly ILieferscheinBuchenService _buchenService;
    private readonly Milet.Application.Lager.ISeriennummernService _seriennummernService;
    private readonly INavigationService _navigation;

    private int _id;
    private IReadOnlyList<ArtikelVerkaufLookupDto> _artikelLookups = [];

    public LieferscheinEditViewModel(
        IBelegService belegService, IVerkaufLookupService lookupService, ILieferscheinBuchenService buchenService,
        Milet.Application.Lager.ISeriennummernService seriennummernService, INavigationService navigation)
    {
        _belegService = belegService;
        _lookupService = lookupService;
        _buchenService = buchenService;
        _seriennummernService = seriennummernService;
        _navigation = navigation;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly BelegDatum { get; set; }
    [ObservableProperty] public partial string KundeAnzeige { get; set; } = string.Empty;
    [ObservableProperty] public partial BelegStatus Status { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; }

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
        KundeAnzeige = beleg.KundeAnzeige;
        Status = beleg.Status;
        Positionen = beleg.Positionen;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        var seriennummernJePosition = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var position in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            var artikel = _artikelLookups.FirstOrDefault(a => a.Id == position.ArtikelId);
            if (artikel is not { HatSeriennummern: true }) continue;

            var verfuegbar = await _seriennummernService.AufLagerAsync(position.ArtikelId!.Value);
            var dialog = new Milet.App.Views.Lager.SeriennummernAuswahlDialog(position, verfuegbar) { XamlRoot = App.MainWindow.Content.XamlRoot };
            var ergebnis = await dialog.ShowAsync();
            if (ergebnis != ContentDialogResult.Primary) return;
            seriennummernJePosition[position.Id] = dialog.Ausgewaehlt();
        }

        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id, seriennummernJePosition);
            Status = gebucht.Status;
            IstBearbeitbar = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<LieferscheinListViewModel>();
}
