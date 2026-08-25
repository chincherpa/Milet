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
    private static readonly Dictionary<BelegTyp, BelegTyp> ErlaubteUebergaenge = new()
    {
        [BelegTyp.Angebot] = BelegTyp.Auftrag,
        [BelegTyp.Auftrag] = BelegTyp.Rechnung,
    };

    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
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
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erwarteterZielTyp) || erwarteterZielTyp != zielTyp)
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");

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

        if (quellVollstaendigUebernommen && quellBeleg.Status == BelegStatus.Entwurf)
            quellBeleg.Status = BelegStatus.Erledigt;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return zielBeleg.ToDto(mitPositionen: true);
    }
}
