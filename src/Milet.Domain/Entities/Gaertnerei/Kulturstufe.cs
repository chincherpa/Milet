using Milet.Domain.Common;

namespace Milet.Domain.Entities.Gaertnerei;

/// <summary>Konfigurierbare Stufe der Pflanzenanzucht (z. B. Jungpflanze → Teenagerpflanze → Verkaufspflanze).
/// Bewusst Stammdaten statt Enum: der Nutzer benennt und erweitert die Stufen selbst (Einstellungen).
/// Referenzen laufen über Id — ein Umbenennen wirkt rückwirkend, weil es dieselbe Stufe bleibt.</summary>
public class Kulturstufe : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Bestimmt die Kette; der Stufenwechsel schlägt die nächsthöhere Stufe vor. Rückstufung bleibt erlaubt.</summary>
    public int Reihenfolge { get; set; }

    /// <summary>Nur Bestand in einer verkaufsfähigen Stufe zählt als lieferbar (Verfügbarkeitsprüfung, Lieferschein-Vorbelegung).</summary>
    public bool IstVerkaufsfaehig { get; set; }

    /// <summary>Highlight-Farbe im Grundriss (#RRGGBB) — trennt die Stufen derselben Pflanze optisch.</summary>
    public string FarbeHex { get; set; } = "#4CAF50";

    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
