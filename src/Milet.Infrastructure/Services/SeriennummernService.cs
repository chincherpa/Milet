using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class SeriennummernService(IDbContextFactory<MiletDbContext> dbContextFactory) : ISeriennummernService
{
    public async Task<IReadOnlyList<SeriennummerDto>> SucheAsync(int? artikelId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Seriennummern.AsNoTracking().AsQueryable();
        if (artikelId is { } id) query = query.Where(s => s.ArtikelId == id);
        var liste = await query.OrderBy(s => s.Nummer).ToListAsync(ct);
        return liste.Select(s => s.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<SeriennummerDto>> AufLagerAsync(int artikelId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var liste = await db.Seriennummern.AsNoTracking()
            .Where(s => s.ArtikelId == artikelId && s.Status == SeriennummerStatus.AufLager)
            .OrderBy(s => s.Nummer)
            .ToListAsync(ct);
        return liste.Select(s => s.ToDto()).ToList();
    }

    public async Task ErfasseAsync(int artikelId, int lagerortId, string nummer, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            throw new InvalidOperationException("Seriennummer darf nicht leer sein.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (await db.Seriennummern.AnyAsync(s => s.ArtikelId == artikelId && s.Nummer == nummer, ct))
            throw new InvalidOperationException($"Seriennummer '{nummer}' ist für diesen Artikel bereits erfasst.");

        db.Seriennummern.Add(new Seriennummer { ArtikelId = artikelId, Nummer = nummer, Status = SeriennummerStatus.AufLager, LagerortId = lagerortId });
        await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, 1m, LagerbewegungTyp.Korrektur, belegPositionId: null, ct);
        await transaction.CommitAsync(ct);
    }
}
