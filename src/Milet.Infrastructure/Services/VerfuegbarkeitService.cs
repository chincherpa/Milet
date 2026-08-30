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
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var bestaende = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => b.ArtikelId == artikelId && b.KulturstufeId != null)
            .Select(b => new { b.Menge, b.Kulturstufe!.IstVerkaufsfaehig })
            .ToListAsync(ct);
        var verkaufsfaehigGesamt = bestaende.Where(b => b.IstVerkaufsfaehig).Sum(b => b.Menge);

        var reserviert = await BerechneReserviertAsync(db, artikelId, ct);
        var frei = verkaufsfaehigGesamt - reserviert;

        var fundstellen = await kulturBestandService.LadeVorkommenAsync(artikelId, ct);
        var stufeIstVerkaufsfaehig = await db.Kulturstufen.AsNoTracking()
            .Select(k => new { k.Id, k.IstVerkaufsfaehig })
            .ToDictionaryAsync(k => k.Id, k => k.IstVerkaufsfaehig, ct);

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

        return new VerfuegbarkeitDto(artikelId, verkaufsfaehigGesamt, reserviert, frei, ampel, fundstellen, nichtVerkaufsfaehig);
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

        var ergebnisse = new List<VerfuegbarkeitDto>();
        foreach (var position in kulturPositionen)
        {
            ergebnisse.Add(await LadeAsync(position.ArtikelId!.Value, position.Menge, ct));
        }

        // Enum-Reihenfolge Gruen < Gelb < Rot — Max liefert damit direkt die schlechteste Einzelampel.
        var gesamtAmpel = ergebnisse.Count == 0 ? VerfuegbarkeitAmpel.Gruen : ergebnisse.Max(v => v.Ampel);
        return new BelegVerfuegbarkeitDto(belegId, gesamtAmpel, ergebnisse);
    }

    /// <summary>Reserviert = Σ offener Mengen aller Auftragspositionen dieses Artikels (Entwurf/Gebucht) —
    /// dieselbe UrsprungsPositionId-Logik wie BelegUeberleitungService, hier nur lesend.</summary>
    private static async Task<decimal> BerechneReserviertAsync(MiletDbContext db, int artikelId, CancellationToken ct)
    {
        var auftragsPositionen = await db.Auftraege.AsNoTracking()
            .Where(a => a.Status == BelegStatus.Entwurf || a.Status == BelegStatus.Gebucht)
            .SelectMany(a => a.Positionen)
            .Where(p => p.ArtikelId == artikelId && p.PositionsTyp == PositionsTyp.Artikel)
            .ToListAsync(ct);

        if (auftragsPositionen.Count == 0) return 0m;

        var quellIds = auftragsPositionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        return auftragsPositionen.Sum(p => Math.Max(0m, BelegPosition.OffeneMenge(p, folgepositionen)));
    }
}
