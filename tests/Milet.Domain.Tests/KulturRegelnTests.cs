using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class KulturRegelnTests
{
    [Theory]
    [InlineData(false, false, null, null)] // Handelsware, Lagerort ohne Sektionen
    [InlineData(false, true, 1, null)] // Handelsware, Lagerort mit Sektionen
    [InlineData(true, false, null, 1)] // Kulturpflanze, Lagerort ohne Sektionen
    [InlineData(true, true, 1, 1)] // Kulturpflanze, Lagerort mit Sektionen
    public void PruefeDimensionen_GueltigeKombination_WirftNicht(bool istKulturpflanze, bool hatSektionen, int? sektionId, int? kulturstufeId)
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeDimensionen(istKulturpflanze, hatSektionen, sektionId, kulturstufeId));
        Assert.Null(ex);
    }

    [Fact]
    public void PruefeDimensionen_KulturpflanzeOhneStufe_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeDimensionen(true, false, null, null));
    }

    [Fact]
    public void PruefeDimensionen_HandelswareMitStufe_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeDimensionen(false, false, null, 1));
    }

    [Fact]
    public void PruefeDimensionen_LagerortMitSektionenOhneSektionId_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeDimensionen(false, true, null, null));
    }

    [Fact]
    public void PruefeDimensionen_LagerortOhneSektionenMitSektionId_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeDimensionen(false, false, 1, null));
    }

    private static Kulturstufe Stufe(int id, int reihenfolge, bool aktiv = true) => new()
    {
        Id = id,
        Code = $"S{id}",
        Bezeichnung = $"Stufe {id}",
        Reihenfolge = reihenfolge,
        Aktiv = aktiv,
    };

    [Fact]
    public void NaechsteStufe_LiefertNaechsthoehereNachReihenfolge()
    {
        var stufen = new[] { Stufe(1, 1), Stufe(2, 2), Stufe(3, 3) };

        var naechste = KulturRegeln.NaechsteStufe(stufen, 1);

        Assert.NotNull(naechste);
        Assert.Equal(2, naechste!.Id);
    }

    [Fact]
    public void NaechsteStufe_HoechsteStufeErreicht_LiefertNull()
    {
        var stufen = new[] { Stufe(1, 1), Stufe(2, 2) };

        var naechste = KulturRegeln.NaechsteStufe(stufen, 2);

        Assert.Null(naechste);
    }

    [Fact]
    public void NaechsteStufe_UeberspringtInaktiveStufen()
    {
        var stufen = new[] { Stufe(1, 1), Stufe(2, 2, aktiv: false), Stufe(3, 3) };

        var naechste = KulturRegeln.NaechsteStufe(stufen, 1);

        Assert.NotNull(naechste);
        Assert.Equal(3, naechste!.Id);
    }

    [Fact]
    public void PruefeStufenwechsel_GueltigeBewegung_WirftNicht()
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(1, 2, 10, 10, 5m));
        Assert.Null(ex);
    }

    [Fact]
    public void PruefeStufenwechsel_GleicheStufeUndSektion_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeStufenwechsel(1, 1, 10, 10, 5m));
    }

    [Fact]
    public void PruefeStufenwechsel_MengeNichtPositiv_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeStufenwechsel(1, 2, 10, 20, 0m));
    }

    [Fact]
    public void PruefeStufenwechsel_GleicheStufeAndereSektion_WirftNicht()
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(1, 1, 10, 20, 5m));
        Assert.Null(ex);
    }

    private static Sektion Sek(decimal x, decimal y, decimal b, decimal h) => new()
    {
        PosXMeter = x,
        PosYMeter = y,
        BreiteMeter = b,
        HoeheMeter = h,
    };

    private static Lagerort Feld(decimal breite, decimal hoehe) => new()
    {
        IstFeld = true,
        BreiteMeter = breite,
        HoeheMeter = hoehe,
    };

    [Fact]
    public void LiegtInnerhalb_SektionPasstInFeld_True()
    {
        Assert.True(KulturRegeln.LiegtInnerhalb(Sek(0, 0, 5, 5), Feld(10, 10)));
    }

    [Fact]
    public void LiegtInnerhalb_SektionRagtUeberFeldrand_False()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(Sek(8, 0, 5, 5), Feld(10, 10)));
    }

    [Fact]
    public void LiegtInnerhalb_NegativePosition_False()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(Sek(-1, 0, 5, 5), Feld(10, 10)));
    }

    [Fact]
    public void LiegtInnerhalb_FeldOhneGeometrie_False()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(Sek(0, 0, 5, 5), new Lagerort()));
    }

    [Fact]
    public void Ueberlappt_ZweiSichSchneidendeRechtecke_True()
    {
        Assert.True(KulturRegeln.Ueberlappt(Sek(0, 0, 5, 5), Sek(3, 3, 5, 5)));
    }

    [Fact]
    public void Ueberlappt_ZweiGetrennteRechtecke_False()
    {
        Assert.False(KulturRegeln.Ueberlappt(Sek(0, 0, 5, 5), Sek(10, 10, 5, 5)));
    }

    [Fact]
    public void Ueberlappt_AneinanderGrenzendeRechtecke_False()
    {
        Assert.False(KulturRegeln.Ueberlappt(Sek(0, 0, 5, 5), Sek(5, 0, 5, 5)));
    }
}
