using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class ZahlungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IZahlungService
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
        berechtigung.PruefeRecht(RechtCodes.Finanzen);
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
            // Nur die tatsächlich geflossenen Beträge — das gewährte/erhaltene Skonto gehört NICHT dazu.
            // Der Wert wird im DATEV-Export gegen das Bankkonto gebucht und muss deshalb dem Kontoauszug
            // entsprechen (100 € Rechnung, 2 € Skonto, 98 € Eingang → Gesamtbetrag 98). Das Skonto wird dort
            // als eigene Zeile aufs Skontokonto gebucht.
            Gesamtbetrag = dto.Zuordnungen.Sum(z => z.Betrag),
        };

        foreach (var zuordnungDto in dto.Zuordnungen)
        {
            var op = await db.OffenePosten.FirstOrDefaultAsync(o => o.Id == zuordnungDto.OffenerPostenId, ct)
                ?? throw new NotFoundException(nameof(OffenerPosten), zuordnungDto.OffenerPostenId);

            // Ohne diese Prüfung könnte eine Zahlung von Kunde A den offenen Posten von Kunde B ausgleichen
            // (oder gar eine Kreditorverbindlichkeit): im DATEV-Export liefe die Zahlung gegen A's
            // Personenkonto, während B's OP als ausgeglichen gilt — beide Konten wären dauerhaft falsch,
            // ohne dass es irgendwo auffiele.
            if (op.Typ != zahlung.Typ)
                throw new InvalidOperationException(
                    $"Offener Posten '{op.Id}' ist vom Typ {op.Typ} und passt nicht zu einer {zahlung.Typ}-Zahlung.");
            if (op.KundeId != zahlung.KundeId || op.LieferantId != zahlung.LieferantId)
                throw new InvalidOperationException(
                    $"Offener Posten '{op.Id}' gehört zu einem anderen Geschäftspartner als die Zahlung.");

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
