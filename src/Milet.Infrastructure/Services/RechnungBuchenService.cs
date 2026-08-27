using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class RechnungBuchenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService,
    IBerechtigungsService berechtigung) : IRechnungBuchenService
{
    public async Task<BelegDto> BuchenAsync(int rechnungId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Verkauf);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var rechnung = await db.Rechnungen.Include(r => r.Positionen)
            .FirstOrDefaultAsync(r => r.Id == rechnungId, ct)
            ?? throw new NotFoundException(nameof(Rechnung), rechnungId);

        if (rechnung.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Rechnung '{rechnung.BelegNummer}' ist bereits gebucht.");
        if (rechnung.Positionen.Count == 0)
            throw new InvalidOperationException("Rechnung ohne Positionen kann nicht gebucht werden.");

        rechnung.BelegNummer = await numberRangeService.NaechsteNummerAsync("RE", ct);
        rechnung.Faelligkeit = rechnung.BelegDatum.AddDays(rechnung.ZahlungsbedingungZielTage);
        rechnung.Status = BelegStatus.Gebucht;

        db.OffenePosten.Add(new OffenerPosten
        {
            BelegId = rechnung.Id,
            KundeId = rechnung.KundeId,
            Typ = OffenerPostenTyp.Debitor,
            Betrag = rechnung.SummeBrutto,
            OffenerBetrag = rechnung.SummeBrutto,
            Faelligkeit = rechnung.Faelligkeit.Value,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return rechnung.ToDto(mitPositionen: true);
    }
}
