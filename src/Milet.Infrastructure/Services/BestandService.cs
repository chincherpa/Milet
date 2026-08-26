using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BestandService(IDbContextFactory<MiletDbContext> dbContextFactory) : IBestandService
{
    private static readonly BestandskorrekturValidator Validator = new();

    public async Task<IReadOnlyList<ArtikelBestandDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.ArtikelBestaende.AsNoTracking().Include(b => b.Artikel).Include(b => b.Lagerort).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b => EF.Functions.Like(b.Artikel!.Bezeichnung, $"%{s}%") || EF.Functions.Like(b.Artikel!.Artikelnummer, $"%{s}%"));
        }
        var liste = await query.OrderBy(b => b.Artikel!.Artikelnummer).ToListAsync(ct);
        return liste.Select(b => b.ToDto()).ToList();
    }

    public async Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BucheBewegungAsync(db, dto.ArtikelId, dto.LagerortId, dto.MengeDelta, LagerbewegungTyp.Korrektur, belegPositionId: null, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Einziger Schreibpfad auf Bestand — ein atomares UPDATE (kein Read-Modify-Write), Negativbestand ist hart gesperrt.
    /// Läuft innerhalb der Transaktion des Aufrufers (Aufrufer öffnet/committet); wiederverwendbar von Bestandskorrektur, Lieferschein-Buchen, Inventur-Abschluss.</summary>
    internal static async Task BucheBewegungAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal mengeDelta,
        LagerbewegungTyp typ, int? belegPositionId, CancellationToken ct)
    {
        var betroffeneZeilen = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE ArtikelBestaende SET Menge = Menge + {mengeDelta}
             WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId} AND Menge + {mengeDelta} >= 0
             """, ct);

        if (betroffeneZeilen == 0)
        {
            if (mengeDelta < 0)
                throw new InvalidOperationException("Nicht genügend Bestand vorhanden — Buchung würde negativen Bestand erzeugen.");

            db.ArtikelBestaende.Add(new ArtikelBestand { ArtikelId = artikelId, LagerortId = lagerortId, Menge = mengeDelta });
        }

        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            ArtikelId = artikelId,
            LagerortId = lagerortId,
            Typ = typ,
            Menge = mengeDelta,
            BelegPositionId = belegPositionId,
            Zeitpunkt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
