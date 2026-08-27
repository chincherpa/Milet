using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class RechnungBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new BelegImmutabilityInterceptor())
            .Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        db.Nummernkreise.Add(new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" });
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeueRechnungAsync(CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(ct);
        var rechnung = new Rechnung
        {
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 10m, GesamtNetto = 10m, MwStSatzWert = 19m }],
            SummeNetto = 10m,
            SummeMwSt = 1.90m,
            SummeBrutto = 11.90m,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);
        return rechnung.Id;
    }

    [Fact]
    public async Task ParallelesBuchen_MehrererRechnungen_LiefertEindeutigeNummern()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungIds = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => NeueRechnungAsync(ct)));

        var service = new RechnungBuchenService(_factory, new NumberRangeService(_factory), AllesErlaubtBerechtigungsService.Instanz);
        var ergebnisse = await Task.WhenAll(rechnungIds.Select(id => service.BuchenAsync(id, ct)));

        Assert.Equal(15, ergebnisse.Select(r => r.BelegNummer).Distinct().Count());
        Assert.All(ergebnisse, r => Assert.Equal(Domain.Entities.Verkauf.BelegStatus.Gebucht, r.Status));
    }

    [Fact]
    public async Task GebuchteRechnung_AenderungWirftImmutabilityFehler()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungId = await NeueRechnungAsync(ct);
        var service = new RechnungBuchenService(_factory, new NumberRangeService(_factory), AllesErlaubtBerechtigungsService.Instanz);
        await service.BuchenAsync(rechnungId, ct);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        rechnung.Kopftext = "Nachträgliche Änderung";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
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
