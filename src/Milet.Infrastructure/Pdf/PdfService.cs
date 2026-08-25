using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

public sealed class PdfService(IBelegService belegService, IFirmenstammService firmenstammService) : IPdfService
{
    static PdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<byte[]> GeneriereBelegPdfAsync(int belegId, CancellationToken ct = default)
    {
        var beleg = await belegService.LadeAsync(belegId, ct);
        var firma = await firmenstammService.LadeAsync(ct);
        var titel = beleg.BelegTyp switch
        {
            BelegTyp.Angebot => "Angebot",
            BelegTyp.Auftrag => "Auftragsbestätigung",
            BelegTyp.Rechnung => "Rechnung",
            _ => throw new ArgumentOutOfRangeException(nameof(belegId)),
        };
        return new BelegPdfDocument(beleg, firma, titel).GeneratePdf();
    }
}
