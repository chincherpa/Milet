using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class SeriennummerAuswahlZeile : ObservableObject
{
    public int Id { get; }
    public string Nummer { get; }

    [ObservableProperty]
    public partial bool Ausgewaehlt { get; set; }

    public SeriennummerAuswahlZeile(SeriennummerDto dto)
    {
        Id = dto.Id;
        Nummer = dto.Nummer;
    }
}

public sealed partial class SeriennummernAuswahlDialog : ContentDialog
{
    public string PositionsBezeichnung { get; }
    public string BenoetigteMengeText { get; }
    public ObservableCollection<SeriennummerAuswahlZeile> Zeilen { get; }

    public SeriennummernAuswahlDialog(BelegPositionDto position, IReadOnlyList<SeriennummerDto> verfuegbar)
    {
        // Abweichung vom Brief (siehe Task-17-Auftrag): InitializeComponent() steht hier bewusst NACH der Zuweisung
        // der x:Bind-gebundenen Properties, nicht davor. Grund: x:Bind ohne explizites Mode ist OneTime und wird
        // synchron INNERHALB von InitializeComponent() ausgewertet — würde InitializeComponent() zuerst laufen,
        // wären PositionsBezeichnung/BenoetigteMengeText/Zeilen zu diesem Zeitpunkt noch nicht gesetzt und der
        // Dialog würde leer angezeigt. Exakt derselbe Fehler wurde in Task 16 (TeillieferungDialog.xaml.cs)
        // gefunden und dort durch dieselbe Umstellung behoben.
        PositionsBezeichnung = position.Bezeichnung;
        BenoetigteMengeText = $"Benötigt: {position.Menge} Stück";
        Zeilen = new ObservableCollection<SeriennummerAuswahlZeile>(verfuegbar.Select(s => new SeriennummerAuswahlZeile(s)));
        InitializeComponent();
    }

    /// <summary>Keine clientseitige Mengen-Validierung — der Server (LieferscheinBuchenService) prüft die exakte Anzahl und liefert bei Abweichung eine verständliche Fehlermeldung.</summary>
    public IReadOnlyList<int> Ausgewaehlt() => Zeilen.Where(z => z.Ausgewaehlt).Select(z => z.Id).ToList();
}
