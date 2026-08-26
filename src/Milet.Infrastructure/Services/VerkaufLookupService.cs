using Microsoft.EntityFrameworkCore;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class VerkaufLookupService(IDbContextFactory<MiletDbContext> dbContextFactory) : IVerkaufLookupService
{
    public async Task<VerkaufLookups> LadeLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var kunden = await db.Kunden.AsNoTracking()
            .OrderBy(k => k.Kundennummer)
            .Select(k => new KundeVerkaufLookupDto(
                k.Id, $"{k.Kundennummer} — {k.Adresse.Name1}", k.ZahlungsbedingungId, k.PreislisteId, k.RabattProzent))
            .ToListAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => !a.Gesperrt)
            .OrderBy(a => a.Artikelnummer)
            .Select(a => new ArtikelVerkaufLookupDto(
                a.Id,
                $"{a.Artikelnummer} — {a.Bezeichnung}",
                a.Bezeichnung,
                a.Listenpreis,
                a.MwStSatzId,
                a.MwStSatz!.Satz,
                a.MwStSatz.SteuerSchluessel,
                a.Einheit!.Kuerzel,
                a.HatSeriennummern))
            .ToListAsync(ct);

        var zahlungsbedingungen = await db.Zahlungsbedingungen.AsNoTracking()
            .OrderBy(z => z.Bezeichnung)
            .Select(z => new LookupDto(z.Id, z.Bezeichnung))
            .ToListAsync(ct);

        return new VerkaufLookups(kunden, artikel, zahlungsbedingungen);
    }

    public async Task<PreisErgebnisDto> ErmittlePreisAsync(int artikelId, decimal menge, int kundeId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artikelId, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Artikel), artikelId);
        var kunde = await db.Kunden.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kundeId, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Kunde), kundeId);

        var staffelpreise = kunde.PreislisteId is int preislisteId
            ? await db.ArtikelPreise.AsNoTracking()
                .Where(p => p.ArtikelId == artikelId && p.PreislisteId == preislisteId)
                .ToListAsync(ct)
            : [];

        var ergebnis = PreisfindungService.ErmittlePreis(artikel, menge, kunde.PreislisteId, staffelpreise, kunde.RabattProzent);
        return new PreisErgebnisDto(ergebnis.Einzelpreis, ergebnis.RabattProzent);
    }
}
