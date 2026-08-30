using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 10 — reine Lesepfade (Pflanzenliste/Vorkommen/Historie). Vor allem eine Übersetzungsprobe:
/// GroupBy über Navigationseigenschaften (Kulturstufe.Bezeichnung/FarbeHex/Reihenfolge) kann bei falscher
/// Formulierung stillschweigend auf Client-Evaluation zurückfallen oder eine InvalidOperationException werfen.</summary>
public sealed class KulturBestandServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _mitBestandId;
    private int _ohneBestandId;
    private int _stufeJpId;
    private int _stufeVpId;
    private int _sektionId;

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

        var mitBestand = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Salvia nemorosa", BotanischerName = "Salvia nemorosa", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var ohneBestand = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Echinacea", BotanischerName = "Echinacea purpurea", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var feld = new Lagerort { Code = "F1", Bezeichnung = "Feld Nord", IstFeld = true, BreiteMeter = 30, HoeheMeter = 20 };
        db.AddRange(mitBestand, ohneBestand, feld);
        await db.SaveChangesAsync();

        var sektion = new Sektion { LagerortId = feld.Id, Code = "A1", Bezeichnung = "Sektion A1", BreiteMeter = 5, HoeheMeter = 5 };
        var stufeJp = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#8BC34A" };
        var stufeVp = new Kulturstufe { Code = "VP", Bezeichnung = "Verkaufspflanze", Reihenfolge = 3, IstVerkaufsfaehig = true, FarbeHex = "#2E7D32" };
        db.AddRange(sektion, stufeJp, stufeVp);
        await db.SaveChangesAsync();

        _mitBestandId = mitBestand.Id;
        _ohneBestandId = ohneBestand.Id;
        _stufeJpId = stufeJp.Id;
        _stufeVpId = stufeVp.Id;
        _sektionId = sektion.Id;

        await using var transaction = await db.Database.BeginTransactionAsync();
        await BestandService.BucheBewegungAsync(db, _mitBestandId, feld.Id, 500m, LagerbewegungTyp.Kulturzugang, null, default, sektion.Id, stufeJp.Id);
        await BestandService.BucheBewegungAsync(db, _mitBestandId, feld.Id, 80m, LagerbewegungTyp.Kulturzugang, null, default, sektion.Id, stufeVp.Id);
        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task LadePflanzenAsync_ArtikelOhneBestand_ErscheintMitMengeNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new KulturBestandService(new TestDbContextFactory(_options));

        var pflanzen = await service.LadePflanzenAsync(null, ct);

        var mit = Assert.Single(pflanzen, p => p.ArtikelId == _mitBestandId);
        Assert.Equal(580m, mit.GesamtMenge);
        Assert.Equal(2, mit.JeStufe.Count);
        Assert.Equal(_stufeJpId, mit.JeStufe[0].KulturstufeId);
        Assert.Equal(_stufeVpId, mit.JeStufe[1].KulturstufeId);

        var ohne = Assert.Single(pflanzen, p => p.ArtikelId == _ohneBestandId);
        Assert.Equal(0m, ohne.GesamtMenge);
        Assert.Empty(ohne.JeStufe);
    }

    [Fact]
    public async Task LadeVorkommenAsync_SortiertNachStufenReihenfolge()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new KulturBestandService(new TestDbContextFactory(_options));

        var vorkommen = await service.LadeVorkommenAsync(_mitBestandId, ct);

        Assert.Equal(2, vorkommen.Count);
        Assert.Equal(_stufeJpId, vorkommen[0].KulturstufeId);
        Assert.Equal(_stufeVpId, vorkommen[1].KulturstufeId);
        Assert.Equal(500m, vorkommen[0].Menge);
        Assert.Equal(80m, vorkommen[1].Menge);
    }

    [Fact]
    public async Task LadeHistorieAsync_ListetBeideZugaengeAbsteigend()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new KulturBestandService(new TestDbContextFactory(_options));

        var historie = await service.LadeHistorieAsync(_mitBestandId, null, null, null, ct);

        Assert.Equal(2, historie.Count);
        Assert.All(historie, h => Assert.Equal("Kulturzugang", h.Typ));
    }

    [Fact]
    public async Task LadeHistorieAsync_FilterAufSektion_SchraenktEin()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new KulturBestandService(new TestDbContextFactory(_options));

        var historie = await service.LadeHistorieAsync(_mitBestandId, _sektionId, null, null, ct);

        Assert.Equal(2, historie.Count);
        Assert.All(historie, h => Assert.Equal("Sektion A1", h.SektionBezeichnung));
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
