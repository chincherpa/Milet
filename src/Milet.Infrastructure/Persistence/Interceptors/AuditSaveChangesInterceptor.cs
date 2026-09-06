using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Milet.Application.Abstractions;
using Milet.Domain.Common;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Setzt die Audit-Felder auf <see cref="AuditableEntity"/> UND protokolliert jede
/// Änderung (Angelegt/Geändert/Gelöscht) als <see cref="AuditLog"/>-Zeile (GoBD-Nachweis,
/// s. PLAN.md "Audit &amp; Concurrency"). Die Erfassung passiert vor dem physischen Speichern
/// (SavingChanges*, PKs von Added-Entitäten noch unbekannt), das Schreiben der AuditLog-Zeilen
/// danach (SavedChanges*, per zusätzlichem SaveChanges-Aufruf — terminiert garantiert nach einer
/// Rekursionsebene, da AuditLog selbst keine AuditableEntity ist und dabei nichts mehr einsammelt).
/// Der Interceptor ist Singleton (mehrere DbContext-Instanzen aus der Factory) — der Zwischenstand
/// je Speichervorgang hängt daher an einer <see cref="ConditionalWeakTable{TKey,TValue}"/> je Context,
/// nicht an Instanzfeldern.
///
/// ATOMARITÄT: die Audit-Zeilen entstehen zwangsläufig in einem ZWEITEN SaveChanges (die Schlüssel neu
/// angelegter Entitäten sind vorher unbekannt). Ohne Schutz hieße das: der fachliche Save ist committet,
/// und wenn das Schreiben der Audit-Zeilen danach scheitert, existiert die Änderung ohne Nachweis — bei
/// einem GoBD-Nachweis genau das falsche Ergebnis. Deshalb öffnet der Interceptor in
/// <c>SavingChanges</c> eine eigene Transaktion, WENN der Aufrufer keine hat, und committet sie erst
/// nach dem Schreiben der Audit-Zeilen. Hat der Aufrufer bereits eine Transaktion (alle Buchungs- und
/// Belegpfade), wird nichts geöffnet — dann liegen beide Saves ohnehin darin.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    private sealed record PendingAudit(EntityEntry Entry, string EntityName, string Aktion, string? EntityId, Dictionary<string, object?> Werte);

    /// <summary>
    /// Properties, die nie in den Audit-Log geschrieben werden. <c>PasswortHash</c>: der AuditLog wird aus
    /// GoBD-Gründen nie gelöscht und ist für jeden Benutzer mit Administration-Recht lesbar — die vollständige
    /// Hash-Historie jedes Benutzers dort abzulegen, vergrößert die Angriffsfläche fürs Offline-Cracking ohne
    /// jeden Nachweisgewinn (dass das Passwort geändert wurde, steht als Aktion ohnehin im Eintrag).
    /// <c>RowVersion</c>: reines Base64-Rauschen aus der Concurrency-Spalte.
    /// Abgleich über den Namen (nicht Typ + Name), damit eine gleichnamige Property auf einer künftigen
    /// Entität nicht versehentlich doch protokolliert wird.
    /// </summary>
    private static readonly HashSet<string> NichtProtokollierteProperties =
    [
        nameof(Benutzer.PasswortHash),
        nameof(Domain.Common.IHasRowVersion.RowVersion),
    ];

    private static readonly ConditionalWeakTable<DbContext, List<PendingAudit>> Pending = new();

    /// <summary>Transaktionen, die DIESER Interceptor geöffnet hat und deshalb auch selbst beenden muss —
    /// eine vom Aufrufer mitgebrachte Transaktion wird nie angefasst.</summary>
    private static readonly ConditionalWeakTable<DbContext, IDbContextTransaction> EigeneTransaktion = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Anwenden(eventData.Context);
        if (BrauchtEigeneTransaktion(eventData.Context))
        {
            EigeneTransaktion.AddOrUpdate(eventData.Context!, eventData.Context!.Database.BeginTransaction());
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Anwenden(eventData.Context);
        if (BrauchtEigeneTransaktion(eventData.Context))
        {
            EigeneTransaktion.AddOrUpdate(
                eventData.Context!, await eventData.Context!.Database.BeginTransactionAsync(cancellationToken));
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Nur wenn es etwas zu protokollieren gibt UND der Aufrufer keine Transaktion mitbringt. Der zweite
    /// Aufruf (das Schreiben der Audit-Zeilen selbst) fällt schon über die erste Bedingung heraus: er sammelt
    /// nichts ein, weil AuditLog keine AuditableEntity ist.
    /// </summary>
    /// <remarks>Setzt voraus, dass keine wiederholende Ausführungsstrategie konfiguriert ist
    /// (<c>EnableRetryOnFailure</c>) — die verbietet vom Benutzer eröffnete Transaktionen. Die
    /// Verbindungskonfiguration in <c>DependencyInjection.AddInfrastructure</c> nutzt die Standardstrategie.</remarks>
    private static bool BrauchtEigeneTransaktion(DbContext? context)
        => context is not null
            && Pending.TryGetValue(context, out _)
            && context.Database.CurrentTransaction is null
            && !EigeneTransaktion.TryGetValue(context, out _);

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Eigener synchroner Pfad statt .GetAwaiter().GetResult() auf der async-Variante: Sync-over-Async
        // kann in einem UI-Kontext blockieren. Genutzt wird durchgängig der async-Pfad, aber der synchrone
        // darf keine Falle sein.
        // Die eigene Transaktion VOR dem Schreiben der Audit-Zeilen aus der Tabelle nehmen: das Schreiben ist
        // selbst ein SaveChanges und läuft erneut durch diesen Interceptor. Stünde sie noch drin, würde der
        // innere Durchlauf sie committen — im Ergebnis zwar richtig, aber nur zufällig.
        var transaktion = TransaktionUebernehmen(eventData.Context);

        var logs = BaueLogs(eventData.Context);
        if (logs is not null)
        {
            eventData.Context!.Set<AuditLog>().AddRange(logs);
            eventData.Context.SaveChanges();
        }

        if (transaktion is not null)
        {
            transaktion.Commit();
            transaktion.Dispose();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        // s. SavedChanges — erst übernehmen, dann schreiben, dann committen.
        var transaktion = TransaktionUebernehmen(eventData.Context);

        await AuditSchreibenAsync(eventData.Context, cancellationToken);

        if (transaktion is not null)
        {
            await transaktion.CommitAsync(cancellationToken);
            await transaktion.DisposeAsync();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Scheitert der fachliche Save, muss die selbst geöffnete Transaktion zurückgerollt werden —
    /// sonst bliebe sie offen und die nächste Operation auf dem Context liefe unbemerkt darin weiter.</summary>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        TransaktionVerwerfen(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        TransaktionVerwerfen(eventData.Context);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>Nimmt die selbst geöffnete Transaktion aus der Tabelle und gibt sie an den Aufrufer ab —
    /// ab hier ist ausschließlich er für Commit/Rollback zuständig.</summary>
    private static IDbContextTransaction? TransaktionUebernehmen(DbContext? context)
    {
        if (context is null || !EigeneTransaktion.TryGetValue(context, out var transaktion))
        {
            return null;
        }

        EigeneTransaktion.Remove(context);
        return transaktion;
    }

    private static void TransaktionVerwerfen(DbContext? context)
    {
        if (context is null || !EigeneTransaktion.TryGetValue(context, out var transaktion))
        {
            return;
        }

        EigeneTransaktion.Remove(context);
        Pending.Remove(context);
        try
        {
            transaktion.Rollback();
        }
        catch (InvalidOperationException)
        {
            // Bereits beendet (z. B. weil der Provider die Verbindung abgeräumt hat) — nichts zu tun.
        }
        finally
        {
            transaktion.Dispose();
        }
    }

    private void Anwenden(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var jetzt = DateTime.UtcNow;
        var audits = new List<PendingAudit>();

        foreach (EntityEntry<AuditableEntity> entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.ErstelltAm = jetzt;
                entry.Entity.ErstelltVonId = currentUser.BenutzerId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.GeaendertAm = jetzt;
                entry.Entity.GeaendertVonId = currentUser.BenutzerId;
            }

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                audits.Add(Erfassen(entry));
            }
        }

        Pending.Remove(context);
        if (audits.Count > 0)
        {
            Pending.Add(context, audits);
        }
    }

    private static PendingAudit Erfassen(EntityEntry entry)
    {
        var aktion = entry.State switch
        {
            EntityState.Added => "Angelegt",
            EntityState.Modified => "Geändert",
            EntityState.Deleted => "Gelöscht",
            _ => entry.State.ToString(),
        };

        var pkNamen = entry.Metadata.FindPrimaryKey()?.Properties.Select(p => p.Name).ToHashSet() ?? [];

        var werte = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (pkNamen.Contains(prop.Metadata.Name))
            {
                continue;
            }

            if (entry.State == EntityState.Modified && !prop.IsModified)
            {
                continue;
            }

            if (NichtProtokollierteProperties.Contains(prop.Metadata.Name))
            {
                continue;
            }

            werte[prop.Metadata.Name] = entry.State == EntityState.Deleted ? prop.OriginalValue : prop.CurrentValue;
        }

        // Bei Added ist der Identity-PK jetzt noch unbekannt — erst nach dem physischen Save lesbar.
        string? entityId = null;
        if (entry.State != EntityState.Added)
        {
            var pk = entry.Properties.FirstOrDefault(p => pkNamen.Contains(p.Metadata.Name));
            entityId = pk?.OriginalValue?.ToString();
        }

        return new PendingAudit(entry, entry.Entity.GetType().Name, aktion, entityId, werte);
    }

    private async Task AuditSchreibenAsync(DbContext? context, CancellationToken ct)
    {
        var logs = BaueLogs(context);
        if (logs is null)
        {
            return;
        }

        context!.Set<AuditLog>().AddRange(logs);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Baut die AuditLog-Zeilen aus dem Zwischenstand des Speichervorgangs; null, wenn nichts
    /// zu protokollieren ist.</summary>
    private List<AuditLog>? BaueLogs(DbContext? context)
    {
        if (context is null || !Pending.TryGetValue(context, out var audits))
        {
            return null;
        }

        Pending.Remove(context);

        if (audits.Count == 0)
        {
            return null;
        }

        var jetzt = DateTime.UtcNow;
        var logs = audits.Select(audit =>
        {
            var entityId = audit.EntityId;
            if (entityId is null)
            {
                var pkNamen = audit.Entry.Metadata.FindPrimaryKey()?.Properties.Select(p => p.Name).ToHashSet() ?? [];
                var pk = audit.Entry.Properties.FirstOrDefault(p => pkNamen.Contains(p.Metadata.Name));
                entityId = pk?.CurrentValue?.ToString() ?? "?";
            }

            return new AuditLog
            {
                Zeitpunkt = jetzt,
                BenutzerId = currentUser.BenutzerId,
                BenutzerName = currentUser.BenutzerName,
                EntityName = audit.EntityName,
                EntityId = entityId,
                Aktion = audit.Aktion,
                Aenderungen = audit.Werte.Count > 0 ? JsonSerializer.Serialize(audit.Werte) : null,
            };
        }).ToList();

        return logs;
    }
}
