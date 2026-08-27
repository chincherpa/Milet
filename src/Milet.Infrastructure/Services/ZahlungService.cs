using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class ZahlungService(IDbContextFactory<MiletDbContext> dbContextFactory) : IZahlungService
{
    private static readonly ZahlungValidator Validator = new();

    public async Task<SkontoVorschlagDto> SkontoVorschlagAsync(int offenerPostenId, DateOnly zahlungsdatum, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var op = await db.OffenePosten.AsNoTracking().Include(o => o.Beleg)
            .FirstOrDefaultAsync(o => o.Id == offenerPostenId, ct)
            ?? throw new NotFoundException(nameof(OffenerPosten), offenerPostenId);

        var beleg = op.Beleg ?? throw new NotFoundException("Beleg zu OffenerPosten", offenerPostenId);

        var skonto = SkontoRechner.BerechneSkonto(
            beleg.BelegDatum, zahlungsdatum, beleg.ZahlungsbedingungSkontoTage, beleg.ZahlungsbedingungSkontoProzent, op.OffenerBetrag);

        return new SkontoVorschlagDto(skonto, op.OffenerBetrag - skonto);
    }

    public async Task<ZahlungDto> ErfasseZahlungAsync(ZahlungDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var zahlung = new Zahlung
        {
            KundeId = dto.KundeId,
            LieferantId = dto.LieferantId,
            Typ = dto.Typ,
            Zahlungsdatum = dto.Zahlungsdatum,
            Zahlungsart = dto.Zahlungsart,
            Referenz = dto.Referenz,
            Gesamtbetrag = dto.Zuordnungen.Sum(z => z.Betrag + z.SkontoBetrag),
        };

        foreach (var zuordnungDto in dto.Zuordnungen)
        {
            var op = await db.OffenePosten.FirstOrDefaultAsync(o => o.Id == zuordnungDto.OffenerPostenId, ct)
                ?? throw new NotFoundException(nameof(OffenerPosten), zuordnungDto.OffenerPostenId);

            db.Entry(op).Property(o => o.RowVersion).OriginalValue = zuordnungDto.RowVersion;

            var angewandterBetrag = zuordnungDto.Betrag + zuordnungDto.SkontoBetrag;
            if (angewandterBetrag > op.OffenerBetrag)
            {
                throw new InvalidOperationException(
                    $"Zahlungsbetrag ({angewandterBetrag:0.00}) übersteigt den offenen Posten '{op.Id}' ({op.OffenerBetrag:0.00}).");
            }

            op.OffenerBetrag -= angewandterBetrag;
            op.Status = op.OffenerBetrag <= 0m ? OffenerPostenStatus.Ausgeglichen : OffenerPostenStatus.TeilweiseBezahlt;

            zahlung.Zuordnungen.Add(new ZahlungZuordnung
            {
                OffenerPostenId = zuordnungDto.OffenerPostenId,
                Betrag = zuordnungDto.Betrag,
                SkontoBetrag = zuordnungDto.SkontoBetrag,
            });
        }

        db.Zahlungen.Add(zahlung);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Zahlung), dto.Id, ct);
        await transaction.CommitAsync(ct);

        return dto with { Id = zahlung.Id };
    }
}
