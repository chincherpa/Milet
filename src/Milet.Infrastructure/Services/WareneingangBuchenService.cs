using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;
using Verkauf = Milet.Application.Verkauf;

namespace Milet.Infrastructure.Services;

public sealed class WareneingangBuchenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung,
    ICurrentUserService currentUser) : IWareneingangBuchenService
{
    public async Task<Verkauf.BelegDto> BuchenAsync(
        int wareneingangId, IReadOnlyDictionary<int, IReadOnlyList<string>> neueSeriennummernJePosition, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Einkauf);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var wareneingang = await db.Wareneingaenge.Include(w => w.Positionen)
            .FirstOrDefaultAsync(w => w.Id == wareneingangId, ct)
            ?? throw new NotFoundException(nameof(Wareneingang), wareneingangId);

        if (wareneingang.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Wareneingang '{wareneingang.BelegNummer}' ist bereits gebucht.");
        if (wareneingang.Positionen.Count == 0)
            throw new InvalidOperationException("Wareneingang ohne Positionen kann nicht gebucht werden.");

        foreach (var position in wareneingang.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId)
                throw new InvalidOperationException($"Position {position.PositionsNr}: Artikel oder Lagerort fehlt.");

            var artikel = await db.Artikel.AsNoTracking().FirstAsync(a => a.Id == artikelId, ct);

            // Positives Delta — BestandService.BucheBewegungAsync ist unverändert wiederverwendbar (siehe
            // Phase-3-Kommentar dort): die atomare UPDATE-Bedingung "Menge + delta >= 0" ist bei einem Zugang
            // immer erfüllt, und legt bei erstem Bestand am Lagerort die ArtikelBestand-Zeile automatisch an.
            await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, position.Menge, LagerbewegungTyp.Wareneingang, position.Id, ct,
                benutzerId: currentUser.BenutzerId);

            if (artikel.HatSeriennummern)
            {
                if (!neueSeriennummernJePosition.TryGetValue(position.Id, out var nummern) || nummern.Count != position.Menge)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: es müssen genau {position.Menge} Seriennummer(n) erfasst werden.");

                // In-Memory-Check: doppelte innerhalb der gleichen Eingabe (vor SaveChangesAsync würden sie nicht gemerkt).
                // OrdinalIgnoreCase, weil der DB-Unique-Index (SeriennummerConfiguration) unter der Standard-Collation
                // von SQL Server case-insensitiv vergleicht — sonst würde z. B. "SN-A"/"sn-a" hier durchrutschen und
                // erst als roher DbUpdateException an der DB scheitern.
                if (nummern.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nummern.Count)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: doppelte Seriennummer(n) in der Eingabe.");

                var doppelte = await db.Seriennummern.AsNoTracking()
                    .Where(s => s.ArtikelId == artikelId && nummern.Contains(s.Nummer))
                    .Select(s => s.Nummer)
                    .ToListAsync(ct);
                if (doppelte.Count > 0)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: Seriennummer(n) bereits vorhanden: {string.Join(", ", doppelte)}.");

                // Neue Seriennummern (anders als LieferscheinBuchenService, das bestehende auswählt): Id ist vor
                // SaveChangesAsync noch 0, daher Verknüpfung über Navigationseigenschaften statt SeriennummerId.
                foreach (var nummer in nummern)
                {
                    var seriennummer = new Seriennummer
                    {
                        ArtikelId = artikelId,
                        Nummer = nummer,
                        Status = SeriennummerStatus.AufLager,
                        LagerortId = lagerortId,
                    };
                    db.Seriennummern.Add(seriennummer);
                    db.BelegPositionSeriennummern.Add(new BelegPositionSeriennummer { BelegPosition = position, Seriennummer = seriennummer });
                }
            }
        }

        wareneingang.Status = BelegStatus.Gebucht;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return wareneingang.ToDto(mitPositionen: true);
    }
}
