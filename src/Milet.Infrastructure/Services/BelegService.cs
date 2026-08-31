using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BelegService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IBelegService
{
    private static readonly BelegValidator Validator = new();

    private static IQueryable<Beleg> SetFuerTyp(MiletDbContext db, BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => db.Angebote,
        BelegTyp.Auftrag => db.Auftraege,
        BelegTyp.Rechnung => db.Rechnungen,
        BelegTyp.Lieferschein => db.Lieferscheine,
        BelegTyp.Bestellung => db.Bestellungen,
        BelegTyp.Wareneingang => db.Wareneingaenge,
        BelegTyp.Eingangsrechnung => db.Eingangsrechnungen,
        BelegTyp.Gutschrift => db.Gutschriften,
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
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
        BelegTyp.Gutschrift => new Gutschrift(),
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
        BelegTyp.Gutschrift => "GS",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    public async Task<IReadOnlyList<BelegDto>> SucheAsync(BelegTyp typ, string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = SetFuerTyp(db, typ).AsNoTracking().Include(b => b.Kunde).Include(b => b.Lieferant).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.BelegNummer, $"%{s}%") ||
                (b.Kunde != null && EF.Functions.Like(b.Kunde.Adresse.Name1, $"%{s}%")) ||
                (b.Lieferant != null && EF.Functions.Like(b.Lieferant.Adresse.Name1, $"%{s}%")));
        }
        var belege = await query.OrderByDescending(b => b.BelegDatum).ThenByDescending(b => b.Id).Take(500).ToListAsync(ct);
        return belege.Select(b => b.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<BelegDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.AsNoTracking()
            .Include(b => b.Kunde)
            .Include(b => b.Lieferant)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        return beleg.ToDto(mitPositionen: true);
    }

    public async Task<BelegDto> SpeichereAsync(BelegDto dto, CancellationToken ct = default)
    {
        // Vorabprüfung anhand des DTO-Typs: im Neuanlage-Pfad gibt es noch keinen Beleg, aus dem sich
        // das Recht ableiten ließe. Im Update-Pfad ist sie nur die erste Hürde — maßgeblich ist die
        // zweite Prüfung unten gegen den tatsächlich geladenen Subtyp (der Aufrufer darf den Typ nicht
        // selbst bestimmen).
        berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(dto.BelegTyp));
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        // Transaktion umschließt Nummernvergabe und Speichern: schlägt das Speichern fehl, rollt die
        // vergebene Belegnummer mit zurück (sonst entstünde eine Lücke im Nummernkreis).
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        Beleg beleg;
        if (dto.Id == 0)
        {
            beleg = NeueInstanz(dto.BelegTyp);
            beleg.BelegNummer = dto.BelegTyp == BelegTyp.Rechnung
                ? string.Empty
                : await NumberRangeService.NaechsteNummerAsync(db, NummernkreisCode(dto.BelegTyp), ct);

            if (dto.BelegTyp.IstEinkaufsBeleg())
            {
                var lieferant = await db.Lieferanten.Include(l => l.Zahlungsbedingung)
                    .FirstOrDefaultAsync(l => l.Id == dto.LieferantId, ct)
                    ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Lieferant), dto.LieferantId ?? 0);
                var firma = await db.Firmenstamm.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);

                beleg.LieferantId = lieferant.Id;
                // Invertierte Semantik ggü. Verkauf (siehe Architektur-Entscheidung 5 im Phase-4-Plan):
                // "Rechnungsadresse" = Anschrift des Geschäftspartners (hier: Lieferant), "Lieferadresse" =
                // wohin die Ware geht (hier: die eigene Firma, nicht der Lieferant).
                beleg.RechnungsadresseSnapshot = lieferant.Adresse.Kopie();
                beleg.LieferadresseSnapshot = firma?.Adresse.Kopie() ?? lieferant.Adresse.Kopie();
                beleg.ZahlungsbedingungZielTage = lieferant.Zahlungsbedingung?.ZielTage ?? 0;
                beleg.ZahlungsbedingungSkontoTage = lieferant.Zahlungsbedingung?.SkontoTage;
                beleg.ZahlungsbedingungSkontoProzent = lieferant.Zahlungsbedingung?.SkontoProzent;
            }
            else
            {
                var kunde = await db.Kunden.Include(k => k.Zahlungsbedingung).FirstOrDefaultAsync(k => k.Id == dto.KundeId, ct)
                    ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Kunde), dto.KundeId);

                beleg.KundeId = kunde.Id;
                beleg.RechnungsadresseSnapshot = kunde.Adresse.Kopie();
                beleg.LieferadresseSnapshot = kunde.Adresse.Kopie();
                beleg.ZahlungsbedingungZielTage = kunde.Zahlungsbedingung?.ZielTage ?? 0;
                beleg.ZahlungsbedingungSkontoTage = kunde.Zahlungsbedingung?.SkontoTage;
                beleg.ZahlungsbedingungSkontoProzent = kunde.Zahlungsbedingung?.SkontoProzent;
            }

            db.Add(beleg);
        }
        else
        {
            beleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Steuersummen)
                .FirstOrDefaultAsync(b => b.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Beleg), dto.Id);

            // db.Belege lädt typunabhängig — ohne diese Prüfung könnte ein DTO mit BelegTyp = Angebot
            // (Recht Verkauf) und der Id einer Bestellung (Recht Einkauf) den Guard oben passieren und
            // anschließend den Einkaufsbeleg ändern.
            var tatsaechlicherTyp = BelegTypErweiterung.TypVon(beleg);
            if (tatsaechlicherTyp != dto.BelegTyp)
                throw new InvalidOperationException(
                    $"Beleg '{beleg.BelegNummer}' ist vom Typ {tatsaechlicherTyp}, übergeben wurde {dto.BelegTyp}.");
            berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(tatsaechlicherTyp));

            if (beleg.Status != BelegStatus.Entwurf)
                throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht mehr geändert werden.");

            db.Entry(beleg).Property(b => b.RowVersion).OriginalValue = dto.RowVersion;
        }

        beleg.BelegDatum = dto.BelegDatum;
        beleg.Leistungsdatum = dto.Leistungsdatum;
        beleg.Kopftext = dto.Kopftext;
        beleg.Fusstext = dto.Fusstext;
        beleg.ExterneReferenz = dto.ExterneReferenz;

        AktualisierePositionen(db, beleg, dto.Positionen);

        db.RemoveRange(beleg.Steuersummen);
        var neueSteuersummen = SteuerRechner.BerechneSteuersummen(beleg.Positionen);
        beleg.Steuersummen = neueSteuersummen.ToList();
        (beleg.SummeNetto, beleg.SummeMwSt, beleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(neueSteuersummen);

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Beleg), beleg.Id, ct);
        await transaktion.CommitAsync(ct);
        return beleg.ToDto(mitPositionen: true);
    }

    private static void AktualisierePositionen(MiletDbContext db, Beleg beleg, IReadOnlyList<BelegPositionDto> positionenDto)
    {
        var vorhandeneIds = positionenDto.Where(p => p.Id != 0).Select(p => p.Id).ToHashSet();
        var zuEntfernen = beleg.Positionen.Where(p => !vorhandeneIds.Contains(p.Id)).ToList();
        foreach (var entfernt in zuEntfernen)
        {
            beleg.Positionen.Remove(entfernt);
            db.Remove(entfernt);
        }

        foreach (var dtoPos in positionenDto)
        {
            var gesamtNetto = SteuerRechner.BerechnePosition(dtoPos.Menge, dtoPos.Einzelpreis, dtoPos.RabattProzent);
            var bestehend = dtoPos.Id != 0 ? beleg.Positionen.FirstOrDefault(p => p.Id == dtoPos.Id) : null;
            if (bestehend is not null)
            {
                bestehend.PositionsNr = dtoPos.PositionsNr;
                bestehend.PositionsTyp = dtoPos.PositionsTyp;
                bestehend.ArtikelId = dtoPos.ArtikelId;
                bestehend.Bezeichnung = dtoPos.Bezeichnung;
                bestehend.EinheitKuerzel = dtoPos.EinheitKuerzel;
                bestehend.Menge = dtoPos.Menge;
                bestehend.Einzelpreis = dtoPos.Einzelpreis;
                bestehend.RabattProzent = dtoPos.RabattProzent;
                bestehend.MwStSatzId = dtoPos.MwStSatzId;
                bestehend.MwStSatzWert = dtoPos.MwStSatzWert;
                bestehend.SteuerSchluessel = dtoPos.SteuerSchluessel;
                bestehend.LagerortId = dtoPos.LagerortId;
                bestehend.SektionId = dtoPos.SektionId;
                bestehend.KulturstufeId = dtoPos.KulturstufeId;
                bestehend.GesamtNetto = gesamtNetto;
                bestehend.UrsprungsPositionId = dtoPos.UrsprungsPositionId;
            }
            else
            {
                beleg.Positionen.Add(new BelegPosition
                {
                    PositionsNr = dtoPos.PositionsNr,
                    PositionsTyp = dtoPos.PositionsTyp,
                    ArtikelId = dtoPos.ArtikelId,
                    Bezeichnung = dtoPos.Bezeichnung,
                    EinheitKuerzel = dtoPos.EinheitKuerzel,
                    Menge = dtoPos.Menge,
                    Einzelpreis = dtoPos.Einzelpreis,
                    RabattProzent = dtoPos.RabattProzent,
                    MwStSatzId = dtoPos.MwStSatzId,
                    MwStSatzWert = dtoPos.MwStSatzWert,
                    SteuerSchluessel = dtoPos.SteuerSchluessel,
                    LagerortId = dtoPos.LagerortId,
                    SektionId = dtoPos.SektionId,
                    KulturstufeId = dtoPos.KulturstufeId,
                    GesamtNetto = gesamtNetto,
                    UrsprungsPositionId = dtoPos.UrsprungsPositionId,
                });
            }
        }
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        var beleg = await db.Belege.Include(b => b.Positionen).FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(BelegTypErweiterung.TypVon(beleg)));
        if (beleg.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht gelöscht werden.");

        await SetzeQuellbelegeZurueckAsync(db, beleg, ct);

        db.Remove(beleg);
        // SaveChangesDeletingAsync statt ...TranslatingConcurrency: BelegPosition.UrsprungsPositionId ist
        // mit DeleteBehavior.Restrict konfiguriert — ist eine Position dieses Belegs selbst schon Quelle
        // einer Folgeposition, scheitert der Cascade-Delete an der FK-Constraint und der Benutzer soll
        // eine verständliche Meldung sehen statt der rohen SQL-Fehlermeldung.
        await db.SaveChangesDeletingAsync(nameof(Beleg), id, ct);
        await transaktion.CommitAsync(ct);
    }

    /// <summary>
    /// Ein per Überleitung entstandener Entwurf hat den Quellbeleg auf <see cref="BelegStatus.Erledigt"/>
    /// gesetzt. Wird er wieder gelöscht, ist dessen Menge erneut offen — ohne diese Korrektur bliebe der
    /// Quellbeleg „erledigt" und verschwände dauerhaft aus den Offene-Aufträge-/Offene-Posten-Sichten.
    ///
    /// Der Status vor der Überleitung ist nirgends festgehalten; zurückgesetzt wird deshalb auf den
    /// Status, den ein überleitbarer Beleg dieses Typs zwingend hatte: Lieferschein und Wareneingang
    /// müssen gebucht sein, um überleitbar zu sein, alle übrigen sind an dieser Stelle Entwürfe.
    /// </summary>
    private static async Task SetzeQuellbelegeZurueckAsync(MiletDbContext db, Beleg zuLoeschen, CancellationToken ct)
    {
        var ursprungsPositionIds = zuLoeschen.Positionen
            .Where(p => p.UrsprungsPositionId != null)
            .Select(p => p.UrsprungsPositionId!.Value)
            .Distinct()
            .ToList();
        if (ursprungsPositionIds.Count == 0) return;

        var quellBelegIds = await db.BelegPositionen.AsNoTracking()
            .Where(p => ursprungsPositionIds.Contains(p.Id))
            .Select(p => p.BelegId)
            .Distinct()
            .ToListAsync(ct);
        if (quellBelegIds.Count == 0) return;

        var quellPositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => quellBelegIds.Contains(p.BelegId))
            .ToListAsync(ct);
        var quellPositionIds = quellPositionen.Select(p => p.Id).ToList();

        // Die Positionen des zu löschenden Belegs zählen nach dem Löschen nicht mehr als übernommen.
        var zuLoeschendeIds = zuLoeschen.Positionen.Select(p => p.Id).ToHashSet();
        var verbleibendeFolgepositionen = await db.BelegPositionen.AsNoTracking().Include(p => p.Beleg)
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);
        verbleibendeFolgepositionen.RemoveAll(p => zuLoeschendeIds.Contains(p.Id));

        var quellBelege = await db.Belege.Where(b => quellBelegIds.Contains(b.Id) && b.Status == BelegStatus.Erledigt).ToListAsync(ct);
        foreach (var quellBeleg in quellBelege)
        {
            var wiederOffen = quellPositionen
                .Where(p => p.BelegId == quellBeleg.Id && p.PositionsTyp == PositionsTyp.Artikel)
                .Any(p => BelegPosition.OffeneMenge(p, verbleibendeFolgepositionen) > 0);
            if (!wiederOffen) continue;

            quellBeleg.Status = BelegTypErweiterung.TypVon(quellBeleg) is BelegTyp.Lieferschein or BelegTyp.Wareneingang
                ? BelegStatus.Gebucht
                : BelegStatus.Entwurf;
        }
    }
}
