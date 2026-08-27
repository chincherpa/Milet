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

public sealed class LieferscheinBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
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
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, kunde, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelSerial = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Serienartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id, HatSeriennummern = true };
        db.AddRange(artikel, artikelSerial);
        await db.SaveChangesAsync();

        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
        _artikelSerialisiertId = artikelSerial.Id;
        _lagerortId = lagerort.Id;

        await BestandService.BucheBewegungAsync(db, _artikelId, _lagerortId, 20m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
        var s1 = new Seriennummer { ArtikelId = _artikelSerialisiertId, Nummer = "SN-1", Status = SeriennummerStatus.AufLager, LagerortId = _lagerortId };
        var s2 = new Seriennummer { ArtikelId = _artikelSerialisiertId, Nummer = "SN-2", Status = SeriennummerStatus.AufLager, LagerortId = _lagerortId };
        db.AddRange(s1, s2);
        await db.SaveChangesAsync();
        await BestandService.BucheBewegungAsync(db, _artikelSerialisiertId, _lagerortId, 2m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<Lieferschein> NeuerLieferscheinAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = $"LS-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Test", Menge = menge, Einzelpreis = 10m, GesamtNetto = menge * 10m,
                MwStSatzWert = 19m, ArtikelId = artikelId, LagerortId = _lagerortId,
            }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);
        return lieferschein;
    }

    [Fact]
    public async Task BuchenAsync_AusreichenderBestand_BuchtAbUndSetztGebucht()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelId, 5, ct);
        var service = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var gebucht = await service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct);

        Assert.Equal(BelegStatus.Gebucht, gebucht.Status);
        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(15m, bestand.Menge);
    }

    [Fact]
    public async Task BuchenAsync_UnzureichenderBestand_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelId, 100, ct);
        var service = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelMitAuswahl_VerknuepftSeriennummernUndSetztAusgeliefert()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelSerialisiertId, 2, ct);
        var positionId = lieferschein.Positionen[0].Id;

        await using var seedDb = new MiletDbContext(_options);
        var seriennummerIds = await seedDb.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).Select(s => s.Id).ToListAsync(ct);

        var service = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>> { [positionId] = seriennummerIds }, ct);

        await using var db = new MiletDbContext(_options);
        Assert.All(await db.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).ToListAsync(ct),
            s => Assert.Equal(SeriennummerStatus.Ausgeliefert, s.Status));
        Assert.Equal(2, await db.BelegPositionSeriennummern.CountAsync(b => b.BelegPositionId == positionId, ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelOhneAuswahl_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelSerialisiertId, 2, ct);
        var service = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct));
    }

    [Fact]
    public async Task ParallelesBuchen_MehrererLieferscheine_NieNegativerBestand()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferscheine = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => NeuerLieferscheinAsync(_artikelId, 5, ct)));
        var service = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var ergebnisse = await Task.WhenAll(lieferscheine.Select(async l =>
        {
            try { await service.BuchenAsync(l.Id, new Dictionary<int, IReadOnlyList<int>>(), ct); return true; }
            catch (InvalidOperationException) { return false; }
        }));

        // 8 x 5 = 40 angefragt, nur 20 verfügbar -> maximal 4 dürfen erfolgreich sein.
        Assert.True(ergebnisse.Count(erfolg => erfolg) <= 4);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.True(bestand.Menge >= 0);
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
