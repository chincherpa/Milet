using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
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

        var bestaende = await db.ArtikelBestaende.AsNoTracking().Where(b => b.LagerortId == lagerortId).ToListAsync(ct);
        // Seriennummern-Artikel sind hier ausgeschlossen: ihr Bestand wird über die Seriennummernliste geführt
        // (siehe SeriennummernService), nicht über bulk Ist-Mengen — sonst desynchronisiert eine Inventur-Korrektur
        // ArtikelBestand.Menge von COUNT(Seriennummern WHERE Status = AufLager). Spiegelt die gleiche Regel wie
        // BestandUebersichtViewModel.ZeigtKorrekturPanel (dort ebenfalls nur für !HatSeriennummern sichtbar).
        var lagerfaehigeArtikel = await db.Artikel
            .Where(a => a.IstLagerartikel && !a.Gesperrt && !a.HatSeriennummern).ToListAsync(ct);

        if (lagerfaehigeArtikel.Count == 0)
            throw new InvalidOperationException("Keine lagerfähigen Artikel für eine Inventur vorhanden.");

        var inventur = new Inventur { LagerortId = lagerortId, Lagerort = lagerort, Datum = DateOnly.FromDateTime(DateTime.Today), Status = InventurStatus.Offen };
        foreach (var artikel in lagerfaehigeArtikel)
        {
            var soll = bestaende.FirstOrDefault(b => b.ArtikelId == artikel.Id)?.Menge ?? 0m;
            inventur.Positionen.Add(new InventurPosition { ArtikelId = artikel.Id, Artikel = artikel, SollMenge = soll });
        }

        db.Add(inventur);
        await db.SaveChangesAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }

    public async Task ErfasseIstMengeAsync(int inventurPositionId, decimal istMenge, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Lager);
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

        foreach (var position in inventur.Positionen.Where(p => p.IstMenge.HasValue && p.IstMenge != p.SollMenge))
        {
            var delta = position.IstMenge!.Value - position.SollMenge;
            await BestandService.BucheBewegungAsync(db, position.ArtikelId, inventur.LagerortId, delta, LagerbewegungTyp.InventurKorrektur, null, ct,
                benutzerId: currentUser.BenutzerId, grund: $"Inventur {inventur.Id} Abschluss ({inventur.Datum:yyyy-MM-dd})");
        }

        inventur.Status = InventurStatus.Abgeschlossen;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await db.Entry(inventur).Collection(i => i.Positionen).Query().Include(p => p.Artikel).LoadAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }
}
