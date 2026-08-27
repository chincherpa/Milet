using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Finanzen;

namespace Milet.App.Views.Finanzen;

public sealed partial class ZahlungZeile : ObservableObject
{
    public int OffenerPostenId { get; }
    public string BelegNummer { get; }
    public decimal OffenerBetrag { get; }
    public byte[] RowVersion { get; }

    [ObservableProperty] public partial decimal Betrag { get; set; }
    [ObservableProperty] public partial decimal SkontoBetrag { get; set; }

    public ZahlungZeile(OffenePostenDto op, decimal skontoVorschlag)
    {
        OffenerPostenId = op.Id;
        BelegNummer = op.BelegNummer;
        OffenerBetrag = op.OffenerBetrag;
        RowVersion = op.RowVersion;
        SkontoBetrag = skontoVorschlag;
        Betrag = op.OffenerBetrag - skontoVorschlag;
    }
}

public sealed partial class ZahlungDialog : ContentDialog
{
    public ObservableCollection<ZahlungZeile> Zeilen { get; }
    public DateTimeOffset Zahlungsdatum { get; set; } = DateTimeOffset.Now;
    public string? Zahlungsart { get; set; }
    public string? Referenz { get; set; }

    public ZahlungDialog(IReadOnlyList<OffenePostenDto> posten, IReadOnlyDictionary<int, decimal> skontoVorschlaege)
    {
        Zeilen = new ObservableCollection<ZahlungZeile>(
            posten.Select(op => new ZahlungZeile(op, skontoVorschlaege.GetValueOrDefault(op.Id, 0m))));
        InitializeComponent();
    }

    public IReadOnlyList<ZahlungZuordnungDto> Zuordnungen() => Zeilen
        .Where(z => z.Betrag + z.SkontoBetrag > 0)
        .Select(z => new ZahlungZuordnungDto(z.OffenerPostenId, z.Betrag, z.SkontoBetrag, z.RowVersion))
        .ToList();
}
