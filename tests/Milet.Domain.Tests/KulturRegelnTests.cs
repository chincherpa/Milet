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
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 2, 10, 10, 5m));
        Assert.Null(ex);
    }

    [Fact]
    public void PruefeStufenwechsel_GleichesFeldGleicheStufeGleicheSektion_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 1, 10, 10, 5m));
    }

    [Fact]
    public void PruefeStufenwechsel_MengeNichtPositiv_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 2, 10, 20, 0m));
    }

    [Fact]
    public void PruefeStufenwechsel_GleicheStufeAndereSektion_WirftNicht()
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 1, 10, 20, 5m));
        Assert.Null(ex);
    }

    /// <summary>Der Fall, für den das Feld überhaupt in die Prüfung kam: Umsetzen zwischen zwei Feldern OHNE
    /// Sektionen. Beide SektionIds sind null, die Stufe bleibt gleich (Umsetzen ist ein reiner Ortswechsel) —
    /// vorher wurde das als "Quelle und Ziel sind identisch" abgewiesen, obwohl es zwei verschiedene Felder sind.</summary>
    [Fact]
    public void PruefeStufenwechsel_AnderesFeldOhneSektionen_WirftNicht()
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(7, 8, 1, 1, null, null, 5m));
        Assert.Null(ex);
    }

    [Fact]
    public void PruefeStufenwechsel_GleichesFeldOhneSektionenGleicheStufe_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 1, null, null, 5m));
    }

    /// <summary>Stufenwechsel am selben Ort ist eine echte Bewegung — nur Feld UND Stufe UND Sektion
    /// gemeinsam identisch ist eine Nulloperation.</summary>
    [Fact]
    public void PruefeStufenwechsel_GleichesFeldGleicheSektionAndereStufe_WirftNicht()
    {
        var ex = Record.Exception(() => KulturRegeln.PruefeStufenwechsel(7, 7, 1, 2, 10, 10, 5m));
        Assert.Null(ex);
    }

    // ---- Feld im Gärtnereiplan (dieselbe Regel eine Ebene höher) ----

    [Fact]
    public void LiegtInnerhalb_FeldVollstaendigImPlan_IstWahr()
    {
        Assert.True(KulturRegeln.LiegtInnerhalb(FeldMitPos(10, 5, 20, 10), Plan(100, 50)));
    }

    [Fact]
    public void LiegtInnerhalb_FeldRagtRechtsHeraus_IstFalsch()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(FeldMitPos(90, 5, 20, 10), Plan(100, 50)));
    }

    [Fact]
    public void LiegtInnerhalb_FeldRagtUntenHeraus_IstFalsch()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(FeldMitPos(10, 45, 20, 10), Plan(100, 50)));
    }

    [Fact]
    public void LiegtInnerhalb_FeldMitNegativerPosition_IstFalsch()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(FeldMitPos(-1, 5, 20, 10), Plan(100, 50)));
    }

    /// <summary>Bündig am Rand ist noch drin — sonst wäre ein Feld, das den Plan exakt ausfüllt, ungültig.</summary>
    [Fact]
    public void LiegtInnerhalb_FeldBuendigAmRand_IstWahr()
    {
        Assert.True(KulturRegeln.LiegtInnerhalb(FeldMitPos(80, 40, 20, 10), Plan(100, 50)));
    }

    [Fact]
    public void LiegtInnerhalb_FeldOhneGeometrie_IstFalsch()
    {
        Assert.False(KulturRegeln.LiegtInnerhalb(new Lagerort { IstFeld = true }, Plan(100, 50)));
    }

    private static Gaertnereiplan Plan(decimal breite, decimal hoehe) => new()
    {
        Bezeichnung = "Testplan",
        BreiteMeter = breite,
        HoeheMeter = hoehe,
    };

    private static Lagerort FeldMitPos(decimal x, decimal y, decimal breite, decimal hoehe) => new()
    {
        IstFeld = true,
        PosXMeter = x,
        PosYMeter = y,
        BreiteMeter = breite,
        HoeheMeter = hoehe,
    };

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
