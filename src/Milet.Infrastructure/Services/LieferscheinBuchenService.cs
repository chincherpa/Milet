using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class LieferscheinBuchenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung,
    ICurrentUserService currentUser) : ILieferscheinBuchenService
{
    public async Task<BelegDto> BuchenAsync(
        int lieferscheinId, IReadOnlyDictionary<int, IReadOnlyList<int>> seriennummernJePosition, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var lieferschein = await db.Lieferscheine.Include(l => l.Positionen)
            .FirstOrDefaultAsync(l => l.Id == lieferscheinId, ct)
            ?? throw new NotFoundException(nameof(Lieferschein), lieferscheinId);

        if (lieferschein.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Lieferschein '{lieferschein.BelegNummer}' ist bereits gebucht.");
        if (lieferschein.Positionen.Count == 0)
            throw new InvalidOperationException("Lieferschein ohne Positionen kann nicht gebucht werden.");

        foreach (var position in lieferschein.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId)
                throw new InvalidOperationException($"Position {position.PositionsNr}: Artikel oder Lagerort fehlt.");

            var artikel = await db.Artikel.AsNoTracking().FirstAsync(a => a.Id == artikelId, ct);

            // Bestand VOR der Seriennummern-Prüfung abbuchen: eine einzige atomare Buchung entscheidet über Verfügbarkeit
            // (kein separater Read-Modify-Write-Check davor, siehe BestandService.BucheBewegungAsync).
            await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, -position.Menge, LagerbewegungTyp.Lieferung, position.Id, ct,
                benutzerId: currentUser.BenutzerId);

            if (artikel.HatSeriennummern)
            {
                if (!seriennummernJePosition.TryGetValue(position.Id, out var gewaehlt) || gewaehlt.Count != position.Menge)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: es müssen genau {position.Menge} Seriennummer(n) ausgewählt werden.");

                var seriennummern = await db.Seriennummern
                    .Where(s => gewaehlt.Contains(s.Id) && s.ArtikelId == artikelId && s.Status == SeriennummerStatus.AufLager)
                    .ToListAsync(ct);

                if (seriennummern.Count != gewaehlt.Count)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: eine oder mehrere gewählte Seriennummern sind nicht mehr verfügbar.");

                foreach (var seriennummer in seriennummern)
                {
                    seriennummer.Status = SeriennummerStatus.Ausgeliefert;
                    seriennummer.LagerortId = null;
                    db.BelegPositionSeriennummern.Add(new BelegPositionSeriennummer { BelegPositionId = position.Id, SeriennummerId = seriennummer.Id });
                }
            }
        }

        lieferschein.Status = BelegStatus.Gebucht;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return lieferschein.ToDto(mitPositionen: true);
    }
}
