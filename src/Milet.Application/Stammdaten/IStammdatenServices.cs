namespace Nexus.Application.Stammdaten;

public interface IKundenService
{
    Task<IReadOnlyList<KundeDto>> SucheAsync(string? suchtext, CancellationToken ct = default);

    Task<KundeDto> LadeAsync(int id, CancellationToken ct = default);

    /// <summary>Id 0 = Neuanlage (Kundennummer wird automatisch vergeben).</summary>
    Task<KundeDto> SpeichereAsync(KundeDto dto, CancellationToken ct = default);

    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface ILieferantenService
{
    Task<IReadOnlyList<LieferantDto>> SucheAsync(string? suchtext, CancellationToken ct = default);

    Task<LieferantDto> LadeAsync(int id, CancellationToken ct = default);

    Task<LieferantDto> SpeichereAsync(LieferantDto dto, CancellationToken ct = default);

    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IArtikelService
{
    Task<IReadOnlyList<ArtikelDto>> SucheAsync(string? suchtext, CancellationToken ct = default);

    Task<ArtikelDto> LadeAsync(int id, CancellationToken ct = default);

    Task<ArtikelDto> SpeichereAsync(ArtikelDto dto, CancellationToken ct = default);

    Task LoescheAsync(int id, CancellationToken ct = default);
}

/// <summary>Lookups für ComboBoxen in Editiermasken.</summary>
public interface IStammdatenLookupService
{
    Task<StammdatenLookups> LadeLookupsAsync(CancellationToken ct = default);
}
