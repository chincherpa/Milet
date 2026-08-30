using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Gaertnerei;

namespace Milet.App.ViewModels.Gaertnerei;

/// <summary>Grundriss-Editor: Felder + Sektionen zeichnen, verschieben, größenändern, anlegen/löschen.
/// Schreibgeschützte Wiederverwendung derselben Elemente-Liste durch PflanzenUebersichtViewModel (Task 16).</summary>
public sealed partial class GrundrissViewModel : ObservableObject
{
    private readonly IGaertnereiplanService _planService;
    private readonly IDialogService _dialogService;
    private int _planId;
    private byte[] _planRowVersion = [];
    private decimal _planBreiteMeter;
    private decimal _planHoeheMeter;

    public GrundrissViewModel(IGaertnereiplanService planService, IDialogService dialogService)
    {
        _planService = planService;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty]
    public partial ObservableCollection<PlanElementViewModel> Elemente { get; set; } = [];

    [ObservableProperty]
    public partial PlanElementViewModel? AusgewaehltesElement { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; } = 20;

    [ObservableProperty]
    public partial double PlanPixelBreite { get; set; }

    [ObservableProperty]
    public partial double PlanPixelHoehe { get; set; }

    [ObservableProperty]
    public partial string? Fehler { get; set; }

    [ObservableProperty]
    public partial string? UeberlappungsWarnung { get; set; }

    // ---- Flache Editor-Felder für das ausgewählte Element ----
    // Dieselbe Konvention wie KleinstammViewModel: das Formular bindet an flache Properties statt an
    // verschachtelte Pfade durch AusgewaehltesElement, und ein OnXxxChanged-Handler spiegelt Eingaben auf
    // die ausgewählte Instanz zurück (dort löst das PixelX/Y/Breite/Hoehe-Neuberechnung + Redraw aus).

    [ObservableProperty]
    public partial string ElementCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElementBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal ElementPosXMeter { get; set; }

    [ObservableProperty]
    public partial decimal ElementPosYMeter { get; set; }

    [ObservableProperty]
    public partial decimal ElementBreiteMeter { get; set; }

    [ObservableProperty]
    public partial decimal ElementHoeheMeter { get; set; }

    public decimal ElementFlaecheQm => ElementBreiteMeter * ElementHoeheMeter;
    public bool IstElementAusgewaehlt => AusgewaehltesElement is not null;
    public bool IstFeldAusgewaehlt => AusgewaehltesElement?.IstFeld == true;

    partial void OnAusgewaehltesElementChanged(PlanElementViewModel? value)
    {
        ElementCode = value?.Code ?? string.Empty;
        ElementBezeichnung = value?.Bezeichnung ?? string.Empty;
        ElementPosXMeter = value?.PosXMeter ?? 0;
        ElementPosYMeter = value?.PosYMeter ?? 0;
        ElementBreiteMeter = value?.BreiteMeter ?? 0;
        ElementHoeheMeter = value?.HoeheMeter ?? 0;
        OnPropertyChanged(nameof(ElementFlaecheQm));
        OnPropertyChanged(nameof(IstElementAusgewaehlt));
        OnPropertyChanged(nameof(IstFeldAusgewaehlt));
    }

    partial void OnElementCodeChanged(string value)
    {
        if (AusgewaehltesElement is { } e) e.Code = value;
    }

    partial void OnElementBezeichnungChanged(string value)
    {
        if (AusgewaehltesElement is { } e) e.Bezeichnung = value;
    }

    partial void OnElementPosXMeterChanged(decimal value)
    {
        if (AusgewaehltesElement is not { } e) return;
        e.PosXMeter = value;
        if (e.IstFeld) FeldVerschoben(e);
    }

    partial void OnElementPosYMeterChanged(decimal value)
    {
        if (AusgewaehltesElement is not { } e) return;
        e.PosYMeter = value;
        if (e.IstFeld) FeldVerschoben(e);
    }

