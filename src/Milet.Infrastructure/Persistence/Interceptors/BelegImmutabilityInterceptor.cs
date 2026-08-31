using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Interceptors;

/// <summary>GoBD: ein bereits gebuchter Beleg darf nicht mehr verändert werden. Der Guard in
/// <c>BelegService</c>/<c>RechnungBuchenService</c> greift zuerst mit einer sprechenden Fehlermeldung;
/// dieser Interceptor ist die harte Sperre für jeden Codepfad, der ihn umgeht.
///
/// Gesperrt wird deshalb nicht nur der Belegkopf, sondern auch seine Positionen und Steuersummen —
/// also genau die GoBD-relevanten Beträge — und zwar in allen drei Zuständen (Added/Modified/Deleted).
/// Eine frühere Fassung prüfte nur <c>Beleg</c> im Zustand <c>Modified</c>: ein <c>db.Remove(beleg)</c>
/// auf einen gebuchten Beleg und jede Änderung an dessen Positionen liefen ungehindert durch.
///
/// Zwei Ausnahmen erlauben eine Statusfortschreibung auf einem sonst gesperrten Beleg:
/// (1) der reine Lebenszyklus-Übergang Gebucht → Erledigt, den <c>BelegUeberleitungService</c> setzt, wenn
/// ein Gebucht-Beleg vollständig in einen Folgebeleg überführt wurde (z. B. Wareneingang → Eingangsrechnung);
/// hier darf ausschließlich Status geändert sein.
/// (2) der Storno-Übergang (Gebucht|Erledigt) → Storniert, den <c>StornoService</c> setzt: hier dürfen
/// zusätzlich zu Status auch Fusstext geändert sein (der Storno-Grund hat kein eigenes Feld, s. StornoService)
/// — alles andere (Positionen/Summen/Kunde/...) bleibt über <see cref="PruefeUntergeordnet{TEntity}"/> bzw.
/// diese Prüfung weiterhin vollständig gesperrt. Ein bereits stornierter Beleg ist in jedem Fall endgültig
/// gesperrt — Storno ist keine Statusmaschine, aus der man weiter herauskäme.</summary>
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

        PruefeBelege(context);
        PruefeUntergeordnet<BelegPosition>(context, "Positionen");
        PruefeUntergeordnet<BelegSteuerSumme>(context, "Steuersummen");
    }

    private static void PruefeBelege(DbContext context)
    {
        foreach (EntityEntry<Beleg> entry in context.ChangeTracker.Entries<Beleg>())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;

            var urspruenglicherStatus = entry.OriginalValues.GetValue<BelegStatus>(nameof(Beleg.Status));

            if (entry.State == EntityState.Deleted)
            {
                // Ein Beleg, der einmal gebucht war, wird nie gelöscht — Korrekturen sind Gegenbuchungen.
                if (urspruenglicherStatus is not BelegStatus.Entwurf)
                    throw Gesperrt(entry.Entity.BelegNummer, urspruenglicherStatus, "er kann nicht mehr gelöscht werden");
                continue;
            }

            if (urspruenglicherStatus is BelegStatus.Storniert)
                throw Gesperrt(entry.Entity.BelegNummer, urspruenglicherStatus, "er kann nicht mehr geändert werden");

            if (urspruenglicherStatus is BelegStatus.Gebucht or BelegStatus.Erledigt)
            {
                var geaendertePropertien = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet();

                var nurStatusFortschreibungAufErledigt =
                    urspruenglicherStatus == BelegStatus.Gebucht
                    && entry.Entity.Status == BelegStatus.Erledigt
                    && geaendertePropertien.SetEquals([nameof(Beleg.Status)]);

                var nurStornoFortschreibung =
                    entry.Entity.Status == BelegStatus.Storniert
                    && geaendertePropertien.IsSubsetOf([nameof(Beleg.Status), nameof(Beleg.Fusstext)]);

                if (!nurStatusFortschreibungAufErledigt && !nurStornoFortschreibung)
                    throw Gesperrt(entry.Entity.BelegNummer, urspruenglicherStatus, "er kann nicht mehr geändert werden");
            }
        }
    }

    /// <summary>
    /// Sperrt Positionen und Steuersummen, sobald ihr Beleg den Entwurfsstatus verlassen hat. Der Status
    /// kommt bevorzugt aus dem Change-Tracker (der übliche Fall: der Beleg wurde mitgeladen); ist der Beleg
    /// nicht getrackt, wird er gezielt nachgelesen. Wird der Beleg im selben SaveChanges selbst gelöscht,
    /// entscheidet allein die Prüfung des Belegkopfes — die Kaskade auf die Positionen ist dann Folge, nicht
    /// eigene Änderung.
    /// </summary>
    private static void PruefeUntergeordnet<TEntity>(DbContext context, string bezeichnung)
        where TEntity : class
    {
        var eintraege = context.ChangeTracker.Entries<TEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (eintraege.Count == 0) return;

        // Id != 0 filtert neu angelegte Belege heraus: sie haben ihren Schlüssel noch nicht (mehrere
        // Added-Belege hätten sonst denselben Dictionary-Key 0) und sind ohnehin Entwürfe. Ihre ebenfalls
        // neuen Positionen tragen dann BelegId 0, finden unten nichts und werden korrekt nicht gesperrt.
        var belegZustand = context.ChangeTracker.Entries<Beleg>()
            .Where(e => e.Entity.Id != 0)
            .ToDictionary(
                e => e.Entity.Id,
                e => (
                    Status: e.State is EntityState.Added
                        ? e.Entity.Status
                        : e.OriginalValues.GetValue<BelegStatus>(nameof(Beleg.Status)),
                    Nummer: e.Entity.BelegNummer,
                    WirdGeloescht: e.State == EntityState.Deleted));

        var offen = new List<int>();
        foreach (var entry in eintraege)
        {
            var belegIdProperty = entry.Property("BelegId");
            var belegId = (int)(entry.State == EntityState.Added
                ? belegIdProperty.CurrentValue!
                : belegIdProperty.OriginalValue!);

            if (belegZustand.TryGetValue(belegId, out var zustand))
            {
                if (zustand.WirdGeloescht) continue;
                if (zustand.Status is not BelegStatus.Entwurf)
                    throw Gesperrt(zustand.Nummer, zustand.Status, $"seine {bezeichnung} können nicht mehr geändert werden");
                continue;
            }

            offen.Add(belegId);
        }

        if (offen.Count == 0) return;

        // Nur für den Ausnahmefall „Beleg nicht mitgeladen": eine gezielte Abfrage auf die betroffenen Ids.
        var ids = offen.Distinct().ToList();
        var ausDb = context.Set<Beleg>().AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Status, b.BelegNummer })
            .ToList();

        foreach (var beleg in ausDb)
        {
            if (beleg.Status is not BelegStatus.Entwurf)
                throw Gesperrt(beleg.BelegNummer, beleg.Status, $"seine {bezeichnung} können nicht mehr geändert werden");
        }
    }

    private static InvalidOperationException Gesperrt(string belegNummer, BelegStatus status, string folge)
    {
        // Die Fehlermeldung ist das Einzige, was der Benutzer von dieser Sperre sieht — sie nennt deshalb
        // den tatsächlichen Status (früher stand für einen stornierten Beleg „ist bereits gebucht").
        var statusText = status switch
        {
            BelegStatus.Gebucht => "gebucht",
            BelegStatus.Storniert => "storniert",
            BelegStatus.Erledigt => "erledigt",
            _ => status.ToString().ToLowerInvariant(),
        };
        return new InvalidOperationException($"Beleg '{belegNummer}' ist bereits {statusText}, {folge} (GoBD).");
    }
}
