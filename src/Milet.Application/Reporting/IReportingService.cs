namespace Milet.Application.Reporting;

public interface IReportingService
{
    Task<IReadOnlyList<UmsatzJeKundeDto>> UmsatzJeKundeAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);

    Task<IReadOnlyList<UmsatzJeArtikelDto>> UmsatzJeArtikelAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);

    Task<IReadOnlyList<UmsatzJeMonatDto>> UmsatzJeMonatAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);

    /// <summary>Bewegungen aus dem Lagerbewegungs-Ledger; <paramref name="artikelId"/> optional (null = alle Artikel).</summary>
    Task<IReadOnlyList<ArtikelbewegungDto>> ArtikelbewegungenAsync(int? artikelId, DateOnly von, DateOnly bis, CancellationToken ct = default);

    Task<IReadOnlyList<TopArtikelDto>> TopArtikelAsync(DateOnly von, DateOnly bis, int anzahl = 10, CancellationToken ct = default);

    /// <summary>Gebuchte Aufträge mit mindestens einer Position, deren Menge noch nicht vollständig
    /// in Lieferschein/Rechnung übernommen wurde (s. BelegPosition.OffeneMenge).</summary>
    Task<IReadOnlyList<OffenerAuftragDto>> OffeneAuftraegeAsync(CancellationToken ct = default);

    /// <summary>Kulturbestand (Phase 8) — Menge je Pflanze × Stufe × Feld × Sektion, optional gefiltert.</summary>
    Task<IReadOnlyList<KulturbestandZeileDto>> KulturbestandAsync(int? feldId, int? kulturstufeId, CancellationToken ct = default);

    /// <summary>Ausfallquote je Pflanze und Stufe im Zeitraum (Σ Ausfall gegen Σ Zugänge).</summary>
    Task<IReadOnlyList<AusfallquoteZeileDto>> AusfallquoteAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);

    /// <summary>Belegte m² je Feld gegen Gesamtfläche.</summary>
    Task<IReadOnlyList<FlaechenbelegungZeileDto>> FlaechenbelegungAsync(CancellationToken ct = default);
}
