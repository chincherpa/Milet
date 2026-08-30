using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>Kulturbuchungen (E6/E7) — jede Methode ist eine eigene Transaktion über den einzigen
/// Schreibpfad BestandService.BucheBewegungAsync. Stufenwechsel/Umsetzen sind zwei Ledger-Zeilen
/// (Abgang + Zugang), nie ein Update — der Ledger bleibt append-only und die Abgangsbuchung läuft
/// durch dieselbe Negativsperre wie ein Lieferschein.</summary>
public sealed class KulturBuchungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IKulturBuchungService
{
    private static readonly KulturZugangValidator ZugangValidator = new();
    private static readonly StufenwechselValidator StufenwechselValidator = new();
    private static readonly UmsetzenValidator UmsetzenValidator = new();
    private static readonly AusfallValidator AusfallValidator = new();

    public async Task ZugangAsync(KulturZugangDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await ZugangValidator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.FeldId, dto.Menge, LagerbewegungTyp.Kulturzugang, null, ct,
            dto.SektionId, dto.KulturstufeId);
        await transaction.CommitAsync(ct);
    }

    public async Task StufenwechselAsync(StufenwechselDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await StufenwechselValidator.ValidateAndThrowAsync(dto, ct);
        KulturRegeln.PruefeStufenwechsel(dto.VonKulturstufeId, dto.NachKulturstufeId, dto.VonSektionId, dto.NachSektionId, dto.Menge);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Die Abgangsbuchung läuft zuerst durch die Negativsperre — schlägt sie fehl, rollt die ganze
        // Transaktion zurück und es entsteht kein Zugang (kein "halber" Stufenwechsel).
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.VonFeldId, -dto.Menge, LagerbewegungTyp.Stufenwechsel, null, ct,
            dto.VonSektionId, dto.VonKulturstufeId);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.NachFeldId, dto.Menge, LagerbewegungTyp.Stufenwechsel, null, ct,
            dto.NachSektionId, dto.NachKulturstufeId);
        await transaction.CommitAsync(ct);
    }

    public async Task UmsetzenAsync(UmsetzenDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await UmsetzenValidator.ValidateAndThrowAsync(dto, ct);
        // Stufe bleibt links wie rechts identisch — PruefeStufenwechsel greift hier nur über den
        // Sektionsvergleich (verhindert "Umsetzen" auf denselben Ort als Nulloperation).
        KulturRegeln.PruefeStufenwechsel(dto.KulturstufeId, dto.KulturstufeId, dto.VonSektionId, dto.NachSektionId, dto.Menge);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.VonFeldId, -dto.Menge, LagerbewegungTyp.Umsetzen, null, ct,
            dto.VonSektionId, dto.KulturstufeId);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.NachFeldId, dto.Menge, LagerbewegungTyp.Umsetzen, null, ct,
            dto.NachSektionId, dto.KulturstufeId);
        await transaction.CommitAsync(ct);
    }

    public async Task AusfallAsync(AusfallDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await AusfallValidator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.FeldId, -dto.Menge, LagerbewegungTyp.Ausfall, null, ct,
            dto.SektionId, dto.KulturstufeId);
        await transaction.CommitAsync(ct);
    }
}
