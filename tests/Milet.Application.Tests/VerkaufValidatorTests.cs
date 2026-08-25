using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Xunit;

namespace Milet.Application.Tests;

public class VerkaufValidatorTests
{
    private static BelegPositionDto GueltigePosition() => new()
    {
        PositionsNr = 1,
        ArtikelId = 1,
        Bezeichnung = "Testartikel",
        Menge = 2,
        Einzelpreis = 10m,
        MwStSatzWert = 19m,
        GesamtNetto = 20m,
    };

    [Fact]
    public void Beleg_OhneKunde_Fehler()
    {
        var dto = new BelegDto { KundeId = 0, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Beleg_OhnePositionen_Fehler()
    {
        var dto = new BelegDto { KundeId = 1, Positionen = [] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Beleg_GueltigeDaten_KeinFehler()
    {
        var dto = new BelegDto { KundeId = 1, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Position_NegativeMenge_Fehler()
    {
        var dto = GueltigePosition() with { Menge = -1 };
        var ergebnis = new BelegPositionValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Position_ArtikeltypOhneArtikelId_Fehler()
    {
        var dto = GueltigePosition() with { ArtikelId = null, PositionsTyp = PositionsTyp.Artikel };
        var ergebnis = new BelegPositionValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }
}
