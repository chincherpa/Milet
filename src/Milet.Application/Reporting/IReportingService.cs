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
}