    partial void OnElementBreiteMeterChanged(decimal value)
    {
        if (AusgewaehltesElement is { } e) e.BreiteMeter = value;
        OnPropertyChanged(nameof(ElementFlaecheQm));
    }

    partial void OnElementHoeheMeterChanged(decimal value)
    {
        if (AusgewaehltesElement is { } e) e.HoeheMeter = value;
        OnPropertyChanged(nameof(ElementFlaecheQm));
    }

    /// <summary>Wird von GrundrissPage.xaml.cs nach einer Maus-Verschiebung/Größenänderung aufgerufen, damit
    /// die Editor-Felder synchron bleiben (die Elemente selbst wurden bereits direkt verändert).</summary>
    public void UebernehmeAusElement(PlanElementViewModel element)
    {
        if (!ReferenceEquals(AusgewaehltesElement, element)) return;
        ElementPosXMeter = element.PosXMeter;
        ElementPosYMeter = element.PosYMeter;
        ElementBreiteMeter = element.BreiteMeter;
        ElementHoeheMeter = element.HoeheMeter;
    }

    partial void OnZoomChanged(double value)
    {
        foreach (var element in Elemente)
        {
            element.Zoom = value;
        }

        AktualisierePlanPixelGroesse();
    }

    private void AktualisierePlanPixelGroesse()
    {
        PlanPixelBreite = (double)_planBreiteMeter * Zoom;
        PlanPixelHoehe = (double)_planHoeheMeter * Zoom;
    }

    [RelayCommand]
    public async Task LadenAsync()
    {
        Fehler = null;
        var plan = await _planService.LadePlanAsync();
        if (plan is null)
        {
            Fehler = "Kein Gärtnereiplan vorhanden.";
            return;
        }

        _planId = plan.Id;
        _planRowVersion = plan.RowVersion;
        _planBreiteMeter = plan.BreiteMeter;
        _planHoeheMeter = plan.HoeheMeter;
        AktualisierePlanPixelGroesse();

        var neu = new ObservableCollection<PlanElementViewModel>();
        foreach (var feld in plan.Felder)
        {
            neu.Add(new PlanElementViewModel
            {
                Id = feld.Id,
                IstFeld = true,
                RowVersion = feld.RowVersion,
                Code = feld.Code,
                Bezeichnung = feld.Bezeichnung,
                PosXMeter = feld.PosXMeter,
                PosYMeter = feld.PosYMeter,
                BreiteMeter = feld.BreiteMeter,
                HoeheMeter = feld.HoeheMeter,
                Zoom = Zoom,
            });

            foreach (var sektion in feld.Sektionen)
            {
                neu.Add(new PlanElementViewModel
                {
                    Id = sektion.Id,
                    IstFeld = false,
                    LagerortId = feld.Id,
                    RowVersion = sektion.RowVersion,
                    Code = sektion.Code,
                    Bezeichnung = sektion.Bezeichnung,
                    PosXMeter = sektion.PosXMeter,
                    PosYMeter = sektion.PosYMeter,
                    BreiteMeter = sektion.BreiteMeter,
                    HoeheMeter = sektion.HoeheMeter,
                    FeldOffsetXMeter = feld.PosXMeter,
                    FeldOffsetYMeter = feld.PosYMeter,
                    Zoom = Zoom,
                });
            }
        }

        Elemente = neu;
    }

    /// <summary>Ein Feld verschieben zieht seine Sektionen mit — deren Koordinaten sind relativ zum Feld
    /// gespeichert, nur der Offset für die Pixeldarstellung muss nachgeführt werden.</summary>
    public void FeldVerschoben(PlanElementViewModel feld)
    {
        foreach (var sektion in Elemente.Where(e => !e.IstFeld && e.LagerortId == feld.Id))
        {
            sektion.FeldOffsetXMeter = feld.PosXMeter;
            sektion.FeldOffsetYMeter = feld.PosYMeter;
        }
    }

