using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 21 — Kulturbestand/Ausfallquote/Flächenbelegung.</summary>
public sealed class GaertnereiReportingServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _artikelId;
    private int _feldId;
    private int _sektionAId;
    private int _sektionBId;
    private int _stufeJpId;
    private int _stufeVpId;

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

        var artikel = new Artikel { Artikelnummer = "ART-K", Bezeichnung = "Salvia", BotanischerName = "Salvia nemorosa", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var feld = new Lagerort { Code = "F1", Bezeichnung = "Feld Nord", IstFeld = true, BreiteMeter = 30, HoeheMeter = 20 };
        db.AddRange(artikel, feld);
        await db.SaveChangesAsync();

        // Sektion A 5x5=25qm, Sektion B 4x5=20qm -> belegt 45qm von 600qm Gesamtflaeche.
        var sektionA = new Sektion { LagerortId = feld.Id, Code = "A", Bezeichnung = "Sektion A", BreiteMeter = 5, HoeheMeter = 5 };
        var sektionB = new Sektion { LagerortId = feld.Id, Code = "B", Bezeichnung = "Sektion B", BreiteMeter = 4, HoeheMeter = 5 };
        var stufeJp = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#8BC34A" };
        var stufeVp = new Kulturstufe { Code = "VP", Bezeichnung = "Verkaufspflanze", Reihenfolge = 3, IstVerkaufsfaehig = true, FarbeHex = "#2E7D32" };
        db.AddRange(sektionA, sektionB, stufeJp, stufeVp);
        await db.SaveChangesAsync();

        _artikelId = artikel.Id;
        _feldId = feld.Id;
        _sektionAId = sektionA.Id;
        _sektionBId = sektionB.Id;
        _stufeJpId = stufeJp.Id;
        _stufeVpId = stufeVp.Id;

        await using var transaction = await db.Database.BeginTransactionAsync();
        // 1000 Zugang JP in Sektion A, davon 100 Ausfall -> Quote 10%. 300 Zugang VP in Sektion B.
        await BestandService.BucheBewegungAsync(db, _artikelId, _feldId, 1000m, LagerbewegungTyp.Kulturzugang, null, default, _sektionAId, _stufeJpId);
        await BestandService.BucheBewegungAsync(db, _artikelId, _feldId, -100m, LagerbewegungTyp.Ausfall, null, default, _sektionAId, _stufeJpId);
        await BestandService.BucheBewegungAsync(db, _artikelId, _feldId, 300m, LagerbewegungTyp.Kulturzugang, null, default, _sektionBId, _stufeVpId);
        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private ReportingService NeuerService() => new(new TestDbContextFactory(_options));

    [Fact]
    public async Task KulturbestandAsync_ListetJedeBestandszeile()
    {
        var ct = TestContext.Current.CancellationToken;
        var zeilen = await NeuerService().KulturbestandAsync(null, null, ct);

        Assert.Equal(2, zeilen.Count);
        Assert.Contains(zeilen, z => z.SektionId == _sektionAId && z.KulturstufeId == _stufeJpId && z.Menge == 900m);
        Assert.Contains(zeilen, z => z.SektionId == _sektionBId && z.KulturstufeId == _stufeVpId && z.Menge == 300m);
        Assert.All(zeilen, z => Assert.Equal("Salvia nemorosa", z.BotanischerName));
    }

    [Fact]
    public async Task KulturbestandAsync_FilterNachKulturstufe_SchraenktEin()
    {
        var ct = TestContext.Current.CancellationToken;
        var zeilen = await NeuerService().KulturbestandAsync(null, _stufeVpId, ct);

        var zeile = Assert.Single(zeilen);
        Assert.Equal(_sektionBId, zeile.SektionId);
    }

    [Fact]
    public async Task AusfallquoteAsync_BerechnetProzentJeStufe()
    {
        var ct = TestContext.Current.CancellationToken;
        var zeilen = await NeuerService().AusfallquoteAsync(DateOnly.MinValue, DateOnly.MaxValue, ct);

        var jpZeile = Assert.Single(zeilen, z => z.KulturstufeId == _stufeJpId);
        Assert.Equal(1000m, jpZeile.SummeZugaenge);
        Assert.Equal(100m, jpZeile.SummeAusfall);
        Assert.Equal(10.00m, jpZeile.AusfallquoteProzent);

        var vpZeile = Assert.Single(zeilen, z => z.KulturstufeId == _stufeVpId);
        Assert.Equal(0m, vpZeile.SummeAusfall);
        Assert.Equal(0m, vpZeile.AusfallquoteProzent);
    }

    [Fact]
    public async Task FlaechenbelegungAsync_BerechnetBelegteFlaecheGegenGesamtflaeche()
    {
        var ct = TestContext.Current.CancellationToken;
        var zeilen = await NeuerService().FlaechenbelegungAsync(ct);

        var zeile = Assert.Single(zeilen, z => z.FeldId == _feldId);
        Assert.Equal(600m, zeile.GesamtflaecheQm); // 30 x 20
        Assert.Equal(45m, zeile.BelegteFlaecheQm); // 25 + 20
        Assert.Equal(7.50m, zeile.BelegungsProzent);
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
