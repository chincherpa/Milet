using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class SkontoAufteilungTests
{
    [Fact]
    public void EineSteuergruppe_LiefertDenVollenBetragMitDerenSchluessel()
    {
        var anteile = SkontoAufteilung.AufSteuergruppen(2.00m, [new SkontoAufteilung.Gruppe(3, 119.00m)]);

        var anteil = Assert.Single(anteile);
        Assert.Equal(3, anteil.SteuerSchluessel);
        Assert.Equal(2.00m, anteil.Betrag);
    }

    [Fact]
    public void ZweiSteuergruppen_VerteiltProportionalZumBruttoanteil()
    {
        var anteile = SkontoAufteilung.AufSteuergruppen(
            10.00m, [new SkontoAufteilung.Gruppe(3, 119.00m), new SkontoAufteilung.Gruppe(2, 107.00m)]);

        Assert.Equal(2, anteile.Count);
        Assert.Equal(5.27m, anteile.First(a => a.SteuerSchluessel == 3).Betrag);
        Assert.Equal(4.73m, anteile.First(a => a.SteuerSchluessel == 2).Betrag);
    }

    [Fact]
    public void Rundungsdifferenz_LandetAufDerGroesstenGruppeUndDieSummeStimmt()
    {
        // Drei gleich große Gruppen: 10,00 € / 3 = 3,33 € je Zeile, in Summe 9,99 €. Die fehlenden 0,01 €
        // müssen auf einer Zeile landen, sonst passt die Gegenbuchung nicht zum ausgeglichenen OP.
        var anteile = SkontoAufteilung.AufSteuergruppen(
            10.00m,
            [
                new SkontoAufteilung.Gruppe(3, 100.00m),
                new SkontoAufteilung.Gruppe(2, 100.00m),
                new SkontoAufteilung.Gruppe(0, 100.00m),
            ]);

        Assert.Equal(10.00m, anteile.Sum(a => a.Betrag));
    }

    [Fact]
    public void OhneSteuersummen_LiefertEineZeileOhneSteuerschluessel()
    {
        var anteile = SkontoAufteilung.AufSteuergruppen(2.00m, []);

        var anteil = Assert.Single(anteile);
        Assert.Null(anteil.SteuerSchluessel);
        Assert.Equal(2.00m, anteil.Betrag);
    }

    [Fact]
    public void GruppenOhneBrutto_WerdenIgnoriert()
    {
        var anteile = SkontoAufteilung.AufSteuergruppen(
            2.00m, [new SkontoAufteilung.Gruppe(3, 119.00m), new SkontoAufteilung.Gruppe(0, 0m)]);

        var anteil = Assert.Single(anteile);
        Assert.Equal(3, anteil.SteuerSchluessel);
        Assert.Equal(2.00m, anteil.Betrag);
    }

    [Fact]
    public void KeinSkonto_LiefertKeineZeile()
    {
        Assert.Empty(SkontoAufteilung.AufSteuergruppen(0m, [new SkontoAufteilung.Gruppe(3, 119.00m)]));
    }
}
