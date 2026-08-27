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

/// <summary>Wrapt IEmailService, protokolliert jeden Versandversuch (Erfolg wie Fehlschlag) in EmailVersand.
/// Wirft nie — Ergebnis immer im DTO, siehe EmailVersandDto.</summary>
public interface IEmailVersandService
{
    Task<EmailVersandDto> SendeBelegPdfAsync(int belegId, string empfaenger, string betreff, string text, CancellationToken ct = default);
    Task<EmailVersandDto> SendeMahnungPdfAsync(int mahnungId, string empfaenger, string betreff, string text, CancellationToken ct = default);
}

/// <summary>Erzeugt den DATEV-EXTF-Buchungsstapel aus gebuchten Rechnungen/Eingangsrechnungen und
/// Zahlungen eines Zeitraums (s. DatevExtfWriter in Milet.Domain für das Format selbst).</summary>
public interface IDatevExportService
{
    /// <summary>Zählt/summiert ohne zu markieren — beliebig oft wiederholbar.</summary>
    Task<DatevExportVorschauDto> VorschauAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);

    /// <summary>Erzeugt die CSV und markiert alle einbezogenen Belege/Zahlungen mit <c>ExportiertAm</c>
    /// in derselben Transaktion — ein zweiter Aufruf für denselben Zeitraum liefert dann 0 Zeilen
    /// (Doppelexport-Schutz).</summary>
    Task<DatevExportErgebnisDto> ExportierenAsync(DateOnly von, DateOnly bis, CancellationToken ct = default);
}
