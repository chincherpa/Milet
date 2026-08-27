namespace Milet.Application.Finanzen;

public interface IOffenePostenService
{
    Task<IReadOnlyList<OffenePostenDto>> ListeAsync(OffenePostenFilterDto? filter = null, CancellationToken ct = default);
    Task<OffenePostenDto> LadeAsync(int id, CancellationToken ct = default);
}

public interface IZahlungService
{
    Task<SkontoVorschlagDto> SkontoVorschlagAsync(int offenerPostenId, DateOnly zahlungsdatum, CancellationToken ct = default);
    Task<ZahlungDto> ErfasseZahlungAsync(ZahlungDto dto, CancellationToken ct = default);
}

public interface IMahnwesenService
{
    Task<IReadOnlyList<MahnstufeDto>> ListeStufenAsync(CancellationToken ct = default);
    Task<MahnstufeDto> SpeichereStufeAsync(MahnstufeDto dto, CancellationToken ct = default);
    Task LoescheStufeAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<MahnlaufGruppeDto>> ErmittleFaelligeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MahnungDto>> MahnlaufDurchfuehrenAsync(IReadOnlyList<int> offenerPostenIds, CancellationToken ct = default);
    Task<MahnungDto> LadeMahnungAsync(int id, CancellationToken ct = default);
}
