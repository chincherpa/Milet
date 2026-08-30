using Milet.Application.Gaertnerei;
using Xunit;

namespace Milet.Application.Tests;

public class GaertnereiValidatorTests
{
    [Fact]
    public void Kulturstufe_GueltigeDaten_KeinFehler()
    {
        var dto = new KulturstufeDto { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#4CAF50" };
        Assert.True(new KulturstufeValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Kulturstufe_ReihenfolgeNull_Fehler()
    {
        var dto = new KulturstufeDto { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 0, FarbeHex = "#4CAF50" };
        Assert.False(new KulturstufeValidator().Validate(dto).IsValid);
    }

    [Theory]
    [InlineData("#4CAF50", true)]
    [InlineData("4CAF50", false)]
    [InlineData("#4CAF5", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("", false)]
    public void Kulturstufe_FarbeHex_RegexPrueft(string farbe, bool erwartetGueltig)
    {
        var dto = new KulturstufeDto { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = farbe };
        Assert.Equal(erwartetGueltig, new KulturstufeValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Gaertnereiplan_MasseNichtPositiv_Fehler()
    {
        var dto = new GaertnereiplanDto { Bezeichnung = "Gärtnerei", BreiteMeter = 0, HoeheMeter = 60 };
        Assert.False(new GaertnereiplanValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Gaertnereiplan_GueltigeDaten_KeinFehler()
    {
        var dto = new GaertnereiplanDto { Bezeichnung = "Gärtnerei", BreiteMeter = 100, HoeheMeter = 60 };
        Assert.True(new GaertnereiplanValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Sektion_OhneCode_Fehler()
    {
        var dto = new SektionDto { LagerortId = 1, Code = "", Bezeichnung = "A", BreiteMeter = 5, HoeheMeter = 5 };
        Assert.False(new SektionValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Sektion_GueltigeDaten_KeinFehler()
    {
        var dto = new SektionDto { LagerortId = 1, Code = "A1", Bezeichnung = "Sektion A1", BreiteMeter = 5, HoeheMeter = 5 };
        Assert.True(new SektionValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Feld_MasseNichtPositiv_Fehler()
    {
        var dto = new FeldDto { Code = "F1", Bezeichnung = "Feld Nord", BreiteMeter = 30, HoeheMeter = 0 };
        Assert.False(new FeldValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void KulturZugang_MengeNichtPositiv_Fehler()
    {
        var dto = new KulturZugangDto { ArtikelId = 1, FeldId = 1, KulturstufeId = 1, Menge = 0 };
        Assert.False(new KulturZugangValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void KulturZugang_GueltigeDaten_KeinFehler()
    {
        var dto = new KulturZugangDto { ArtikelId = 1, FeldId = 1, KulturstufeId = 1, Menge = 10 };
        Assert.True(new KulturZugangValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Stufenwechsel_FehlendeZielstufe_Fehler()
    {
        var dto = new StufenwechselDto { ArtikelId = 1, VonFeldId = 1, VonKulturstufeId = 1, NachFeldId = 1, NachKulturstufeId = 0, Menge = 5 };
        Assert.False(new StufenwechselValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Umsetzen_GueltigeDaten_KeinFehler()
    {
        var dto = new UmsetzenDto { ArtikelId = 1, VonFeldId = 1, NachFeldId = 2, KulturstufeId = 1, Menge = 5 };
        Assert.True(new UmsetzenValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Ausfall_MengeNichtPositiv_Fehler()
    {
        var dto = new AusfallDto { ArtikelId = 1, FeldId = 1, KulturstufeId = 1, Menge = -5 };
        Assert.False(new AusfallValidator().Validate(dto).IsValid);
    }
}
