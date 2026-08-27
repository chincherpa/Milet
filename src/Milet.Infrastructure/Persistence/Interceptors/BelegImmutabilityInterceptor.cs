using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Interceptors;

/// <summary>GoBD: ein bereits gebuchter Beleg darf nicht mehr verändert werden. Der Guard in
/// <c>BelegService</c>/<c>RechnungBuchenService</c> greift zuerst mit einer sprechenden Fehlermeldung;
/// dieser Interceptor ist die harte Sperre für jeden Codepfad, der ihn umgeht.
///
/// Einzige Ausnahme: der reine Lebenszyklus-Übergang Gebucht → Erledigt, den <c>BelegUeberleitungService</c>
/// setzt, wenn ein Gebucht-Beleg vollständig in einen Folgebeleg überführt wurde (z. B. Wareneingang →
/// Eingangsrechnung). Das ist keine inhaltliche Änderung am GoBD-relevanten Beleg, sondern nur eine
/// Statusfortschreibung — deshalb erlaubt, aber nur wenn Status wirklich die EINZIGE geänderte Property ist
/// (sonst könnte eine inhaltliche Änderung am Status-Flip vorbeigeschmuggelt werden). Storniert bleibt in
/// jedem Fall vollständig gesperrt.</summary>
public sealed class BelegImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Pruefen(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Pruefen(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Pruefen(DbContext? context)
    {
        if (context is null) return;
        foreach (EntityEntry<Beleg> entry in context.ChangeTracker.Entries<Beleg>())
        {
            if (entry.State != EntityState.Modified) continue;
            var urspruenglicherStatus = entry.OriginalValues.GetValue<BelegStatus>(nameof(Beleg.Status));
            if (urspruenglicherStatus is BelegStatus.Storniert)
            {
                throw new InvalidOperationException(
                    $"Beleg '{entry.Entity.BelegNummer}' ist bereits gebucht und damit unveränderlich (GoBD).");
            }
            if (urspruenglicherStatus is BelegStatus.Gebucht)
            {
                var geaendertePropertien = entry.Properties.Where(p => p.IsModified).ToList();
                var nurStatusFortschreibungAufErledigt =
                    geaendertePropertien.Count == 1
                    && geaendertePropertien[0].Metadata.Name == nameof(Beleg.Status)
                    && entry.Entity.Status == BelegStatus.Erledigt;

                if (!nurStatusFortschreibungAufErledigt)
                {
                    throw new InvalidOperationException(
                        $"Beleg '{entry.Entity.BelegNummer}' ist bereits gebucht und damit unveränderlich (GoBD).");
                }
            }
        }
    }
}
