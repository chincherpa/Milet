using Microsoft.EntityFrameworkCore;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>ZahlungService hatte bislang keinen eigenen Test (s. STATUS.md, Plan Phase 9 Task 22) —
/// nur die reine Domain-Logik (SkontoRechner) war getestet.</summary>
public sealed class ZahlungServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
    private int _andererKundeId;

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
        var kunde = new Kunde { Kundennummer = "KD-1", Adresse = new() { Name1 = "Testkunde" } };
        var andererKunde = new Kunde { Kundennummer = "KD-2", Adresse = new() { Name1 = "Anderer Kunde" } };
        db.AddRange(kunde, andererKunde);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
        _andererKundeId = andererKunde.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<(int OpId, byte[] RowVersion)> NeuerOffenerPostenAsync(int kundeId, decimal betrag, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == kundeId, ct);
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht,
            SummeBrutto = betrag,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);

        var op = new OffenerPosten
        {
            Beleg = rechnung, KundeId = kunde.Id, Typ = OffenerPostenTyp.Debitor,
            Betrag = betrag, OffenerBetrag = betrag, Faelligkeit = DateOnly.FromDateTime(DateTime.Today),
        };
        db.Add(op);
        await db.SaveChangesAsync(ct);
        return (op.Id, op.RowVersion);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_VollstaendigeZahlung_GleichtOffenenPostenAus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opId, rowVersion) = await NeuerOffenerPostenAsync(_kundeId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var dto = new ZahlungDto(0, _kundeId, null, OffenerPostenTyp.Debitor, DateOnly.FromDateTime(DateTime.Today), "Überweisung", "REF-1",
            [new ZahlungZuordnungDto(opId, 100m, 0m, rowVersion)]);
        var gespeichert = await service.ErfasseZahlungAsync(dto, ct);

        Assert.NotEqual(0, gespeichert.Id);
        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(0m, op.OffenerBetrag);
        Assert.Equal(OffenerPostenStatus.Ausgeglichen, op.Status);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_TeilzahlungMitSkonto_SetztTeilweiseBezahltUndGesamtbetragOhneSkonto()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opId, rowVersion) = await NeuerOffenerPostenAsync(_kundeId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        // 98 gezahlt + 2 Skonto = 100 angewandt, aber Gesamtbetrag der Zahlung selbst ist nur der
        // tatsächlich geflossene Betrag (98) — s. Kommentar in ZahlungService.ErfasseZahlungAsync.
        var dto = new ZahlungDto(0, _kundeId, null, OffenerPostenTyp.Debitor, DateOnly.FromDateTime(DateTime.Today), "Überweisung", null,
            [new ZahlungZuordnungDto(opId, 48m, 2m, rowVersion)]);
        var gespeichert = await service.ErfasseZahlungAsync(dto, ct);

        await using var db = new MiletDbContext(_options);
        var zahlung = await db.Zahlungen.FirstAsync(z => z.Id == gespeichert.Id, ct);
        Assert.Equal(48m, zahlung.Gesamtbetrag);

        var op = await db.OffenePosten.FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(50m, op.OffenerBetrag);
        Assert.Equal(OffenerPostenStatus.TeilweiseBezahlt, op.Status);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_BetragUebersteigtOffenenPosten_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opId, rowVersion) = await NeuerOffenerPostenAsync(_kundeId, 50m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var dto = new ZahlungDto(0, _kundeId, null, OffenerPostenTyp.Debitor, DateOnly.FromDateTime(DateTime.Today), null, null,
            [new ZahlungZuordnungDto(opId, 60m, 0m, rowVersion)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ErfasseZahlungAsync(dto, ct));
    }

    [Fact]
    public async Task ErfasseZahlungAsync_OffenerPostenGehoertAnderemKunden_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opId, rowVersion) = await NeuerOffenerPostenAsync(_andererKundeId, 50m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        // Zahlung ist auf _kundeId ausgestellt, der OP gehört aber _andererKundeId.
        var dto = new ZahlungDto(0, _kundeId, null, OffenerPostenTyp.Debitor, DateOnly.FromDateTime(DateTime.Today), null, null,
            [new ZahlungZuordnungDto(opId, 50m, 0m, rowVersion)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ErfasseZahlungAsync(dto, ct));
        Assert.Contains("Geschäftspartner", ex.Message);
    }

    [Fact]
    public async Task SkontoVorschlagAsync_InnerhalbSkontofrist_LiefertSkontobetrag()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-{Guid.NewGuid():N}"[..12], BelegDatum = heute, KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(), LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Status = BelegStatus.Gebucht, SummeBrutto = 100m,
            ZahlungsbedingungSkontoTage = 10, ZahlungsbedingungSkontoProzent = 2m,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);
        var op = new OffenerPosten { Beleg = rechnung, KundeId = kunde.Id, Typ = OffenerPostenTyp.Debitor, Betrag = 100m, OffenerBetrag = 100m, Faelligkeit = heute };
        db.Add(op);
        await db.SaveChangesAsync(ct);

        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var vorschlag = await service.SkontoVorschlagAsync(op.Id, heute, ct);

        Assert.Equal(2m, vorschlag.SkontoBetrag);
        Assert.Equal(98m, vorschlag.ZuZahlenderBetrag);
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
