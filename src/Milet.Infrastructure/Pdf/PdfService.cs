using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Finanzen;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

public sealed class PdfService(
    IBelegService belegService, IFirmenstammService firmenstammService,
    IMahnwesenService mahnwesenService, IKundenService kundenService) : IPdfService
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
            _ => throw new InvalidOperationException($"PDF-Erzeugung für Belegtyp '{beleg.BelegTyp}' wird nicht unterstützt."),
        };
        return new BelegPdfDocument(beleg, firma, titel).GeneratePdf();
    }

    public async Task<byte[]> GeneriereMahnungPdfAsync(int mahnungId, CancellationToken ct = default)
    {
        var mahnung = await mahnwesenService.LadeMahnungAsync(mahnungId, ct);
        var kunde = await kundenService.LadeAsync(mahnung.KundeId, ct);
        var firma = await firmenstammService.LadeAsync(ct);
        var titel = mahnung.Mahnstufe switch
        {
            1 => "Zahlungserinnerung",
            2 => "1. Mahnung",
            _ => $"{mahnung.Mahnstufe - 1}. Mahnung",
        };
        return new MahnungPdfDocument(mahnung, kunde, firma, titel).GeneratePdf();
    }
}
