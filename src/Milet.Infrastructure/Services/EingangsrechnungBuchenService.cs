using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;
using Verkauf = Milet.Application.Verkauf;

namespace Milet.Infrastructure.Services;

public sealed class EingangsrechnungBuchenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IEingangsrechnungBuchenService
{
    public async Task<EingangsrechnungBuchenErgebnisDto> BuchenAsync(int eingangsrechnungId, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Einkauf);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var eingangsrechnung = await db.Eingangsrechnungen.Include(e => e.Positionen)
            .FirstOrDefaultAsync(e => e.Id == eingangsrechnungId, ct)
            ?? throw new NotFoundException(nameof(Eingangsrechnung), eingangsrechnungId);

        if (eingangsrechnung.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Eingangsrechnung '{eingangsrechnung.BelegNummer}' ist bereits gebucht.");
        if (eingangsrechnung.Positionen.Count == 0)
            throw new InvalidOperationException("Eingangsrechnung ohne Positionen kann nicht gebucht werden.");
        if (eingangsrechnung.LieferantId is not { } lieferantId)
            throw new InvalidOperationException("Eingangsrechnung ohne Lieferant kann nicht gebucht werden.");

        // Abweichungs-Soft-Warnung (siehe Architektur-Entscheidung 7 im Phase-4-Plan): Rechnungssumme vs. Summe
        // des ursprünglichen Wareneingangs. Die Positionen einer Eingangsrechnung entstehen per Überleitung aus
        // genau einem Wareneingang (UeberleitenAsync, v1 keine Sammel-Eingangsrechnung) — daher genügt ein Hop
        // über UrsprungsPositionId der ersten Position, um den Quell-Beleg zu finden.
        var erwarteterBetrag = eingangsrechnung.SummeBrutto;
        var ersteQuellPositionId = eingangsrechnung.Positionen.Select(p => p.UrsprungsPositionId).FirstOrDefault(id => id != null);
        if (ersteQuellPositionId is int quellPositionId)
        {
            var quellBelegId = await db.BelegPositionen.AsNoTracking()
                .Where(p => p.Id == quellPositionId)
                .Select(p => (int?)p.BelegId)
                .FirstOrDefaultAsync(ct);
            if (quellBelegId is int belegId)
            {
                erwarteterBetrag = await db.Belege.AsNoTracking()
                    .Where(b => b.Id == belegId)
                    .Select(b => b.SummeBrutto)
                    .FirstOrDefaultAsync(ct);
            }
        }

        var abweichung = eingangsrechnung.SummeBrutto - erwarteterBetrag;
        var weichtAb = Math.Abs(abweichung) > 0.01m;

        eingangsrechnung.Status = BelegStatus.Gebucht;
        eingangsrechnung.Faelligkeit = eingangsrechnung.BelegDatum.AddDays(eingangsrechnung.ZahlungsbedingungZielTage);

        db.OffenePosten.Add(new OffenerPosten
        {
            BelegId = eingangsrechnung.Id,
            LieferantId = lieferantId,
            Typ = OffenerPostenTyp.Kreditor,
            Betrag = eingangsrechnung.SummeBrutto,
            OffenerBetrag = eingangsrechnung.SummeBrutto,
            Faelligkeit = eingangsrechnung.Faelligkeit.Value,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new EingangsrechnungBuchenErgebnisDto(eingangsrechnung.ToDto(mitPositionen: true), weichtAb, erwarteterBetrag, abweichung);
    }
}
