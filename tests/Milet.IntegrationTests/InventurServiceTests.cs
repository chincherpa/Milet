using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 12 (E10) — Inventur auf Feldern zählt je Bestandszeile (Artikel × Sektion × Stufe), das
/// Hauptlager bleibt unverändert (Regressionspfad). Ein Fehler hier würde Differenzen still auf eine
/// willkürliche Dimension buchen statt korrekt zuzuordnen — deshalb hier gezielt getestet.</summary>
public sealed class InventurServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _hauptlagerId;
    private int _feldId;
    private int _artikelHandelswareId;
    private int _artikelKulturId;
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
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        db.AddRange(einheit, mwst);
        await db.SaveChangesAsync();

        var hauptlager = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        var feld = new Lagerort { Code = "F1", Bezeichnung = "Feld Nord", IstFeld = true, BreiteMeter = 30, HoeheMeter = 20 };
        var artikelHandelsware = new Artikel { Artikelnummer = "ART-H", Bezeichnung = "Topf", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelKultur = new Artikel { Artikelnummer = "ART-K", Bezeichnung = "Salvia", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        db.AddRange(hauptlager, feld, artikelHandelsware, artikelKultur);
        await db.SaveChangesAsync();

        var sektionA = new Sektion { LagerortId = feld.Id, Code = "A", Bezeichnung = "Sektion A", BreiteMeter = 5, HoeheMeter = 5 };
        var sektionB = new Sektion { LagerortId = feld.Id, Code = "B", Bezeichnung = "Sektion B", BreiteMeter = 5, HoeheMeter = 5 };
        var stufeJp = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, FarbeHex = "#8BC34A" };
        var stufeTp = new Kulturstufe { Code = "TP", Bezeichnung = "Teenagerpflanze", Reihenfolge = 2, FarbeHex = "#4CAF50" };
        db.AddRange(sektionA, sektionB, stufeJp, stufeTp);
        await db.SaveChangesAsync();

        _hauptlagerId = hauptlager.Id;
        _feldId = feld.Id;
        _artikelHandelswareId = artikelHandelsware.Id;
        _artikelKulturId = artikelKultur.Id;
        _sektionAId = sektionA.Id;
        _sektionBId = sektionB.Id;
        _stufeJpId = stufeJp.Id;
        _stufeTpId = stufeTp.Id;

        await BestandService.BucheBewegungAsync(db, _artikelHandelswareId, _hauptlagerId, 50m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);

        await using var transaction = await db.Database.BeginTransactionAsync();
        await BestandService.BucheBewegungAsync(db, _artikelKulturId, _feldId, 500m, LagerbewegungTyp.Kulturzugang, null, CancellationToken.None, _sektionAId, _stufeJpId);
        await BestandService.BucheBewegungAsync(db, _artikelKulturId, _feldId, 200m, LagerbewegungTyp.Kulturzugang, null, CancellationToken.None, _sektionAId, _stufeTpId);
        await BestandService.BucheBewegungAsync(db, _artikelKulturId, _feldId, 80m, LagerbewegungTyp.Kulturzugang, null, CancellationToken.None, _sektionBId, _stufeTpId);
        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private InventurService NeuerService() => new(_factory, AllesErlaubtBerechtigungsService.Instanz);

    [Fact]
    public async Task NeueInventurAsync_Feld_ErzeugtEinePositionJeBestandszeile()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();

        var inventur = await service.NeueInventurAsync(_feldId, ct);

        Assert.Equal(3, inventur.Positionen.Count);
        Assert.Contains(inventur.Positionen, p => p.SektionId == _sektionAId && p.KulturstufeId == _stufeJpId && p.SollMenge == 500m);
        Assert.Contains(inventur.Positionen, p => p.SektionId == _sektionAId && p.KulturstufeId == _stufeTpId && p.SollMenge == 200m);
        Assert.Contains(inventur.Positionen, p => p.SektionId == _sektionBId && p.KulturstufeId == _stufeTpId && p.SollMenge == 80m);
    }

    [Fact]
    public async Task NeueInventurAsync_Hauptlager_UnveraendertJeArtikelEinePositionOhneDimensionen()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();

        var inventur = await service.NeueInventurAsync(_hauptlagerId, ct);

        var position = Assert.Single(inventur.Positionen, p => p.ArtikelId == _artikelHandelswareId);
        Assert.Equal(50m, position.SollMenge);
        Assert.Null(position.SektionId);
        Assert.Null(position.KulturstufeId);
    }

    [Fact]
    public async Task AbschliessenAsync_Feld_BuchtJedeDifferenzAufIhreDimension()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        var inventur = await service.NeueInventurAsync(_feldId, ct);

        var posSektionAJp = inventur.Positionen.First(p => p.SektionId == _sektionAId && p.KulturstufeId == _stufeJpId);
        var posSektionATp = inventur.Positionen.First(p => p.SektionId == _sektionAId && p.KulturstufeId == _stufeTpId);
        await service.ErfasseIstMengeAsync(posSektionAJp.Id, 480m, ct); // 20 Ausfall nicht erfasst
        await service.ErfasseIstMengeAsync(posSektionATp.Id, 200m, ct); // unverändert

        await service.AbschliessenAsync(inventur.Id, ct);

        await using var db = new MiletDbContext(_options);
        var bestandAJp = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeJpId, ct);
        var bestandATp = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionAId && b.KulturstufeId == _stufeTpId, ct);
        var bestandBTp = await db.ArtikelBestaende.FirstAsync(b => b.SektionId == _sektionBId && b.KulturstufeId == _stufeTpId, ct);
        Assert.Equal(480m, bestandAJp.Menge);
        Assert.Equal(200m, bestandATp.Menge); // unverändert, keine Korrekturbuchung nötig
        Assert.Equal(80m, bestandBTp.Menge); // von der Zählung unberührt
    }

    [Fact]
    public async Task AbschliessenAsync_BestandHatSichSeitBeginnGeaendert_WirftMitSektionsUndStufenbezug()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();
        var inventur = await service.NeueInventurAsync(_feldId, ct);

        var posSektionAJp = inventur.Positionen.First(p => p.SektionId == _sektionAId && p.KulturstufeId == _stufeJpId);
        await service.ErfasseIstMengeAsync(posSektionAJp.Id, 480m, ct);

        // Während der Zählung wird zusätzlich umgebucht — Drift auf genau dieser Dimension.
        await using (var db = new MiletDbContext(_options))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await BestandService.BucheBewegungAsync(db, _artikelKulturId, _feldId, -10m, LagerbewegungTyp.Ausfall, null, ct, _sektionAId, _stufeJpId);
            await transaction.CommitAsync(ct);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AbschliessenAsync(inventur.Id, ct));
        Assert.Contains($"Sektion-Id {_sektionAId}", ex.Message);
        Assert.Contains($"Kulturstufe-Id {_stufeJpId}", ex.Message);
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
