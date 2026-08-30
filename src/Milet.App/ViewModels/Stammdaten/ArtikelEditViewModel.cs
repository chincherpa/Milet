using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Common;
using Milet.Application.Stammdaten;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class ArtikelEditViewModel : ObservableObject, INavigationAware
{
    private readonly IArtikelService _artikelService;
    private readonly IStammdatenLookupService _lookupService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    private int _id;
    private byte[] _rowVersion = [];

    public ArtikelEditViewModel(
        IArtikelService artikelService,
        IStammdatenLookupService lookupService,
        INavigationService navigation,
        IDialogService dialogService)
    {
        _artikelService = artikelService;
        _lookupService = lookupService;
        _navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty]
    public partial string Artikelnummer { get; set; } = "(automatisch)";

    [ObservableProperty]
    public partial string Bezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Beschreibung { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LookupDto> Einheiten { get; set; } = [];

    [ObservableProperty]
    public partial int EinheitId { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LookupDto> MwStSaetze { get; set; } = [];

    [ObservableProperty]
    public partial int MwStSatzId { get; set; }

    [ObservableProperty]
    public partial decimal Einkaufspreis { get; set; }

    [ObservableProperty]
    public partial decimal Listenpreis { get; set; }

    [ObservableProperty]
    public partial string? Ean { get; set; }

    [ObservableProperty]
    public partial bool IstLagerartikel { get; set; } = true;

    [ObservableProperty]
    public partial decimal? Mindestbestand { get; set; }

    [ObservableProperty]
    public partial bool Gesperrt { get; set; }

    [ObservableProperty]
    public partial bool IstKulturpflanze { get; set; }

    [ObservableProperty]
    public partial string? BotanischerName { get; set; }

    [ObservableProperty]
    public partial string? Fehlermeldung { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Einheiten = lookups.Einheiten;
        MwStSaetze = lookups.MwStSaetze;

        if (_id != 0)
        {
            var artikel = await _artikelService.LadeAsync(_id);
            _rowVersion = artikel.RowVersion;
            Artikelnummer = artikel.Artikelnummer;
            Bezeichnung = artikel.Bezeichnung;
            Beschreibung = artikel.Beschreibung;
            EinheitId = artikel.EinheitId;
            MwStSatzId = artikel.MwStSatzId;
            Einkaufspreis = artikel.Einkaufspreis;
            Listenpreis = artikel.Listenpreis;
            Ean = artikel.Ean;
            IstLagerartikel = artikel.IstLagerartikel;
            Mindestbestand = artikel.Mindestbestand;
            Gesperrt = artikel.Gesperrt;
            IstKulturpflanze = artikel.IstKulturpflanze;
            BotanischerName = artikel.BotanischerName;
        }
        else if (Einheiten.Count > 0)
        {
            EinheitId = Einheiten[0].Id;
            MwStSatzId = MwStSaetze[0].Id;
        }
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;

        var dto = new ArtikelDto
        {
            Id = _id,
            RowVersion = _rowVersion,
            Bezeichnung = Bezeichnung,
            Beschreibung = Beschreibung,
            EinheitId = EinheitId,
            MwStSatzId = MwStSatzId,
            Einkaufspreis = Einkaufspreis,
            Listenpreis = Listenpreis,
            Ean = Ean,
            IstLagerartikel = IstLagerartikel,
            Mindestbestand = Mindestbestand,
            Gesperrt = Gesperrt,
            IstKulturpflanze = IstKulturpflanze,
            BotanischerName = BotanischerName,
        };

        try
        {
            await _artikelService.SpeichereAsync(dto);
            _navigation.Navigate<ArtikelListViewModel>();
        }
        catch (ValidationException ex)
        {
            Fehlermeldung = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (ConcurrencyConflictException)
        {
            var neuLaden = await _dialogService.BestaetigenAsync(
                "Datensatz geändert",
                "Dieser Artikel wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");

            if (neuLaden)
            {
                await InitAsync();
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<ArtikelListViewModel>();
}
