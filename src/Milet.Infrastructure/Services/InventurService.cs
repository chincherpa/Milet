using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class InventurService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung,
    ICurrentUserService currentUser) : IInventurService
{
    public async Task<IReadOnlyList<InventurDto>> SucheAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var liste = await db.Inventuren.AsNoTracking().Include(i => i.Lagerort)
            .OrderByDescending(i => i.Datum).ThenByDescending(i => i.Id).ToListAsync(ct);
        return liste.Select(i => i.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<InventurDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var inventur = await db.Inventuren.AsNoTracking().Include(i => i.Lagerort)
            .Include(i => i.Positionen).ThenInclude(p => p.Artikel)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(Inventur), id);
        return inventur.ToDto(mitPositionen: true);
    }

    public async Task<InventurDto> NeueInventurAsync(int lagerortId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == lagerortId, ct)
            ?? throw new NotFoundException(nameof(Lagerort), lagerortId);

        // Zwei gleichzeitig offene Inventuren desselben Lagerorts würden nacheinander gegen denselben
        // eingefrorenen Sollstand korrigieren und den Bestand doppelt verschieben.
        if (await db.Inventuren.AnyAsync(i => i.LagerortId == lagerortId && i.Status == InventurStatus.Offen, ct))
            throw new InvalidOperationException(
                $"Für Lagerort '{lagerort.Code}' läuft bereits eine Inventur — sie muss erst abgeschlossen werden.");

        var inventur = new Inventur { LagerortId = lagerortId, Lagerort = lagerort, Datum = DateOnly.FromDateTime(DateTime.Today), Status = InventurStatus.Offen };

        if (lagerort.IstFeld)
        {
            // E10: auf einem Feld gibt es je Artikel potenziell mehrere Bestandszeilen (Sektion × Kulturstufe).
            // Eine Position je existierender Bestandszeile — NICHT je Kreuzprodukt aller Artikel × Sektionen ×
            // Stufen, das wären bei vielen Sorten/Sektionen tausende leere Zeilen (s. Plan, Risiko 5).
            //
            // Bewusst zwei getrennte Abfragen statt eines LINQ-Join: die Tracking-Behandlung
            // (AsNoTracking/getrackt) gilt für eine zusammengesetzte Query als Ganzes, nicht pro Quelle. Ein
            // Join mit einer AsNoTracking-Seite hätte auch die Artikel-Seite untrackbar gemacht — pro Bestandszeile
            // eine EIGENE, nicht getrackte Artikel-Instanz mit bereits vergebener Id, die db.Add(inventur) beim
            // Graph-Walk als NEUE Zeile einzufügen versucht (IDENTITY_INSERT-Fehler; bei Mehrfachvorkommen
            // desselben Artikels sogar ein "cannot be tracked"-Konflikt zwischen den Instanzen). Die getrackte
            // Artikel-Abfrage hier liefert dagegen je Id genau eine (Unchanged) Instanz — dieselbe Garantie wie im
            // Regressionspfad unten, der ebenfalls ohne AsNoTracking lädt.
            var bestaende = await db.ArtikelBestaende.AsNoTracking().Where(b => b.LagerortId == lagerortId).ToListAsync(ct);
            var bestandsArtikelIds = bestaende.Select(b => b.ArtikelId).Distinct().ToList();
            var artikelJeId = await db.Artikel
                .Where(a => bestandsArtikelIds.Contains(a.Id) && a.IstLagerartikel && !a.Gesperrt && !a.HatSeriennummern)
                .ToDictionaryAsync(a => a.Id, ct);

            if (artikelJeId.Count == 0)
                throw new InvalidOperationException($"Für Feld '{lagerort.Bezeichnung}' sind keine Bestandszeilen vorhanden.");

            foreach (var bestand in bestaende)
            {
                if (!artikelJeId.TryGetValue(bestand.ArtikelId, out var artikel)) continue;

                inventur.Positionen.Add(new InventurPosition
                {
                    ArtikelId = artikel.Id,
                    Artikel = artikel,
                    SektionId = bestand.SektionId,
                    KulturstufeId = bestand.KulturstufeId,
                    SollMenge = bestand.Menge,
                });
            }
        }
        else
        {
            // Regressionspfad — unverändert wie vor Phase 8: je lagerfähigem Artikel eine Position, Dimensionen NULL.
            var bestaende = await db.ArtikelBestaende.AsNoTracking().Where(b => b.LagerortId == lagerortId).ToListAsync(ct);
            // Seriennummern-Artikel sind hier ausgeschlossen: ihr Bestand wird über die Seriennummernliste geführt
            // (siehe SeriennummernService), nicht über bulk Ist-Mengen — sonst desynchronisiert eine Inventur-Korrektur
            // ArtikelBestand.Menge von COUNT(Seriennummern WHERE Status = AufLager). Spiegelt die gleiche Regel wie
            // BestandUebersichtViewModel.ZeigtKorrekturPanel (dort ebenfalls nur für !HatSeriennummern sichtbar).
            var lagerfaehigeArtikel = await db.Artikel
                .Where(a => a.IstLagerartikel && !a.Gesperrt && !a.HatSeriennummern).ToListAsync(ct);

            if (lagerfaehigeArtikel.Count == 0)
                throw new InvalidOperationException("Keine lagerfähigen Artikel für eine Inventur vorhanden.");

            foreach (var artikel in lagerfaehigeArtikel)
            {
                var soll = bestaende.FirstOrDefault(b => b.ArtikelId == artikel.Id)?.Menge ?? 0m;
                inventur.Positionen.Add(new InventurPosition { ArtikelId = artikel.Id, Artikel = artikel, SollMenge = soll });
            }
        }

        db.Add(inventur);
        await db.SaveChangesAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }

    public async Task ErfasseIstMengeAsync(int inventurPositionId, decimal istMenge, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        if (istMenge < 0)
            throw new InvalidOperationException("Die gezählte Menge kann nicht negativ sein.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var position = await db.InventurPositionen.Include(p => p.Inventur)
            .FirstOrDefaultAsync(p => p.Id == inventurPositionId, ct)
            ?? throw new NotFoundException(nameof(InventurPosition), inventurPositionId);
        if (position.Inventur!.Status != InventurStatus.Offen)
            throw new InvalidOperationException("Inventur ist bereits abgeschlossen.");

        position.IstMenge = istMenge;
        await db.SaveChangesAsync(ct);
    }

    public async Task<InventurDto> AbschliessenAsync(int inventurId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var inventur = await db.Inventuren.Include(i => i.Positionen).Include(i => i.Lagerort)
            .FirstOrDefaultAsync(i => i.Id == inventurId, ct)
            ?? throw new NotFoundException(nameof(Inventur), inventurId);
        if (inventur.Status != InventurStatus.Offen)
            throw new InvalidOperationException("Inventur ist bereits abgeschlossen.");

        // Der Abschluss bucht delta = Ist − Soll ADDITIV auf den aktuellen Bestand. Das ist nur richtig,
        // solange der aktuelle Bestand noch dem eingefrorenen Sollstand entspricht: wurde während der
        // Zählung etwa ein Lieferschein über 5 Stück gebucht, ist der Bestand bereits um 5 reduziert — die
        // physisch gezählte Ist-Menge spiegelt den Abgang aber ebenfalls schon wider, der Abgang würde also
        // ein zweites Mal abgezogen. Da es keine Sperre des Lagerorts für die Dauer der Zählung gibt, wird
        // die Abweichung hier erkannt und eine Neuaufnahme verlangt, statt still falsch zu buchen.
        // E10: Schlüssel ist (ArtikelId, SektionId, KulturstufeId), nicht nur ArtikelId — sonst würde auf
        // einem Feld mit mehreren Bestandszeilen je Artikel eine willkürliche Zeile geprüft/gebucht.
        var aktuelleBestaende = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => b.LagerortId == inventur.LagerortId)
            .ToDictionaryAsync(b => (b.ArtikelId, b.SektionId, b.KulturstufeId), b => b.Menge, ct);

        var zuBuchen = inventur.Positionen.Where(p => p.IstMenge.HasValue && p.IstMenge != p.SollMenge).ToList();
        var veraendertePositionen = zuBuchen
            .Where(p => (aktuelleBestaende.TryGetValue((p.ArtikelId, p.SektionId, p.KulturstufeId), out var menge) ? menge : 0m) != p.SollMenge)
            .ToList();
        if (veraendertePositionen.Count > 0)
        {
            var beschreibungen = veraendertePositionen.Take(5).Select(p => p.SektionId is not null || p.KulturstufeId is not null
                ? $"Artikel-Id {p.ArtikelId} (Sektion-Id {p.SektionId}, Kulturstufe-Id {p.KulturstufeId})"
                : $"Artikel-Id {p.ArtikelId}");
            throw new InvalidOperationException(
                $"Der Bestand hat sich seit Beginn der Inventur bei {veraendertePositionen.Count} Position(en) verändert "
                + $"({string.Join("; ", beschreibungen)}). Die Inventur muss neu aufgenommen werden.");
        }

        foreach (var position in zuBuchen)
        {
            var delta = position.IstMenge!.Value - position.SollMenge;
            await BestandService.BucheBewegungAsync(
                db, position.ArtikelId, inventur.LagerortId, delta, LagerbewegungTyp.InventurKorrektur, null, ct,
                position.SektionId, position.KulturstufeId,
                $"Inventurabschluss {inventur.Lagerort!.Code} vom {inventur.Datum:dd.MM.yyyy}", currentUser.BenutzerId);
        }

        inventur.Status = InventurStatus.Abgeschlossen;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await db.Entry(inventur).Collection(i => i.Positionen).Query().Include(p => p.Artikel).LoadAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }
}
