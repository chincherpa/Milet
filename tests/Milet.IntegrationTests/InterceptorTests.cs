using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.ValueObjects;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>
/// Die beiden SaveChangesInterceptor sind die Stellen mit der höchsten Fehlerfolge im Projekt — sie setzen
/// die GoBD-Zusagen durch (Unveränderlichkeit gebuchter Belege, lückenloser Änderungsnachweis) und hatten
/// bis hierher keine eigenen Tests (Befund 29 aus REVIEW_2026-08-29.md).
///
/// Gegen einen echten SQL Server, weil beides nur im Zusammenspiel mit dem Change-Tracker und einer echten
/// Transaktion beobachtbar ist: der Immutability-Interceptor liest OriginalValues, der Audit-Interceptor
/// schreibt in einem zweiten SaveChanges und öffnet dafür ggf. eine eigene Transaktion.
/// </summary>
public sealed class InterceptorTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _kundeId;
    private int _artikelId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(
                new AuditSaveChangesInterceptor(new CurrentSessionService()),
                new BelegImmutabilityInterceptor())
            .Options;

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var kunde = new Kunde { Kundennummer = "KD-1", Adresse = new Adresse { Name1 = "Testkunde" } };
        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Testartikel", Einheit = einheit };
        db.AddRange(einheit, kunde, artikel);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    // ---- BelegImmutabilityInterceptor ----

    [Fact]
    public async Task GebuchterBeleg_KopfAendern_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Gebucht, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Kopftext = "nachträglich geändert";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        Assert.Contains("gebucht", ex.Message);
    }

    [Fact]
    public async Task GebuchterBeleg_Loeschen_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Gebucht, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        db.Remove(beleg);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task GebuchterBeleg_PositionAendern_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Gebucht, ct);

        await using var db = new MiletDbContext(_options);
        var position = await db.BelegPositionen.FirstAsync(p => p.BelegId == id, ct);
        position.Menge = 999m;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        Assert.Contains("Positionen", ex.Message);
    }

    /// <summary>Der Fall, den der Interceptor ausdrücklich durchlassen muss — sonst könnte die Überleitung
    /// einen vollständig übernommenen Beleg nicht fortschreiben.</summary>
    [Fact]
    public async Task GebuchterBeleg_NurStatusAufErledigt_IstErlaubt()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Gebucht, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Status = BelegStatus.Erledigt;
        await db.SaveChangesAsync(ct);

        Assert.Equal(BelegStatus.Erledigt, (await db.Rechnungen.AsNoTracking().FirstAsync(b => b.Id == id, ct)).Status);
    }

    /// <summary>Gegenprobe: der Statusübergang darf nicht als Vehikel für eine inhaltliche Änderung dienen.</summary>
    [Fact]
    public async Task GebuchterBeleg_StatusUndKopftext_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Gebucht, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Status = BelegStatus.Erledigt;
        beleg.Kopftext = "am Status-Flip vorbeigeschmuggelt";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
    }

    /// <summary>Die Lücke aus Befund 5 des Reviews vom 2026-09-06: Erledigt fiel durch alle Zweige.</summary>
    [Fact]
    public async Task ErledigterBeleg_KopfAendern_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Erledigt, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Kopftext = "nachträglich geändert";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        Assert.Contains("erledigt", ex.Message);
    }

    /// <summary>Die Gegen-Ausnahme: BelegService.SetzeQuellbelegeZurueckAsync nimmt Erledigt zurück, wenn der
    /// Folgeentwurf gelöscht wird. Das muss erlaubt bleiben.</summary>
    [Fact]
    public async Task ErledigterBeleg_NurStatusZurueckAufGebucht_IstErlaubt()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Erledigt, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Status = BelegStatus.Gebucht;
        await db.SaveChangesAsync(ct);

        Assert.Equal(BelegStatus.Gebucht, (await db.Rechnungen.AsNoTracking().FirstAsync(b => b.Id == id, ct)).Status);
    }

    [Fact]
    public async Task EntwurfsBeleg_BleibtAenderbar()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await NeuerBelegAsync(BelegStatus.Entwurf, ct);

        await using var db = new MiletDbContext(_options);
        var beleg = await db.Rechnungen.FirstAsync(b => b.Id == id, ct);
        beleg.Kopftext = "Entwürfe dürfen sich ändern";
        await db.SaveChangesAsync(ct);

        Assert.Equal("Entwürfe dürfen sich ändern",
            (await db.Rechnungen.AsNoTracking().FirstAsync(b => b.Id == id, ct)).Kopftext);
    }

    // ---- AuditSaveChangesInterceptor ----

    [Fact]
    public async Task Audit_NeueEntitaet_SchreibtAngelegtMitEchterId()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var db = new MiletDbContext(_options);
        var kunde = new Kunde { Kundennummer = "KD-AUDIT", Adresse = new Adresse { Name1 = "Auditkunde" } };
        db.Add(kunde);
        await db.SaveChangesAsync(ct);

        var eintrag = await db.Set<AuditLog>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.EntityName == nameof(Kunde) && a.EntityId == kunde.Id.ToString(), ct);

        Assert.NotNull(eintrag);
        Assert.Equal("Angelegt", eintrag!.Aktion);
    }

    /// <summary>Befund 10 aus REVIEW_2026-08-29.md — der Hash darf nie im Klartext-JSON landen.</summary>
    [Fact]
    public async Task Audit_Benutzer_ProtokolliertKeinenPasswortHash()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var db = new MiletDbContext(_options);
        var recht = new Recht { Code = "AuditRecht", Bezeichnung = "AuditRecht" };
        var rolle = new Rolle { Name = "Auditrolle", Rechte = [recht] };
        var benutzer = new Benutzer
        {
            Benutzername = "audituser",
            Anzeigename = "Audit User",
            PasswortHash = Milet.Domain.Services.PasswortHasher.Hash("geheimes-Testpasswort"),
            Rolle = rolle,
        };
        db.AddRange(recht, rolle, benutzer);
        await db.SaveChangesAsync(ct);

        var eintraege = await db.Set<AuditLog>().AsNoTracking()
            .Where(a => a.EntityName == nameof(Benutzer))
            .ToListAsync(ct);

        Assert.NotEmpty(eintraege);
        Assert.All(eintraege, e =>
        {
            Assert.DoesNotContain("PasswortHash", e.Aenderungen ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("RowVersion", e.Aenderungen ?? string.Empty, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Befund 23 aus REVIEW_2026-08-29.md: die Audit-Zeilen entstehen in einem zweiten SaveChanges. Bringt
    /// der Aufrufer eine Transaktion mit und rollt sie zurück, dürfen weder die fachliche Änderung noch die
    /// Audit-Zeile übrig bleiben.
    /// </summary>
    [Fact]
    public async Task Audit_RollbackDerAufruferTransaktion_LaesstKeineAuditZeileZurueck()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var db = new MiletDbContext(_options);
        await using (var transaktion = await db.Database.BeginTransactionAsync(ct))
        {
            db.Add(new Kunde { Kundennummer = "KD-ROLLBACK", Adresse = new Adresse { Name1 = "Rollbackkunde" } });
            await db.SaveChangesAsync(ct);
            await transaktion.RollbackAsync(ct);
        }

        await using var pruefDb = new MiletDbContext(_options);
        Assert.False(await pruefDb.Kunden.AnyAsync(k => k.Kundennummer == "KD-ROLLBACK", ct));
        Assert.False(await pruefDb.Set<AuditLog>().AnyAsync(a => a.EntityId != null && a.Aenderungen!.Contains("Rollbackkunde"), ct));
    }

    private async Task<int> NeuerBelegAsync(BelegStatus status, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var beleg = new Rechnung
        {
            BelegNummer = $"RE-TEST-{Guid.NewGuid():N}"[..18],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = _kundeId,
            // Bewusst als Entwurf angelegt und erst danach hochgesetzt: der Interceptor prüft
            // OriginalValues, ein direkt als "Gebucht" eingefügter Beleg wäre Added und damit erlaubt.
            Status = BelegStatus.Entwurf,
            Positionen =
            [
                new BelegPosition
                {
                    PositionsNr = 1,
                    PositionsTyp = PositionsTyp.Artikel,
                    ArtikelId = _artikelId,
                    Bezeichnung = "Testartikel",
                    Menge = 1m,
                    Einzelpreis = 100m,
                    MwStSatzWert = 19m,
                    GesamtNetto = 100m,
                },
            ],
        };
        db.Add(beleg);
        await db.SaveChangesAsync(ct);

        if (status != BelegStatus.Entwurf)
        {
            beleg.Status = status == BelegStatus.Erledigt ? BelegStatus.Gebucht : status;
            await db.SaveChangesAsync(ct);

            if (status == BelegStatus.Erledigt)
            {
                beleg.Status = BelegStatus.Erledigt;
                await db.SaveChangesAsync(ct);
            }
        }

        return beleg.Id;
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
        catch (Exception)
        {
            return false;
        }
    }
}
