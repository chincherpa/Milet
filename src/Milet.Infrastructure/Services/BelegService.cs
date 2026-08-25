using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BelegService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IBelegService
{
    private static readonly BelegValidator Validator = new();

    private static IQueryable<Beleg> SetFuerTyp(MiletDbContext db, BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => db.Angebote,
        BelegTyp.Auftrag => db.Auftraege,
        BelegTyp.Rechnung => db.Rechnungen,
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
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

    public async Task<IReadOnlyList<BelegDto>> SucheAsync(BelegTyp typ, string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = SetFuerTyp(db, typ).AsNoTracking().Include(b => b.Kunde).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.BelegNummer, $"%{s}%") ||
                (b.Kunde != null && EF.Functions.Like(b.Kunde.Adresse.Name1, $"%{s}%")));
        }
        var belege = await query.OrderByDescending(b => b.BelegDatum).ThenByDescending(b => b.Id).Take(500).ToListAsync(ct);
        return belege.Select(b => b.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<BelegDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.AsNoTracking()
            .Include(b => b.Kunde)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        return beleg.ToDto(mitPositionen: true);
    }

    public async Task<BelegDto> SpeichereAsync(BelegDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Beleg beleg;
        if (dto.Id == 0)
        {
            var kunde = await db.Kunden.Include(k => k.Zahlungsbedingung).FirstOrDefaultAsync(k => k.Id == dto.KundeId, ct)
                ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Kunde), dto.KundeId);

            beleg = NeueInstanz(dto.BelegTyp);
            beleg.BelegNummer = dto.BelegTyp == BelegTyp.Rechnung
                ? string.Empty
                : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(dto.BelegTyp), ct);
            beleg.KundeId = kunde.Id;
            beleg.RechnungsadresseSnapshot = kunde.Adresse.Kopie();
            beleg.LieferadresseSnapshot = kunde.Adresse.Kopie();
            beleg.ZahlungsbedingungZielTage = kunde.Zahlungsbedingung?.ZielTage ?? 0;
            beleg.ZahlungsbedingungSkontoTage = kunde.Zahlungsbedingung?.SkontoTage;
            beleg.ZahlungsbedingungSkontoProzent = kunde.Zahlungsbedingung?.SkontoProzent;
            db.Add(beleg);
        }
        else
        {
            beleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Steuersummen)
                .FirstOrDefaultAsync(b => b.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Beleg), dto.Id);

            if (beleg.Status != BelegStatus.Entwurf)
                throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht mehr geändert werden.");

            db.Entry(beleg).Property(b => b.RowVersion).OriginalValue = dto.RowVersion;
        }

        beleg.BelegDatum = dto.BelegDatum;
        beleg.Leistungsdatum = dto.Leistungsdatum;
        beleg.Kopftext = dto.Kopftext;
        beleg.Fusstext = dto.Fusstext;

        AktualisierePositionen(db, beleg, dto.Positionen);

        db.RemoveRange(beleg.Steuersummen);
        var neueSteuersummen = SteuerRechner.BerechneSteuersummen(beleg.Positionen);
        beleg.Steuersummen = neueSteuersummen.ToList();
        (beleg.SummeNetto, beleg.SummeMwSt, beleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(neueSteuersummen);

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Beleg), beleg.Id, ct);
        return beleg.ToDto(mitPositionen: true);
    }

    private static void AktualisierePositionen(MiletDbContext db, Beleg beleg, IReadOnlyList<BelegPositionDto> positionenDto)
    {
        var vorhandeneIds = positionenDto.Where(p => p.Id != 0).Select(p => p.Id).ToHashSet();
        var zuEntfernen = beleg.Positionen.Where(p => !vorhandeneIds.Contains(p.Id)).ToList();
        foreach (var entfernt in zuEntfernen)
        {
            beleg.Positionen.Remove(entfernt);
            db.Remove(entfernt);
        }

        foreach (var dtoPos in positionenDto)
        {
            var gesamtNetto = SteuerRechner.BerechnePosition(dtoPos.Menge, dtoPos.Einzelpreis, dtoPos.RabattProzent);
            var bestehend = dtoPos.Id != 0 ? beleg.Positionen.FirstOrDefault(p => p.Id == dtoPos.Id) : null;
            if (bestehend is not null)
            {
                bestehend.PositionsNr = dtoPos.PositionsNr;
                bestehend.PositionsTyp = dtoPos.PositionsTyp;
                bestehend.ArtikelId = dtoPos.ArtikelId;
                bestehend.Bezeichnung = dtoPos.Bezeichnung;
                bestehend.EinheitKuerzel = dtoPos.EinheitKuerzel;
                bestehend.Menge = dtoPos.Menge;
                bestehend.Einzelpreis = dtoPos.Einzelpreis;
                bestehend.RabattProzent = dtoPos.RabattProzent;
                bestehend.MwStSatzId = dtoPos.MwStSatzId;
                bestehend.MwStSatzWert = dtoPos.MwStSatzWert;
                bestehend.SteuerSchluessel = dtoPos.SteuerSchluessel;
                bestehend.GesamtNetto = gesamtNetto;
                bestehend.UrsprungsPositionId = dtoPos.UrsprungsPositionId;
            }
            else
            {
                beleg.Positionen.Add(new BelegPosition
                {
                    PositionsNr = dtoPos.PositionsNr,
                    PositionsTyp = dtoPos.PositionsTyp,
                    ArtikelId = dtoPos.ArtikelId,
                    Bezeichnung = dtoPos.Bezeichnung,
                    EinheitKuerzel = dtoPos.EinheitKuerzel,
                    Menge = dtoPos.Menge,
                    Einzelpreis = dtoPos.Einzelpreis,
                    RabattProzent = dtoPos.RabattProzent,
                    MwStSatzId = dtoPos.MwStSatzId,
                    MwStSatzWert = dtoPos.MwStSatzWert,
                    SteuerSchluessel = dtoPos.SteuerSchluessel,
                    GesamtNetto = gesamtNetto,
                    UrsprungsPositionId = dtoPos.UrsprungsPositionId,
                });
            }
        }
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        if (beleg.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht gelöscht werden.");
        db.Remove(beleg);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Beleg), id, ct);
    }
}
