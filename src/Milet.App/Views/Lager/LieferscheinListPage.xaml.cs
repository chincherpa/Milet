using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class LieferscheinListPage : Page
{
    public LieferscheinListViewModel ViewModel { get; }
    public LieferscheinListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferscheinListViewModel>();
        InitializeComponent();
    }

    // SelectionMode="Multiple" macht ListView.SelectedItem mehrdeutig (liefert nur "irgendein" ausgewähltes Element).
    // Ein zusätzliches x:Bind TwoWay auf SelectedItem konkurrierte damit um ViewModel.Ausgewaehlt und konnte
    // die hier gesetzte Einzelauswahl sofort wieder überschreiben — deshalb wird Ausgewaehlt jetzt ausschließlich
    // hier aus SelectedItems abgeleitet: nur bei genau einem markierten Lieferschein gesetzt (für
    // Bearbeiten/Löschen), sonst null, damit diese Einzel-Aktionen bei einer Mehrfachauswahl für die
    // Sammelrechnung nicht versehentlich auf dem falschen Beleg ausgeführt werden.
    private void LieferscheineListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ausgewaehlt = ((ListView)sender).SelectedItems.Cast<BelegDto>().ToList();
        ViewModel.AusgewaehlteIds = ausgewaehlt.Select(b => b.Id).ToList();
        ViewModel.Ausgewaehlt = ausgewaehlt.Count == 1 ? ausgewaehlt[0] : null;
    }
}
