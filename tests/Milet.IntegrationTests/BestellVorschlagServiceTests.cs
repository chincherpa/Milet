using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BestellVorschlagServiceTests : IAsyncLifetime
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
        _options = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(_container.GetConnectionString()).Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, lagerort);
        await db.SaveChangesAsync();

        var unterschritten = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Knapp", EinheitId = einheit.Id, MwStSatzId = mwst.Id, Mindestbestand = 10m, Einkaufspreis = 5m };
        var ausreichend = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Ausreichend", EinheitId = einheit.Id, MwStSatzId = mwst.Id, Mindestbestand = 5m, Einkaufspreis = 5m };
        var ohneMindestbestand = new Artikel { Artikelnummer = "ART-3", Bezeichnung = "Kein Minimum", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        db.AddRange(unterschritten, ausreichend, ohneMindestbestand);
        await db.SaveChangesAsync();

        await BestandService.BucheBewegungAsync(db, unterschritten.Id, lagerort.Id, 3m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
        await BestandService.BucheBewegungAsync(db, ausreichend.Id, lagerort.Id, 8m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task ErmittleVorschlaegeAsync_NurArtikelUnterMindestbestand_MitKorrekterVorschlagsmenge()
    {
        var service = new BestellVorschlagService(_factory);
        var vorschlaege = await service.ErmittleVorschlaegeAsync(TestContext.Current.CancellationToken);

        var vorschlag = Assert.Single(vorschlaege);
        Assert.Equal("ART-1", vorschlag.Artikelnummer);
        Assert.Equal(3m, vorschlag.AktuellerBestand);
        Assert.Equal(7m, vorschlag.VorschlagsMenge);
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
