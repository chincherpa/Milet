using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class DatevExportServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeMitKontoId;
    private int _kundeOhneKontoId;

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

        db.FibuKonfiguration.Add(new FibuKonfiguration
        {
            Id = 1, Kontenrahmen = Kontenrahmen.Skr03, BeraterNr = 1001, MandantNr = 1,
            WirtschaftsjahrBeginnMonat = 1, SachkontenLaenge = 4, BankkontoNr = 1200,
        });
        db.MwStSaetze.Add(new MwStSatz { Bezeichnung = "Voll", Satz = 19m, SteuerSchluessel = 3, GueltigAb = new DateOnly(2007, 1, 1), ErloeskontoNr = 8400, AufwandskontoNr = 3400 });

        var kundeMitKonto = new Kunde { Kundennummer = "KD-1", Adresse = new() { Name1 = "Kunde Mit Konto" }, DebitorenkontoNr = 10001 };
        var kundeOhneKonto = new Kunde { Kundennummer = "KD-2", Adresse = new() { Name1 = "Kunde Ohne Konto" } };
        db.Kunden.AddRange(kundeMitKonto, kundeOhneKonto);
        await db.SaveChangesAsync();
        _kundeMitKontoId = kundeMitKonto.Id;
        _kundeOhneKontoId = kundeOhneKonto.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeueGebuchteRechnungAsync(int kundeId, DateOnly belegDatum, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == kundeId, ct);
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = belegDatum,
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeNetto = 100m,
            SummeMwSt = 19m,
            SummeBrutto = 119m,
            Steuersummen = [new BelegSteuerSumme { MwStSatzWert = 19m, SteuerSchluessel = 3, NettoSumme = 100m, MwStBetrag = 19m }],
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);
        return rechnung.Id;
    }

    [Fact]
    public async Task VorschauAsync_GebuchteRechnungMitKonten_ZaehltOhneZuMarkieren()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var rechnungId = await NeueGebuchteRechnungAsync(_kundeMitKontoId, heute, ct);

        var service = new DatevExportService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var vorschau = await service.VorschauAsync(heute.AddDays(-1), heute.AddDays(1), ct);

        Assert.Equal(1, vorschau.AnzahlRechnungen);
        Assert.Equal(1, vorschau.AnzahlBuchungszeilen);
        Assert.Equal(119m, vorschau.SummeUmsatz);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        Assert.Null(rechnung.ExportiertAm);
    }

    [Fact]
    public async Task ExportierenAsync_MarkiertBelegUndSchliesstZweitenExportImSelbenZeitraumAus()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var rechnungId = await NeueGebuchteRechnungAsync(_kundeMitKontoId, heute, ct);

        var service = new DatevExportService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ergebnis = await service.ExportierenAsync(heute.AddDays(-1), heute.AddDays(1), ct);

        Assert.Equal(1, ergebnis.AnzahlBuchungszeilen);
        Assert.NotEmpty(ergebnis.CsvBytes);
        Assert.Contains("EXTF", System.Text.Encoding.GetEncoding(1252).GetString(ergebnis.CsvBytes));

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        Assert.NotNull(rechnung.ExportiertAm);

        var zweiteVorschau = await service.VorschauAsync(heute.AddDays(-1), heute.AddDays(1), ct);
        Assert.Equal(0, zweiteVorschau.AnzahlBuchungszeilen);
    }

    [Fact]
    public async Task ExportierenAsync_KundeOhneDebitorenkonto_ErzeugtKeineZeileUndBleibtUnmarkiert()
    {
        var ct = TestContext.Current.CancellationToken;
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var rechnungId = await NeueGebuchteRechnungAsync(_kundeOhneKontoId, heute, ct);

        var service = new DatevExportService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var ergebnis = await service.ExportierenAsync(heute.AddDays(-1), heute.AddDays(1), ct);

        Assert.Equal(0, ergebnis.AnzahlBuchungszeilen);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId, ct);
        Assert.Null(rechnung.ExportiertAm);
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
