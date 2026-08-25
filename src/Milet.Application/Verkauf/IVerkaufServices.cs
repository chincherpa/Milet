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
    /// <summary>Kopiert alle offenen Positionen von <paramref name="quellBelegId"/> in einen neuen Beleg vom Typ <paramref name="zielTyp"/>.</summary>
    Task<BelegDto> UeberleitenAsync(int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);
}

public interface IRechnungBuchenService
{
    /// <summary>Vergibt atomar die Rechnungsnummer, friert den Beleg ein, legt den Offenen Posten an.</summary>
    Task<BelegDto> BuchenAsync(int rechnungId, CancellationToken ct = default);
}
