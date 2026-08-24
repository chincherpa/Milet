using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.App.ViewModels;
using Milet.Application.Common;
using Milet.Application.Stammdaten;

namespace Milet.App.ViewModels.Stammdaten;

public sealed partial class KundeEditViewModel : ObservableObject, INavigationAware
{
    private readonly IKundenService _kundenService;
    private readonly IStammdatenLookupService _lookupService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    private int _id;
    private byte[] _rowVersion = [];

    public KundeEditViewModel(
        IKundenService kundenService,
        IStammdatenLookupService lookupService,
        INavigationService navigation,
        IDialogService dialogService)
    {
        _kundenService = kundenService;
        _lookupService = lookupService;
        _navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty]
    public partial string Kundennummer { get; set; } = "(automatisch)";

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
    public partial decimal RabattProzent { get; set; }

    [ObservableProperty]
    public partial bool Liefersperre { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LookupDto> Zahlungsbedingungen { get; set; } = [];

    [ObservableProperty]
    public partial int? ZahlungsbedingungId { get; set; }

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
        Zahlungsbedingungen = lookups.Zahlungsbedingungen;

        if (_id != 0)
        {
            var kunde = await _kundenService.LadeAsync(_id);
            _rowVersion = kunde.RowVersion;
            Kundennummer = kunde.Kundennummer;
            Name1 = kunde.Adresse.Name1;
            Name2 = kunde.Adresse.Name2;
            Strasse = kunde.Adresse.Strasse;
            Plz = kunde.Adresse.Plz;
            Ort = kunde.Adresse.Ort;
            Land = kunde.Adresse.Land;
            Email = kunde.Email;
            Telefon = kunde.Telefon;
            UStIdNr = kunde.UStIdNr;
            RabattProzent = kunde.RabattProzent;
            Liefersperre = kunde.Liefersperre;
            ZahlungsbedingungId = kunde.ZahlungsbedingungId;
        }
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;

        var dto = new KundeDto
        {
            Id = _id,
            RowVersion = _rowVersion,
            Adresse = new AdresseDto { Name1 = Name1, Name2 = Name2, Strasse = Strasse, Plz = Plz, Ort = Ort, Land = Land },
            Email = Email,
            Telefon = Telefon,
            UStIdNr = UStIdNr,
            RabattProzent = RabattProzent,
            Liefersperre = Liefersperre,
            ZahlungsbedingungId = ZahlungsbedingungId,
        };

        try
        {
            await _kundenService.SpeichereAsync(dto);
            _navigation.Navigate<KundenListViewModel>();
        }
        catch (ValidationException ex)
        {
            Fehlermeldung = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (ConcurrencyConflictException)
        {
            var neuLaden = await _dialogService.BestaetigenAsync(
                "Datensatz geändert",
                "Dieser Kunde wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");

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
    private void Abbrechen() => _navigation.Navigate<KundenListViewModel>();
}
