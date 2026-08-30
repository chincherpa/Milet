using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 6 — die vier Dimensionen (Artikel, Lagerort, Sektion, Kulturstufe) im einzigen Schreibpfad
/// auf Bestand (<see cref="BestandService.BucheBewegungAsync"/>). Deckt den in E4 gefundenen Altbestandsfehler
/// (paralleles Erstanlegen) und die NULL-sichere Negativsperre je Dimension ab.</summary>
public sealed class BestandServiceKulturDimensionenTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _kulturArtikelId;
    private int _handelswareArtikelId;
    private int _hauptlagerId;
    private int _feldId;
    private int _sektionAId;
    private int _sektionBId;
    private int _stufeJpId;
    private int _stufeTpId;

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

        var kulturArtikel = new Artikel { Artikelnummer = "ART-KULTUR", Bezeichnung = "Salvia", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var handelsware = new Artikel { Artikelnummer = "ART-HANDEL", Bezeichnung = "Topf", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var hauptlager = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        var feld = new Lagerort { Code = "F1", Bezeichnung = "Feld Nord", IstFeld = true, BreiteMeter = 30m, HoeheMeter = 20m };
        db.AddRange(kulturArtikel, handelsware, hauptlager, feld);
        await db.SaveChangesAsync();

        var sektionA = new Sektion { LagerortId = feld.Id, Code = "A", Bezeichnung = "Sektion A", PosXMeter = 0, PosYMeter = 0, BreiteMeter = 5, HoeheMeter = 5 };
        var sektionB = new Sektion { LagerortId = feld.Id, Code = "B", Bezeichnung = "Sektion B", PosXMeter = 10, PosYMeter = 0, BreiteMeter = 5, HoeheMeter = 5 };
        var stufeJp = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#8BC34A" };
        var stufeTp = new Kulturstufe { Code = "TP", Bezeichnung = "Teenagerpflanze", Reihenfolge = 2, FarbeHex = "#4CAF50" };
        db.AddRange(sektionA, sektionB, stufeJp, stufeTp);
        await db.SaveChangesAsync();

        _kulturArtikelId = kulturArtikel.Id;
        _handelswareArtikelId = handelsware.Id;
        _hauptlagerId = hauptlager.Id;
        _feldId = feld.Id;
        _sektionAId = sektionA.Id;
        _sektionBId = sektionB.Id;
        _stufeJpId = stufeJp.Id;
        _stufeTpId = stufeTp.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task DimensionsloseBuchung_SchreibtWeiterhinGenauEineZeileFort()
    {
        var ct = TestContext.Current.CancellationToken;

        await BuchenAsync(_handelswareArtikelId, _hauptlagerId, 10m, null, null, ct);
        await BuchenAsync(_handelswareArtikelId, _hauptlagerId, 5m, null, null, ct);

        await using var db = new MiletDbContext(_options);
        var zeilen = await db.ArtikelBestaende.Where(b => b.ArtikelId == _handelswareArtikelId).ToListAsync(ct);
        Assert.Single(zeilen);
        Assert.Equal(15m, zeilen[0].Menge);
        Assert.Null(zeilen[0].SektionId);
        Assert.Null(zeilen[0].KulturstufeId);
    }

    [Fact]
    public async Task ParalleleErstbuchungen_GleicheKombination_EineZeileMengeIstSumme()
    {
        var ct = TestContext.Current.CancellationToken;

        var aufgaben = Enumerable.Range(0, 10)
            .Select(_ => BuchenAsync(_kulturArtikelId, _feldId, 10m, _sektionAId, _stufeJpId, ct));
        await Task.WhenAll(aufgaben);

        await using var db = new MiletDbContext(_options);
        var zeilen = await db.ArtikelBestaende
            .Where(b => b.ArtikelId == _kulturArtikelId && b.LagerortId == _feldId && b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId)
            .ToListAsync(ct);

        Assert.Single(zeilen);
        Assert.Equal(100m, zeilen[0].Menge);
    }

    [Fact]
    public async Task Negativsperre_ProDimension_AndereKombinationBleibtUnberuehrt()
    {
        var ct = TestContext.Current.CancellationToken;

        await BuchenAsync(_kulturArtikelId, _feldId, 100m, _sektionAId, _stufeJpId, ct);
        await BuchenAsync(_kulturArtikelId, _feldId, 50m, _sektionBId, _stufeJpId, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuchenAsync(_kulturArtikelId, _feldId, -101m, _sektionAId, _stufeJpId, ct));

        // Sektion B hat eigenen Bestand und ist von der Negativsperre auf Sektion A nicht betroffen.
        await BuchenAsync(_kulturArtikelId, _feldId, -50m, _sektionBId, _stufeJpId, ct);

        await using var db = new MiletDbContext(_options);
        var bestandA = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        var bestandB = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionBId && b.KulturstufeId == _stufeJpId, ct);
        Assert.Equal(100m, bestandA.Menge);
        Assert.Equal(0m, bestandB.Menge);
    }

    [Fact]
    public async Task Regelverletzung_KulturpflanzeOhneKulturstufe_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuchenAsync(_kulturArtikelId, _feldId, 10m, _sektionAId, null, ct));
    }

    [Fact]
    public async Task Regelverletzung_HandelswareMitKulturstufe_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuchenAsync(_handelswareArtikelId, _hauptlagerId, 10m, null, _stufeJpId, ct));
    }

    [Fact]
    public async Task LedgerInvariante_SummeJeVierDimensionenGleichSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;

        await BuchenAsync(_kulturArtikelId, _feldId, 200m, _sektionAId, _stufeJpId, ct);
        await BuchenAsync(_kulturArtikelId, _feldId, -50m, _sektionAId, _stufeJpId, ct);
        await BuchenAsync(_kulturArtikelId, _feldId, 30m, _sektionBId, _stufeTpId, ct);

        await using var db = new MiletDbContext(_options);
        var bestaende = await db.ArtikelBestaende.Where(b => b.ArtikelId == _kulturArtikelId).ToListAsync(ct);
        foreach (var bestand in bestaende)
        {
            var ledgerSumme = await db.Lagerbewegungen
                .Where(l => l.ArtikelId == bestand.ArtikelId && l.LagerortId == bestand.LagerortId
                    && l.SektionId == bestand.SektionId && l.KulturstufeId == bestand.KulturstufeId)
                .SumAsync(l => l.Menge, ct);
            Assert.Equal(ledgerSumme, bestand.Menge);
        }
    }

    [Fact]
    public async Task SucheAsync_KulturartikelMitMehrerenDimensionen_LiefertEineZeileJeKombination()
    {
        var ct = TestContext.Current.CancellationToken;
        await BuchenAsync(_kulturArtikelId, _feldId, 200m, _sektionAId, _stufeJpId, ct);
        await BuchenAsync(_kulturArtikelId, _feldId, 30m, _sektionBId, _stufeTpId, ct);
        await BuchenAsync(_handelswareArtikelId, _hauptlagerId, 15m, null, null, ct);

        var service = new BestandService(new TestDbContextFactory(_options), AllesErlaubtBerechtigungsService.Instanz);
        var ergebnis = await service.SucheAsync(null, ct);

        var kulturZeilen = ergebnis.Where(b => b.ArtikelId == _kulturArtikelId && b.LagerortId == _feldId).ToList();
        Assert.Equal(2, kulturZeilen.Count);
        Assert.Contains(kulturZeilen, z => z.SektionId == _sektionAId && z.KulturstufeId == _stufeJpId && z.Menge == 200m && z.IstKulturpflanze);
        Assert.Contains(kulturZeilen, z => z.SektionId == _sektionBId && z.KulturstufeId == _stufeTpId && z.Menge == 30m);

        var handelswareZeile = Assert.Single(ergebnis, b => b.ArtikelId == _handelswareArtikelId && b.LagerortId == _hauptlagerId);
        Assert.Null(handelswareZeile.SektionId);
        Assert.Null(handelswareZeile.KulturstufeId);
        Assert.False(handelswareZeile.IstKulturpflanze);
        Assert.Equal(15m, handelswareZeile.Menge);
    }

    private async Task BuchenAsync(int artikelId, int lagerortId, decimal delta, int? sektionId, int? kulturstufeId, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, delta, LagerbewegungTyp.Kulturzugang, null, ct, sektionId, kulturstufeId);
        await transaction.CommitAsync(ct);
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
