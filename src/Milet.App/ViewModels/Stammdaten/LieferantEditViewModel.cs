using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Nexus.App.Services;
using Nexus.Application.Common;
using Nexus.Application.Stammdaten;

namespace Nexus.App.ViewModels.Stammdaten;

public sealed partial class LieferantEditViewModel : ObservableObject, INavigationAware
{
    private readonly ILieferantenService _lieferantenService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    private int _id;
    private byte[] _rowVersion = [];

    public LieferantEditViewModel(ILieferantenService lieferantenService, INavigationService navigation, IDialogService dialogService)
    {
        _lieferantenService = lieferantenService;
        _navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty]
    public partial string Lieferantennummer { get; set; } = "(automatisch)";

    [ObservableProperty]
    public partial string Name1 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Name2 { get; set; }

    [ObservableProperty]
    public partial string Strasse { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Plz { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Ort { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Land { get; set; } = "DE";

    [ObservableProperty]
    public partial string? Email { get; set; }

    [ObservableProperty]
    public partial string? Telefon { get; set; }

    [ObservableProperty]
    public partial string? UStIdNr { get; set; }

    [ObservableProperty]
    public partial string? Fehlermeldung { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        if (_id != 0)
        {
            var lieferant = await _lieferantenService.LadeAsync(_id);
            _rowVersion = lieferant.RowVersion;
            Lieferantennummer = lieferant.Lieferantennummer;
            Name1 = lieferant.Adresse.Name1;
            Name2 = lieferant.Adresse.Name2;
            Strasse = lieferant.Adresse.Strasse;
            Plz = lieferant.Adresse.Plz;
            Ort = lieferant.Adresse.Ort;
            Land = lieferant.Adresse.Land;
            Email = lieferant.Email;
            Telefon = lieferant.Telefon;
            UStIdNr = lieferant.UStIdNr;
        }
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;

        var dto = new LieferantDto
        {
            Id = _id,
            RowVersion = _rowVersion,
            Adresse = new AdresseDto { Name1 = Name1, Name2 = Name2, Strasse = Strasse, Plz = Plz, Ort = Ort, Land = Land },
            Email = Email,
            Telefon = Telefon,
            UStIdNr = UStIdNr,
        };

        try
        {
            await _lieferantenService.SpeichereAsync(dto);
            _navigation.Navigate<LieferantenListViewModel>();
        }
        catch (ValidationException ex)
        {
            Fehlermeldung = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (ConcurrencyConflictException)
        {
            var neuLaden = await _dialogService.BestaetigenAsync(
                "Datensatz geändert",
                "Dieser Lieferant wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");

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
    private void Abbrechen() => _navigation.Navigate<LieferantenListViewModel>();
}
