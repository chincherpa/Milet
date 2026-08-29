using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Finanzen;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class EmailVersandService(
    IDbContextFactory<MiletDbContext> dbContextFactory, IPdfService pdfService, IEmailService emailService,
    IBelegService belegService, IMahnwesenService mahnwesenService,
    IBerechtigungsService berechtigung) : IEmailVersandService
{
    public async Task<EmailVersandDto> SendeBelegPdfAsync(int belegId, string empfaenger, string betreff, string text, CancellationToken ct = default)
    {
        var beleg = await belegService.LadeAsync(belegId, ct);
        // Der Versand gibt einen Beleg an eine frei wählbare Adresse heraus — dafür gilt dasselbe Recht
        // wie für den Beleg selbst (Verkaufsbeleg: Verkauf, Lieferschein: Lager, Einkaufsbeleg: Einkauf).
        berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(beleg.BelegTyp));
        var pdf = await pdfService.GeneriereBelegPdfAsync(belegId, ct);
        return await VersendenUndProtokollierenAsync(empfaenger, betreff, text, pdf, $"{beleg.BelegNummer}.pdf", belegId: belegId, mahnungId: null, ct);
    }

    public async Task<EmailVersandDto> SendeMahnungPdfAsync(int mahnungId, string empfaenger, string betreff, string text, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Finanzen);
        var mahnung = await mahnwesenService.LadeMahnungAsync(mahnungId, ct);
        var pdf = await pdfService.GeneriereMahnungPdfAsync(mahnungId, ct);
        return await VersendenUndProtokollierenAsync(empfaenger, betreff, text, pdf, $"Mahnung-{mahnung.Id}.pdf", belegId: null, mahnungId: mahnungId, ct);
    }

    private async Task<EmailVersandDto> VersendenUndProtokollierenAsync(
        string empfaenger, string betreff, string text, byte[] anhang, string anhangDateiname,
        int? belegId, int? mahnungId, CancellationToken ct)
    {
        bool erfolgreich;
        string? fehlermeldung = null;
        try
        {
            await emailService.SendeMailMitAnhangAsync(empfaenger, betreff, text, anhang, anhangDateiname, ct);
            erfolgreich = true;
        }
        catch (Exception ex)
        {
            erfolgreich = false;
            fehlermeldung = ex.Message;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        db.EmailVersand.Add(new EmailVersand
        {
            BelegId = belegId,
            MahnungId = mahnungId,
            Empfaenger = empfaenger,
            Betreff = betreff,
            GesendetAm = DateTime.UtcNow,
            Erfolgreich = erfolgreich,
            Fehlermeldung = fehlermeldung,
        });
        await db.SaveChangesAsync(ct);

        return new EmailVersandDto(erfolgreich, fehlermeldung);
    }
}
