using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;

namespace Milet.Infrastructure.Services;

internal static class ConcurrencyHelper
{
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
    /// Führt SaveChangesAsync für eine Löschung aus und übersetzt eine durch Fremdschlüssel
    /// blockierte Löschung in eine verständliche Fehlermeldung.
    /// </summary>
    public static async Task SaveChangesDeletingAsync(
        this DbContext db, string entitaet, object id, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                $"{entitaet} (Id {id}) kann nicht gelöscht werden, da noch Datensätze darauf verweisen.", ex);
        }
    }
}
