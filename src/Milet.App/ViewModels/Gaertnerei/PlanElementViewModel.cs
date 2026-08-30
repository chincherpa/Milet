using CommunityToolkit.Mvvm.ComponentModel;

namespace Milet.App.ViewModels.Gaertnerei;

/// <summary>Ein Feld oder eine Sektion auf dem Grundriss. Rechnet Meter → Pixel selbst und meldet bei jeder
/// Geometrie-/Zoom-Änderung OnPropertyChanged — Views/GrundrissPage.xaml.cs zeichnet danach neu (Plan B:
/// Code-Behind-Rendering statt Attached-Property-Style-Binding, s. PLAN.md-Risiko 4).</summary>
public sealed partial class PlanElementViewModel : ObservableObject
{
    public required int Id { get; init; }
    public required bool IstFeld { get; init; }

    /// <summary>Nur bei Sektionen gesetzt — der Lagerort (Feld), zu dem sie gehört.</summary>
    public int? LagerortId { get; init; }

    public byte[] RowVersion { get; set; } = [];

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Bezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal PosXMeter { get; set; }

    [ObservableProperty]
    public partial decimal PosYMeter { get; set; }

    [ObservableProperty]
    public partial decimal BreiteMeter { get; set; }

    [ObservableProperty]
    public partial decimal HoeheMeter { get; set; }

    /// <summary>Nur bei Sektionen: absolute Position des übergeordneten Feldes — Sektionskoordinaten sind
    /// relativ zum Feld gespeichert, die Pixelposition auf der gemeinsamen Planfläche braucht die Summe.</summary>
    [ObservableProperty]
    public partial decimal FeldOffsetXMeter { get; set; }

    [ObservableProperty]
    public partial decimal FeldOffsetYMeter { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; } = 20;

    public double PixelX => (double)(PosXMeter + FeldOffsetXMeter) * Zoom;
    public double PixelY => (double)(PosYMeter + FeldOffsetYMeter) * Zoom;
    public double PixelBreite => (double)BreiteMeter * Zoom;
    public double PixelHoehe => (double)HoeheMeter * Zoom;
    public decimal FlaecheQm => Math.Round(BreiteMeter * HoeheMeter, 2);

    partial void OnPosXMeterChanged(decimal value) => OnPropertyChanged(nameof(PixelX));
    partial void OnPosYMeterChanged(decimal value) => OnPropertyChanged(nameof(PixelY));
    partial void OnFeldOffsetXMeterChanged(decimal value) => OnPropertyChanged(nameof(PixelX));
    partial void OnFeldOffsetYMeterChanged(decimal value) => OnPropertyChanged(nameof(PixelY));

    partial void OnBreiteMeterChanged(decimal value)
    {
        OnPropertyChanged(nameof(PixelBreite));
        OnPropertyChanged(nameof(FlaecheQm));
    }

    partial void OnHoeheMeterChanged(decimal value)
    {
        OnPropertyChanged(nameof(PixelHoehe));
        OnPropertyChanged(nameof(FlaecheQm));
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(PixelX));
        OnPropertyChanged(nameof(PixelY));
        OnPropertyChanged(nameof(PixelBreite));
        OnPropertyChanged(nameof(PixelHoehe));
    }

    /// <summary>Verschiebt um ein Pixel-Delta, gerastert auf 0,5 m (E11).</summary>
    public void VerschiebenUmPixel(double deltaX, double deltaY)
    {
        PosXMeter = RasternAufHalbenMeter(PosXMeter + (decimal)(deltaX / Zoom));
        PosYMeter = RasternAufHalbenMeter(PosYMeter + (decimal)(deltaY / Zoom));
    }

    public void GroesseAendernUmPixel(double deltaBreite, double deltaHoehe)
    {
        BreiteMeter = Math.Max(0.5m, RasternAufHalbenMeter(BreiteMeter + (decimal)(deltaBreite / Zoom)));
        HoeheMeter = Math.Max(0.5m, RasternAufHalbenMeter(HoeheMeter + (decimal)(deltaHoehe / Zoom)));
    }

    private static decimal RasternAufHalbenMeter(decimal wert) => Math.Round(wert * 2, MidpointRounding.AwayFromZero) / 2;
}
