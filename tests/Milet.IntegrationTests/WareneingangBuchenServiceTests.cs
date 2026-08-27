using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class WareneingangBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _lieferantId;
    private int _artikelId;
    private int _artikelSerialisiertId;
    private int _lagerortId;

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

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var lieferant = new Lieferant { Lieferantennummer = "LF-TEST", Adresse = new() { Name1 = "Testlieferant" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, lieferant, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelSerial = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Serienartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id, HatSeriennummern = true };
        db.AddRange(artikel, artikelSerial);
        await db.SaveChangesAsync();

        _lieferantId = lieferant.Id;
        _artikelId = artikel.Id;
        _artikelSerialisiertId = artikelSerial.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<Wareneingang> NeuerWareneingangAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var lieferant = await db.Lieferanten.FirstAsync(l => l.Id == _lieferantId, ct);
        var wareneingang = new Wareneingang
        {
            BelegNummer = $"WE-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = lieferant.Id,
            RechnungsadresseSnapshot = lieferant.Adresse.Kopie(),
            LieferadresseSnapshot = lieferant.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Test", Menge = menge, Einzelpreis = 5m, GesamtNetto = menge * 5m,
                MwStSatzWert = 19m, ArtikelId = artikelId, LagerortId = _lagerortId,
            }],
        };
        db.Add(wareneingang);
        await db.SaveChangesAsync(ct);
        return wareneingang;
    }

    [Fact]
    public async Task BuchenAsync_NormalArtikel_ErhoehtBestandUndSetztGebucht()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelId, 20, ct);
        var service = new WareneingangBuchenService(_factory);

        var gebucht = await service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>>(), ct);

        Assert.Equal(BelegStatus.Gebucht, gebucht.Status);
        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(20m, bestand.Menge);
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelMitNeuenNummern_LegtSeriennummernAn()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelSerialisiertId, 2, ct);
        var positionId = wareneingang.Positionen[0].Id;
        var service = new WareneingangBuchenService(_factory);

        await service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>> { [positionId] = ["SN-A", "SN-B"] }, ct);

        await using var db = new MiletDbContext(_options);
        var seriennummern = await db.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).ToListAsync(ct);
        Assert.Equal(2, seriennummern.Count);
        Assert.All(seriennummern, s => Assert.Equal(SeriennummerStatus.AufLager, s.Status));
        Assert.Equal(2, await db.BelegPositionSeriennummern.CountAsync(b => b.BelegPositionId == positionId, ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelFalscheAnzahl_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelSerialisiertId, 2, ct);
        var positionId = wareneingang.Positionen[0].Id;
        var service = new WareneingangBuchenService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>> { [positionId] = ["SN-A"] }, ct));
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
