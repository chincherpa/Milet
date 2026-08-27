using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Xunit;

namespace Milet.Application.Tests;

public class FinanzenValidatorTests
{
    private static ZahlungDto GueltigeZahlung(params ZahlungZuordnungDto[] zuordnungen) => new(
        Id: 0,
        KundeId: 1,
        LieferantId: null,
        Typ: OffenerPostenTyp.Debitor,
        Zahlungsdatum: DateOnly.FromDateTime(DateTime.Today),
        Zahlungsart: "Überweisung",
        Referenz: null,
        Zuordnungen: zuordnungen);

    [Fact]
    public void Zahlung_GueltigeDaten_KeinFehler()
    {
        var dto = GueltigeZahlung(new ZahlungZuordnungDto(1, 100m, 0m, []));
        Assert.True(new ZahlungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Zahlung_DebitorOhneKunde_Fehler()
    {
        var dto = GueltigeZahlung(new ZahlungZuordnungDto(1, 100m, 0m, [])) with { KundeId = null };
        Assert.False(new ZahlungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Zahlung_KreditorOhneLieferant_Fehler()
    {
        var dto = GueltigeZahlung(new ZahlungZuordnungDto(1, 100m, 0m, [])) with { Typ = OffenerPostenTyp.Kreditor, KundeId = null };
        Assert.False(new ZahlungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Zahlung_ZukuenftigesDatum_Fehler()
    {
        var dto = GueltigeZahlung(new ZahlungZuordnungDto(1, 100m, 0m, []))
            with { Zahlungsdatum = DateOnly.FromDateTime(DateTime.Today.AddDays(1)) };
        Assert.False(new ZahlungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Zahlung_OhneZuordnungen_Fehler()
    {
        var dto = GueltigeZahlung();
        Assert.False(new ZahlungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ZahlungZuordnung_BetragUndSkontoBeideNull_Fehler()
    {
        var dto = new ZahlungZuordnungDto(1, 0m, 0m, []);
        Assert.False(new ZahlungZuordnungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ZahlungZuordnung_NurSkonto_KeinFehler()
    {
        var dto = new ZahlungZuordnungDto(1, 0m, 5m, []);
        Assert.True(new ZahlungZuordnungValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ZahlungZuordnung_NegativerBetrag_Fehler()
    {
        var dto = new ZahlungZuordnungDto(1, -10m, 0m, []);
        Assert.False(new ZahlungZuordnungValidator().Validate(dto).IsValid);
    }
}
