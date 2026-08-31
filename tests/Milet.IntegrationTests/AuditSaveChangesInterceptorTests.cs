using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>AuditSaveChangesInterceptor hatte bislang keinen eigenen Test (s. STATUS.md, Plan Phase 9
/// Task 22) — nur indirekt über AdminServiceTests' AuditLog-Zeilenzahl-Check nach einem Benutzer-Save.
/// Diese Tests prüfen die Zweistufigkeit (Erfassung vor, Schreiben nach dem physischen Speichern) und die
/// Ausschlussliste (PasswortHash/RowVersion) gezielt.</summary>
public sealed class AuditSaveChangesInterceptorTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new AuditSaveChangesInterceptor(TestCurrentUserService.Instanz))
            .Options;

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task Hinzufuegen_SetztErstelltFelderUndSchreibtAngelegtLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };

        await using var db = new MiletDbContext(_options);
        db.Add(kunde);
        await db.SaveChangesAsync(ct);

        Assert.NotEqual(default, kunde.ErstelltAm);
        Assert.Equal(TestCurrentUserService.Instanz.BenutzerId, kunde.ErstelltVonId);
        Assert.Null(kunde.GeaendertAm);

        var log = await db.AuditLog.SingleAsync(l => l.EntityName == nameof(Kunde) && l.EntityId == kunde.Id.ToString(), ct);
        Assert.Equal("Angelegt", log.Aktion);
        Assert.Equal(TestCurrentUserService.Instanz.BenutzerId, log.BenutzerId);
        Assert.NotNull(log.Aenderungen);
        Assert.Contains("Kundennummer", log.Aenderungen);
    }

    [Fact]
    public async Task Aendern_SetztGeaendertFelderUndSchreibtGeaendertLogNurMitGeaenderterProperty()
    {
        var ct = TestContext.Current.CancellationToken;
        int kundeId;
        await using (var db = new MiletDbContext(_options))
        {
            var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
            db.Add(kunde);
            await db.SaveChangesAsync(ct);
            kundeId = kunde.Id;
        }

        await using (var db = new MiletDbContext(_options))
        {
            var kunde = await db.Kunden.FirstAsync(k => k.Id == kundeId, ct);
            kunde.Telefon = "0123456789";
            await db.SaveChangesAsync(ct);

            Assert.NotNull(kunde.GeaendertAm);
            Assert.Equal(TestCurrentUserService.Instanz.BenutzerId, kunde.GeaendertVonId);
        }

        await using var pruefDb = new MiletDbContext(_options);
        var log = await pruefDb.AuditLog.SingleAsync(l => l.EntityName == nameof(Kunde) && l.EntityId == kundeId.ToString() && l.Aktion == "Geändert", ct);
        Assert.Contains("0123456789", log.Aenderungen);
        // Kundennummer wurde nicht geändert — sie darf nicht Teil des Änderungs-Diffs sein (nur IsModified-Properties).
        Assert.DoesNotContain("Kundennummer", log.Aenderungen!);
    }

    [Fact]
    public async Task Aendern_PasswortHashUndRowVersion_WerdenNichtProtokolliert()
    {
        var ct = TestContext.Current.CancellationToken;
        int benutzerId;
        await using (var db = new MiletDbContext(_options))
        {
            var rolle = new Rolle { Name = "Testrolle" };
            var benutzer = new Benutzer
            {
                Benutzername = "tuser", Anzeigename = "Test User", Rolle = rolle,
                PasswortHash = PasswortHasher.Hash("altes-passwort"), Aktiv = true,
            };
            db.Add(benutzer);
            await db.SaveChangesAsync(ct);
            benutzerId = benutzer.Id;
        }

        await using (var db = new MiletDbContext(_options))
        {
            var benutzer = await db.Benutzer.FirstAsync(b => b.Id == benutzerId, ct);
            benutzer.PasswortHash = PasswortHasher.Hash("neues-passwort");
            benutzer.Aktiv = false;
            await db.SaveChangesAsync(ct);
        }

        await using var pruefDb = new MiletDbContext(_options);
        var log = await pruefDb.AuditLog.SingleAsync(l => l.EntityName == nameof(Benutzer) && l.EntityId == benutzerId.ToString() && l.Aktion == "Geändert", ct);
        Assert.DoesNotContain("PasswortHash", log.Aenderungen!);
        Assert.DoesNotContain("RowVersion", log.Aenderungen!);
        Assert.Contains("Aktiv", log.Aenderungen!);
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
}
