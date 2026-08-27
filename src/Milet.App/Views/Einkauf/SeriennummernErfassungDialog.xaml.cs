using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class SeriennummerErfassungZeile : ObservableObject
{
    [ObservableProperty]
    public partial string Nummer { get; set; } = string.Empty;
}

public sealed partial class SeriennummernErfassungDialog : ContentDialog
{
    public string PositionsBezeichnung { get; }
    public string BenoetigteMengeText { get; }
    public ObservableCollection<SeriennummerErfassungZeile> Zeilen { get; }

    public SeriennummernErfassungDialog(BelegPositionDto position)
    {
        // Gleiche Reihenfolge-Regel wie SeriennummernAuswahlDialog (Phase 3): Properties VOR
        // InitializeComponent() setzen, da x:Bind ohne Mode synchron innerhalb InitializeComponent() ausgewertet wird.
        PositionsBezeichnung = position.Bezeichnung;
        BenoetigteMengeText = $"Benötigt: {position.Menge} Stück";
        Zeilen = new ObservableCollection<SeriennummerErfassungZeile>(
            Enumerable.Range(0, (int)position.Menge).Select(_ => new SeriennummerErfassungZeile()));
        InitializeComponent();
    }

    /// <summary>Keine clientseitige Duplikat-/Leerprüfung — WareneingangBuchenService prüft serverseitig
    /// (exakte Anzahl, Duplikate im Artikelbestand) und liefert bei Verstoß eine verständliche Fehlermeldung.</summary>
    public IReadOnlyList<string> ErfassteNummern() => Zeilen.Select(z => z.Nummer.Trim()).Where(n => n.Length > 0).ToList();
}
