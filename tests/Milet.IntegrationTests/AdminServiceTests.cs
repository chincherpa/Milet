using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>
/// Phase 7 (Admin+Härtung): Login (AuthService), Rechte-Guard (BerechtigungsService via
/// Benutzerverwaltungs-/Rollenverwaltungsservice) und AuditLog-Protokollierung — end-to-end
/// gegen einen echten SQL Server, damit die ConditionalWeakTable-basierte
/// Zwei-Speichervorgänge-Logik im AuditSaveChangesInterceptor tatsächlich verifiziert ist
/// (nicht nur compile-geprüft).
/// </summary>
public sealed class AdminServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _rolleId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        // AddInterceptors hier wie in DependencyInjection.AddInfrastructure — sonst würde
        // AuditLog_NachAendernEinesBenutzers_EnthaeltGeaendert_Eintrag stumm ins Leere laufen,
        // weil der rohe DbContextOptionsBuilder ohne Interceptor keine AuditLog-Zeilen schreibt.
        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new Milet.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor(new CurrentSessionService()))
            .Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        // Kompletter Rechte-Katalog (nicht nur "Administration"), sonst findet
        // RollenverwaltungService.SpeichereAsync die per RechteCodes zugewiesenen Codes
        // ("Verkauf"/"Stammdaten" in Rollenverwaltung_SpeichertRechteZuweisung) nicht in der DB.
        db.Rechte.AddRange(RechtCodes.Alle.Select(code => new Recht { Code = code, Bezeichnung = code }));
        await db.SaveChangesAsync();

        var recht = await db.Rechte.FirstAsync(r => r.Code == RechtCodes.Administration);
        var rolle = new Rolle { Name = "Testrolle", Rechte = [recht] };
        var benutzer = new Benutzer
        {
            Benutzername = "tuser",
            Anzeigename = "Test User",
            PasswortHash = PasswortHasher.Hash("korrektes-passwort"),
            Rolle = rolle,
            Aktiv = true,
        };
        var inaktiverBenutzer = new Benutzer
        {
            Benutzername = "inaktiv",
            Anzeigename = "Inaktiver User",
            PasswortHash = PasswortHasher.Hash("korrektes-passwort"),
            Rolle = rolle,
            Aktiv = false,
        };
        // Je Testfall ein eigener Benutzer: Fehlversuchszähler und Sperre sind Zustand auf der Zeile, ein
        // geteilter Benutzer würde die Tests voneinander abhängig machen (xUnit parallelisiert je Klasse).
        Benutzer Weiterer(string name, bool wechselErforderlich = false) => new()
        {
            Benutzername = name,
            Anzeigename = name,
            PasswortHash = PasswortHasher.Hash("korrektes-passwort"),
            Rolle = rolle,
            Aktiv = true,
            PasswortAenderungErforderlich = wechselErforderlich,
        };

        db.AddRange(
            benutzer, inaktiverBenutzer,
            Weiterer("sperrbar"), Weiterer("zaehler"),
            Weiterer("wechsler", wechselErforderlich: true), Weiterer("ruecksetzbar"));
        await db.SaveChangesAsync();
        _rolleId = rolle.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private static Milet.Application.Abstractions.ICurrentSessionService AngemeldeteSession(params string[] rechte)
    {
        var session = new CurrentSessionService();
        session.Anmelden(1, "Testsitzung", "Testrolle", rechte);
        return session;
    }

    [Fact]
    public async Task AnmeldenAsync_KorrektesPasswort_LiefertSessionMitRechten()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        var ergebnis = await service.AnmeldenAsync("tuser", "korrektes-passwort", ct);

        var session = ergebnis.Session;
        Assert.NotNull(session);
        Assert.Null(ergebnis.GesperrtBis);
        Assert.False(ergebnis.PasswortAenderungErforderlich);
        Assert.Equal("Test User", session!.BenutzerName);
        Assert.Equal("Testrolle", session.RollenName);
        Assert.Contains(RechtCodes.Administration, session.Rechte);
    }

    [Fact]
    public async Task AnmeldenAsync_FalschesPasswort_LiefertKeineSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        var ergebnis = await service.AnmeldenAsync("tuser", "falsches-passwort", ct);

        Assert.Null(ergebnis.Session);
        // Die Sperre wird bei falschem Passwort NIE offengelegt — sonst verriete die Meldung, dass es den
        // Benutzernamen gibt (genau das, was der Dummy-Hash verhindern soll).
        Assert.Null(ergebnis.GesperrtBis);
    }

    // ---- Lockout und erzwungener Passwortwechsel (Befunde 13/30 aus REVIEW_2026-08-29.md) ----

    [Fact]
    public async Task AnmeldenAsync_FuenfFehlversuche_SperrtUndLegtSperreErstBeiKorrektemPasswortOffen()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        for (var i = 0; i < 5; i++)
        {
            var fehlversuch = await service.AnmeldenAsync("sperrbar", "falsch-falsch", ct);
            Assert.Null(fehlversuch.Session);
            // Während des Sperrens darf nach außen nichts anders aussehen als bei einem unbekannten Benutzer.
            Assert.Null(fehlversuch.GesperrtBis);
        }

        // Jetzt mit dem RICHTIGEN Passwort: der Zugang bleibt zu, und erst hier wird die Sperre benannt.
        var gesperrt = await service.AnmeldenAsync("sperrbar", "korrektes-passwort", ct);
        Assert.Null(gesperrt.Session);
        Assert.NotNull(gesperrt.GesperrtBis);
        Assert.True(gesperrt.GesperrtBis > DateTime.Now);

        await using var db = new MiletDbContext(_options);
        var benutzer = await db.Benutzer.AsNoTracking().FirstAsync(b => b.Benutzername == "sperrbar", ct);
        Assert.Equal(5, benutzer.FehlversuchZaehler);
        Assert.NotNull(benutzer.GesperrtBis);
    }

    [Fact]
    public async Task AnmeldenAsync_ErfolgNachFehlversuchen_SetztZaehlerZurueck()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        // Unter der Schwelle bleiben, sonst greift die Sperre und der Erfolgspfad wird gar nicht erreicht.
        await service.AnmeldenAsync("zaehler", "falsch-falsch", ct);
        await service.AnmeldenAsync("zaehler", "falsch-falsch", ct);

        var ergebnis = await service.AnmeldenAsync("zaehler", "korrektes-passwort", ct);
        Assert.NotNull(ergebnis.Session);

        await using var db = new MiletDbContext(_options);
        var benutzer = await db.Benutzer.AsNoTracking().FirstAsync(b => b.Benutzername == "zaehler", ct);
        Assert.Equal(0, benutzer.FehlversuchZaehler);
        Assert.Null(benutzer.GesperrtBis);
    }

    [Fact]
    public async Task AnmeldenAsync_FlagGesetzt_MeldetPasswortwechselErforderlich()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        var ergebnis = await service.AnmeldenAsync("wechsler", "korrektes-passwort", ct);

        Assert.NotNull(ergebnis.Session);
        Assert.True(ergebnis.PasswortAenderungErforderlich);
    }

    [Fact]
    public async Task PasswortAendernAsync_LoeschtFlagUndSperreUndSetztNeuesPasswort()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        await service.PasswortAendernAsync("wechsler", "korrektes-passwort", "ein-langes-neues-Passwort", ct);

        var mitNeuem = await service.AnmeldenAsync("wechsler", "ein-langes-neues-Passwort", ct);
        Assert.NotNull(mitNeuem.Session);
        Assert.False(mitNeuem.PasswortAenderungErforderlich);

        var mitAltem = await service.AnmeldenAsync("wechsler", "korrektes-passwort", ct);
        Assert.Null(mitAltem.Session);
    }

    [Fact]
    public async Task PasswortAendernAsync_FalschesAltesPasswort_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PasswortAendernAsync("tuser", "falsch-falsch", "ein-langes-neues-Passwort", ct));
    }

    [Fact]
    public async Task PasswortAendernAsync_ZuKurzesPasswort_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PasswortAendernAsync("tuser", "korrektes-passwort", "kurz", ct));
    }

    [Fact]
    public async Task PasswortZuruecksetzenAsync_ErzwingtWechselBeimNaechstenLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var verwaltung = new BenutzerverwaltungService(
            _factory, new BerechtigungsService(AngemeldeteSession(RechtCodes.Administration)));

        await using (var db = new MiletDbContext(_options))
        {
            var benutzer = await db.Benutzer.AsNoTracking().FirstAsync(b => b.Benutzername == "ruecksetzbar", ct);
            await verwaltung.PasswortZuruecksetzenAsync(benutzer.Id, "vom-Admin-gesetzt-lang", ct);
        }

        var ergebnis = await new AuthService(_factory).AnmeldenAsync("ruecksetzbar", "vom-Admin-gesetzt-lang", ct);
        Assert.NotNull(ergebnis.Session);
        Assert.True(ergebnis.PasswortAenderungErforderlich);
    }

    [Fact]
    public async Task AnmeldenAsync_DeaktivierterBenutzer_LiefertKeineSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        var ergebnis = await service.AnmeldenAsync("inaktiv", "korrektes-passwort", ct);

        Assert.Null(ergebnis.Session);
    }

    [Fact]
    public async Task AnmeldenAsync_UnbekannterBenutzer_LiefertKeineSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new AuthService(_factory);

        var ergebnis = await service.AnmeldenAsync("gibt-es-nicht", "irgendwas1", ct);

        Assert.Null(ergebnis.Session);
    }

    [Fact]
    public async Task Benutzerverwaltung_OhneAdministrationsrecht_WirftKeinZugriffException()
    {
        var ct = TestContext.Current.CancellationToken;
        var berechtigung = new BerechtigungsService(AngemeldeteSession(RechtCodes.Verkauf));
        var service = new BenutzerverwaltungService(_factory, berechtigung);

        await Assert.ThrowsAsync<KeinZugriffException>(() => service.ListeAsync(ct));
    }

    [Fact]
    public async Task Benutzerverwaltung_MitAdministrationsrecht_LegtBenutzerAn()
    {
        var ct = TestContext.Current.CancellationToken;
        var berechtigung = new BerechtigungsService(AngemeldeteSession(RechtCodes.Administration));
        var service = new BenutzerverwaltungService(_factory, berechtigung);

        var dto = new BenutzerDto
        {
            Benutzername = "neuerbenutzer",
            Anzeigename = "Neuer Benutzer",
            RolleId = _rolleId,
            NeuesPasswort = "ein-neues-passwort",
        };

        var angelegt = await service.SpeichereAsync(dto, ct);

        Assert.NotEqual(0, angelegt.Id);
        var liste = await service.ListeAsync(ct);
        Assert.Contains(liste, b => b.Benutzername == "neuerbenutzer");
    }

    [Fact]
    public async Task Rollenverwaltung_SpeichertRechteZuweisung()
    {
        var ct = TestContext.Current.CancellationToken;
        var berechtigung = new BerechtigungsService(AngemeldeteSession(RechtCodes.Administration));
        var rollenService = new RollenverwaltungService(_factory, berechtigung);

        var neu = await rollenService.SpeichereAsync(
            new RolleDto { Name = "Verkaufsrolle", RechteCodes = [RechtCodes.Verkauf, RechtCodes.Stammdaten] }, ct);

        Assert.NotEqual(0, neu.Id);
        Assert.Equal(2, neu.RechteCodes.Count);
        Assert.Contains(RechtCodes.Verkauf, neu.RechteCodes);
    }

    [Fact]
    public async Task AuditLog_NachAendernEinesBenutzers_EnthaeltGeaendert_Eintrag()
    {
        var ct = TestContext.Current.CancellationToken;
        var berechtigung = new BerechtigungsService(AngemeldeteSession(RechtCodes.Administration));
        var benutzerService = new BenutzerverwaltungService(_factory, berechtigung);
        var auditService = new AuditLogService(_factory, berechtigung);

        var angelegt = await benutzerService.SpeichereAsync(
            new BenutzerDto { Benutzername = "audituser", Anzeigename = "Audit User", RolleId = _rolleId, NeuesPasswort = "ein-passwort-123" }, ct);

        await benutzerService.SpeichereAsync(angelegt with { Anzeigename = "Audit User Geändert" }, ct);

        var logs = await auditService.ListeAsync(new AuditLogFilterDto { EntityName = nameof(Benutzer) }, ct);

        Assert.Contains(logs, l => l.Aktion == "Angelegt" && l.EntityId == angelegt.Id.ToString());
        Assert.Contains(logs, l => l.Aktion == "Geändert" && l.EntityId == angelegt.Id.ToString());
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
