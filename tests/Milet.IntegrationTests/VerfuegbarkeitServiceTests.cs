using Microsoft.EntityFrameworkCore;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Task 13 (E8) — Verfügbarkeit ist beratend, nicht sperrend; Reservierung wird berechnet.</summary>
public sealed class VerfuegbarkeitServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
    private int _artikelId;
    private int _feldId;
    private int _sektionId;
    private int _stufeVerkaufsfaehigId;
    private int _stufeNichtVerkaufsfaehigId;

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
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        db.AddRange(einheit, mwst, kunde);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-K", Bezeichnung = "Salvia", EinheitId = einheit.Id, MwStSatzId = mwst.Id, IstKulturpflanze = true };
        var feld = new Lagerort { Code = "F1", Bezeichnung = "Feld Nord", IstFeld = true, BreiteMeter = 30, HoeheMeter = 20 };
        db.AddRange(artikel, feld);
        await db.SaveChangesAsync();

        var sektion = new Sektion { LagerortId = feld.Id, Code = "A1", Bezeichnung = "Sektion A1", BreiteMeter = 5, HoeheMeter = 5 };
        var stufeVerkaufsfaehig = new Kulturstufe { Code = "VP", Bezeichnung = "Verkaufspflanze", Reihenfolge = 3, IstVerkaufsfaehig = true, FarbeHex = "#2E7D32" };
        var stufeNichtVerkaufsfaehig = new Kulturstufe { Code = "JP", Bezeichnung = "Jungpflanze", Reihenfolge = 1, IstVerkaufsfaehig = false, FarbeHex = "#8BC34A" };
        db.AddRange(sektion, stufeVerkaufsfaehig, stufeNichtVerkaufsfaehig);
        await db.SaveChangesAsync();

        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
        _feldId = feld.Id;
        _sektionId = sektion.Id;
        _stufeVerkaufsfaehigId = stufeVerkaufsfaehig.Id;
        _stufeNichtVerkaufsfaehigId = stufeNichtVerkaufsfaehig.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private VerfuegbarkeitService NeuerService() => new(_factory, new KulturBestandService(_factory));

    private async Task ZugangAsync(int? kulturstufeId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(db, _artikelId, _feldId, menge, LagerbewegungTyp.Kulturzugang, null, ct, _sektionId, kulturstufeId);
        await transaction.CommitAsync(ct);
    }

    private async Task<Auftrag> NeuerAuftragAsync(decimal menge, BelegStatus status, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var auftrag = new Auftrag
        {
            BelegNummer = $"AU-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            Status = status,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Salvia", Menge = menge, Einzelpreis = 5m, GesamtNetto = menge * 5m,
                MwStSatzWert = 19m, ArtikelId = _artikelId,
            }],
        };
        db.Add(auftrag);
        await db.SaveChangesAsync(ct);
        return auftrag;
    }

    [Fact]
    public async Task LadeAsync_NurVerkaufsfaehigerBestand_ZaehltAlsVerkaufsfaehig()
    {
        var ct = TestContext.Current.CancellationToken;
        await ZugangAsync(_stufeVerkaufsfaehigId, 200m, ct);
        await ZugangAsync(_stufeNichtVerkaufsfaehigId, 500m, ct);
        var service = NeuerService();

        var verfuegbarkeit = await service.LadeAsync(_artikelId, 100m, ct);

        Assert.Equal(200m, verfuegbarkeit.VerkaufsfaehigGesamt);
        Assert.Equal(200m, verfuegbarkeit.Frei);
        Assert.Equal(VerfuegbarkeitAmpel.Gruen, verfuegbarkeit.Ampel);
        Assert.Single(verfuegbarkeit.NichtVerkaufsfaehig);
        Assert.Equal(500m, verfuegbarkeit.NichtVerkaufsfaehig[0].Menge);
    }

    [Fact]
    public async Task LadeAsync_ReservierungReduziertFrei()
    {
        var ct = TestContext.Current.CancellationToken;
        await ZugangAsync(_stufeVerkaufsfaehigId, 200m, ct);
        await NeuerAuftragAsync(150m, BelegStatus.Gebucht, ct);
        var service = NeuerService();

        var verfuegbarkeit = await service.LadeAsync(_artikelId, 100m, ct);

        Assert.Equal(200m, verfuegbarkeit.VerkaufsfaehigGesamt);
        Assert.Equal(150m, verfuegbarkeit.Reserviert);
        Assert.Equal(50m, verfuegbarkeit.Frei);
        Assert.Equal(VerfuegbarkeitAmpel.Gelb, verfuegbarkeit.Ampel); // 50 frei < 100 benötigt, aber verkaufsfähiger Bestand vorhanden
    }

    [Fact]
    public async Task LadeAsync_TeilgelieferterAuftrag_ReserviertNurRestmenge()
    {
        var ct = TestContext.Current.CancellationToken;
        await ZugangAsync(_stufeVerkaufsfaehigId, 200m, ct);
        var auftrag = await NeuerAuftragAsync(150m, BelegStatus.Gebucht, ct);

        // Teillieferung von 100 der 150 -> Folgeposition mit UrsprungsPositionId, offene Menge sinkt auf 50.
        await using (var db = new MiletDbContext(_options))
        {
            var lieferschein = new Lieferschein
            {
                BelegNummer = $"LS-{Guid.NewGuid():N}"[..12],
                BelegDatum = DateOnly.FromDateTime(DateTime.Today),
                KundeId = _kundeId,
                Status = BelegStatus.Gebucht,
                RechnungsadresseSnapshot = (await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct)).Adresse.Kopie(),
                LieferadresseSnapshot = (await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct)).Adresse.Kopie(),
                Positionen = [new BelegPosition
                {
                    PositionsNr = 1, Bezeichnung = "Salvia", Menge = 100m, Einzelpreis = 5m, GesamtNetto = 500m,
                    MwStSatzWert = 19m, ArtikelId = _artikelId, UrsprungsPositionId = auftrag.Positionen[0].Id,
                }],
            };
            db.Add(lieferschein);
            await db.SaveChangesAsync(ct);
        }

        var service = NeuerService();
        var verfuegbarkeit = await service.LadeAsync(_artikelId, 10m, ct);

        Assert.Equal(50m, verfuegbarkeit.Reserviert);
        Assert.Equal(150m, verfuegbarkeit.Frei);
    }

    [Fact]
    public async Task LadeAsync_NurVorstufenbestand_MachtGelbNichtGruen()
    {
        var ct = TestContext.Current.CancellationToken;
        await ZugangAsync(_stufeNichtVerkaufsfaehigId, 500m, ct);
        var service = NeuerService();

        var verfuegbarkeit = await service.LadeAsync(_artikelId, 50m, ct);

        Assert.Equal(0m, verfuegbarkeit.VerkaufsfaehigGesamt);
        Assert.Equal(VerfuegbarkeitAmpel.Gelb, verfuegbarkeit.Ampel);
    }

    [Fact]
    public async Task LadeAsync_KeinBestandUeberhaupt_Rot()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NeuerService();

        var verfuegbarkeit = await service.LadeAsync(_artikelId, 10m, ct);

        Assert.Equal(VerfuegbarkeitAmpel.Rot, verfuegbarkeit.Ampel);
    }

    [Fact]
    public async Task LadeFuerBelegAsync_GesamtampelIstSchlechtesteEinzelampel()
    {
        var ct = TestContext.Current.CancellationToken;
        await ZugangAsync(_stufeVerkaufsfaehigId, 5m, ct); // reicht nicht für Menge 150 -> Gelb
        var auftrag = await NeuerAuftragAsync(150m, BelegStatus.Entwurf, ct);
        var service = NeuerService();

        var ergebnis = await service.LadeFuerBelegAsync(auftrag.Id, ct);

        Assert.Equal(VerfuegbarkeitAmpel.Gelb, ergebnis.GesamtAmpel);
        Assert.Single(ergebnis.JePosition);
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
