namespace Milet.Application.Abstractions;

public interface IPdfService
{
    /// <summary>Rendert den Beleg (Angebot/Auftrag/Rechnung) als PDF anhand seines Typs.</summary>
    Task<byte[]> GeneriereBelegPdfAsync(int belegId, CancellationToken ct = default);
}
