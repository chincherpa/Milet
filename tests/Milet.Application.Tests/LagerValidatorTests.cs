using Milet.Application.Lager;
using Xunit;

namespace Milet.Application.Tests;

public class LagerValidatorTests
{
    [Fact]
    public void Lagerort_OhneCode_Fehler()
    {
        var dto = new LagerortDto { Code = "", Bezeichnung = "Hauptlager" };
        Assert.False(new LagerortValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Lagerort_GueltigeDaten_KeinFehler()
    {
        var dto = new LagerortDto { Code = "HL", Bezeichnung = "Hauptlager" };
        Assert.True(new LagerortValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_MengeDeltaNull_Fehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 0, Grund = "Inventur" };
        Assert.False(new BestandskorrekturValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_OhneGrund_Fehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 5, Grund = "" };
        Assert.False(new BestandskorrekturValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_GueltigeDaten_KeinFehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 10, Grund = "Erstbestückung" };
        Assert.True(new BestandskorrekturValidator().Validate(dto).IsValid);
    }
}
