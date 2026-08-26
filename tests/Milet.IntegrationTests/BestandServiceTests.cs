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
        var service = new BestandService(new TestDbContextFactory(_options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -1, Grund = "Test" }, ct));
    }

    [Fact]
    public async Task Korrektur_PositivGefolgtVonNegativUeberBestand_LetzeBuchungWirftBestandBleibtKonsistent()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options));

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
        var service = new BestandService(factory);

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
