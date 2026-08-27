using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class WareneingangMengenZeile : ObservableObject
{
    public int PositionId { get; }
    public string Bezeichnung { get; }
    public decimal OffeneMenge { get; }

    [ObservableProperty]
    public partial decimal GewaehlteMenge { get; set; }

    public WareneingangMengenZeile(OffenePositionDto dto)
    {
        PositionId = dto.PositionId;
        Bezeichnung = dto.EinheitKuerzel is { } einheit ? $"{dto.Bezeichnung} ({einheit})" : dto.Bezeichnung;
        OffeneMenge = dto.OffeneMenge;
        GewaehlteMenge = dto.OffeneMenge;
    }
}

public sealed partial class WareneingangMengenDialog : ContentDialog
{
    public ObservableCollection<WareneingangMengenZeile> Zeilen { get; }
    public IReadOnlyList<LagerortDto> Lagerorte { get; }
    public int AusgewaehlterLagerortId { get; set; }

    public WareneingangMengenDialog(IReadOnlyList<OffenePositionDto> offenePositionen, IReadOnlyList<LagerortDto> lagerorte)
    {
        Zeilen = new ObservableCollection<WareneingangMengenZeile>(offenePositionen.Select(p => new WareneingangMengenZeile(p)));
        Lagerorte = lagerorte;
        AusgewaehlterLagerortId = lagerorte[0].Id;
        InitializeComponent();
    }

    public IReadOnlyDictionary<int, decimal> GewaehlteMengen() =>
        Zeilen.Where(z => z.GewaehlteMenge > 0).ToDictionary(z => z.PositionId, z => z.GewaehlteMenge);
}
