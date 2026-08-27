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

    private static BenutzerDto GueltigerNeuerBenutzer() => new()
    {
        Benutzername = "mmuster",
        Anzeigename = "Max Mustermann",
        RolleId = 1,
        NeuesPasswort = "sicheres-passwort",
    };

    [Fact]
    public void Benutzer_GueltigeNeuanlage_KeineFehler()
    {
        var ergebnis = new BenutzerValidator().Validate(GueltigerNeuerBenutzer());

        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Benutzer_NeuanlageOhnePasswort_Fehler()
    {
        var dto = GueltigerNeuerBenutzer() with { NeuesPasswort = null };

        var ergebnis = new BenutzerValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Benutzer_NeuanlageMitZuKurzemPasswort_Fehler()
    {
        var dto = GueltigerNeuerBenutzer() with { NeuesPasswort = "kurz" };

        var ergebnis = new BenutzerValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Benutzer_BestehenderBenutzerOhnePasswortaenderung_KeineFehler()
    {
        var dto = GueltigerNeuerBenutzer() with { Id = 7, NeuesPasswort = null };

        var ergebnis = new BenutzerValidator().Validate(dto);

        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Benutzer_OhneRolle_Fehler()
    {
        var dto = GueltigerNeuerBenutzer() with { RolleId = 0 };

        var ergebnis = new BenutzerValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Benutzer_UngueltigeEmail_Fehler()
    {
        var dto = GueltigerNeuerBenutzer() with { Email = "keine-email" };

        var ergebnis = new BenutzerValidator().Validate(dto);

        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Rolle_GueltigerName_KeineFehler()
    {
        var ergebnis = new RolleValidator().Validate(new RolleDto { Name = "Verkauf" });

        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Rolle_LeererName_Fehler()
    {
        var ergebnis = new RolleValidator().Validate(new RolleDto { Name = string.Empty });

        Assert.False(ergebnis.IsValid);
    }
}