    [RelayCommand]
    private void FeldAnlegen()
    {
        var feld = new PlanElementViewModel
        {
            Id = 0,
            IstFeld = true,
            Code = string.Empty,
            Bezeichnung = "Neues Feld",
            BreiteMeter = 10,
            HoeheMeter = 10,
            Zoom = Zoom,
        };
        Elemente.Add(feld);
        AusgewaehltesElement = feld;
    }

    [RelayCommand]
    private void SektionAnlegen()
    {
        if (AusgewaehltesElement is not { IstFeld: true } feld)
        {
            Fehler = "Bitte zuerst das Feld auswählen, in das die Sektion soll.";
            return;
        }

        var sektion = new PlanElementViewModel
        {
            Id = 0,
            IstFeld = false,
            LagerortId = feld.Id,
            Code = string.Empty,
            Bezeichnung = "Neue Sektion",
            BreiteMeter = 5,
            HoeheMeter = 5,
            FeldOffsetXMeter = feld.PosXMeter,
            FeldOffsetYMeter = feld.PosYMeter,
            Zoom = Zoom,
        };
        Elemente.Add(sektion);
        AusgewaehltesElement = sektion;
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehler = null;
        UeberlappungsWarnung = null;
        if (AusgewaehltesElement is not { } element)
        {
            return;
        }

        try
        {
            if (element.IstFeld)
            {
                var dto = new FeldDto
                {
                    Id = element.Id,
                    Code = element.Code,
                    Bezeichnung = element.Bezeichnung,
                    PosXMeter = element.PosXMeter,
                    PosYMeter = element.PosYMeter,
                    BreiteMeter = element.BreiteMeter,
                    HoeheMeter = element.HoeheMeter,
                    RowVersion = element.RowVersion,
                };
                // Id ist init-only — nach dem Speichern lädt LadenAsync() unten ohnehin die komplette Liste
                // mit den serverseitig vergebenen Ids neu, ein Nachtragen hier wäre überflüssig.
                await _planService.SpeichereFeldAsync(_planId, dto);
            }
            else
            {
                var dto = new SektionDto
                {
                    Id = element.Id,
                    LagerortId = element.LagerortId!.Value,
                    Code = element.Code,
                    Bezeichnung = element.Bezeichnung,
                    PosXMeter = element.PosXMeter,
                    PosYMeter = element.PosYMeter,
                    BreiteMeter = element.BreiteMeter,
                    HoeheMeter = element.HoeheMeter,
                    RowVersion = element.RowVersion,
                };
                var ergebnis = await _planService.SpeichereSektionAsync(dto);
                element.RowVersion = ergebnis.Sektion.RowVersion;
                if (ergebnis.Warnungen.Count > 0)
                {
                    UeberlappungsWarnung = string.Join(" ", ergebnis.Warnungen);
                }
            }

            var istFeld = element.IstFeld;
            var code = element.Code;
            await LadenAsync();
            // Nach dem Neuladen zeigt "Elemente" frische Instanzen (auch für ein neu angelegtes Feld/Sektion
            // mit jetzt echter Id) — Auswahl anhand Code+Typ wiederherstellen, sonst hinge das Formular an
            // einer Instanz, die nicht mehr Teil der Liste ist.
            AusgewaehltesElement = Elemente.FirstOrDefault(e => e.IstFeld == istFeld && e.Code == code);
        }
        catch (ValidationException ex)
        {
            Fehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            Fehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (AusgewaehltesElement is not { } element)
        {
            return;
        }

        if (element.Id == 0)
        {
            // Noch nicht gespeichert — einfach aus der lokalen Liste entfernen.
            Elemente.Remove(element);
            AusgewaehltesElement = null;
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync(
            element.IstFeld ? "Feld löschen" : "Sektion löschen",
            $"'{element.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            if (element.IstFeld)
            {
                await _planService.LoescheFeldAsync(element.Id);
            }
            else
            {
                await _planService.LoescheSektionAsync(element.Id);
            }

            AusgewaehltesElement = null;
            await LadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
}
