using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class StornoServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
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

        var jahr = DateTime.UtcNow.Year;
        db.Nummernkreise.AddRange(
            new Nummernkreis { Code = "RE", Jahr = jahr, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" },
            new Nummernkreis { Code = "LS", Jahr = jahr, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" },
            new Nummernkreis { Code = "WE", Jahr = jahr, NaechsteNummer = 1, Format = "WE-{1}-{0:0000}" },
            new Nummernkreis { Code = "GS", Jahr = jahr, NaechsteNummer = 1, Format = "GS-{1}-{0:0000}" });

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        var lieferant = new Lieferant { Lieferantennummer = "LF-TEST", Adresse = new() { Name1 = "Testlieferant" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, kunde, lieferant, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelSerial = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Serienartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id, HatSeriennummern = true };
        db.AddRange(artikel, artikelSerial);
        await db.SaveChangesAsync();

        _kundeId = kunde.Id;
        _lieferantId = lieferant.Id;
        _artikelId = artikel.Id;
        _artikelSerialisiertId = artikelSerial.Id;
        _lagerortId = lagerort.Id;

        await BestandService.BucheBewegungAsync(db, _artikelId, _lagerortId, 20m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> GebuchteRechnungAsync(CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var rechnung = new Rechnung
        {
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 2, Einzelpreis = 10m, GesamtNetto = 20m, MwStSatzWert = 19m, ArtikelId = _artikelId }],
            SummeNetto = 20m,
            SummeMwSt = 3.80m,
            SummeBrutto = 23.80m,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);

        var buchen = new RechnungBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gebucht = await buchen.BuchenAsync(rechnung.Id, ct);
        return gebucht.Id;
    }

    private async Task<int> GebuchterLieferscheinAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
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

        var buchen = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gebucht = await buchen.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct);
        return gebucht.Id;
    }

    private async Task<(int WareneingangId, int PositionId)> GebuchterWareneingangAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var lieferant = await db.Lieferanten.FirstAsync(l => l.Id == _lieferantId, ct);
        var wareneingang = new Wareneingang
        {
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = lieferant.Id,
            RechnungsadresseSnapshot = lieferant.Adresse.Kopie(),
            LieferadresseSnapshot = lieferant.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Test", Menge = menge, Einzelpreis = 8m, GesamtNetto = menge * 8m,
                MwStSatzWert = 19m, ArtikelId = artikelId, LagerortId = _lagerortId,
            }],
        };
        db.Add(wareneingang);
        await db.SaveChangesAsync(ct);

        var buchen = new WareneingangBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gebucht = await buchen.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>>(), ct);
        return (gebucht.Id, gebucht.Positionen[0].Id);
    }

    [Fact]
    public async Task StorniereRechnungAsync_UnbezahlteRechnung_ErzeugtGutschriftUndGleichtOPAus()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungId = await GebuchteRechnungAsync(ct);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gutschrift = await service.StorniereRechnungAsync(rechnungId, "Testgrund", ct);

        Assert.Equal(BelegTyp.Gutschrift, gutschrift.BelegTyp);
        Assert.Equal(rechnungId, gutschrift.StorniertenBelegId);
        Assert.Equal(BelegStatus.Gebucht, gutschrift.Status);
        Assert.Equal(23.80m, gutschrift.SummeBrutto);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        Assert.Equal(BelegStatus.Storniert, rechnung.Status);

        var alleOffenenPosten = await db.OffenePosten.Where(o => o.BelegId == rechnungId || o.Beleg!.StorniertenBelegId == rechnungId).ToListAsync(ct);
        Assert.Equal(2, alleOffenenPosten.Count);
        Assert.All(alleOffenenPosten, o => Assert.Equal(OffenerPostenStatus.Ausgeglichen, o.Status));
        Assert.All(alleOffenenPosten, o => Assert.Equal(0m, o.OffenerBetrag));
        Assert.Contains(alleOffenenPosten, o => o.Betrag == -23.80m);
    }

    [Fact]
    public async Task StorniereRechnungAsync_StornierteRechnung_KannNichtMehrGeaendertWerden()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungId = await GebuchteRechnungAsync(ct);
        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await service.StorniereRechnungAsync(rechnungId, "Testgrund", ct);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        rechnung.Kopftext = "Nachträgliche Änderung";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task StorniereRechnungAsync_BereitsBezahlt_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungId = await GebuchteRechnungAsync(ct);

        await using (var db = new MiletDbContext(_options))
        {
            var op = await db.OffenePosten.FirstAsync(o => o.BelegId == rechnungId, ct);
            op.OffenerBetrag -= 5m;
            op.Status = OffenerPostenStatus.TeilweiseBezahlt;
            await db.SaveChangesAsync(ct);
        }

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StorniereRechnungAsync(rechnungId, "Testgrund", ct));
        Assert.Contains("bereits", ex.Message);
    }

    [Fact]
    public async Task StorniereRechnungAsync_ParallelesDoppelStorno_NurEinerGewinnt()
    {
        var ct = TestContext.Current.CancellationToken;
        var rechnungId = await GebuchteRechnungAsync(ct);
        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var ergebnisse = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await service.StorniereRechnungAsync(rechnungId, "Testgrund", ct); return true; }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateConcurrencyException) { return false; }
        }));

        Assert.Equal(1, ergebnisse.Count(erfolg => erfolg));

        await using var db = new MiletDbContext(_options);
        Assert.Equal(1, await db.Gutschriften.CountAsync(g => g.StorniertenBelegId == rechnungId, ct));
    }

    [Fact]
    public async Task StorniereLieferscheinAsync_BuchtBestandZurueckUndSetztStorniert()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferscheinId = await GebuchterLieferscheinAsync(_artikelId, 5, ct);

        await using (var db = new MiletDbContext(_options))
            Assert.Equal(15m, (await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId, ct)).Menge);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var storniert = await service.StorniereLieferscheinAsync(lieferscheinId, "Rückläufer", ct);

        Assert.Equal(BelegStatus.Storniert, storniert.Status);
        Assert.Contains("Rückläufer", storniert.Fusstext);

        await using var nachDb = new MiletDbContext(_options);
        Assert.Equal(20m, (await nachDb.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId, ct)).Menge);
    }

    [Fact]
    public async Task StorniereLieferscheinAsync_SerialisierterArtikel_SetztSeriennummernAufLagerZurueck()
    {
        var ct = TestContext.Current.CancellationToken;
        int seriennummerId;
        await using (var db = new MiletDbContext(_options))
        {
            var seriennummer = new Seriennummer { ArtikelId = _artikelSerialisiertId, Nummer = "SN-STORNO-1", Status = SeriennummerStatus.AufLager, LagerortId = _lagerortId };
            db.Add(seriennummer);
            await db.SaveChangesAsync(ct);
            await BestandService.BucheBewegungAsync(db, _artikelSerialisiertId, _lagerortId, 1m, LagerbewegungTyp.Korrektur, null, ct);
            seriennummerId = seriennummer.Id;
        }

        int lieferscheinId;
        int positionId;
        await using (var db = new MiletDbContext(_options))
        {
            var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
            var lieferschein = new Lieferschein
            {
                BelegDatum = DateOnly.FromDateTime(DateTime.Today),
                KundeId = kunde.Id,
                RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
                LieferadresseSnapshot = kunde.Adresse.Kopie(),
                Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Serie", Menge = 1, Einzelpreis = 10m, GesamtNetto = 10m, MwStSatzWert = 19m, ArtikelId = _artikelSerialisiertId, LagerortId = _lagerortId }],
            };
            db.Add(lieferschein);
            await db.SaveChangesAsync(ct);
            lieferscheinId = lieferschein.Id;
            positionId = lieferschein.Positionen[0].Id;
        }

        var buchen = new LieferscheinBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await buchen.BuchenAsync(lieferscheinId, new Dictionary<int, IReadOnlyList<int>> { [positionId] = [seriennummerId] }, ct);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await service.StorniereLieferscheinAsync(lieferscheinId, "Rückläufer", ct);

        await using var nachDb = new MiletDbContext(_options);
        var seriennummerNachStorno = await nachDb.Seriennummern.FirstAsync(s => s.Id == seriennummerId, ct);
        Assert.Equal(SeriennummerStatus.AufLager, seriennummerNachStorno.Status);
        Assert.Equal(_lagerortId, seriennummerNachStorno.LagerortId);
    }

    [Fact]
    public async Task StorniereLieferscheinAsync_MitAktivemFolgebeleg_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferscheinId = await GebuchterLieferscheinAsync(_artikelId, 5, ct);

        await using (var db = new MiletDbContext(_options))
        {
            var lieferschein = await db.Lieferscheine.Include(l => l.Positionen).FirstAsync(l => l.Id == lieferscheinId, ct);
            var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
            var rechnung = new Rechnung
            {
                BelegDatum = DateOnly.FromDateTime(DateTime.Today),
                KundeId = kunde.Id,
                RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
                LieferadresseSnapshot = kunde.Adresse.Kopie(),
                Positionen = [new BelegPosition
                {
                    PositionsNr = 1, Bezeichnung = "Test", Menge = 5, Einzelpreis = 10m, GesamtNetto = 50m,
                    MwStSatzWert = 19m, ArtikelId = _artikelId, UrsprungsPositionId = lieferschein.Positionen[0].Id,
                }],
            };
            db.Add(rechnung);
            await db.SaveChangesAsync(ct);
        }

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StorniereLieferscheinAsync(lieferscheinId, "Testgrund", ct));
        Assert.Contains("Folgebeleg", ex.Message);
    }

    [Fact]
    public async Task StorniereWareneingangAsync_BuchtBestandZurueckUndSetztStorniert()
    {
        var ct = TestContext.Current.CancellationToken;
        var (wareneingangId, _) = await GebuchterWareneingangAsync(_artikelId, 5, ct);

        await using (var db = new MiletDbContext(_options))
            Assert.Equal(25m, (await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId, ct)).Menge);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var storniert = await service.StorniereWareneingangAsync(wareneingangId, "Falschlieferung", ct);

        Assert.Equal(BelegStatus.Storniert, storniert.Status);

        await using var nachDb = new MiletDbContext(_options);
        Assert.Equal(20m, (await nachDb.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId, ct)).Menge);
    }

    [Fact]
    public async Task StorniereWareneingangAsync_BereitsWeiterverkauft_WirftMitVerstaendlicherMeldung()
    {
        var ct = TestContext.Current.CancellationToken;
        var (wareneingangId, _) = await GebuchterWareneingangAsync(_artikelId, 5, ct);
        // Kompletter Bestand (20 Start + 5 Zugang) bis auf 1 verkauft — für den Storno von 5 reichen die
        // verbleibenden 1 nicht.
        await GebuchterLieferscheinAsync(_artikelId, 24, ct);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StorniereWareneingangAsync(wareneingangId, "Testgrund", ct));
        Assert.Contains("Bestand", ex.Message);
    }

    [Fact]
    public async Task StorniereWareneingangAsync_SeriennummernpflichtigerArtikel_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var (wareneingangId, _) = await GebuchterWareneingangMitSeriennummerAsync(ct);

        var service = new StornoService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StorniereWareneingangAsync(wareneingangId, "Testgrund", ct));
        Assert.Contains("Seriennummer", ex.Message);
    }

    private async Task<(int WareneingangId, int PositionId)> GebuchterWareneingangMitSeriennummerAsync(CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var lieferant = await db.Lieferanten.FirstAsync(l => l.Id == _lieferantId, ct);
        var wareneingang = new Wareneingang
        {
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = lieferant.Id,
            RechnungsadresseSnapshot = lieferant.Adresse.Kopie(),
            LieferadresseSnapshot = lieferant.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Serie", Menge = 1, Einzelpreis = 8m, GesamtNetto = 8m,
                MwStSatzWert = 19m, ArtikelId = _artikelSerialisiertId, LagerortId = _lagerortId,
            }],
        };
        db.Add(wareneingang);
        await db.SaveChangesAsync(ct);

        var buchen = new WareneingangBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gebucht = await buchen.BuchenAsync(
            wareneingang.Id,
            new Dictionary<int, IReadOnlyList<string>> { [wareneingang.Positionen[0].Id] = ["SN-WE-1"] }, ct);
        return (gebucht.Id, gebucht.Positionen[0].Id);
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
