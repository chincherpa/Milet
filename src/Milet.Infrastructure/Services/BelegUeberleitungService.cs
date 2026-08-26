using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BelegUeberleitungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IBelegUeberleitungService
{
    private static readonly Dictionary<BelegTyp, BelegTyp[]> ErlaubteUebergaenge = new()
    {
        [BelegTyp.Angebot] = [BelegTyp.Auftrag],
        [BelegTyp.Auftrag] = [BelegTyp.Rechnung, BelegTyp.Lieferschein],
        [BelegTyp.Lieferschein] = [BelegTyp.Rechnung],
    };

    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        Lieferschein => BelegTyp.Lieferschein,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    public async Task<BelegDto> UeberleitenAsync(int quellBelegId, BelegTyp zielTyp, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quellBeleg = await db.Belege.Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");
        if (quellTyp == BelegTyp.Lieferschein && quellBeleg.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"Lieferschein '{quellBeleg.BelegNummer}' muss erst gebucht werden, bevor er berechnet werden kann.");

        // Offene-Mengen-Prüfung explizit in derselben Transaktion — Schutz gegen Race zweier gleichzeitiger Überleitungen.
        var quellPositionIds = quellBeleg.Positionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var zielBeleg = NeueInstanz(zielTyp);
        zielBeleg.BelegNummer = zielTyp == BelegTyp.Rechnung
            ? string.Empty
            : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(zielTyp), ct);
        zielBeleg.BelegDatum = DateOnly.FromDateTime(DateTime.Today);
        zielBeleg.KundeId = quellBeleg.KundeId;
        zielBeleg.RechnungsadresseSnapshot = quellBeleg.RechnungsadresseSnapshot.Kopie();
        zielBeleg.LieferadresseSnapshot = quellBeleg.LieferadresseSnapshot.Kopie();
        zielBeleg.ZahlungsbedingungZielTage = quellBeleg.ZahlungsbedingungZielTage;
        zielBeleg.ZahlungsbedingungSkontoTage = quellBeleg.ZahlungsbedingungSkontoTage;
        zielBeleg.ZahlungsbedingungSkontoProzent = quellBeleg.ZahlungsbedingungSkontoProzent;
        zielBeleg.Kopftext = quellBeleg.Kopftext;
        zielBeleg.Fusstext = quellBeleg.Fusstext;

        var quellVollstaendigUebernommen = true;
        var positionsNr = 1;
        foreach (var quellPosition in quellBeleg.Positionen.OrderBy(p => p.PositionsNr))
        {
            var menge = quellPosition.PositionsTyp == PositionsTyp.Artikel
                ? BelegPosition.OffeneMenge(quellPosition, folgepositionen)
                : quellPosition.Menge;

            if (quellPosition.PositionsTyp == PositionsTyp.Artikel && menge <= 0)
                continue;

            if (quellPosition.PositionsTyp == PositionsTyp.Artikel && menge < quellPosition.Menge)
                quellVollstaendigUebernommen = false;

            zielBeleg.Positionen.Add(new BelegPosition
            {
                PositionsNr = positionsNr++,
                PositionsTyp = quellPosition.PositionsTyp,
                ArtikelId = quellPosition.ArtikelId,
                Bezeichnung = quellPosition.Bezeichnung,
                EinheitKuerzel = quellPosition.EinheitKuerzel,
                Menge = menge,
                Einzelpreis = quellPosition.Einzelpreis,
                RabattProzent = quellPosition.RabattProzent,
                MwStSatzId = quellPosition.MwStSatzId,
                MwStSatzWert = quellPosition.MwStSatzWert,
                SteuerSchluessel = quellPosition.SteuerSchluessel,
                GesamtNetto = SteuerRechner.BerechnePosition(menge, quellPosition.Einzelpreis, quellPosition.RabattProzent),
                UrsprungsPositionId = quellPosition.Id,
            });
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");

        var steuersummen = SteuerRechner.BerechneSteuersummen(zielBeleg.Positionen);
        zielBeleg.Steuersummen = steuersummen.ToList();
        (zielBeleg.SummeNetto, zielBeleg.SummeMwSt, zielBeleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(zielBeleg);

        if (quellVollstaendigUebernommen && quellBeleg.Status is BelegStatus.Entwurf or BelegStatus.Gebucht)
            quellBeleg.Status = BelegStatus.Erledigt;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return zielBeleg.ToDto(mitPositionen: true);
    }

    public async Task<IReadOnlyList<OffenePositionDto>> LadeOffenePositionenAsync(int quellBelegId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var quellBeleg = await db.Belege.AsNoTracking().Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellPositionIds = quellBeleg.Positionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        return quellBeleg.Positionen
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel)
            .Select(p => new OffenePositionDto(p.Id, p.Bezeichnung, p.EinheitKuerzel, BelegPosition.OffeneMenge(p, folgepositionen)))
            .Where(p => p.OffeneMenge > 0)
            .ToList();
    }

    /// <summary>Wie <see cref="UeberleitenAsync"/>, aber mit expliziter Menge je Quellposition (Teillieferung) statt automatisch voller offener Menge.
    /// Bewusst als eigene Methode statt Parametrisierung von <see cref="UeberleitenAsync"/> — beide Pfade sind klar genug getrennt (voll vs. Auswahl),
    /// eine gemeinsame Abstraktion würde hier mehr Indirektion als Nutzen bringen.</summary>
    public async Task<BelegDto> UeberleitenMitAuswahlAsync(
        int quellBelegId, BelegTyp zielTyp, IReadOnlyDictionary<int, decimal> mengenJePosition, int? lagerortId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quellBeleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Kunde)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");

        if (zielTyp == BelegTyp.Lieferschein)
        {
            if (lagerortId is null)
                throw new InvalidOperationException("Lagerort ist für die Lieferschein-Erstellung erforderlich.");
            if (quellBeleg.Kunde?.Liefersperre == true)
                throw new InvalidOperationException($"Kunde '{quellBeleg.Kunde.Kundennummer}' hat Liefersperre.");
        }

        var quellPositionIds = quellBeleg.Positionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var zielBeleg = NeueInstanz(zielTyp);
        zielBeleg.BelegNummer = zielTyp == BelegTyp.Rechnung
            ? string.Empty
            : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(zielTyp), ct);
        zielBeleg.BelegDatum = DateOnly.FromDateTime(DateTime.Today);
        zielBeleg.KundeId = quellBeleg.KundeId;
        zielBeleg.RechnungsadresseSnapshot = quellBeleg.RechnungsadresseSnapshot.Kopie();
        zielBeleg.LieferadresseSnapshot = quellBeleg.LieferadresseSnapshot.Kopie();
        zielBeleg.ZahlungsbedingungZielTage = quellBeleg.ZahlungsbedingungZielTage;
        zielBeleg.ZahlungsbedingungSkontoTage = quellBeleg.ZahlungsbedingungSkontoTage;
        zielBeleg.ZahlungsbedingungSkontoProzent = quellBeleg.ZahlungsbedingungSkontoProzent;
        zielBeleg.Kopftext = quellBeleg.Kopftext;
        zielBeleg.Fusstext = quellBeleg.Fusstext;

        var quellVollstaendigUebernommen = true;
        var positionsNr = 1;
        foreach (var quellPosition in quellBeleg.Positionen.OrderBy(p => p.PositionsNr))
        {
            if (quellPosition.PositionsTyp != PositionsTyp.Artikel) continue;

            var offeneMenge = BelegPosition.OffeneMenge(quellPosition, folgepositionen);
            if (!mengenJePosition.TryGetValue(quellPosition.Id, out var gewaehlteMenge) || gewaehlteMenge <= 0)
            {
                if (offeneMenge > 0) quellVollstaendigUebernommen = false;
                continue;
            }

            // Erneute Prüfung in derselben Transaktion — Schutz gegen Race zweier gleichzeitiger Teillieferungen aus demselben Auftrag.
            if (gewaehlteMenge > offeneMenge)
                throw new InvalidOperationException(
                    $"Position {quellPosition.PositionsNr}: gewählte Menge ({gewaehlteMenge}) übersteigt offene Menge ({offeneMenge}).");

            if (gewaehlteMenge < offeneMenge) quellVollstaendigUebernommen = false;

            zielBeleg.Positionen.Add(new BelegPosition
            {
                PositionsNr = positionsNr++,
                PositionsTyp = PositionsTyp.Artikel,
                ArtikelId = quellPosition.ArtikelId,
                Bezeichnung = quellPosition.Bezeichnung,
                EinheitKuerzel = quellPosition.EinheitKuerzel,
                Menge = gewaehlteMenge,
                Einzelpreis = quellPosition.Einzelpreis,
                RabattProzent = quellPosition.RabattProzent,
                MwStSatzId = quellPosition.MwStSatzId,
                MwStSatzWert = quellPosition.MwStSatzWert,
                SteuerSchluessel = quellPosition.SteuerSchluessel,
                GesamtNetto = SteuerRechner.BerechnePosition(gewaehlteMenge, quellPosition.Einzelpreis, quellPosition.RabattProzent),
                UrsprungsPositionId = quellPosition.Id,
                LagerortId = zielTyp == BelegTyp.Lieferschein ? lagerortId : null,
            });
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine Positionen zum Überleiten ausgewählt.");

        var steuersummen = SteuerRechner.BerechneSteuersummen(zielBeleg.Positionen);
        zielBeleg.Steuersummen = steuersummen.ToList();
        (zielBeleg.SummeNetto, zielBeleg.SummeMwSt, zielBeleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(zielBeleg);

        if (quellVollstaendigUebernommen && quellBeleg.Status == BelegStatus.Entwurf)
            quellBeleg.Status = BelegStatus.Erledigt;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return zielBeleg.ToDto(mitPositionen: true);
    }
}
