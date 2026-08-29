using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>
/// Vergibt Nummern über ein atomares UPDATE ... OUTPUT auf genau einer Zeile (via TOP(1)-CTE) —
/// kein Read-Modify-Write, daher unter Nebenläufigkeit ohne Retry-Loop korrekt
/// (siehe Architektur-Plan §2.6). Existieren für denselben Code sowohl ein jahresbezogener
/// als auch ein jahresloser Kreis, gewinnt der jahresbezogene.
///
/// Findet sich für einen jahresbezogenen Code keine Zeile des laufenden Jahres, wird sie beim
/// ersten Zugriff angelegt (Lazy-Jahreswechsel, s. <see cref="LegeJahreskreisAnAsync"/>) — sonst
/// stünde das System am 01.01. still, bis jemand den Migrator startet.
///
/// Die statische Überladung mit explizitem <see cref="MiletDbContext"/> ist der Pfad für
/// Buchungsvorgänge: sie läuft auf der Verbindung (und damit in der Transaktion) des Aufrufers,
/// sodass eine vergebene Nummer bei einem Rollback des Buchungsvorgangs mit zurückrollt. Wird sie
/// über die Instanzmethode auf einem eigenen Context aufgerufen, committet das UPDATE sofort und
/// die Nummer wäre bei einem späteren Fehlschlag verbraucht (Lücke — bei Rechnungsnummern ein
/// Verstoß gegen §14 UStG, s. Kommentar in Beleg.cs).
/// </summary>
public sealed class NumberRangeService(IDbContextFactory<MiletDbContext> dbContextFactory) : INumberRangeService
{
    private sealed record VergebeneNummer(int NaechsteNummer, string Format);

    public async Task<string> NaechsteNummerAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await NaechsteNummerAsync(db, code, cancellationToken);
    }

    /// <summary>
    /// Vergibt die nächste Nummer auf dem übergebenen Context — also innerhalb einer bereits
    /// laufenden Transaktion des Aufrufers. Statisch (analog zu
    /// <c>BestandService.BucheBewegungAsync</c>), weil der Aufrufer den Context ohnehin hält.
    /// </summary>
    public static async Task<string> NaechsteNummerAsync(MiletDbContext db, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Bewusst DateTime.Today (lokal) und nicht UtcNow: das Belegdatum wird ebenfalls lokal
        // gebildet (BelegService/BelegUeberleitungService). Bei UtcNow bekäme ein Beleg vom
        // 01.01. in der ersten Stunde des Jahres eine Nummer aus dem Vorjahreskreis.
        var jahr = DateTime.Today.Year;

        var vergeben = await VergebeAsync(db, code, jahr, cancellationToken);
        if (vergeben is null)
        {
            await LegeJahreskreisAnAsync(db, code, jahr, cancellationToken);
            vergeben = await VergebeAsync(db, code, jahr, cancellationToken);
        }

        if (vergeben is null)
        {
            throw new InvalidOperationException($"Nummernkreis '{code}' existiert nicht.");
        }

        return string.Format(vergeben.Format, vergeben.NaechsteNummer, jahr);
    }

    private static async Task<VergebeneNummer?> VergebeAsync(MiletDbContext db, string code, int jahr, CancellationToken cancellationToken)
    {
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

        return vergeben.Count == 0 ? null : vergeben[0];
    }

    /// <summary>
    /// Legt den Kreis für das laufende Jahr an, wenn es für den Code bereits jahresbezogene Kreise
    /// gibt (Format wird vom jüngsten übernommen, Zählung startet wieder bei 1 — genau die
    /// Jahresfolge, die der Seed anlegen würde). Für einen Code, den es überhaupt nicht gibt,
    /// passiert nichts: der Aufrufer wirft dann wie bisher.
    ///
    /// Das INSERT ist über NOT EXISTS gegen den Normalfall abgesichert; gewinnt trotzdem ein
    /// paralleler Aufrufer das Rennen, schlägt es am Unique-Index (Code, Jahr) fehl. Der Fehler
    /// wird geschluckt — die Zeile, die angelegt werden sollte, existiert danach ja, und der
    /// erneute Vergabeversuch des Aufrufers greift.
    /// </summary>
    private static async Task LegeJahreskreisAnAsync(MiletDbContext db, string code, int jahr, CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO Nummernkreise (Code, Jahr, NaechsteNummer, Format)
                 SELECT TOP (1) {code}, {jahr}, 1, Format
                 FROM Nummernkreise
                 WHERE Code = {code} AND Jahr IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Nummernkreise WHERE Code = {code} AND Jahr = {jahr})
                 ORDER BY Jahr DESC
                 """, cancellationToken);
        }
        catch (DbException)
        {
            // Paralleler Aufrufer war schneller — s. Kommentar oben. Bewusst DbException und nicht
            // SqlException: der Provider-Typ soll hier nicht in die Infrastruktur durchschlagen.
            // Unbedenklich auch innerhalb einer laufenden Transaktion des Aufrufers: eine Verletzung des
            // Unique-Index rollt in SQL Server nur die Anweisung zurück, nicht die Transaktion.
        }
    }
}
