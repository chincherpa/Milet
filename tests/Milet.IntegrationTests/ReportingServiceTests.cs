using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class ReportingServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
    private int _artikelId;

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
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, SteuerSchluessel = 3, GueltigAb = new DateOnly(2007, 1, 1) };
        var kunde = new Kunde { Kundennummer = "KD-1", Adresse = new() { Name1 = "Testkunde" } };
        db.AddRange(einheit, mwst, kunde);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Testartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        db.Add(artikel);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task NeueGebuchteRechnungAsync(DateOnly belegDatum, decimal menge, decimal einzelpreis, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var gesamtNetto = menge * einzelpreis;
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = belegDatum,
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeNetto = gesamtNetto,
            SummeMwSt = Math.Round(gesamtNetto * 0.19m, 2),
            SummeBrutto = gesamtNetto + Math.Round(gesamtNetto * 0.19m, 2),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, PositionsTyp = PositionsTyp.Artikel, ArtikelId = _artikelId,
                Bezeichnung = "Testartikel", Menge = menge, Einzelpreis = einzelpreis, GesamtNetto = gesamtNetto, MwStSatzWert = 19m,
            }],
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task UmsatzJeKundeAsync_SummiertGebuchteRechnungenImZeitraum()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeueGebuchteRechnungAsync(heute, 2, 50m, ct);
        await NeueGebuchteRechnungAsync(heute, 3, 10m, ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.UmsatzJeKundeAsync(heute.AddDays(-1), heute.AddDays(1), ct);

        var zeile = Assert.Single(ergebnis);
        Assert.Equal(2, zeile.AnzahlRechnungen);
        Assert.Equal(130m, zeile.SummeNetto); // 2*50 + 3*10
    }

    [Fact]
    public async Task UmsatzJeArtikelAsync_SummiertMengeUndNetto()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeueGebuchteRechnungAsync(heute, 2, 50m, ct);
        await NeueGebuchteRechnungAsync(heute, 3, 10m, ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.UmsatzJeArtikelAsync(heute.AddDays(-1), heute.AddDays(1), ct);

        var zeile = Assert.Single(ergebnis);
        Assert.Equal("ART-1", zeile.ArtikelNummer);
        Assert.Equal(5m, zeile.Menge);
        Assert.Equal(130m, zeile.SummeNetto);
    }

    [Fact]
    public async Task TopArtikelAsync_BegrenztAufAnzahl()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeueGebuchteRechnungAsync(heute, 1, 10m, ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.TopArtikelAsync(heute.AddDays(-1), heute.AddDays(1), anzahl: 0, ct);

        Assert.Empty(ergebnis);
    }

    [Fact]
    public async Task UmsatzJeMonatAsync_AusserhalbZeitraum_LeeresErgebnis()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await NeueGebuchteRechnungAsync(heute, 1, 10m, ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.UmsatzJeMonatAsync(heute.AddYears(-5), heute.AddYears(-4), ct);

        Assert.Empty(ergebnis);
    }

    [Fact]
    public async Task OffeneAuftraegeAsync_AuftragMitOffenerMenge_ErscheintInListe()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var auftrag = new Auftrag
        {
            BelegNummer = $"AU-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeBrutto = 59.50m,
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, PositionsTyp = PositionsTyp.Artikel, ArtikelId = _artikelId,
                Bezeichnung = "Testartikel", Menge = 5, Einzelpreis = 10m, GesamtNetto = 50m, MwStSatzWert = 19m,
            }],
        };
        db.Add(auftrag);
        await db.SaveChangesAsync(ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.OffeneAuftraegeAsync(ct);

        var zeile = Assert.Single(ergebnis);
        Assert.Equal(auftrag.BelegNummer, zeile.BelegNummer);
        Assert.Equal(5m, zeile.OffeneMenge);
    }

    [Fact]
    public async Task OffeneAuftraegeAsync_VollstaendigUebernommeneMenge_ErscheintNichtInListe()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var auftrag = new Auftrag
        {
            BelegNummer = $"AU-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeBrutto = 59.50m,
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, PositionsTyp = PositionsTyp.Artikel, ArtikelId = _artikelId,
                Bezeichnung = "Testartikel", Menge = 5, Einzelpreis = 10m, GesamtNetto = 50m, MwStSatzWert = 19m,
            }],
        };
        db.Add(auftrag);
        await db.SaveChangesAsync(ct);

        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeNetto = 50m, SummeMwSt = 9.50m, SummeBrutto = 59.50m,
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, PositionsTyp = PositionsTyp.Artikel, ArtikelId = _artikelId,
                Bezeichnung = "Testartikel", Menge = 5, Einzelpreis = 10m, GesamtNetto = 50m, MwStSatzWert = 19m,
                UrsprungsPositionId = auftrag.Positionen[0].Id,
            }],
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);

        var service = new ReportingService(_factory);
        var ergebnis = await service.OffeneAuftraegeAsync(ct);

        Assert.Empty(ergebnis);
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
