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
        [BelegTyp.Bestellung] = [BelegTyp.Wareneingang],
        [BelegTyp.Wareneingang] = [BelegTyp.Eingangsrechnung],
    };

    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        Lieferschein => BelegTyp.Lieferschein,
        Bestellung => BelegTyp.Bestellung,
        Wareneingang => BelegTyp.Wareneingang,
        Eingangsrechnung => BelegTyp.Eingangsrechnung,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        BelegTyp.Bestellung => new Bestellung(),
        BelegTyp.Wareneingang => new Wareneingang(),
        BelegTyp.Eingangsrechnung => new Eingangsrechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        BelegTyp.Bestellung => "BE",
        BelegTyp.Wareneingang => "WE",
        BelegTyp.Eingangsrechnung => "ER",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    public async Task<BelegDto> UeberleitenAsync(int quellBelegId, BelegTyp zielTyp, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // UPDLOCK+ROWLOCK auf dem Quellbeleg: sperrt die Zeile für die Dauer der Transaktion, damit zwei
        // gleichzeitige Überleitungen aus demselben Quellbeleg nicht beide auf demselben (veralteten)
        // Stand der offenen Menge aufsetzen können (READ COMMITTED ohne Sperre würde das zulassen).
        var quellBeleg = await db.Belege
            .FromSqlInterpolated($"SELECT * FROM Belege WITH (UPDLOCK, ROWLOCK) WHERE Id = {quellBelegId}")
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");
        if (zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang)
            throw new InvalidOperationException($"{zielTyp}-Erstellung erfordert Mengenauswahl und Lagerort — verwenden Sie UeberleitenMitAuswahlAsync.");
        if (quellTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang && quellBeleg.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"{quellTyp} '{quellBeleg.BelegNummer}' muss erst gebucht werden, bevor er überführt werden kann.");

        // Offene-Mengen-Berechnung liest jetzt unter dem oben genommenen UPDLOCK — eine zweite, gleichzeitig
        // laufende Überleitung aus demselben Quellbeleg blockiert an der UPDLOCK-Zeile, bis diese Transaktion
        // committet/rollt zurück, und sieht danach die bereits aktualisierten Folgepositionen.
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
        zielBeleg.LieferantId = quellBeleg.LieferantId;
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

        // UPDLOCK+ROWLOCK auf dem Quellbeleg — s. Begründung in UeberleitenAsync (Schutz gegen parallele Teillieferung/Sammelrechnung).
        var quellBeleg = await db.Belege
            .FromSqlInterpolated($"SELECT * FROM Belege WITH (UPDLOCK, ROWLOCK) WHERE Id = {quellBelegId}")
            .Include(b => b.Positionen).Include(b => b.Kunde)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");
        if (quellTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang && quellBeleg.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"{quellTyp} '{quellBeleg.BelegNummer}' muss erst gebucht werden, bevor er überführt werden kann.");

        if (zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang)
        {
            if (lagerortId is null)
                throw new InvalidOperationException($"Lagerort ist für die {zielTyp}-Erstellung erforderlich.");
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
        zielBeleg.LieferantId = quellBeleg.LieferantId;
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
            if (quellPosition.PositionsTyp != PositionsTyp.Artikel)
            {
                // Freitext-/Zwischensummen-Zeilen haben keine Mengenauswahl — sie werden auf die erste
                // Teilüberleitung mitgenommen (nicht verworfen wie zuvor) und danach nicht erneut dupliziert,
                // sobald bereits eine Folgeposition auf sie referenziert.
                if (folgepositionen.Any(p => p.UrsprungsPositionId == quellPosition.Id))
                    continue;

                zielBeleg.Positionen.Add(new BelegPosition
                {
                    PositionsNr = positionsNr++,
                    PositionsTyp = quellPosition.PositionsTyp,
                    Bezeichnung = quellPosition.Bezeichnung,
                    EinheitKuerzel = quellPosition.EinheitKuerzel,
                    Menge = quellPosition.Menge,
                    Einzelpreis = quellPosition.Einzelpreis,
                    RabattProzent = quellPosition.RabattProzent,
                    MwStSatzId = quellPosition.MwStSatzId,
                    MwStSatzWert = quellPosition.MwStSatzWert,
                    SteuerSchluessel = quellPosition.SteuerSchluessel,
                    GesamtNetto = SteuerRechner.BerechnePosition(quellPosition.Menge, quellPosition.Einzelpreis, quellPosition.RabattProzent),
                    UrsprungsPositionId = quellPosition.Id,
                });
                continue;
            }

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
                LagerortId = zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang ? lagerortId : null,
            });
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine Positionen zum Überleiten ausgewählt.");

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

    /// <summary>Führt mehrere Quellbelege gleichen Kunden/gleicher Zahlungsbedingung in einen Zielbeleg zusammen (Sammelrechnung).</summary>
    public async Task<BelegDto> UeberleitenMehrereAsync(IReadOnlyList<int> quellBelegIds, BelegTyp zielTyp, CancellationToken ct = default)
    {
        if (quellBelegIds.Count == 0)
            throw new InvalidOperationException("Mindestens ein Quellbeleg erforderlich.");

        // Sammel-Eingangsrechnung aus mehreren Wareneingängen ist explizit out of scope (Phase-4-Plan) — die
        // Methode wurde nur für den Verkaufs-Fall (mehrere Lieferscheine -> eine Sammelrechnung) gebaut und
        // kennt kein LieferantId auf dem Zielbeleg, keinen Schutz vor unterschiedlichen Lieferanten (null != null
        // wäre immer false) und keinen generalisierten Gebucht-Guard für Wareneingang. ErlaubteUebergaenge lässt
        // Wareneingang->Eingangsrechnung inzwischen technisch zu (für den 1:1-Pfad in UeberleitenAsync/-MitAuswahlAsync),
        // also muss hier ein expliziter Schutz stehen statt sich auf "keine UI ruft das so auf" zu verlassen.
        if (zielTyp.IstEinkaufsBeleg())
            throw new InvalidOperationException("Sammel-Eingangsrechnung aus mehreren Wareneingängen wird nicht unterstützt.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // UPDLOCK+ROWLOCK auf allen Quellbelegen — s. Begründung in UeberleitenAsync. Platzhalter statt
        // eingebetteter Werte: echte SQL-Parameter, kein String-Interpolation-Injection-Risiko.
        var idPlatzhalter = string.Join(",", quellBelegIds.Select((_, i) => $"{{{i}}}"));
        var sperrSql = $"SELECT * FROM Belege WITH (UPDLOCK, ROWLOCK) WHERE Id IN ({idPlatzhalter})";
        var quellBelege = await db.Belege
            .FromSqlRaw(sperrSql, [.. quellBelegIds.Cast<object>()])
            .Include(b => b.Positionen)
            .ToListAsync(ct);
        if (quellBelege.Count != quellBelegIds.Count)
            throw new NotFoundException(nameof(Beleg), string.Join(",", quellBelegIds));

        if (quellBelege.Any(b => TypVon(b).IstEinkaufsBeleg()))
            throw new InvalidOperationException("Sammel-Eingangsrechnung aus mehreren Wareneingängen wird nicht unterstützt.");

        var ersterBeleg = quellBelege[0];
        var ersteZahlungsbedingung = (ersterBeleg.ZahlungsbedingungZielTage, ersterBeleg.ZahlungsbedingungSkontoTage, ersterBeleg.ZahlungsbedingungSkontoProzent);
        foreach (var beleg in quellBelege)
        {
            var typ = TypVon(beleg);
            if (!ErlaubteUebergaenge.TryGetValue(typ, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
                throw new InvalidOperationException($"Überleitung von {typ} nach {zielTyp} wird nicht unterstützt.");
            if (typ == BelegTyp.Lieferschein && beleg.Status != BelegStatus.Gebucht)
                throw new InvalidOperationException($"Lieferschein '{beleg.BelegNummer}' muss erst gebucht werden, bevor er berechnet werden kann.");
            if (beleg.KundeId != ersterBeleg.KundeId)
                throw new InvalidOperationException("Sammelüberleitung nur für Belege desselben Kunden möglich.");
            if ((beleg.ZahlungsbedingungZielTage, beleg.ZahlungsbedingungSkontoTage, beleg.ZahlungsbedingungSkontoProzent) != ersteZahlungsbedingung)
                throw new InvalidOperationException("Sammelüberleitung nur für Belege derselben Zahlungsbedingung möglich.");
        }

        var alleQuellPositionIds = quellBelege.SelectMany(b => b.Positionen).Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && alleQuellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var zielBeleg = NeueInstanz(zielTyp);
        zielBeleg.BelegNummer = zielTyp == BelegTyp.Rechnung
            ? string.Empty
            : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(zielTyp), ct);
        zielBeleg.BelegDatum = DateOnly.FromDateTime(DateTime.Today);
        zielBeleg.KundeId = ersterBeleg.KundeId;
        zielBeleg.RechnungsadresseSnapshot = ersterBeleg.RechnungsadresseSnapshot.Kopie();
        zielBeleg.LieferadresseSnapshot = ersterBeleg.LieferadresseSnapshot.Kopie();
        zielBeleg.ZahlungsbedingungZielTage = ersterBeleg.ZahlungsbedingungZielTage;
        zielBeleg.ZahlungsbedingungSkontoTage = ersterBeleg.ZahlungsbedingungSkontoTage;
        zielBeleg.ZahlungsbedingungSkontoProzent = ersterBeleg.ZahlungsbedingungSkontoProzent;

        var positionsNr = 1;
        foreach (var quellBeleg in quellBelege.OrderBy(b => b.BelegDatum).ThenBy(b => b.Id))
        {
            var quellVollstaendigUebernommen = true;
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

            if (quellVollstaendigUebernommen && quellBeleg.Status is BelegStatus.Entwurf or BelegStatus.Gebucht)
                quellBeleg.Status = BelegStatus.Erledigt;
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");

        var steuersummen = SteuerRechner.BerechneSteuersummen(zielBeleg.Positionen);
        zielBeleg.Steuersummen = steuersummen.ToList();
        (zielBeleg.SummeNetto, zielBeleg.SummeMwSt, zielBeleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(zielBeleg);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return zielBeleg.ToDto(mitPositionen: true);
    }
}
