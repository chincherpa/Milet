using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.ValueObjects;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

/// <summary>
/// ZahlungService und MahnwesenService hatten bis hierher keine eigenen Tests (Befund 29 aus
/// REVIEW_2026-08-29.md) — beide schreiben auf offene Posten und damit auf die Zahlen, die später im
/// DATEV-Export landen. Geprüft werden vor allem die Guards, die der Review vom 2026-08-29 eingezogen hat
/// (Befund 7: Gesamtbetrag ohne Skonto; Befund 8: Zuordnung nur zum passenden Geschäftspartner/Typ).
/// </summary>
public sealed class FinanzenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeAId;
    private int _kundeBId;
    private int _lieferantId;

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

        var kundeA = new Kunde { Kundennummer = "KD-A", Adresse = new Adresse { Name1 = "Kunde A" } };
        var kundeB = new Kunde { Kundennummer = "KD-B", Adresse = new Adresse { Name1 = "Kunde B" } };
        var lieferant = new Lieferant { Lieferantennummer = "LF-1", Adresse = new Adresse { Name1 = "Lieferant" } };
        db.AddRange(kundeA, kundeB, lieferant);
        await db.SaveChangesAsync();

        _kundeAId = kundeA.Id;
        _kundeBId = kundeB.Id;
        _lieferantId = lieferant.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    // ---- ZahlungService ----

    [Fact]
    public async Task ErfasseZahlungAsync_MitSkonto_GesamtbetragIstNurDerGeflosseneBetrag()
    {
        var ct = TestContext.Current.CancellationToken;
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 119m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        // 119 € Forderung, 2 € Skonto, 117 € tatsächlich überwiesen.
        var ergebnis = await service.ErfasseZahlungAsync(await ZahlungMitZuordnungAsync(
            _kundeAId, OffenerPostenTyp.Debitor, opId, betrag: 117m, skonto: 2m, ct), ct);

        await using var db = new MiletDbContext(_options);
        var zahlung = await db.Zahlungen.AsNoTracking().Include(z => z.Zuordnungen)
            .FirstAsync(z => z.Id == ergebnis.Id, ct);

        // Der Wert wird im DATEV-Export gegen das Bankkonto gebucht und muss dem Kontoauszug entsprechen.
        Assert.Equal(117m, zahlung.Gesamtbetrag);
        Assert.Equal(2m, Assert.Single(zahlung.Zuordnungen).SkontoBetrag);

        var op = await db.OffenePosten.AsNoTracking().FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(0m, op.OffenerBetrag);
        Assert.Equal(OffenerPostenStatus.Ausgeglichen, op.Status);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_Teilzahlung_SetztStatusTeilweiseBezahlt()
    {
        var ct = TestContext.Current.CancellationToken;
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        await service.ErfasseZahlungAsync(await ZahlungMitZuordnungAsync(
            _kundeAId, OffenerPostenTyp.Debitor, opId, betrag: 40m, skonto: 0m, ct), ct);

        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.AsNoTracking().FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(60m, op.OffenerBetrag);
        Assert.Equal(OffenerPostenStatus.TeilweiseBezahlt, op.Status);
    }

    /// <summary>Befund 8 aus REVIEW_2026-08-29.md — sonst gliche eine Zahlung von Kunde A den Posten von
    /// Kunde B aus, und beide Personenkonten wären dauerhaft falsch, ohne dass es auffiele.</summary>
    [Fact]
    public async Task ErfasseZahlungAsync_PostenEinesAnderenKunden_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var opVonB = await NeuerDebitorPostenAsync(_kundeBId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var dto = await ZahlungMitZuordnungAsync(_kundeAId, OffenerPostenTyp.Debitor, opVonB, 100m, 0m, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ErfasseZahlungAsync(dto, ct));
        Assert.Contains("anderen Geschäftspartner", ex.Message);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_TypPasstNichtZumPosten_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var debitorPosten = await NeuerDebitorPostenAsync(_kundeAId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        // Kreditor-Zahlung auf einen Debitor-Posten.
        var dto = new ZahlungDto(
            Id: 0, KundeId: null, LieferantId: _lieferantId, Typ: OffenerPostenTyp.Kreditor,
            Zahlungsdatum: DateOnly.FromDateTime(DateTime.Today), Zahlungsart: null, Referenz: null,
            Zuordnungen: [new ZahlungZuordnungDto(debitorPosten, 100m, 0m, await RowVersionAsync(debitorPosten, ct))]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ErfasseZahlungAsync(dto, ct));
        Assert.Contains("passt nicht", ex.Message);
    }

    [Fact]
    public async Task ErfasseZahlungAsync_BetragUebersteigtOffenenPosten_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var dto = await ZahlungMitZuordnungAsync(_kundeAId, OffenerPostenTyp.Debitor, opId, 150m, 0m, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ErfasseZahlungAsync(dto, ct));
    }

    [Fact]
    public async Task ErfasseZahlungAsync_VeralteteRowVersion_MeldetKonflikt()
    {
        var ct = TestContext.Current.CancellationToken;
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 100m, ct);
        var service = new ZahlungService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var veraltet = await RowVersionAsync(opId, ct);

        // Erste Zahlung geht durch und verändert damit die RowVersion des Postens.
        await service.ErfasseZahlungAsync(await ZahlungMitZuordnungAsync(
            _kundeAId, OffenerPostenTyp.Debitor, opId, 10m, 0m, ct), ct);

        var mitAltemStand = new ZahlungDto(
            Id: 0, KundeId: _kundeAId, LieferantId: null, Typ: OffenerPostenTyp.Debitor,
            Zahlungsdatum: DateOnly.FromDateTime(DateTime.Today), Zahlungsart: null, Referenz: null,
            Zuordnungen: [new ZahlungZuordnungDto(opId, 10m, 0m, veraltet)]);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.ErfasseZahlungAsync(mitAltemStand, ct));
    }

    // ---- MahnwesenService ----

    [Fact]
    public async Task ErmittleFaelligeAsync_UeberfaelligerPosten_WirdVorgeschlagen()
    {
        var ct = TestContext.Current.CancellationToken;
        await MahnstufenAnlegenAsync(ct);
        await NeuerDebitorPostenAsync(_kundeAId, 100m, ct, faelligkeitVorTagen: 30);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var gruppen = await service.ErmittleFaelligeAsync(ct);

        var gruppe = Assert.Single(gruppen, g => g.KundeId == _kundeAId);
        Assert.Equal(1, Assert.Single(gruppe.Kandidaten).NaechsteMahnstufe);
    }

    [Fact]
    public async Task ErmittleFaelligeAsync_MitMahnsperre_WirdNichtVorgeschlagen()
    {
        var ct = TestContext.Current.CancellationToken;
        await MahnstufenAnlegenAsync(ct);
        var opId = await NeuerDebitorPostenAsync(_kundeBId, 100m, ct, faelligkeitVorTagen: 30);

        await using (var db = new MiletDbContext(_options))
        {
            var op = await db.OffenePosten.FirstAsync(o => o.Id == opId, ct);
            op.Mahnsperre = true;
            await db.SaveChangesAsync(ct);
        }

        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var gruppen = await service.ErmittleFaelligeAsync(ct);

        Assert.DoesNotContain(gruppen, g => g.KundeId == _kundeBId);
    }

    [Fact]
    public async Task MahnlaufDurchfuehrenAsync_SchreibtMahnstufeUndSnapshotFort()
    {
        var ct = TestContext.Current.CancellationToken;
        await MahnstufenAnlegenAsync(ct);
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 250m, ct, faelligkeitVorTagen: 30);
        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);

        var mahnungen = await service.MahnlaufDurchfuehrenAsync([opId], ct);

        var mahnung = Assert.Single(mahnungen);
        Assert.Equal(1, mahnung.Mahnstufe);
        // Stufe 1 hat im Testaufbau keine Gebühr — Gesamtbetrag ist damit exakt der offene Betrag.
        Assert.Equal(250m, mahnung.Gesamtbetrag);
        Assert.Equal(250m, Assert.Single(mahnung.Positionen).OffenerBetragSnapshot);

        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.AsNoTracking().FirstAsync(o => o.Id == opId, ct);
        Assert.Equal(1, op.Mahnstufe);
        // Die Gebühr wird bewusst NICHT zur Forderung (s. Mahnung.Gebuehr) — der offene Betrag bleibt unberührt.
        Assert.Equal(250m, op.OffenerBetrag);
    }

    [Fact]
    public async Task MahnlaufDurchfuehrenAsync_ZwischenzeitlichBezahlt_MahntNicht()
    {
        var ct = TestContext.Current.CancellationToken;
        await MahnstufenAnlegenAsync(ct);
        var opId = await NeuerDebitorPostenAsync(_kundeAId, 100m, ct, faelligkeitVorTagen: 30);

        await using (var db = new MiletDbContext(_options))
        {
            var op = await db.OffenePosten.FirstAsync(o => o.Id == opId, ct);
            op.OffenerBetrag = 0m;
            op.Status = OffenerPostenStatus.Ausgeglichen;
            await db.SaveChangesAsync(ct);
        }

        var service = new MahnwesenService(_factory, AllesErlaubtBerechtigungsService.Instanz);
        var mahnungen = await service.MahnlaufDurchfuehrenAsync([opId], ct);

        // Re-Check zum Ausführungszeitpunkt: die Auswahl stammt aus einer ggf. älteren Kandidatenliste.
        Assert.Empty(mahnungen);
    }

    // ---- Aufbauhilfen ----

    private async Task MahnstufenAnlegenAsync(CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        if (await db.Mahnstufen.AnyAsync(ct)) return;

        db.Mahnstufen.AddRange(
            new Mahnstufe { Stufe = 1, Karenztage = 7, Gebuehr = 0m },
            new Mahnstufe { Stufe = 2, Karenztage = 14, Gebuehr = 5m });
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> NeuerDebitorPostenAsync(
        int kundeId, decimal betrag, CancellationToken ct, int faelligkeitVorTagen = 0)
    {
        await using var db = new MiletDbContext(_options);
        var rechnung = new Rechnung
        {
            BelegNummer = $"RE-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today).AddDays(-faelligkeitVorTagen),
            KundeId = kundeId,
            Status = BelegStatus.Gebucht,
            SummeBrutto = betrag,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync(ct);

        var op = new OffenerPosten
        {
            BelegId = rechnung.Id,
            KundeId = kundeId,
            Typ = OffenerPostenTyp.Debitor,
            Betrag = betrag,
            OffenerBetrag = betrag,
            Faelligkeit = DateOnly.FromDateTime(DateTime.Today).AddDays(-faelligkeitVorTagen),
        };
        db.Add(op);
        await db.SaveChangesAsync(ct);
        return op.Id;
    }

    private async Task<byte[]> RowVersionAsync(int offenerPostenId, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        return (await db.OffenePosten.AsNoTracking().FirstAsync(o => o.Id == offenerPostenId, ct)).RowVersion;
    }

    private async Task<ZahlungDto> ZahlungMitZuordnungAsync(
        int kundeId, OffenerPostenTyp typ, int offenerPostenId, decimal betrag, decimal skonto, CancellationToken ct)
        => new(
            Id: 0, KundeId: kundeId, LieferantId: null, Typ: typ,
            Zahlungsdatum: DateOnly.FromDateTime(DateTime.Today), Zahlungsart: null, Referenz: null,
            Zuordnungen: [new ZahlungZuordnungDto(offenerPostenId, betrag, skonto, await RowVersionAsync(offenerPostenId, ct))]);

    private sealed class TestDbContextFactory(DbContextOptions<MiletDbContext> options) : IDbContextFactory<MiletDbContext>
    {
        public MiletDbContext CreateDbContext() => new(options);
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
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
