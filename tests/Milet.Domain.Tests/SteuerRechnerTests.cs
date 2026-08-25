using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class SteuerRechnerTests
{
    [Fact]
    public void BerechnePosition_MengeEinzelpreisRabatt_RundetAufZweiStellen()
    {
        var netto = SteuerRechner.BerechnePosition(menge: 3, einzelpreis: 19.995m, rabattProzent: 10);
        // 3 * 19.995 = 59.985; abzgl. 10% Rabatt = 53.9865 -> ToEven auf 53.99
        Assert.Equal(53.99m, netto);
    }

    [Fact]
    public void BerechneSteuersummen_GruppiertNachSatzUndRundetAmEnde()
    {
        var positionen = new List<BelegPosition>
        {
            new() { MwStSatzWert = 19m, GesamtNetto = 10.005m },
            new() { MwStSatzWert = 19m, GesamtNetto = 10.005m },
            new() { MwStSatzWert = 7m, GesamtNetto = 5.00m },
        };

        var summen = SteuerRechner.BerechneSteuersummen(positionen);

        var satz19 = Assert.Single(summen, s => s.MwStSatzWert == 19m);
        Assert.Equal(20.01m, satz19.NettoSumme);
        Assert.Equal(Math.Round(20.01m * 0.19m, 2, MidpointRounding.ToEven), satz19.MwStBetrag);

        var satz7 = Assert.Single(summen, s => s.MwStSatzWert == 7m);
        Assert.Equal(5.00m, satz7.NettoSumme);
        Assert.Equal(0.35m, satz7.MwStBetrag);
    }

    [Fact]
    public void BerechneKopfsummen_SummiertAlleSteuergruppen()
    {
        var steuersummen = new List<BelegSteuerSumme>
        {
            new() { NettoSumme = 20.01m, MwStBetrag = 3.80m },
            new() { NettoSumme = 5.00m, MwStBetrag = 0.35m },
        };

        var (netto, mwst, brutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        Assert.Equal(25.01m, netto);
        Assert.Equal(4.15m, mwst);
        Assert.Equal(29.16m, brutto);
    }
}
