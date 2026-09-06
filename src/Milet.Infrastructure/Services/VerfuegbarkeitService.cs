using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>Beratend, nicht sperrend (E8) — die harte Verkaufsregel sitzt beim Lieferschein-Buchen
/// (s. LieferscheinBuchenService). Reservierung ist eine Berechnung über die bestehende
/// BelegPosition.OffeneMenge-Logik, keine eigene, driftanfällige Tabelle.</summary>
public sealed class VerfuegbarkeitService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IKulturBestandService kulturBestandService) : IVerfuegbarkeitService
{
    public async Task<VerfuegbarkeitDto> LadeAsync(int artikelId, decimal? benoetigteMenge, CancellationToken ct = default)
    {
        var ergebnisse = await LadeMehrereAsync([(artikelId, benoetigteMenge)], ohneBelegId: null, ct);
        return ergebnisse[0];
    }

    public async Task<BelegVerfuegbarkeitDto> LadeFuerBelegAsync(int belegId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.AsNoTracking()
            .Include(b => b.Positionen).ThenInclude(p => p.Artikel)
            .FirstOrDefaultAsync(b => b.Id == belegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), belegId);

        var kulturPositionen = beleg.Positionen
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel && p.Artikel?.IstKulturpflanze == true)
            .ToList();

        // Der Beleg wird aus der Reservierung ausgeklammert: seine eigenen offenen Mengen sind genau die,
        // die hier bewertet werden. Ohne das zählte ein Auftrag über 10 Stück bei 10 Stück Bestand sich
        // selbst als reserviert (frei = 0) und stand auf Gelb, obwohl er exakt bedienbar ist.
        var ergebnisse = await LadeMehrereAsync(
            kulturPositionen.Select(p => (p.ArtikelId!.Value, (decimal?)p.Menge)).ToList(), belegId, ct);

        // Enum-Reihenfolge Gruen < Gelb < Rot — Max liefert damit direkt die schlechteste Einzelampel.
        var gesamtAmpel = ergebnisse.Count == 0 ? VerfuegbarkeitAmpel.Gruen : ergebnisse.Max(v => v.Ampel);
        return new BelegVerfuegbarkeitDto(belegId, gesamtAmpel, ergebnisse);
    }

    /// <summary>
    /// Bewertet mehrere Positionen mit einer festen Zahl von Abfragen statt einer Abfragegruppe je Position.
    /// Vorher öffnete jede Position über <c>LadeAsync</c> einen eigenen DbContext, <c>LadeVorkommenAsync</c>
    /// einen zweiten, und die komplette Kulturstufen-Tabelle wurde je Position neu geladen — bei 20
    /// Pflanzenpositionen 40 Verbindungen und über 60 Round-Trips für eine reine Anzeige.
    ///
    /// Ein Artikel darf mehrfach vorkommen (zwei Positionen desselben Artikels): die Bestände werden je
    /// Artikel einmal geladen und je Eintrag ausgewertet, das Ergebnis bleibt positionsweise.
    /// </summary>
    private async Task<List<VerfuegbarkeitDto>> LadeMehrereAsync(
        IReadOnlyList<(int ArtikelId, decimal? BenoetigteMenge)> positionen, int? ohneBelegId, CancellationToken ct)
    {
        if (positionen.Count == 0) return [];

        var artikelIds = positionen.Select(p => p.ArtikelId).Distinct().ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var bestaende = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => artikelIds.Contains(b.ArtikelId) && b.KulturstufeId != null)
            .Select(b => new { b.ArtikelId, b.Menge, b.Kulturstufe!.IstVerkaufsfaehig })
            .ToListAsync(ct);
        var verkaufsfaehigJeArtikel = bestaende
            .Where(b => b.IstVerkaufsfaehig)
            .GroupBy(b => b.ArtikelId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Menge));

        var reserviertJeArtikel = await BerechneReserviertAsync(db, artikelIds, ohneBelegId, ct);
        var fundstellenJeArtikel = await kulturBestandService.LadeVorkommenAsync(artikelIds, ct);

        var stufeIstVerkaufsfaehig = await db.Kulturstufen.AsNoTracking()
            .Select(k => new { k.Id, k.IstVerkaufsfaehig })
            .ToDictionaryAsync(k => k.Id, k => k.IstVerkaufsfaehig, ct);

        var ergebnisse = new List<VerfuegbarkeitDto>(positionen.Count);
        foreach (var (artikelId, benoetigteMenge) in positionen)
        {
            var verkaufsfaehigGesamt = verkaufsfaehigJeArtikel.GetValueOrDefault(artikelId);
            var reserviert = reserviertJeArtikel.GetValueOrDefault(artikelId);
            var frei = verkaufsfaehigGesamt - reserviert;

            var fundstellen = fundstellenJeArtikel[artikelId].ToList();
            var nichtVerkaufsfaehig = fundstellen
                .Where(f => !stufeIstVerkaufsfaehig.GetValueOrDefault(f.KulturstufeId))
                .GroupBy(f => new { f.KulturstufeId, f.StufeBezeichnung, f.FarbeHex })
                .Select(g => new MengeJeStufeDto(g.Key.KulturstufeId, g.Key.StufeBezeichnung, g.Key.FarbeHex, g.Sum(x => x.Menge)))
                .ToList();

            // Ohne konkrete Bestellmenge (allgemeiner Verfügbarkeits-Check) wird "mindestens 1 Stück lieferbar"
            // als Maßstab verwendet — die Frage "ist die Pflanze überhaupt verkaufsfähig vorrätig?".
            var benoetigt = benoetigteMenge ?? 1m;
            var gesamtNichtVerkaufsfaehig = nichtVerkaufsfaehig.Sum(n => n.Menge);
            var ampel = frei >= benoetigt
                ? VerfuegbarkeitAmpel.Gruen
                : verkaufsfaehigGesamt > 0 || gesamtNichtVerkaufsfaehig > 0
                    ? VerfuegbarkeitAmpel.Gelb
                    : VerfuegbarkeitAmpel.Rot;

            ergebnisse.Add(new VerfuegbarkeitDto(
                artikelId, verkaufsfaehigGesamt, reserviert, frei, ampel, fundstellen, nichtVerkaufsfaehig));
        }

        return ergebnisse;
    }

    /// <summary>Reserviert = Σ offener Mengen aller Auftragspositionen dieses Artikels (Entwurf/Gebucht) —
    /// dieselbe UrsprungsPositionId-Logik wie BelegUeberleitungService, hier nur lesend.
    ///
    /// <paramref name="ohneBelegId"/> klammert einen Auftrag aus: wird die Ampel FÜR diesen Auftrag
    /// gerechnet, sind seine eigenen offenen Mengen keine Reservierung gegen ihn selbst.</summary>
    private static async Task<Dictionary<int, decimal>> BerechneReserviertAsync(
        MiletDbContext db, IReadOnlyList<int> artikelIds, int? ohneBelegId, CancellationToken ct)
    {
        var auftragsPositionen = await db.Auftraege.AsNoTracking()
            .Where(a => (a.Status == BelegStatus.Entwurf || a.Status == BelegStatus.Gebucht)
                && (ohneBelegId == null || a.Id != ohneBelegId))
            .SelectMany(a => a.Positionen)
            .Where(p => p.ArtikelId != null && artikelIds.Contains(p.ArtikelId.Value) && p.PositionsTyp == PositionsTyp.Artikel)
            .ToListAsync(ct);

        if (auftragsPositionen.Count == 0) return [];

        var quellIds = auftragsPositionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null
                && quellIds.Contains(p.UrsprungsPositionId.Value)
                && p.Beleg!.Status != BelegStatus.Storniert)
            .ToListAsync(ct);

        return auftragsPositionen
            .GroupBy(p => p.ArtikelId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => Math.Max(0m, BelegPosition.OffeneMenge(p, folgepositionen))));
    }
}
