namespace Milet.Application.Admin;

public interface IFibuKonfigurationService
{
    Task<FibuKonfigurationDto> LadeAsync(CancellationToken ct = default);
    Task SpeichereAsync(FibuKonfigurationDto dto, CancellationToken ct = default);
}
