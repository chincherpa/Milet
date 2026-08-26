using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class TeillieferungZeile : ObservableObject
{
    public int PositionId { get; }
    public string Bezeichnung { get; }
    public decimal OffeneMenge { get; }

    [ObservableProperty]
    public partial decimal GewaehlteMenge { get; set; }

    public TeillieferungZeile(OffenePositionDto dto)
    {
        PositionId = dto.PositionId;
        Bezeichnung = dto.EinheitKuerzel is { } einheit ? $"{dto.Bezeichnung} ({einheit})" : dto.Bezeichnung;
        OffeneMenge = dto.OffeneMenge;
        GewaehlteMenge = dto.OffeneMenge;
    }
}

public sealed partial class TeillieferungDialog : ContentDialog
{
    public ObservableCollection<TeillieferungZeile> Zeilen { get; }
    public IReadOnlyList<LagerortDto> Lagerorte { get; }
    public int AusgewaehlterLagerortId { get; set; }

    public TeillieferungDialog(IReadOnlyList<OffenePositionDto> offenePositionen, IReadOnlyList<LagerortDto> lagerorte)
    {
        Zeilen = new ObservableCollection<TeillieferungZeile>(offenePositionen.Select(p => new TeillieferungZeile(p)));
        Lagerorte = lagerorte;
        AusgewaehlterLagerortId = lagerorte[0].Id;
        InitializeComponent();
    }

    public IReadOnlyDictionary<int, decimal> GewaehlteMengen() =>
        Zeilen.Where(z => z.GewaehlteMenge > 0).ToDictionary(z => z.PositionId, z => z.GewaehlteMenge);
}
