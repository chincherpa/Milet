using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Gaertnerei;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class TeillieferungZeile : ObservableObject
{
    public int PositionId { get; }
    public string Bezeichnung { get; }
    public decimal OffeneMenge { get; }
    public int? ArtikelId { get; }

    /// <summary>Nur bei Kulturpflanzen befüllt — ausschließlich verkaufsfähige Fundstellen (nicht-verkaufsfähige
    /// Stufen würden beim Buchen ohnehin durch LieferscheinBuchenService abgelehnt, E8).</summary>
    public IReadOnlyList<PflanzenVorkommenDto> FundstellenOptionen { get; }
    public bool IstKulturartikel => FundstellenOptionen.Count > 0;

    [ObservableProperty]
    public partial decimal GewaehlteMenge { get; set; }

    [ObservableProperty]
    public partial PflanzenVorkommenDto? AusgewaehlteFundstelle { get; set; }

    public TeillieferungZeile(OffenePositionDto dto, IReadOnlyList<PflanzenVorkommenDto> fundstellenOptionen)
    {
        PositionId = dto.PositionId;
        Bezeichnung = dto.EinheitKuerzel is { } einheit ? $"{dto.Bezeichnung} ({einheit})" : dto.Bezeichnung;
        OffeneMenge = dto.OffeneMenge;
        GewaehlteMenge = dto.OffeneMenge;
        ArtikelId = dto.ArtikelId;
        FundstellenOptionen = fundstellenOptionen;

        // E9: verkaufsfähige Stufe mit der größten verfügbaren Menge, darin die Sektion mit der größten Menge.
        // Der Nutzer kann umstellen; automatisches Splitten über mehrere Sektionen ist nicht Teil von v1.
        AusgewaehlteFundstelle = fundstellenOptionen.OrderByDescending(f => f.Menge).FirstOrDefault();
    }
}

public sealed partial class TeillieferungDialog : ContentDialog
{
    public ObservableCollection<TeillieferungZeile> Zeilen { get; }
    public IReadOnlyList<LagerortDto> Lagerorte { get; }
    public int AusgewaehlterLagerortId { get; set; }

    public TeillieferungDialog(
        IReadOnlyList<OffenePositionDto> offenePositionen,
        IReadOnlyList<LagerortDto> lagerorte,
        IReadOnlyDictionary<int, IReadOnlyList<PflanzenVorkommenDto>>? fundstellenJeArtikel = null)
    {
        Zeilen = new ObservableCollection<TeillieferungZeile>(offenePositionen.Select(p => new TeillieferungZeile(
            p,
            p.ArtikelId is { } artikelId && fundstellenJeArtikel is not null && fundstellenJeArtikel.TryGetValue(artikelId, out var f) ? f : [])));
        Lagerorte = lagerorte;
        AusgewaehlterLagerortId = lagerorte[0].Id;
        InitializeComponent();
    }

    public IReadOnlyDictionary<int, decimal> GewaehlteMengen() =>
        Zeilen.Where(z => z.GewaehlteMenge > 0).ToDictionary(z => z.PositionId, z => z.GewaehlteMenge);

    /// <summary>Nur für Kulturpflanzen-Positionen mit gewählter Fundstelle — Nicht-Kulturartikel bleiben
    /// ohne Eintrag (Task 11: fehlender Eintrag ⇒ beide Dimensionen NULL, bestehendes Verhalten unverändert).</summary>
    public IReadOnlyDictionary<int, BelegPositionDimensionenDto>? DimensionenJePosition()
    {
        var ergebnis = Zeilen
            .Where(z => z.IstKulturartikel && z.AusgewaehlteFundstelle is not null)
            .ToDictionary(z => z.PositionId, z => new BelegPositionDimensionenDto(z.AusgewaehlteFundstelle!.SektionId, z.AusgewaehlteFundstelle!.KulturstufeId));
        return ergebnis.Count > 0 ? ergebnis : null;
    }
}
