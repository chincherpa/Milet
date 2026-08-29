using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Anwenden(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Anwenden(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Eigener synchroner Pfad statt .GetAwaiter().GetResult() auf der async-Variante: Sync-over-Async
        // kann in einem UI-Kontext blockieren. Genutzt wird durchgängig der async-Pfad, aber der synchrone
        // darf keine Falle sein.
        var logs = BaueLogs(eventData.Context);
        if (logs is not null)
        {
            eventData.Context!.Set<AuditLog>().AddRange(logs);
            eventData.Context.SaveChanges();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await AuditSchreibenAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
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
