using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;
using Xunit;

namespace Milet.Application.Tests;

public class AdminValidatorTests
{
    private static FibuKonfigurationDto Gueltig() => new()
    {
        Kontenrahmen = Kontenrahmen.Skr03,
        BeraterNr = 1001,
        MandantNr = 42,
        WirtschaftsjahrBeginnMonat = 1,
        SachkontenLaenge = 4,
        BankkontoNr = 1200,
    };

    [Fact]
    public void FibuKonfiguration_GueltigeDaten_KeineFehler()
    {
        var ergebnis = new FibuKonfigurationValidator().Validate(Gueltig());

        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void FibuKonfiguration_BeraterNrFehlt_Fehler()
    {
        var dto = Gueltig() with { BeraterNr = 0 };

        var ergebnis = new FibuKonfigurationValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void FibuKonfiguration_BankkontoFehlt_Fehler()
    {
        var dto = Gueltig() with { BankkontoNr = 0 };

        var ergebnis = new FibuKonfigurationValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FibuKonfiguration_WjBeginnAusserhalbBereich_Fehler(int monat)
    {
        var dto = Gueltig() with { WirtschaftsjahrBeginnMonat = monat };

        var ergebnis = new FibuKonfigurationValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }
}
