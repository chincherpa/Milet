using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

/// <summary>Storno gebuchter Belege — Gegenbuchung statt Löschen/Ändern (GoBD, s. BelegImmutabilityInterceptor,
/// der Gebucht|Erledigt → Storniert als einzige inhaltliche Ausnahme zulässt). Bewusste Scope-Grenzen dieser
/// v1 (s. docs/superpowers/plans/2026-08-31-luecken-schliessen.md, Task 6):
/// - Rechnung-Storno lehnt eine bereits (teil-)bezahlte Rechnung ab — ein echter Rückzahlungsfall braucht
///   manuelle Klärung, keine automatische Rückbuchung.
/// - Lieferschein-/Wareneingang-Storno lehnt ab, wenn bereits ein nicht-stornierter Folgebeleg existiert
///   (Rechnung bzw. Eingangsrechnung) — sonst würde ein Storno stillschweigend hinter einer bereits
///   abgerechneten Menge herbuchen.
/// - Wareneingang-Storno lehnt seriennummernpflichtige Artikel ab (Rückabwicklung neu angelegter
///   Seriennummern ist nicht automatisiert, s. Methode).
/// Der Grund hat kein eigenes Feld (das bekäme erst Lagerbewegung.Bemerkung/Grund in einem späteren Block,
/// s. Plan Task 13) — er landet bei Lieferschein/Wareneingang im Fusstext (vom Interceptor als einzige neben
/// Status erlaubte Änderung akzeptiert) und bei der Rechnung im Kopftext der neuen Gutschrift.</summary>
public sealed class StornoService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung,
    ICurrentUserService currentUser) : IStornoService
{
    private const int MaxGrundLaenge = 200;

    public async Task<BelegDto> StorniereRechnungAsync(int rechnungId, string grund, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Verkauf);
        PruefeGrund(grund);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var rechnung = await db.Rechnungen.Include(r => r.Positionen)
            .FirstOrDefaultAsync(r => r.Id == rechnungId, ct)
            ?? throw new NotFoundException(nameof(Rechnung), rechnungId);

        if (rechnung.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"Rechnung '{rechnung.BelegNummer}' ist nicht gebucht und kann nicht storniert werden.");

        var offenerPosten = await db.OffenePosten.FirstOrDefaultAsync(o => o.BelegId == rechnungId, ct)
            ?? throw new InvalidOperationException($"Zu Rechnung '{rechnung.BelegNummer}' existiert kein offener Posten.");

        // Bewusste Scope-Grenze: eine bereits (teil-)bezahlte Rechnung automatisiert zu stornieren würde eine
        // echte Rückzahlung an den Kunden implizieren — das ist ein Vorgang außerhalb dieses Belegs (Überweisung,
        // Verrechnung mit einer anderen Forderung, ...) und wird hier bewusst nicht miterledigt.
        if (offenerPosten.OffenerBetrag != offenerPosten.Betrag)
            throw new InvalidOperationException(
                $"Rechnung '{rechnung.BelegNummer}' wurde bereits (teilweise) bezahlt — ein automatisches Storno ist dafür nicht vorgesehen. Zahlungsausgleich bitte manuell klären.");

        var gutschrift = new Gutschrift
        {
            BelegNummer = await NumberRangeService.NaechsteNummerAsync(db, "GS", ct),
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = rechnung.KundeId,
            RechnungsadresseSnapshot = rechnung.RechnungsadresseSnapshot.Kopie(),
            LieferadresseSnapshot = rechnung.LieferadresseSnapshot.Kopie(),
            ZahlungsbedingungZielTage = rechnung.ZahlungsbedingungZielTage,
            ZahlungsbedingungSkontoTage = rechnung.ZahlungsbedingungSkontoTage,
            ZahlungsbedingungSkontoProzent = rechnung.ZahlungsbedingungSkontoProzent,
            Status = BelegStatus.Gebucht,
            StorniertenBelegId = rechnung.Id,
            Kopftext = $"Storno zu Rechnung {rechnung.BelegNummer}. Grund: {grund}",
        };

        // Positionen unverändert gespiegelt (nicht vorzeichenumgekehrt): die Gutschrift liest sich wie ein
        // normales Dokument ("was wird gutgeschrieben"), der negative Anspruch entsteht über den OffenerPosten
        // unten, nicht über negative Positionsmengen (vermeidet ungetestetes Verhalten von SteuerRechner bei
        // negativen Mengen).
        var positionsNr = 1;
        foreach (var quellPosition in rechnung.Positionen.OrderBy(p => p.PositionsNr))
        {
            gutschrift.Positionen.Add(new BelegPosition
            {
                PositionsNr = positionsNr++,
                PositionsTyp = quellPosition.PositionsTyp,
                ArtikelId = quellPosition.ArtikelId,
                Bezeichnung = quellPosition.Bezeichnung,
                EinheitKuerzel = quellPosition.EinheitKuerzel,
                Menge = quellPosition.Menge,
                Einzelpreis = quellPosition.Einzelpreis,
                RabattProzent = quellPosition.RabattProzent,
                MwStSatzId = quellPosition.MwStSatzId,
                MwStSatzWert = quellPosition.MwStSatzWert,
                SteuerSchluessel = quellPosition.SteuerSchluessel,
                GesamtNetto = quellPosition.GesamtNetto,
                UrsprungsPositionId = quellPosition.Id,
            });
        }

        var steuersummen = SteuerRechner.BerechneSteuersummen(gutschrift.Positionen);
        gutschrift.Steuersummen = steuersummen.ToList();
        (gutschrift.SummeNetto, gutschrift.SummeMwSt, gutschrift.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(gutschrift);

        // Der ursprüngliche OP ist erledigt (die Forderung besteht nicht mehr); der negative Anspruch aus der
        // Gutschrift ist ein eigener OP (PLAN.md: „Gutschrift = negativer OP") — beide zusammen ausgeglichen,
        // weil aus dem Storno selbst kein Zahlungsfluss entsteht (das würde erst ein späterer Ausgleich tun,
        // falls der Kunde die Gutschrift gegen eine andere Forderung verrechnet — außerhalb dieses Scopes).
        offenerPosten.OffenerBetrag = 0m;
        offenerPosten.Status = OffenerPostenStatus.Ausgeglichen;

        db.OffenePosten.Add(new OffenerPosten
        {
            Beleg = gutschrift,
            KundeId = rechnung.KundeId,
            Typ = OffenerPostenTyp.Debitor,
            Betrag = -gutschrift.SummeBrutto,
            OffenerBetrag = 0m,
            Faelligkeit = gutschrift.BelegDatum,
            Status = OffenerPostenStatus.Ausgeglichen,
        });

        rechnung.Status = BelegStatus.Storniert;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return gutschrift.ToDto(mitPositionen: true);
    }

    public async Task<BelegDto> StorniereLieferscheinAsync(int lieferscheinId, string grund, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        PruefeGrund(grund);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var lieferschein = await db.Lieferscheine.Include(l => l.Positionen)
            .FirstOrDefaultAsync(l => l.Id == lieferscheinId, ct)
            ?? throw new NotFoundException(nameof(Lieferschein), lieferscheinId);

        if (lieferschein.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"Lieferschein '{lieferschein.BelegNummer}' ist nicht gebucht und kann nicht storniert werden.");

        await PruefeKeineAktivenFolgebelegeAsync(db, lieferschein.Positionen.Select(p => p.Id).ToList(), lieferschein.BelegNummer, ct);

        var artikelIds = lieferschein.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel && p.ArtikelId != null)
            .Select(p => p.ArtikelId!.Value).Distinct().ToList();
        var artikelJeId = await db.Artikel.AsNoTracking().Where(a => artikelIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        foreach (var position in lieferschein.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId) continue;

            // Positiver Rückgang — spiegelbildlich zur negativen Buchung beim Lieferschein-Buchen
            // (LieferscheinBuchenService), gleiche Dimensionen (Sektion/Kulturstufe).
            await BestandService.BucheBewegungAsync(
                db, artikelId, lagerortId, position.Menge, LagerbewegungTyp.StornoRueckgabe, position.Id, ct,
                position.SektionId, position.KulturstufeId,
                $"Storno Lieferschein {lieferschein.BelegNummer}: {grund}", currentUser.BenutzerId);

            if (artikelJeId[artikelId].HatSeriennummern)
            {
                var seriennummern = await db.BelegPositionSeriennummern
                    .Where(bs => bs.BelegPositionId == position.Id)
                    .Select(bs => bs.Seriennummer!)
                    .ToListAsync(ct);
                foreach (var seriennummer in seriennummern)
                {
                    seriennummer.Status = SeriennummerStatus.AufLager;
                    seriennummer.LagerortId = lagerortId;
                }
            }
        }

        lieferschein.Status = BelegStatus.Storniert;
        lieferschein.Fusstext = MitStornoHinweis(lieferschein.Fusstext, grund);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return lieferschein.ToDto(mitPositionen: true);
    }

    public async Task<BelegDto> StorniereWareneingangAsync(int wareneingangId, string grund, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Einkauf);
        PruefeGrund(grund);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var wareneingang = await db.Wareneingaenge.Include(w => w.Positionen)
            .FirstOrDefaultAsync(w => w.Id == wareneingangId, ct)
            ?? throw new NotFoundException(nameof(Wareneingang), wareneingangId);

        if (wareneingang.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"Wareneingang '{wareneingang.BelegNummer}' ist nicht gebucht und kann nicht storniert werden.");

        await PruefeKeineAktivenFolgebelegeAsync(db, wareneingang.Positionen.Select(p => p.Id).ToList(), wareneingang.BelegNummer, ct);

        var artikelPositionen = wareneingang.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel).ToList();
        var artikelIds = artikelPositionen.Where(p => p.ArtikelId != null).Select(p => p.ArtikelId!.Value).Distinct().ToList();
        var artikelJeId = await db.Artikel.AsNoTracking().Where(a => artikelIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        // Vorab-Check über alle Positionen: seriennummernpflichtige Artikel würden neu angelegte Seriennummern
        // hinterlassen, deren Rückabwicklung (Löschen kollidiert mit dem Restrict-FK auf BelegPositionSeriennummer,
        // ein Statuswechsel ist unklar bei bereits weitergegebenen Nummern) hier bewusst nicht automatisiert wird.
        foreach (var position in artikelPositionen)
        {
            if (position.ArtikelId is { } artikelId && artikelJeId[artikelId].HatSeriennummern)
                throw new InvalidOperationException(
                    $"Position {position.PositionsNr}: Wareneingänge mit seriennummernpflichtigen Artikeln können derzeit nicht automatisiert storniert werden — Seriennummern und Bestand bitte manuell prüfen und korrigieren.");
        }

        foreach (var position in artikelPositionen)
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId) continue;

            // Negativer Rückgang — schlägt mit der bestehenden, verständlichen Fehlermeldung aus
            // BestandService fehl, wenn die Ware bereits weiterverkauft/-verarbeitet wurde (kein SQL-Fehler).
            await BestandService.BucheBewegungAsync(
                db, artikelId, lagerortId, -position.Menge, LagerbewegungTyp.StornoRueckgabe, position.Id, ct,
                position.SektionId, position.KulturstufeId,
                $"Storno Wareneingang {wareneingang.BelegNummer}: {grund}", currentUser.BenutzerId);
        }

        wareneingang.Status = BelegStatus.Storniert;
        wareneingang.Fusstext = MitStornoHinweis(wareneingang.Fusstext, grund);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return wareneingang.ToDto(mitPositionen: true);
    }

    private static void PruefeGrund(string grund)
    {
        if (string.IsNullOrWhiteSpace(grund))
            throw new ArgumentException("Ein Storno erfordert einen Grund.", nameof(grund));
        if (grund.Length > MaxGrundLaenge)
            throw new ArgumentException($"Grund darf höchstens {MaxGrundLaenge} Zeichen lang sein.", nameof(grund));
    }

    private static string MitStornoHinweis(string? bestehenderFusstext, string grund)
    {
        var hinweis = $"Storniert: {grund}";
        return string.IsNullOrWhiteSpace(bestehenderFusstext) ? hinweis : $"{bestehenderFusstext}\n{hinweis}";
    }

    /// <summary>Blockiert das Storno, solange bereits ein nicht-stornierter Folgebeleg auf eine der Positionen
    /// verweist (Lieferschein→Rechnung, Wareneingang→Eingangsrechnung) — sonst könnte ein Storno rückwirkend
    /// unter eine bereits abgerechnete/fakturierte Menge buchen, ohne dass der Folgebeleg davon etwas weiß.</summary>
    private static async Task PruefeKeineAktivenFolgebelegeAsync(
        MiletDbContext db, IReadOnlyList<int> positionIds, string belegNummer, CancellationToken ct)
    {
        if (positionIds.Count == 0) return;

        var hatAktivenFolgebeleg = await db.BelegPositionen
            .Where(p => p.UrsprungsPositionId != null && positionIds.Contains(p.UrsprungsPositionId.Value))
            .AnyAsync(p => p.Beleg!.Status != BelegStatus.Storniert, ct);

        if (hatAktivenFolgebeleg)
            throw new InvalidOperationException(
                $"Beleg '{belegNummer}' wurde bereits weiterverarbeitet (Folgebeleg vorhanden) — zuerst dessen Storno vornehmen.");
    }
}
