using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class NumberRangeServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
        {
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");
        }

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        db.Nummernkreise.Add(new Nummernkreis { Code = "TEST", NaechsteNummer = 1, Format = "TEST-{0:0000}" });
        // Jahresbezogener Kreis, für den es NUR das Vorjahr gibt — der Zustand, in dem sich jede Installation
        // am 01.01. befindet, wenn niemand den Migrator gestartet hat.
        db.Nummernkreise.Add(new Nummernkreis
        {
            Code = "TESTJAHR",
            Jahr = DateTime.Today.Year - 1,
            NaechsteNummer = 5,
            Format = "TESTJAHR-{1}-{0:0000}",
        });
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task ParalleleVergabe_LiefertLueckenloseEindeutigeNummern()
    {
        var service = new NumberRangeService(new TestDbContextFactory(_options));

        var tasks = Enumerable.Range(0, 25).Select(_ => service.NaechsteNummerAsync("TEST"));
        var nummern = await Task.WhenAll(tasks);

        Assert.Equal(25, nummern.Distinct().Count());

        var erwartet = Enumerable.Range(1, 25).Select(i => $"TEST-{i:0000}").OrderBy(s => s);
        Assert.Equal(erwartet, nummern.OrderBy(s => s));
    }

    [Fact]
    public async Task NurVorjahreskreisVorhanden_LegtJahreskreisAnUndVergibtAbEins()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new NumberRangeService(new TestDbContextFactory(_options));

        var nummer = await service.NaechsteNummerAsync("TESTJAHR", ct);

        Assert.Equal($"TESTJAHR-{DateTime.Today.Year}-0001", nummer);

        await using var db = new MiletDbContext(_options);
        var vorjahr = await db.Nummernkreise.AsNoTracking()
            .FirstAsync(n => n.Code == "TESTJAHR" && n.Jahr == DateTime.Today.Year - 1, ct);
        Assert.Equal(5, vorjahr.NaechsteNummer);

        var laufendesJahr = await db.Nummernkreise.AsNoTracking()
            .FirstAsync(n => n.Code == "TESTJAHR" && n.Jahr == DateTime.Today.Year, ct);
        Assert.Equal(2, laufendesJahr.NaechsteNummer);
    }

    [Fact]
    public async Task ParalleleVergabeUeberDenJahreswechsel_LegtDenKreisNurEinmalAn()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new NumberRangeService(new TestDbContextFactory(_options));

        // Zweiter jahresbezogener Code, damit dieser Test unabhängig vom Test oben läuft (xUnit führt
        // Testmethoden derselben Klasse zwar nicht parallel aus, aber die Reihenfolge ist nicht zugesichert).
        await using (var vorbereitung = new MiletDbContext(_options))
        {
            vorbereitung.Nummernkreise.Add(new Nummernkreis
            {
                Code = "TESTPAR",
                Jahr = DateTime.Today.Year - 1,
                NaechsteNummer = 1,
                Format = "TESTPAR-{1}-{0:0000}",
            });
            await vorbereitung.SaveChangesAsync(ct);
        }

        var nummern = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.NaechsteNummerAsync("TESTPAR")));

        Assert.Equal(10, nummern.Distinct().Count());

        await using var db = new MiletDbContext(_options);
        var kreise = await db.Nummernkreise.AsNoTracking()
            .Where(n => n.Code == "TESTPAR" && n.Jahr == DateTime.Today.Year)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(kreise);
    }

    [Fact]
    public async Task UnbekannterCode_Wirft()
    {
        var service = new NumberRangeService(new TestDbContextFactory(_options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.NaechsteNummerAsync("UNBEKANNT", TestContext.Current.CancellationToken));
    }

    private sealed class TestDbContextFactory(DbContextOptions<MiletDbContext> options) : IDbContextFactory<MiletDbContext>
    {
        public MiletDbContext CreateDbContext() => new(options);

        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
