namespace Milet.Application.Verkauf;

public interface IBelegService
{
    Task<IReadOnlyList<BelegDto>> SucheAsync(Domain.Entities.Verkauf.BelegTyp typ, string? suchtext, CancellationToken ct = default);
    Task<BelegDto> LadeAsync(int id, CancellationToken ct = default);
    Task<BelegDto> SpeichereAsync(BelegDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IVerkaufLookupService
{
    Task<VerkaufLookups> LadeLookupsAsync(CancellationToken ct = default);
    Task<PreisErgebnisDto> ErmittlePreisAsync(int artikelId, decimal menge, int kundeId, CancellationToken ct = default);
}

public interface IBelegUeberleitungService
{
    Task<BelegDto> UeberleitenAsync(int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);

    /// <summary>Offene Menge je Position des Quellbelegs — Grundlage für die Auswahl im Teillieferungs-Dialog.</summary>
    Task<IReadOnlyList<OffenePositionDto>> LadeOffenePositionenAsync(int quellBelegId, CancellationToken ct = default);

    /// <summary>Wie <see cref="UeberleitenAsync"/>, aber mit expliziter (ggf. reduzierter) Menge je Quellposition statt automatisch voller offener Menge — Basis der Teillieferung. <paramref name="lagerortId"/> nur bei zielTyp Lieferschein erforderlich.
    /// <paramref name="dimensionenJePosition"/> (Phase 8, E9): optionale Sektion/Kulturstufe je Quellposition, keyed auf deren Id — fehlt ein Eintrag oder wird der Parameter ganz weggelassen, bleiben beide Dimensionen NULL (bestehendes Verhalten für Nicht-Kulturartikel bleibt identisch).</summary>
    Task<BelegDto> UeberleitenMitAuswahlAsync(
        int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp,
        IReadOnlyDictionary<int, decimal> mengenJePosition, int? lagerortId,
        IReadOnlyDictionary<int, BelegPositionDimensionenDto>? dimensionenJePosition = null, CancellationToken ct = default);

    /// <summary>Führt mehrere Quellbelege (z. B. mehrere Lieferscheine gleichen Kunden) in einen Zielbeleg zusammen — Basis der Sammelrechnung.</summary>
    Task<BelegDto> UeberleitenMehrereAsync(IReadOnlyList<int> quellBelegIds, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);
}

public interface IRechnungBuchenService
{
    /// <summary>Vergibt atomar die Rechnungsnummer, friert den Beleg ein, legt den Offenen Posten an.</summary>
    Task<BelegDto> BuchenAsync(int rechnungId, CancellationToken ct = default);
}

public interface ILieferscheinBuchenService
{
    /// <summary>Bucht: prüft/bucht Bestand atomar je Artikelposition, verknüpft ausgewählte Seriennummern, setzt Status Gebucht — eine Transaktion.</summary>
    Task<BelegDto> BuchenAsync(
        int lieferscheinId, IReadOnlyDictionary<int, IReadOnlyList<int>> seriennummernJePosition, CancellationToken ct = default);
}
