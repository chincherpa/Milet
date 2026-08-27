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
