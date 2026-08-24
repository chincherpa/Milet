using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Services;

/// <summary>
/// Ergebnis der Preisfindung für eine Belegposition.
/// </summary>
/// <param name="Einzelpreis">Ermittelter Netto-Einzelpreis.</param>
/// <param name="RabattProzent">Vorgeschlagener Rabatt (Kundenrabatt; 0 bei Staffel-/Kundenpreis).</param>
/// <param name="Quelle">Woher der Preis stammt.</param>
public sealed record PreisErgebnis(decimal Einzelpreis, decimal RabattProzent, PreisQuelle Quelle);

public enum PreisQuelle
{
    Listenpreis = 0,
    Preisliste = 1,
}

/// <summary>
/// Reine Preisfindungslogik ohne Datenzugriff.
/// Auflösung: kundenspezifischer Staffelpreis (beste AbMenge ≤ Menge in der Preisliste
/// des Kunden) → Listenpreis. Der Kundenrabatt wird nur auf den Listenpreis
/// vorgeschlagen — Preislistenpreise gelten als bereits verhandelt.
/// </summary>
public static class PreisfindungService
{
    public static PreisErgebnis ErmittlePreis(
        Artikel artikel,
        decimal menge,
        int? kundenPreislisteId,
        IReadOnlyCollection<ArtikelPreis> staffelpreise,
        decimal kundenRabattProzent)
    {
        ArgumentNullException.ThrowIfNull(artikel);
        ArgumentNullException.ThrowIfNull(staffelpreise);

        if (menge <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(menge), menge, "Menge muss positiv sein.");
        }

        if (kundenPreislisteId is int preislisteId)
        {
            var bester = staffelpreise
                .Where(p => p.PreislisteId == preislisteId
                    && p.ArtikelId == artikel.Id
                    && p.AbMenge <= menge)
                .OrderByDescending(p => p.AbMenge)
                .FirstOrDefault();

            if (bester is not null)
            {
                return new PreisErgebnis(bester.Preis, 0m, PreisQuelle.Preisliste);
            }
        }

        return new PreisErgebnis(artikel.Listenpreis, kundenRabattProzent, PreisQuelle.Listenpreis);
    }
}
