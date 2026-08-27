using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class SkontoRechnerTests
{
    private static readonly DateOnly Rechnungsdatum = new(2026, 1, 1);

    [Fact]
    public void BerechneSkonto_InnerhalbFrist_GewaehrtSkonto()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 10), skontoTage: 14, skontoProzent: 2m, betrag: 100m);

        Assert.Equal(2.00m, skonto);
    }

    [Fact]
    public void BerechneSkonto_AmLetztenTagDerFrist_GewaehrtNochSkonto()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 15), skontoTage: 14, skontoProzent: 2m, betrag: 100m);

        Assert.Equal(2.00m, skonto);
    }

    [Fact]
    public void BerechneSkonto_EinenTagZuSpaet_KeinSkonto()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 16), skontoTage: 14, skontoProzent: 2m, betrag: 100m);

        Assert.Equal(0m, skonto);
    }

    [Fact]
    public void BerechneSkonto_OhneSkontoVereinbarung_KeinSkonto()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 2), skontoTage: null, skontoProzent: null, betrag: 100m);

        Assert.Equal(0m, skonto);
    }

    [Fact]
    public void BerechneSkonto_RundetKaufmaennischAufZweiStellen()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 5), skontoTage: 14, skontoProzent: 2m, betrag: 33.335m);
        // 33.335 * 0.02 = 0.6667 -> ToEven auf 0.67
        Assert.Equal(0.67m, skonto);
    }

    [Fact]
    public void BerechneSkonto_NullProzent_KeinSkonto()
    {
        var skonto = SkontoRechner.BerechneSkonto(
            Rechnungsdatum, zahlungsdatum: new DateOnly(2026, 1, 2), skontoTage: 14, skontoProzent: 0m, betrag: 100m);

        Assert.Equal(0m, skonto);
    }
}
