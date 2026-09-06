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
/// durch dieselbe Negativsperre wie ein Lieferschein.
///
/// Datum und Bemerkung aus den DTOs werden durchgereicht, nicht verworfen: das erfasste Datum wird zum
/// fachlichen Zeitpunkt der Ledger-Zeile (ein nachgetragener Zugang von vorgestern erscheint in der
/// Historie unter vorgestern), die Bemerkung landet in Lagerbewegung.Bemerkung. Bei einem Stufenwechsel
/// bzw. Umsetzen tragen BEIDE Zeilen — Abgang und Zugang — denselben Zeitpunkt und dieselbe Bemerkung;
/// sie beschreiben denselben Vorgang.</summary>
public sealed class KulturBuchungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IKulturBuchungService
{
    private static readonly KulturZugangValidator ZugangValidator = new();
    private static readonly StufenwechselValidator StufenwechselValidator = new();
    private static readonly UmsetzenValidator UmsetzenValidator = new();
    private static readonly AusfallValidator AusfallValidator = new();

    /// <summary>Fachlicher Zeitpunkt einer Kulturbuchung: das vom Benutzer erfasste Datum, kombiniert mit der
    /// aktuellen Uhrzeit. Die Uhrzeit hält mehrere Buchungen desselben Tages in der Reihenfolge, in der sie
    /// entstanden sind — ohne sie stünden alle auf 00:00 und die Historie (OrderByDescending(Zeitpunkt))
    /// würde sie willkürlich sortieren. Lokale Zeit, s. Lagerbewegung.Zeitpunkt.</summary>
    private static DateTime ZeitpunktAus(DateOnly datum) => datum.ToDateTime(TimeOnly.FromDateTime(DateTime.Now));

    public async Task ZugangAsync(KulturZugangDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await ZugangValidator.ValidateAndThrowAsync(dto, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.FeldId, dto.Menge, LagerbewegungTyp.Kulturzugang, null, ct,
            dto.SektionId, dto.KulturstufeId, ZeitpunktAus(dto.Datum), dto.Bemerkung);
        await transaction.CommitAsync(ct);
    }

    public async Task StufenwechselAsync(StufenwechselDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await StufenwechselValidator.ValidateAndThrowAsync(dto, ct);
        KulturRegeln.PruefeStufenwechsel(
            dto.VonFeldId, dto.NachFeldId, dto.VonKulturstufeId, dto.NachKulturstufeId,
            dto.VonSektionId, dto.NachSektionId, dto.Menge);

        var zeitpunkt = ZeitpunktAus(dto.Datum);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Die Abgangsbuchung läuft zuerst durch die Negativsperre — schlägt sie fehl, rollt die ganze
        // Transaktion zurück und es entsteht kein Zugang (kein "halber" Stufenwechsel).
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.VonFeldId, -dto.Menge, LagerbewegungTyp.Stufenwechsel, null, ct,
            dto.VonSektionId, dto.VonKulturstufeId, zeitpunkt, dto.Bemerkung);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.NachFeldId, dto.Menge, LagerbewegungTyp.Stufenwechsel, null, ct,
            dto.NachSektionId, dto.NachKulturstufeId, zeitpunkt, dto.Bemerkung);
        await transaction.CommitAsync(ct);
    }

    public async Task UmsetzenAsync(UmsetzenDto dto, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Gaertnerei);
        await UmsetzenValidator.ValidateAndThrowAsync(dto, ct);
        // Stufe bleibt links wie rechts identisch — PruefeStufenwechsel greift hier über Feld UND Sektion
        // (verhindert "Umsetzen" auf denselben Ort als Nulloperation, lässt aber den Wechsel zwischen zwei
        // Feldern ohne Sektionen zu, bei dem beide SektionIds null sind).
        KulturRegeln.PruefeStufenwechsel(
            dto.VonFeldId, dto.NachFeldId, dto.KulturstufeId, dto.KulturstufeId,
            dto.VonSektionId, dto.NachSektionId, dto.Menge);

        var zeitpunkt = ZeitpunktAus(dto.Datum);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.VonFeldId, -dto.Menge, LagerbewegungTyp.Umsetzen, null, ct,
            dto.VonSektionId, dto.KulturstufeId, zeitpunkt, dto.Bemerkung);
        await BestandService.BucheBewegungAsync(
            db, dto.ArtikelId, dto.NachFeldId, dto.Menge, LagerbewegungTyp.Umsetzen, null, ct,
            dto.NachSektionId, dto.KulturstufeId, zeitpunkt, dto.Bemerkung);
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
            dto.SektionId, dto.KulturstufeId, ZeitpunktAus(dto.Datum), dto.Bemerkung);
        await transaction.CommitAsync(ct);
    }
}
