using Nexus.Application.Stammdaten;
using Xunit;

namespace Nexus.Application.Tests;

public class StammdatenValidatorTests
{
    private static readonly AdresseDto GueltigeAdresse = new() { Name1 = "Mustermann GmbH", Land = "DE" };

    [Fact]
    public void Kunde_GueltigeDaten_KeineFehler()
    {
        var dto = new KundeDto { Adresse = GueltigeAdresse, RabattProzent = 5 };

        var ergebnis = new KundeValidator().Validate(dto);

        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Kunde_LeererName1_Fehler()
    {
        var dto = new KundeDto { Adresse = GueltigeAdresse with { Name1 = "" } };

        var ergebnis = new KundeValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Kunde_UngueltigeEmail_Fehler()
    {
        var dto = new KundeDto { Adresse = GueltigeAdresse, Email = "keine-email" };

        var ergebnis = new KundeValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Kunde_RabattAusserhalbBereich_Fehler(decimal rabatt)
    {
        var dto = new KundeDto { Adresse = GueltigeAdresse, RabattProzent = rabatt };

        var ergebnis = new KundeValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Artikel_OhneBezeichnung_Fehler()
    {
        var dto = new ArtikelDto { Bezeichnung = "", EinheitId = 1, MwStSatzId = 1 };

        var ergebnis = new ArtikelValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Artikel_OhneEinheit_Fehler()
    {
        var dto = new ArtikelDto { Bezeichnung = "Testartikel", EinheitId = 0, MwStSatzId = 1 };

        var ergebnis = new ArtikelValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Artikel_GueltigeDaten_KeineFehler()
    {
        var dto = new ArtikelDto { Bezeichnung = "Testartikel", EinheitId = 1, MwStSatzId = 1, Listenpreis = 9.99m };

        var ergebnis = new ArtikelValidator().Validate(dto);

        Assert.True(ergebnis.IsValid);
    }
}
