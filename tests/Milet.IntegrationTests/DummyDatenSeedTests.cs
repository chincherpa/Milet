using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Seed;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class DummyDatenSeedTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private ServiceProvider? _services;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Milet", _container.GetConnectionString())])
            .Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddInfrastructure(configuration);
        _services = serviceCollection.BuildServiceProvider();

        var dbFactory = _services.GetRequiredService<IDbContextFactory<MiletDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await StammdatenSeed.ApplyAsync(db);
        await AdminSeed.ApplyAsync(db);

        var sitzung = _services.GetRequiredService<ICurrentSessionService>();
        sitzung.Anmelden(null, "Test", "Administrator", RechtCodes.Alle);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task ApplyAsync_ErzeugtKonsistenteGaertnereiUndBleibtIdempotent()
    {
        var services = _services!;
        var angelegt = await DummyDatenSeed.ApplyAsync(services, TestContext.Current.CancellationToken);

        Assert.True(angelegt);

        var dbFactory = services.GetRequiredService<IDbContextFactory<MiletDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var felder = await db.Lagerorte.Where(l => l.IstFeld).ToListAsync(TestContext.Current.CancellationToken);
        var sektionen = await db.Sektionen.ToListAsync(TestContext.Current.CancellationToken);
        var bestaende = await db.ArtikelBestaende.ToListAsync(TestContext.Current.CancellationToken);
        var bewegungen = await db.Lagerbewegungen.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["F1", "F2", "F3"], felder.Select(f => f.Code).Order());
        Assert.Equal(15, sektionen.Count);
        Assert.All(sektionen, sektion => Assert.Contains(felder, feld => feld.Id == sektion.LagerortId));
        Assert.All(bestaende.Where(b => b.SektionId.HasValue), bestand =>
            Assert.Equal(sektionen.Single(s => s.Id == bestand.SektionId).LagerortId, bestand.LagerortId));

        foreach (var bestand in bestaende)
        {
            var ledgerMenge = bewegungen
                .Where(b => b.ArtikelId == bestand.ArtikelId
                    && b.LagerortId == bestand.LagerortId
                    && b.SektionId == bestand.SektionId
                    && b.KulturstufeId == bestand.KulturstufeId)
                .Sum(b => b.Menge);
            Assert.Equal(ledgerMenge, bestand.Menge);
        }

        var artikelAnzahl = await db.Artikel.CountAsync(TestContext.Current.CancellationToken);
        var bewegungsAnzahl = bewegungen.Count;

        Assert.False(await DummyDatenSeed.ApplyAsync(services, TestContext.Current.CancellationToken));
        Assert.Equal(artikelAnzahl, await db.Artikel.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(bewegungsAnzahl, await db.Lagerbewegungen.CountAsync(TestContext.Current.CancellationToken));
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            process!.WaitForExit(3_000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}