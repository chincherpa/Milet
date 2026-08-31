using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Reproduziert und verifiziert den Fix für den seit Phase 3 in STATUS.md geführten
/// READ-COMMITTED-Verdacht (Plan Phase 9, Task 20/21): zwei parallele Teillieferungen desselben
/// Auftrags lasen beide "voll offen" und committeten beide, weil keine der Lesestellen den Quellbeleg
/// sperrte.</summary>
public sealed class BelegUeberleitungRaceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
    private int _artikelId;
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
        db.Nummernkreise.Add(new Nummernkreis { Code = "LS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" });
        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, kunde, lagerort);
        await db.SaveChangesAsync();
        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Testartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        db.Add(artikel);
        await db.SaveChangesAsync();

        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeuerAuftragAsync(decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var auftrag = new Auftrag
        {
            BelegNummer = $"AU-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Testartikel", Menge = menge, Einzelpreis = 10m, GesamtNetto = menge * 10m,
                MwStSatzWert = 19m, ArtikelId = _artikelId,
            }],
        };
        db.Add(auftrag);
        await db.SaveChangesAsync(ct);
        return auftrag.Id;
    }

    [Fact]
    public async Task ParalleleTeillieferungenDesselbenAuftrags_NieMehrAlsDieOffeneMenge()
    {
        var ct = TestContext.Current.CancellationToken;
        var auftragId = await NeuerAuftragAsync(10m, ct);
        var quellPositionId = (await new MiletDbContext(_options).Auftraege.Include(a => a.Positionen).FirstAsync(a => a.Id == auftragId, ct)).Positionen[0].Id;
        var service = new BelegUeberleitungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        // Beide Aufrufe fragen bewusst nur eine TEIL-Menge (6 von 10) an: bleibt quellVollstaendigUebernommen
        // dabei false, ändert keiner der beiden Aufrufe den Status des Auftrags selbst — ein voller Abruf
        // (10 von 10) würde stattdessen zufällig über den RowVersion-Konflikt auf dem Auftragsstatus
        // geschützt und den eigentlichen Bug (stale offene Menge) verdecken. Mit der UPDLOCK/HOLDLOCK-Sperre
        // (Task 21) laufen beide Aufrufe serialisiert: der zweite sieht die vom ersten committete Folgemenge
        // und lehnt korrekt mit "offene Menge (4)" ab, statt (wie vor dem Fix) ebenfalls 6 zu liefern und
        // damit 12 gegen einen Auftrag über 10 zu erzeugen.
        var ergebnisse = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await service.UeberleitenMitAuswahlAsync(
                    auftragId, BelegTyp.Lieferschein, new Dictionary<int, decimal> { [quellPositionId] = 6m }, _lagerortId, ct: ct);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));

        Assert.Equal(1, ergebnisse.Count(erfolg => erfolg));

        await using var db = new MiletDbContext(_options);
        var gesamtGeliefert = await db.BelegPositionen
            .Where(p => p.UrsprungsPositionId == quellPositionId)
            .SumAsync(p => p.Menge, ct);

        Assert.True(gesamtGeliefert <= 10m, $"Es wurden {gesamtGeliefert} Stück gegen einen Auftrag über 10 Stück geliefert (Doppellieferung durch Race).");
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
