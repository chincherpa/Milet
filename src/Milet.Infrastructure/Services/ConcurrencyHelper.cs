using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;

namespace Milet.Infrastructure.Services;

internal static class ConcurrencyHelper
{
    /// <summary>Fremdschlüsselverletzung — die Löschung ist durch verweisende Zeilen blockiert.</summary>
    private const int SqlFehlerFremdschluessel = 547;

    /// <summary>Verletzung eines Unique-Index (2601) bzw. einer Unique-Constraint (2627).</summary>
    private const int SqlFehlerUniqueIndex = 2601;
    private const int SqlFehlerUniqueConstraint = 2627;

    /// <summary>
    /// Prüft, ob hinter einer <see cref="DbUpdateException"/> eine bestimmte SQL-Server-Fehlernummer steckt.
    ///
    /// Hier bewusst auf <see cref="SqlException"/> getypt — anders als in <c>NumberRangeService</c>, wo eine
    /// erwartete Unique-Verletzung nur geschluckt wird und der genaue Fehler egal ist. Hier ist die Nummer
    /// der ganze Punkt: pauschal jede <c>DbUpdateException</c> als „wird noch verwendet" zu erklären, gibt
    /// einem Timeout oder Deadlock die falsche Begründung und schickt den Benutzer an die falsche Stelle.
    /// </summary>
    private static bool IstSqlFehler(DbUpdateException ex, params int[] nummern)
        => ex.InnerException is SqlException sql && nummern.Contains(sql.Number);

    /// <summary>
    /// Führt SaveChangesAsync aus und übersetzt einen DbUpdateConcurrencyException
    /// in die anwendungsseitige ConcurrencyConflictException (siehe Architektur-Plan §2.7).
    /// </summary>
    public static async Task SaveChangesTranslatingConcurrencyAsync(
        this DbContext db, string entitaet, object id, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(entitaet, id, ex);
        }
    }

    /// <summary>
    /// Wie <see cref="SaveChangesTranslatingConcurrencyAsync"/>, übersetzt zusätzlich die Verletzung eines
    /// Unique-Index in eine sprechende Meldung. Für Entitäten mit einem fachlich eindeutigen Feld neben dem
    /// Primärschlüssel (Kulturstufe.Reihenfolge/Code, Mahnstufe.Stufe): ohne das erreicht den Benutzer die
    /// rohe SQL-Meldung „Cannot insert duplicate key row in object ...".
    /// </summary>
    public static async Task SaveChangesTranslatingConcurrencyAsync(
        this DbContext db, string entitaet, object id, string uniqueMeldung, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(entitaet, id, ex);
        }
        catch (DbUpdateException ex) when (IstSqlFehler(ex, SqlFehlerUniqueIndex, SqlFehlerUniqueConstraint))
        {
            throw new InvalidOperationException(uniqueMeldung, ex);
        }
    }

    /// <summary>
    /// Führt SaveChangesAsync für eine Löschung aus und übersetzt eine durch Fremdschlüssel
    /// blockierte Löschung in eine verständliche Fehlermeldung. Andere Datenbankfehler (Timeout, Deadlock)
    /// werden durchgereicht statt umgedeutet.
    /// </summary>
    public static async Task SaveChangesDeletingAsync(
        this DbContext db, string entitaet, object id, CancellationToken cancellationToken)
        => await db.SaveChangesDeletingAsync(
            $"{entitaet} (Id {id}) kann nicht gelöscht werden, da noch Datensätze darauf verweisen.",
            cancellationToken);

    /// <summary>Wie oben, mit fachlich formulierter Meldung des Aufrufers.</summary>
    public static async Task SaveChangesDeletingAsync(
        this DbContext db, string meldung, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IstSqlFehler(ex, SqlFehlerFremdschluessel))
        {
            throw new InvalidOperationException(meldung, ex);
        }
    }

    /// <summary>
    /// Führt SaveChangesAsync aus und übersetzt eine Unique-Verletzung in die Meldung des Aufrufers — für
    /// Einfügepfade, deren Eindeutigkeit die Datenbank durchsetzt statt eine vorgelagerte Prüfung
    /// (InventurService: höchstens eine offene Inventur je Lagerort).
    /// </summary>
    public static async Task SaveChangesTranslatingUniqueAsync(
        this DbContext db, string meldung, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IstSqlFehler(ex, SqlFehlerUniqueIndex, SqlFehlerUniqueConstraint))
        {
            throw new InvalidOperationException(meldung, ex);
        }
    }
}
