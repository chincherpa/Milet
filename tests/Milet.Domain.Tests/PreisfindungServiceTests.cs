using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class PreisfindungServiceTests
{
    private static readonly Artikel Artikel = new()
    {
        Id = 1,
        Artikelnummer = "ART-00001",
        Listenpreis = 100m,
    };

    private static ArtikelPreis Staffel(int preislisteId, decimal abMenge, decimal preis, int artikelId = 1)
        => new() { PreislisteId = preislisteId, ArtikelId = artikelId, AbMenge = abMenge, Preis = preis };

    [Fact]
    public void OhnePreisliste_LiefertListenpreisMitKundenrabatt()
    {
        var ergebnis = PreisfindungService.ErmittlePreis(Artikel, 5, null, [], 10m);

        Assert.Equal(100m, ergebnis.Einzelpreis);
        Assert.Equal(10m, ergebnis.RabattProzent);
        Assert.Equal(PreisQuelle.Listenpreis, ergebnis.Quelle);
    }

    [Fact]
    public void MitPreisliste_BesteStaffelUnterMenge_Gewinnt()
    {
        var staffeln = new[]
        {
            Staffel(7, 1, 95m),
            Staffel(7, 10, 90m),
            Staffel(7, 100, 80m),
        };

        var ergebnis = PreisfindungService.ErmittlePreis(Artikel, 50, 7, staffeln, 10m);

        Assert.Equal(90m, ergebnis.Einzelpreis);
        Assert.Equal(0m, ergebnis.RabattProzent);
        Assert.Equal(PreisQuelle.Preisliste, ergebnis.Quelle);
    }

    [Fact]
    public void Staffelkante_ExaktErreicht_NimmtDieseStufe()
    {
        var staffeln = new[] { Staffel(7, 1, 95m), Staffel(7, 10, 90m) };

        var ergebnis = PreisfindungService.ErmittlePreis(Artikel, 10, 7, staffeln, 0m);

        Assert.Equal(90m, ergebnis.Einzelpreis);
    }

    [Fact]
    public void MengeUnterKleinsterStaffel_FaelltAufListenpreisZurueck()
    {
        var staffeln = new[] { Staffel(7, 10, 90m) };

        var ergebnis = PreisfindungService.ErmittlePreis(Artikel, 5, 7, staffeln, 5m);

        Assert.Equal(100m, ergebnis.Einzelpreis);
        Assert.Equal(5m, ergebnis.RabattProzent);
        Assert.Equal(PreisQuelle.Listenpreis, ergebnis.Quelle);
    }

    [Fact]
    public void FremdePreislisteOderFremderArtikel_WirdIgnoriert()
    {
        var staffeln = new[]
        {
            Staffel(99, 1, 1m),
            Staffel(7, 1, 50m, artikelId: 2),
        };

        var ergebnis = PreisfindungService.ErmittlePreis(Artikel, 5, 7, staffeln, 0m);

        Assert.Equal(100m, ergebnis.Einzelpreis);
        Assert.Equal(PreisQuelle.Listenpreis, ergebnis.Quelle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UngueltigeMenge_Wirft(decimal menge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreisfindungService.ErmittlePreis(Artikel, menge, null, [], 0m));
    }
}
