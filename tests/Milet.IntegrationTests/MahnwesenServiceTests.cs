using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>MahnwesenService hatte bislang keinen eigenen Test (s. STATUS.md, Plan Phase 9 Task 22) —
/// nur die reine Domain-Logik (MahnSelektionService) war getestet.</summary>
public sealed class MahnwesenServiceTests : IAsyncLifetime
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
        _options = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(_container.GetConnectionString()).Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        db.Mahnstufen.Add(new Mahnstufe { Stufe = 1, Karenztage = 5, Gebuehr = 5m, Mahntext = "Zahlungserinnerung" });
        var kunde = new Kunde { Kundennummer = "KD-1", Adresse = new() { Name1 = "Testkunde" } };
        db.Add(kunde);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeuerOffenerPostenAsync(DateOnly faelligkeit, bool mahnsperre, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-{Guid.NewGuid():N}"[..12], BelegDatum = faelligkeit, KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(), LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht, SummeBrutto = 100m,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);
        var op = new OffenerPosten
        {
            Beleg = rechnung, KundeId = kunde.Id, Typ = OffenerPostenTyp.Debitor,
            Betrag = 100m, OffenerBetrag = 100m, Faelligkeit = faelligkeit, Mahnsperre = mahnsperre,
        };
        db.Add(op);
        await db.SaveChangesAsync(ct);
        return op.Id;
    }

    [Fact]
    public async Task ErmittleFaelligeAsync_UeberfaelligerPosten_ErscheintAlsKandidat()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeuerOffenerPostenAsync(heute.AddDays(-10), mahnsperre: false, ct);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var gruppen = await service.ErmittleFaelligeAsync(ct);

        var kandidat = Assert.Single(Assert.Single(gruppen).Kandidaten);
        Assert.Equal(1, kandidat.NaechsteMahnstufe);
    }

    [Fact]
    public async Task ErmittleFaelligeAsync_NochNichtUeberfaelligerPosten_ErscheintNicht()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeuerOffenerPostenAsync(heute.AddDays(-2), mahnsperre: false, ct);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var gruppen = await service.ErmittleFaelligeAsync(ct);

        Assert.Empty(gruppen);
    }

    [Fact]
    public async Task ErmittleFaelligeAsync_PostenMitMahnsperre_ErscheintNicht()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeuerOffenerPostenAsync(heute.AddDays(-10), mahnsperre: true, ct);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var gruppen = await service.ErmittleFaelligeAsync(ct);

        Assert.Empty(gruppen);
    }

    [Fact]
    public async Task MahnlaufDurchfuehrenAsync_ErzeugtMahnungUndSetztMahnstufeAufDenOP()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var opId = await NeuerOffenerPostenAsync(heute.AddDays(-10), mahnsperre: false, ct);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var mahnungen = await service.MahnlaufDurchfuehrenAsync([opId], ct);

        var mahnung = Assert.Single(mahnungen);
        Assert.Equal(1, mahnung.Mahnstufe);
        Assert.Equal(105m, mahnung.Gesamtbetrag);

        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(1, op.Mahnstufe);
    }

    [Fact]
    public async Task MahnlaufDurchfuehrenAsync_LeereListe_LiefertLeereListeOhneDbZugriff()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var mahnungen = await service.MahnlaufDurchfuehrenAsync([], ct);

        Assert.Empty(mahnungen);
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
