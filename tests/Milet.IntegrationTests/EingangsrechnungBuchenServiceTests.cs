using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class EingangsrechnungBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _lieferantId;
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

        db.Nummernkreise.Add(new Nummernkreis { Code = "ER", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "ER-{1}-{0:0000}" });

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var lieferant = new Lieferant { Lieferantennummer = "LF-TEST", Adresse = new() { Name1 = "Testlieferant" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, lieferant, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        db.Add(artikel);
        await db.SaveChangesAsync();

        _lieferantId = lieferant.Id;
        _artikelId = artikel.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    /// <summary>Baut die für beide Testfälle gemeinsame Kette auf: ein Wareneingang mit einer Artikelposition
    /// (Menge 1, Einzelpreis 100 -> SummeBrutto 119,00 bei 19% MwSt) wird über WareneingangBuchenService gebucht
    /// und per BelegUeberleitungService.UeberleitenAsync 1:1 in eine Eingangsrechnung überführt (Positionen mit
    /// gesetzter UrsprungsPositionId, unveränderter Preis) — Grundlage, um danach optional eine Abweichung zu erzeugen.</summary>
    private async Task<int> NeueUnveraenderteEingangsrechnungAsync(CancellationToken ct)
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
                PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 100m, GesamtNetto = 100m,
                MwStSatzWert = 19m, ArtikelId = _artikelId, LagerortId = _lagerortId,
            }],
        };
        // Kopfsummen (SummeNetto/SummeMwSt/SummeBrutto/Steuersummen) sind plain properties ohne berechneten
        // Getter (Beleg.cs) — sie werden von keiner Stelle unterhalb (WareneingangBuchenService, Interceptors,
        // ToDto) automatisch befüllt. Ohne diese Berechnung bliebe SummeBrutto beim Default 0m, wodurch
        // EingangsrechnungBuchenService (das genau diese Spalte als erwarteterBetrag liest) fälschlich eine
        // Abweichung meldet. Gleiche Berechnung wie in VerteuerePositionAsync und in BelegUeberleitungService selbst.
        var steuersummen = SteuerRechner.BerechneSteuersummen(wareneingang.Positionen);
        wareneingang.Steuersummen = steuersummen.ToList();
        (wareneingang.SummeNetto, wareneingang.SummeMwSt, wareneingang.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);
        db.Add(wareneingang);
        await db.SaveChangesAsync(ct);

        var wareneingangBuchenService = new WareneingangBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        await wareneingangBuchenService.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>>(), ct);

        var ueberleitungService = new BelegUeberleitungService(_factory, new NumberRangeService(_factory));
        var eingangsrechnung = await ueberleitungService.UeberleitenAsync(wareneingang.Id, BelegTyp.Eingangsrechnung, ct);
        return eingangsrechnung.Id;
    }

    /// <summary>Erhöht den Einzelpreis der (einzigen) Position der übergebenen Eingangsrechnung und rechnet die
    /// Kopfsummen neu — simuliert eine reale Lieferantenrechnung, deren Betrag vom ursprünglichen Wareneingang abweicht.</summary>
    private async Task VerteuerePositionAsync(int eingangsrechnungId, decimal neuerEinzelpreis, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var eingangsrechnung = await db.Eingangsrechnungen.Include(e => e.Positionen)
            .FirstAsync(e => e.Id == eingangsrechnungId, ct);
        var position = eingangsrechnung.Positionen[0];
        position.Einzelpreis = neuerEinzelpreis;
        position.GesamtNetto = SteuerRechner.BerechnePosition(position.Menge, position.Einzelpreis, position.RabattProzent);

        var steuersummen = SteuerRechner.BerechneSteuersummen(eingangsrechnung.Positionen);
        eingangsrechnung.Steuersummen = steuersummen.ToList();
        (eingangsrechnung.SummeNetto, eingangsrechnung.SummeMwSt, eingangsrechnung.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task BuchenAsync_BetragStimmtMitWareneingangUeberein_KeineAbweichung_LegtKreditorOpAn()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: Wareneingang (gebucht) mit SummeBrutto = 119,00 (100 netto + 19% MwSt), dann Eingangsrechnung
        // per UeberleitenAsync daraus erzeugt (Positionen 1:1 übernommen, UrsprungsPositionId gesetzt) und
        // unverändert gebucht.
        var eingangsrechnungId = await NeueUnveraenderteEingangsrechnungAsync(ct);
        var lieferantId = _lieferantId;
        var service = new EingangsrechnungBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var ergebnis = await service.BuchenAsync(eingangsrechnungId, ct);

        Assert.False(ergebnis.BetragWeichtAb);
        Assert.Equal(0m, ergebnis.AbweichungBetrag);
        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.SingleAsync(o => o.BelegId == eingangsrechnungId, ct);
        Assert.Equal(OffenerPostenTyp.Kreditor, op.Typ);
        Assert.Equal(lieferantId, op.LieferantId);
        Assert.Null(op.KundeId);
    }

    [Fact]
    public async Task BuchenAsync_BetragWeichtAb_MeldetSoftWarnungLegtOpTrotzdemAn()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange wie oben, aber Einzelpreis der Eingangsrechnung-Position vor dem Buchen manuell auf einen
        // höheren Wert geändert (simuliert eine reale Rechnung mit abweichendem Preis).
        var eingangsrechnungId = await NeueUnveraenderteEingangsrechnungAsync(ct);
        await VerteuerePositionAsync(eingangsrechnungId, 150m, ct);
        var service = new EingangsrechnungBuchenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var ergebnis = await service.BuchenAsync(eingangsrechnungId, ct);

        Assert.True(ergebnis.BetragWeichtAb);
        // Wareneingang bleibt bei 119,00 (100 netto + 19% MwSt); Eingangsrechnung nach Verteuerung bei 178,50
        // (150 netto + 19% MwSt) -> Abweichung exakt 59,50. Exakter Wert statt reinem ">0"-Check, damit eine
        // künftige Regression auf den ursprünglichen Bug (Wareneingang-Summen nicht berechnet -> erwarteterBetrag
        // fälschlich 0) tatsächlich auffällt statt zufällig weiter zu bestehen.
        Assert.Equal(59.50m, ergebnis.AbweichungBetrag);
        await using var db = new MiletDbContext(_options);
        Assert.Equal(1, await db.OffenePosten.CountAsync(o => o.BelegId == eingangsrechnungId, ct));
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
