using Microsoft.EntityFrameworkCore;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>Lieferadresse war im Belegeditor bislang nie änderbar — immer 1:1 aus dem Kundenstamm
/// eingefroren (s. STATUS.md "Bekannte Risiken", Plan Phase 9 Task 23). Diese Tests prüfen sowohl die
/// neue Editierbarkeit im Entwurf als auch, dass die Neuanlage weiterhin korrekt aus dem Kundenstamm
/// vorbelegt — nicht versehentlich mit einem leeren DTO-Default überschrieben wird.</summary>
public sealed class BelegServiceLieferadresseTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;

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
        db.Nummernkreise.Add(new Nummernkreis { Code = "AN", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AN-{1}-{0:0000}" });
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde", Strasse = "Hauptstr. 1", Plz = "12345", Ort = "Musterstadt" } };
        db.Add(kunde);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task SpeichereAsync_NeuanlageOhneAbweichendeLieferadresse_UebernimmtKundenadresse()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BelegService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var neu = new BelegDto
        {
            BelegTyp = BelegTyp.Angebot, KundeId = _kundeId, BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            Positionen = [new BelegPositionDto { PositionsNr = 1, PositionsTyp = PositionsTyp.Freitext, Bezeichnung = "Testposition", Menge = 1, Einzelpreis = 10m }],
        };
        var gespeichert = await service.SpeichereAsync(neu, ct);

        Assert.Equal("Testkunde", gespeichert.LieferadresseSnapshot.Name1);
        Assert.Equal("Hauptstr. 1", gespeichert.LieferadresseSnapshot.Strasse);
    }

    [Fact]
    public async Task SpeichereAsync_UpdateMitAbweichenderLieferadresse_UebernimmtSieUndUeberschreibtNichtDenKundenstamm()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BelegService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var neu = new BelegDto
        {
            BelegTyp = BelegTyp.Angebot, KundeId = _kundeId, BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            Positionen = [new BelegPositionDto { PositionsNr = 1, PositionsTyp = PositionsTyp.Freitext, Bezeichnung = "Testposition", Menge = 1, Einzelpreis = 10m }],
        };
        var gespeichert = await service.SpeichereAsync(neu, ct);

        var abweichendeAdresse = new AdresseDto { Name1 = "Baustelle Nord", Strasse = "Feldweg 9", Plz = "54321", Ort = "Anderswo" };
        var aktualisiert = gespeichert with { LieferadresseSnapshot = abweichendeAdresse };
        var ergebnis = await service.SpeichereAsync(aktualisiert, ct);

        Assert.Equal("Baustelle Nord", ergebnis.LieferadresseSnapshot.Name1);
        Assert.Equal("Feldweg 9", ergebnis.LieferadresseSnapshot.Strasse);

        // Rechnungsadresse bleibt unberührt vom Kundenstamm-Snapshot, der Kunde selbst erst recht.
        Assert.Equal("Testkunde", ergebnis.RechnungsadresseSnapshot.Name1);
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        Assert.Equal("Hauptstr. 1", kunde.Adresse.Strasse);
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
