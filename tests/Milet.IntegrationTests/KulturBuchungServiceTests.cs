using Microsoft.EntityFrameworkCore;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 9 — Kulturbuchungen (Zugang/Stufenwechsel/Umsetzen/Ausfall).</summary>
public sealed class KulturBuchungServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _artikelId;
    private int _feldAId;
    private int _feldBId;
    private int _sektionAId;
    private int _sektionBId;
    private int _stufeJpId;
    private int _stufeTpId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(_container.GetConnectionString()).Options;

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        db.AddRange(einheit, mwst);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-KULTUR", Bezeichnung = "Salvia", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var feldA = new Lagerort { Code = "FA", Bezeichnung = "Feld A", IstFeld = true, BreiteMeter = 30m, HoeheMeter = 20m };
        var feldB = new Lagerort { Code = "FB", Bezeichnung = "Feld B", IstFeld = true, BreiteMeter = 30m, HoeheMeter = 20m };
        db.AddRange(artikel, feldA, feldB);
        await db.SaveChangesAsync();

        var sektionA = new Sektion { LagerortId = feldA.Id, Code = "A1", Bezeichnung = "Sektion A1", BreiteMeter = 5, HoeheMeter = 5 };
        var sektionB = new Sektion { LagerortId = feldB.Id, Code = "B1", Bezeichnung = "Sektion B1", BreiteMeter = 5, HoeheMeter = 5 };
        var stufeJp = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#8BC34A" };
        var stufeTp = new Kulturstufe { Code = "TP", Bezeichnung = "Teenagerpflanze", Reihenfolge = 2, FarbeHex = "#4CAF50" };
        db.AddRange(sektionA, sektionB, stufeJp, stufeTp);
        await db.SaveChangesAsync();

        _artikelId = artikel.Id;
        _feldAId = feldA.Id;
        _feldBId = feldB.Id;
        _sektionAId = sektionA.Id;
        _sektionBId = sektionB.Id;
        _stufeJpId = stufeJp.Id;
        _stufeTpId = stufeTp.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private KulturBuchungService NeuerService() => new(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz);

    [Fact]
    public async Task Zugang_BuchtPositivenBestand()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();

        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 500m }, ct);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        Assert.Equal(500m, bestand.Menge);
    }

    [Fact]
    public async Task Stufenwechsel_VerschiebtMengeExaktUndErzeugtZweiLedgerZeilen()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 500m }, ct);

        await service.StufenwechselAsync(new StufenwechselDto
        {
            ArtikelId = _artikelId,
            VonFeldId = _feldAId,
            VonSektionId = _sektionAId,
            VonKulturstufeId = _stufeJpId,
            NachFeldId = _feldBId,
            NachSektionId = _sektionBId,
            NachKulturstufeId = _stufeTpId,
            Menge = 400m,
        }, ct);

        await using var db = new MiletDbContext(_options);
        var quelle = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        var ziel = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionBId && b.KulturstufeId == _stufeTpId, ct);
        Assert.Equal(100m, quelle.Menge);
        Assert.Equal(400m, ziel.Menge);

        var stufenwechselBewegungen = await db.Lagerbewegungen.Where(l => l.Typ == LagerbewegungTyp.Stufenwechsel).ToListAsync(ct);
        Assert.Equal(2, stufenwechselBewegungen.Count);
        Assert.Contains(stufenwechselBewegungen, b => b.Menge == -400m);
        Assert.Contains(stufenwechselBewegungen, b => b.Menge == 400m);
    }

    [Fact]
    public async Task Stufenwechsel_UeberDenBestandHinaus_WirftUndHinterlaesstKeineTeilaenderung()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 100m }, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StufenwechselAsync(new StufenwechselDto
        {
            ArtikelId = _artikelId,
            VonFeldId = _feldAId,
            VonSektionId = _sektionAId,
            VonKulturstufeId = _stufeJpId,
            NachFeldId = _feldBId,
            NachSektionId = _sektionBId,
            NachKulturstufeId = _stufeTpId,
            Menge = 200m,
        }, ct));

        await using var db = new MiletDbContext(_options);
        var quelle = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        Assert.Equal(100m, quelle.Menge);
        var zielExistiert = await db.ArtikelBestaende.AnyAsync(b => b.SektionId == _sektionBId && b.KulturstufeId == _stufeTpId, ct);
        Assert.False(zielExistiert, "Rollback muss auch den Zugang auf der Zielseite verhindern.");
    }

    [Fact]
    public async Task Umsetzen_GleicheStufeAndereSektion_VerschiebtMenge()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 300m }, ct);

        await service.UmsetzenAsync(new UmsetzenDto
        {
            ArtikelId = _artikelId,
            VonFeldId = _feldAId,
            VonSektionId = _sektionAId,
            NachFeldId = _feldBId,
            NachSektionId = _sektionBId,
            KulturstufeId = _stufeJpId,
            Menge = 300m,
        }, ct);

        await using var db = new MiletDbContext(_options);
        var ziel = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionBId && b.KulturstufeId == _stufeJpId, ct);
        Assert.Equal(300m, ziel.Menge);
        var umsetzenBewegungen = await db.Lagerbewegungen.CountAsync(l => l.Typ == LagerbewegungTyp.Umsetzen, ct);
        Assert.Equal(2, umsetzenBewegungen);
    }

    [Fact]
    public async Task Umsetzen_GleicheSektionUndStufe_Nulloperation_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 100m }, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UmsetzenAsync(new UmsetzenDto
        {
            ArtikelId = _artikelId,
            VonFeldId = _feldAId,
            VonSektionId = _sektionAId,
            NachFeldId = _feldAId,
            NachSektionId = _sektionAId,
            KulturstufeId = _stufeJpId,
            Menge = 50m,
        }, ct));
    }

    [Fact]
    public async Task Ausfall_ReduziertBestandUndIstUeberDenTypAuswertbar()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 200m }, ct);

        await service.AusfallAsync(new AusfallDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 30m }, ct);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        Assert.Equal(170m, bestand.Menge);
        var ausfallSumme = await db.Lagerbewegungen.Where(l => l.Typ == LagerbewegungTyp.Ausfall).SumAsync(l => l.Menge, ct);
        Assert.Equal(-30m, ausfallSumme);
    }

    [Fact]
    public async Task ParallelerStufenwechselDerselbenQuelle_UeberziehtNicht()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        await service.ZugangAsync(new KulturZugangDto { ArtikelId = _artikelId, FeldId = _feldAId, SektionId = _sektionAId, KulturstufeId = _stufeJpId, Menge = 100m }, ct);

        var aufgaben = Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await service.StufenwechselAsync(new StufenwechselDto
                {
                    ArtikelId = _artikelId,
                    VonFeldId = _feldAId,
                    VonSektionId = _sektionAId,
                    VonKulturstufeId = _stufeJpId,
                    NachFeldId = _feldBId,
                    NachSektionId = _sektionBId,
                    NachKulturstufeId = _stufeTpId,
                    Menge = 30m,
                }, ct);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });
        var ergebnisse = await Task.WhenAll(aufgaben);

        await using var db = new MiletDbContext(_options);
        var quelle = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        Assert.True(quelle.Menge >= 0);
        Assert.Equal(100m - 30m * ergebnisse.Count(e => e), quelle.Menge);
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            return process is not null && process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<MiletDbContext> options) : IDbContextFactory<MiletDbContext>
    {
        public MiletDbContext CreateDbContext() => new(options);
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
