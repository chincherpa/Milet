using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Milet.App.Converters;
using Milet.App.Themes;
using Milet.App.ViewModels.Gaertnerei;

namespace Milet.App.Views.Gaertnerei;

/// <summary>Schreibgeschützte Variante des Grundriss-Renderings (Task 15) — keine Pointer-Handler für
/// Ziehen/Größenändern, dafür Einfärbung nach Kulturstufe der gewählten Pflanze (HighlightFarbeHex) und
/// Ausgrauen aller übrigen Sektionen (IstAusgegraut).</summary>
public sealed partial class PflanzenUebersichtPage : Page
{
    public PflanzenUebersichtViewModel ViewModel { get; }
    private readonly Dictionary<PlanElementViewModel, Border> _boxenJeElement = [];
    private readonly Dictionary<PlanElementViewModel, PropertyChangedEventHandler> _handlerJeElement = [];

    public PflanzenUebersichtPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<PflanzenUebersichtViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Elemente.CollectionChanged += Elemente_CollectionChanged;

        // s. GrundrissPage: imperativ gezeichnete Themefarben müssen beim Umschalten neu gesetzt werden,
        // und im Konstruktor steht ActualTheme noch nicht auf dem geerbten Wert.
        ActualThemeChanged += (_, _) => NeuZeichnen();
        Loaded += (_, _) => NeuZeichnen();

        NeuZeichnen();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PflanzenUebersichtViewModel.Elemente):
                ViewModel.Elemente.CollectionChanged += Elemente_CollectionChanged;
                NeuZeichnen();
                break;
            case nameof(PflanzenUebersichtViewModel.Zoom):
                NeuZeichnen();
                break;
        }
    }

    private void Elemente_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => NeuZeichnen();

    private void NeuZeichnen()
    {
        // s. GrundrissPage: ohne Abmelden sammeln sich die Handler bei jedem Neuzeichnen an.
        foreach (var (element, handler) in _handlerJeElement)
        {
            element.PropertyChanged -= handler;
        }

        _handlerJeElement.Clear();
        PlanCanvas.Children.Clear();
        _boxenJeElement.Clear();

        ZeichneRaster();
        foreach (var element in ViewModel.Elemente)
        {
            ZeichneElement(element);
        }
    }

    private void ZeichneRaster()
    {
        var breite = ViewModel.PlanPixelBreite;
        var hoehe = ViewModel.PlanPixelHoehe;
        var schritt = ViewModel.Zoom;
        if (schritt <= 0) return;

        var rasterBrush = ThemeRessourcen.Brush(this, "MiletPlanRasterBrush");
        for (var x = 0.0; x <= breite; x += schritt)
        {
            PlanCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = hoehe, Stroke = rasterBrush, StrokeThickness = 0.5 });
        }

        for (var y = 0.0; y <= hoehe; y += schritt)
        {
            PlanCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = breite, Y2 = y, Stroke = rasterBrush, StrokeThickness = 0.5 });
        }
    }

    private void ZeichneElement(PlanElementViewModel element)
    {
        var box = new Border
        {
            Width = element.PixelBreite,
            Height = element.PixelHoehe,
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            Opacity = element.IstAusgegraut ? 0.25 : 1.0,
        };
        AktualisiereFarbe(element, box);

        var label = new TextBlock
        {
            Text = string.IsNullOrEmpty(element.Bezeichnung) ? element.Code : $"{element.Code}\n{element.Bezeichnung}",
            FontSize = 11,
            Margin = new Thickness(4),
            TextWrapping = TextWrapping.Wrap,
        };
        if (element.HighlightMenge is { } menge)
        {
            label.Text += $"\n{menge:0.###}";
        }

        box.Child = label;
        ToolTipService.SetToolTip(box, $"{element.Bezeichnung} ({element.BreiteMeter}×{element.HoeheMeter} m)"
            + (element.HighlightMenge is { } m ? $" — {m:0.###} Stück" : string.Empty));

        Canvas.SetLeft(box, element.PixelX);
        Canvas.SetTop(box, element.PixelY);
        Canvas.SetZIndex(box, element.IstFeld ? 0 : 1);

        PlanCanvas.Children.Add(box);
        _boxenJeElement[element] = box;

        PropertyChangedEventHandler handler = (_, _) => AktualisierePosition(element);
        element.PropertyChanged += handler;
        _handlerJeElement[element] = handler;
    }

    private void AktualisiereFarbe(PlanElementViewModel element, Border box)
    {
        if (element.IstFeld)
        {
            box.Background = ThemeRessourcen.Brush(this, "MiletPlanFeldFuellungBrush");
            box.BorderBrush = ThemeRessourcen.Brush(this, "MiletPlanFeldRandBrush");
            return;
        }

        // Hervorgehobene Sektionen tragen die Stammdatenfarbe der Kulturstufe, nicht die Themefarbe —
        // die bleibt deshalb bewusst hartcodiert aus den Daten. Das Parsen läuft über FarbHelfer, weil
        // ein fehlerhafter Wert hier sonst beim Zeichnen eine FormatException wirft.
        if (FarbHelfer.VersucheHexZuFarbe(element.HighlightFarbeHex, alpha: 180, out var fuellung)
            && FarbHelfer.VersucheHexZuFarbe(element.HighlightFarbeHex, alpha: 255, out var rand))
        {
            box.Background = new SolidColorBrush(fuellung);
            box.BorderBrush = new SolidColorBrush(rand);
        }
        else
        {
            box.Background = ThemeRessourcen.Brush(this, "MiletPlanUnbekanntFuellungBrush");
            box.BorderBrush = ThemeRessourcen.Brush(this, "MiletPlanUnbekanntRandBrush");
        }
    }

    private void AktualisierePosition(PlanElementViewModel element)
    {
        if (!_boxenJeElement.TryGetValue(element, out var box)) return;
        box.Width = element.PixelBreite;
        box.Height = element.PixelHoehe;
        box.Opacity = element.IstAusgegraut ? 0.25 : 1.0;
        AktualisiereFarbe(element, box);
        Canvas.SetLeft(box, element.PixelX);
        Canvas.SetTop(box, element.PixelY);
    }
}
