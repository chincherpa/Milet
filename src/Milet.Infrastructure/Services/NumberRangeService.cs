using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>
/// Vergibt Nummern über ein atomares UPDATE ... OUTPUT auf genau einer Zeile (via TOP(1)-CTE) —
/// kein Read-Modify-Write, daher unter Nebenläufigkeit ohne Retry-Loop korrekt
/// (siehe Architektur-Plan §2.6). Existieren für denselben Code sowohl ein jahresbezogener
/// als auch ein jahresloser Kreis, gewinnt der jahresbezogene.
/// </summary>
public sealed class NumberRangeService(IDbContextFactory<MiletDbContext> dbContextFactory) : INumberRangeService
{
    private sealed record VergebeneNummer(int NaechsteNummer, string Format);

    public async Task<string> NaechsteNummerAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var jahr = DateTime.UtcNow.Year;

        var vergeben = await db.Database.SqlQuery<VergebeneNummer>(
                $"""
                 ;WITH Kandidat AS (
                     SELECT TOP (1) *
                     FROM Nummernkreise
                     WHERE Code = {code} AND (Jahr = {jahr} OR Jahr IS NULL)
                     ORDER BY CASE WHEN Jahr = {jahr} THEN 0 ELSE 1 END
                 )
                 UPDATE Kandidat
                 SET NaechsteNummer = NaechsteNummer + 1
                 OUTPUT deleted.NaechsteNummer AS NaechsteNummer, deleted.Format AS Format
                 """)
            .ToListAsync(cancellationToken);

        if (vergeben.Count == 0)
        {
            throw new InvalidOperationException($"Nummernkreis '{code}' existiert nicht.");
        }

        return string.Format(vergeben[0].Format, vergeben[0].NaechsteNummer, jahr);
    }
}
