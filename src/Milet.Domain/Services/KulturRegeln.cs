using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;

namespace Milet.Domain.Services;

/// <summary>Reine Regeln der Kulturführung — ohne DB testbar, gebraucht an drei Stellen
/// (Bestandsbuchung, Validatoren, UI-Vorbelegung). Dieselbe Rolle wie <see cref="PreisfindungService"/>/<see cref="SteuerRechner"/>.</summary>
public static class KulturRegeln
{
    /// <summary>Erzwingt die vier zentralen Dimensionsregeln aus dem Datenmodell. Wirft mit sprechendem Text bei Verletzung.</summary>
    public static void PruefeDimensionen(bool istKulturpflanze, bool lagerortHatSektionen, int? sektionId, int? kulturstufeId)
    {
        if (istKulturpflanze && kulturstufeId is null)
        {
            throw new InvalidOperationException("Kulturpflanze erfordert eine Kulturstufe.");
        }

        if (!istKulturpflanze && kulturstufeId is not null)
        {
            throw new InvalidOperationException("Handelsware darf keine Kulturstufe haben.");
        }

        if (lagerortHatSektionen && sektionId is null)
        {
            throw new InvalidOperationException("Dieser Lagerort hat Sektionen — eine Sektion muss angegeben werden.");
        }

        if (!lagerortHatSektionen && sektionId is not null)
        {
            throw new InvalidOperationException("Dieser Lagerort hat keine Sektionen — es darf keine Sektion angegeben werden.");
        }
    }

    /// <summary>Nächsthöhere aktive Stufe nach Reihenfolge, oder null wenn die höchste Stufe bereits erreicht ist.</summary>
    public static Kulturstufe? NaechsteStufe(IReadOnlyList<Kulturstufe> stufen, int aktuelleStufeId)
    {
        ArgumentNullException.ThrowIfNull(stufen);
        var aktuelle = stufen.FirstOrDefault(s => s.Id == aktuelleStufeId);
        if (aktuelle is null)
        {
            return null;
        }

        return stufen
            .Where(s => s.Aktiv && s.Reihenfolge > aktuelle.Reihenfolge)
            .OrderBy(s => s.Reihenfolge)
            .FirstOrDefault();
    }

    /// <summary>Verhindert Nulloperationen (gleiche Sektion und gleiche Stufe) und ungültige Mengen bei Stufenwechsel/Umsetzen.</summary>
    public static void PruefeStufenwechsel(int vonStufeId, int nachStufeId, int? vonSektionId, int? nachSektionId, decimal menge)
    {
        if (menge <= 0)
        {
            throw new InvalidOperationException("Menge muss größer als 0 sein.");
        }

        if (vonStufeId == nachStufeId && vonSektionId == nachSektionId)
        {
            throw new InvalidOperationException("Quelle und Ziel sind identisch — das wäre keine Bewegung.");
        }
    }

    /// <summary>Sektion muss vollständig im Feld liegen (Koordinaten relativ zum Feld).</summary>
    public static bool LiegtInnerhalb(Sektion sektion, Lagerort feld)
    {
        ArgumentNullException.ThrowIfNull(sektion);
        ArgumentNullException.ThrowIfNull(feld);
        if (feld.BreiteMeter is null || feld.HoeheMeter is null)
        {
            return false;
        }

        return sektion.PosXMeter >= 0
            && sektion.PosYMeter >= 0
            && sektion.PosXMeter + sektion.BreiteMeter <= feld.BreiteMeter
            && sektion.PosYMeter + sektion.HoeheMeter <= feld.HoeheMeter;
    }

    /// <summary>Rechteck-Schnitt zweier Sektionen — Grundlage für eine Überlappungs-Warnung (kein Fehler, s. E11).</summary>
    public static bool Ueberlappt(Sektion a, Sektion b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.PosXMeter < b.PosXMeter + b.BreiteMeter
            && b.PosXMeter < a.PosXMeter + a.BreiteMeter
            && a.PosYMeter < b.PosYMeter + b.HoeheMeter
            && b.PosYMeter < a.PosYMeter + a.HoeheMeter;
    }
}
