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

public sealed class BelegUeberleitungServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;

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
        db.Nummernkreise.AddRange(
            new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" });
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeuerGebuchterLieferscheinAsync(decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = $"LS-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            Status = BelegStatus.Gebucht,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = menge, Einzelpreis = 10m, GesamtNetto = menge * 10m, MwStSatzWert = 19m }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);
        return lieferschein.Id;
    }

    [Fact]
    public async Task UeberleitenMehrereAsync_ZweiLieferscheineGleicherKunde_ErgibtEineSammelrechnung()
    {
        var ct = TestContext.Current.CancellationToken;
        var ls1 = await NeuerGebuchterLieferscheinAsync(3, ct);
        var ls2 = await NeuerGebuchterLieferscheinAsync(5, ct);

        var service = new BelegUeberleitungService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var rechnung = await service.UeberleitenMehrereAsync([ls1, ls2], BelegTyp.Rechnung, ct);

        Assert.Equal(2, rechnung.Positionen.Count);
        Assert.Equal(80m, rechnung.SummeNetto);

        await using var db = new MiletDbContext(_options);
        Assert.Equal(BelegStatus.Erledigt, (await db.Belege.FirstAsync(b => b.Id == ls1, ct)).Status);
        Assert.Equal(BelegStatus.Erledigt, (await db.Belege.FirstAsync(b => b.Id == ls2, ct)).Status);
    }

    [Fact]
    public async Task UeberleitenMehrereAsync_NichtGebuchterLieferschein_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = "LS-ENTWURF",
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            Status = BelegStatus.Entwurf,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 10m, GesamtNetto = 10m, MwStSatzWert = 19m }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);

        var service = new BelegUeberleitungService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UeberleitenMehrereAsync([lieferschein.Id], BelegTyp.Rechnung, ct));
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
