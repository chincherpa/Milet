using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Milet.App.ViewModels.Gaertnerei;
using Windows.Foundation;
using Windows.UI;

namespace Milet.App.Views.Gaertnerei;

/// <summary>Plan B aus PLAN.md-Risiko 4: die Rechtecke werden direkt im Code-Behind in den Canvas gehängt,
/// statt über ein ItemContainerStyle mit Attached-Property-Bindings (x:Bind kann Canvas.Left/Top im Style
/// nicht setzen). Die ViewModels (PlanElementViewModel) bleiben unverändert wiederverwendbar.</summary>
public sealed partial class GrundrissPage : Page
{
    public GrundrissViewModel ViewModel { get; }

    private readonly Dictionary<PlanElementViewModel, Border> _boxenJeElement = [];
    private readonly Dictionary<PlanElementViewModel, Border> _handlesJeElement = [];
    private PlanElementViewModel? _ziehElement;
    private bool _istGroessenAenderung;
    private Point _letzterZiehPunkt;

    public GrundrissPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<GrundrissViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Elemente.CollectionChanged += Elemente_CollectionChanged;
        NeuZeichnen();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GrundrissViewModel.Elemente):
                ViewModel.Elemente.CollectionChanged += Elemente_CollectionChanged;
                NeuZeichnen();
                break;
            case nameof(GrundrissViewModel.AusgewaehltesElement):
                AktualisiereAuswahlDarstellung();
                break;
            case nameof(GrundrissViewModel.Zoom):
                NeuZeichnen();
                break;
        }
    }

    private void Elemente_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => NeuZeichnen();

    private void NeuZeichnen()
    {
        PlanCanvas.Children.Clear();
        _boxenJeElement.Clear();
        _handlesJeElement.Clear();

        ZeichneRaster();
        foreach (var element in ViewModel.Elemente)
        {
            ZeichneElement(element);
        }
    }

    /// <summary>1-Meter-Raster als dünne Linien — Maßangaben ergeben sich direkt aus dem Zoom (Pixel je Meter).</summary>
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
        var ausgewaehlt = ReferenceEquals(element, ViewModel.AusgewaehltesElement);
        var box = new Border
        {
            Width = element.PixelBreite,
            Height = element.PixelHoehe,
            Background = element.IstFeld
                ? new SolidColorBrush(Color.FromArgb(40, 33, 150, 243))
                : new SolidColorBrush(Color.FromArgb(120, 76, 175, 80)),
            BorderBrush = element.IstFeld ? new SolidColorBrush(Colors.DodgerBlue) : new SolidColorBrush(Colors.DarkGreen),
            BorderThickness = new Thickness(ausgewaehlt ? 3 : 1),
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(element.Bezeichnung) ? element.Code : $"{element.Code}\n{element.Bezeichnung}",
                FontSize = 11,
                Margin = new Thickness(4),
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false,
            },
        };

        Canvas.SetLeft(box, element.PixelX);
        Canvas.SetTop(box, element.PixelY);
        Canvas.SetZIndex(box, element.IstFeld ? 0 : 1);

        box.PointerPressed += (_, e) => ElementPointerPressed(element, box, e, groesse: false);
        box.PointerMoved += (_, e) => ElementPointerMoved(element, e);
        box.PointerReleased += (_, e) => ElementPointerReleased(element, box, e);

        PlanCanvas.Children.Add(box);
        _boxenJeElement[element] = box;

        var handle = new Border
        {
            Width = 10,
            Height = 10,
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Colors.Black),
            BorderThickness = new Thickness(1),
            Visibility = ausgewaehlt ? Visibility.Visible : Visibility.Collapsed,
        };
        Canvas.SetLeft(handle, element.PixelX + element.PixelBreite - 5);
        Canvas.SetTop(handle, element.PixelY + element.PixelHoehe - 5);
        Canvas.SetZIndex(handle, 10);
        handle.PointerPressed += (_, e) => ElementPointerPressed(element, handle, e, groesse: true);
        handle.PointerMoved += (_, e) => ElementPointerMoved(element, e);
        handle.PointerReleased += (_, e) => ElementPointerReleased(element, handle, e);
        PlanCanvas.Children.Add(handle);
        _handlesJeElement[element] = handle;

        // Numerische Eingabe im Formular ist gleichwertig zur Maus — beide Wege ändern dieselben
        // PlanElementViewModel-Properties, PropertyChanged hält die Zeichnung synchron.
        element.PropertyChanged += (_, _) => AktualisierePosition(element);
    }

    private void AktualisierePosition(PlanElementViewModel element)
    {
        if (!_boxenJeElement.TryGetValue(element, out var box)) return;
        box.Width = element.PixelBreite;
        box.Height = element.PixelHoehe;
        Canvas.SetLeft(box, element.PixelX);
        Canvas.SetTop(box, element.PixelY);

        if (_handlesJeElement.TryGetValue(element, out var handle))
        {
            Canvas.SetLeft(handle, element.PixelX + element.PixelBreite - 5);
            Canvas.SetTop(handle, element.PixelY + element.PixelHoehe - 5);
        }
    }

    private void AktualisiereAuswahlDarstellung()
    {
        foreach (var (element, box) in _boxenJeElement)
        {
            box.BorderThickness = new Thickness(ReferenceEquals(element, ViewModel.AusgewaehltesElement) ? 3 : 1);
        }

        foreach (var (element, handle) in _handlesJeElement)
        {
            handle.Visibility = ReferenceEquals(element, ViewModel.AusgewaehltesElement) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ElementPointerPressed(PlanElementViewModel element, UIElement ziel, PointerRoutedEventArgs e, bool groesse)
    {
        ViewModel.AusgewaehltesElement = element;
        _ziehElement = element;
        _istGroessenAenderung = groesse;
        _letzterZiehPunkt = e.GetCurrentPoint(PlanCanvas).Position;
        ziel.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ElementPointerMoved(PlanElementViewModel element, PointerRoutedEventArgs e)
    {
        if (!ReferenceEquals(_ziehElement, element)) return;
        var punkt = e.GetCurrentPoint(PlanCanvas);
        if (!punkt.Properties.IsLeftButtonPressed) return;

        var deltaX = punkt.Position.X - _letzterZiehPunkt.X;
        var deltaY = punkt.Position.Y - _letzterZiehPunkt.Y;
        if (Math.Abs(deltaX) < 1 && Math.Abs(deltaY) < 1) return;

        if (_istGroessenAenderung)
        {
            element.GroesseAendernUmPixel(deltaX, deltaY);
        }
        else
        {
            element.VerschiebenUmPixel(deltaX, deltaY);
            if (element.IstFeld)
            {
                // Sektionskoordinaten sind relativ zum Feld — sie "kleben" am Feld, wenn dessen Offset nachgeführt wird.
                ViewModel.FeldVerschoben(element);
            }
        }

        ViewModel.UebernehmeAusElement(element);
        _letzterZiehPunkt = punkt.Position;
        e.Handled = true;
    }

    private void ElementPointerReleased(PlanElementViewModel element, UIElement ziel, PointerRoutedEventArgs e)
    {
        ziel.ReleasePointerCapture(e.Pointer);
        _ziehElement = null;
        e.Handled = true;
    }

    private void PlanCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource == PlanCanvas)
        {
            ViewModel.AusgewaehltesElement = null;
        }
    }
}
