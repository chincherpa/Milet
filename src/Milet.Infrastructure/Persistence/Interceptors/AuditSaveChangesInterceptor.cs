using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexus.Application.Abstractions;
using Nexus.Domain.Common;

namespace Nexus.Infrastructure.Persistence.Interceptors;

public sealed class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
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

    private void Anwenden(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var jetzt = DateTime.UtcNow;

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
        }
    }
}
