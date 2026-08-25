namespace Milet.Application.Stammdaten;

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

public interface IEinheitenService
{
    Task<IReadOnlyList<EinheitDto>> ListeAsync(CancellationToken ct = default);
    Task<EinheitDto> SpeichereAsync(EinheitDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IMwStSaetzeService
{
    Task<IReadOnlyList<MwStSatzDto>> ListeAsync(CancellationToken ct = default);
    Task<MwStSatzDto> SpeichereAsync(MwStSatzDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IZahlungsbedingungenService
{
    Task<IReadOnlyList<ZahlungsbedingungDto>> ListeAsync(CancellationToken ct = default);
    Task<ZahlungsbedingungDto> SpeichereAsync(ZahlungsbedingungDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IVersandartenService
{
    Task<IReadOnlyList<VersandartDto>> ListeAsync(CancellationToken ct = default);
    Task<VersandartDto> SpeichereAsync(VersandartDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IPreislistenService
{
    Task<IReadOnlyList<PreislisteDto>> ListeAsync(CancellationToken ct = default);
    Task<PreislisteDto> SpeichereAsync(PreislisteDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}
