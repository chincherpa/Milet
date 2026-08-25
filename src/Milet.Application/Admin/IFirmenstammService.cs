namespace Milet.Application.Admin;

public interface IFirmenstammService
{
    Task<FirmenstammDto> LadeAsync(CancellationToken ct = default);
    Task SpeichereAsync(FirmenstammDto dto, CancellationToken ct = default);
}
