using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BestandServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _artikelId;
    private int _lagerortId;

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
        var artikel = new Artikel { Artikelnummer = "ART-TEST", Bezeichnung = "Testartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(artikel, lagerort);
        await db.SaveChangesAsync();
        _artikelId = artikel.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task Korrektur_UnzureichenderBestand_WirftNegativsperre()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz, TestCurrentUserService.Instanz);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -1, Grund = "Test" }, ct));
    }

    [Fact]
    public async Task Korrektur_PositivGefolgtVonNegativUeberBestand_LetzeBuchungWirftBestandBleibtKonsistent()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz, TestCurrentUserService.Instanz);

        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 10, Grund = "Erstbestückung" }, ct);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -15, Grund = "Zu viel" }, ct));

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(10m, bestand.Menge);
    }

    [Fact]
    public async Task ParalleleBuchungen_LedgerSummeGleichSnapshot_NieNegativ()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = new TestDbContextFactory(_options);
        var service = new BestandService(factory, AllesErlaubtBerechtigungsService.Instanz, TestCurrentUserService.Instanz);

        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 100, Grund = "Start" }, ct);

        // 30 parallele Abbuchungen à 5 = 150 angefragt, nur 100 verfügbar -> ein Teil muss mit Negativsperre scheitern, Rest darf nie unter 0 fallen.
        var aufgaben = Enumerable.Range(0, 30).Select(async _ =>
        {
            try { await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -5, Grund = "Parallel" }, ct); return true; }
            catch (InvalidOperationException) { return false; }
        });
        var ergebnisse = await Task.WhenAll(aufgaben);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        var ledgerSumme = await db.Lagerbewegungen.Where(l => l.ArtikelId == _artikelId && l.LagerortId == _lagerortId).SumAsync(l => l.Menge, ct);

        Assert.True(bestand.Menge >= 0);
        Assert.Equal(ledgerSumme, bestand.Menge);
        Assert.Equal(100m - 5m * ergebnisse.Count(erfolg => erfolg), bestand.Menge);
    }

    [Fact]
    public async Task SucheAsync_DeaktivierterLagerortMitBestand_ZeigtBestandAberKeineSynthetischeNullzeile()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz, TestCurrentUserService.Instanz);
        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 7, Grund = "Start" }, ct);

        int zweiterArtikelId;
        await using (var db = new MiletDbContext(_options))
        {
            var zweiterArtikel = new Artikel { Artikelnummer = "ART-OHNE-BESTAND", Bezeichnung = "Ohne Bestand hier", EinheitId = (await db.Einheiten.FirstAsync(ct)).Id, MwStSatzId = (await db.MwStSaetze.FirstAsync(ct)).Id };
            db.Add(zweiterArtikel);
            var lagerort = await db.Lagerorte.FirstAsync(l => l.Id == _lagerortId, ct);
            lagerort.Aktiv = false;
            await db.SaveChangesAsync(ct);
            zweiterArtikelId = zweiterArtikel.Id;
        }

        var ergebnis = await service.SucheAsync(null, ct);

        // Der erste Artikel hat echten Bestand am jetzt deaktivierten Lagerort — muss weiterhin sichtbar sein.
        var zeileMitBestand = Assert.Single(ergebnis, z => z.ArtikelId == _artikelId && z.LagerortId == _lagerortId);
        Assert.Equal(7m, zeileMitBestand.Menge);
        Assert.False(zeileMitBestand.LagerortAktiv);

        // Der zweite Artikel hat DORT keinen Bestand — keine synthetische Nullzeile für einen deaktivierten Lagerort.
        Assert.DoesNotContain(ergebnis, z => z.ArtikelId == zweiterArtikelId && z.LagerortId == _lagerortId);
    }

    [Fact]
    public async Task Korrektur_SetztBemerkungUndBenutzerIdAufDerLagerbewegung()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz, TestCurrentUserService.Instanz);

        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 5, Grund = "Inventurdifferenz" }, ct);

        await using var db = new MiletDbContext(_options);
        var bewegung = await db.Lagerbewegungen.SingleAsync(l => l.ArtikelId == _artikelId && l.LagerortId == _lagerortId, ct);
        Assert.Equal("Inventurdifferenz", bewegung.Bemerkung);
        Assert.Equal(TestCurrentUserService.Instanz.BenutzerId, bewegung.BenutzerId);
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
