using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Milet.App.ViewModels.Gaertnerei;
using Windows.UI;

namespace Milet.App.Views.Gaertnerei;

/// <summary>Schreibgeschützte Variante des Grundriss-Renderings (Task 15) — keine Pointer-Handler für
/// Ziehen/Größenändern, dafür Einfärbung nach Kulturstufe der gewählten Pflanze (HighlightFarbeHex) und
/// Ausgrauen aller übrigen Sektionen (IstAusgegraut).</summary>
public sealed partial class PflanzenUebersichtPage : Page
{
    public PflanzenUebersichtViewModel ViewModel { get; }
    private readonly Dictionary<PlanElementViewModel, Border> _boxenJeElement = [];

    public PflanzenUebersichtPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<PflanzenUebersichtViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Elemente.CollectionChanged += Elemente_CollectionChanged;
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

        var rasterBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
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

        element.PropertyChanged += (_, _) => AktualisierePosition(element);
    }

    private static void AktualisiereFarbe(PlanElementViewModel element, Border box)
    {
        if (element.IstFeld)
        {
            box.Background = new SolidColorBrush(Color.FromArgb(20, 33, 150, 243));
            box.BorderBrush = new SolidColorBrush(Colors.DodgerBlue);
            return;
        }

        if (element.HighlightFarbeHex is { } hex && hex.Length == 7 && hex[0] == '#')
        {
            var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
            var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
            var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
            box.Background = new SolidColorBrush(Color.FromArgb(180, r, g, b));
            box.BorderBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
        else
        {
            box.Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            box.BorderBrush = new SolidColorBrush(Colors.Gray);
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
