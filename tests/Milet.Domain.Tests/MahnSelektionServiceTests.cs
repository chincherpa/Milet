using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class MahnSelektionServiceTests
{
    private static readonly Mahnstufe Stufe1 = new() { Stufe = 1, Karenztage = 7, Gebuehr = 0m };
    private static readonly Mahnstufe Stufe2 = new() { Stufe = 2, Karenztage = 14, Gebuehr = 5m };
    private static readonly DateOnly Faelligkeit = new(2026, 1, 1);

    private static OffenerPosten NeuerOp(int mahnstufe = 0, decimal offenerBetrag = 100m,
        bool mahnsperre = false, OffenerPostenStatus status = OffenerPostenStatus.Offen) => new()
    {
        Faelligkeit = Faelligkeit,
        Mahnstufe = mahnstufe,
        OffenerBetrag = offenerBetrag,
        Mahnsperre = mahnsperre,
        Status = status,
    };

    [Fact]
    public void ErmittleFaelligeStufe_VorKarenzablauf_KeinKandidat()
    {
        var op = NeuerOp();
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 1, 7), [Stufe1, Stufe2]);
        Assert.Null(stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_AmKantentag_IstFaellig()
    {
        var op = NeuerOp();
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 1, 8), [Stufe1, Stufe2]);
        Assert.Equal(1, stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_Mahnsperre_BlocktImmer()
    {
        var op = NeuerOp(mahnsperre: true);
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 3, 1), [Stufe1, Stufe2]);
        Assert.Null(stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_BereitsAusgeglichen_BlocktImmer()
    {
        var op = NeuerOp(status: OffenerPostenStatus.Ausgeglichen, offenerBetrag: 0m);
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 3, 1), [Stufe1, Stufe2]);
        Assert.Null(stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_FehlendeStufenConfig_UeberspringtNichtAufHoehereStufe()
    {
        var op = NeuerOp(mahnstufe: 1); // naechste waere Stufe 2, aber nur Stufe 1 konfiguriert
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 3, 1), [Stufe1]);
        Assert.Null(stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_ZweiteMahnungNachFortgeschrittenerZeit_EskaliertAufStufeZwei()
    {
        var op = NeuerOp(mahnstufe: 1); // bereits einmal gemahnt
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 1, 15), [Stufe1, Stufe2]);
        Assert.Equal(2, stufe);
    }

    [Fact]
    public void ErmittleFaelligeStufe_OffenerBetragNull_KeinKandidat()
    {
        var op = NeuerOp(offenerBetrag: 0m);
        var stufe = MahnSelektionService.ErmittleFaelligeStufe(op, heute: new DateOnly(2026, 3, 1), [Stufe1, Stufe2]);
        Assert.Null(stufe);
    }
}
