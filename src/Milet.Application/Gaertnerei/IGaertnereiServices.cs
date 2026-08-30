namespace Milet.Application.Gaertnerei;

public interface IKulturstufenService
{
    Task<IReadOnlyList<KulturstufeDto>> ListeAsync(CancellationToken ct = default);
    Task<KulturstufeDto> SpeichereAsync(KulturstufeDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IGaertnereiplanService
{
    /// <summary>Plan + alle Felder (Lagerorte mit IstFeld) + deren Sektionen — die Datenquelle für Grundriss-Editor UND Pflanzenübersicht.</summary>
    Task<GaertnereiplanDto?> LadePlanAsync(CancellationToken ct = default);

    Task<GaertnereiplanDto> SpeicherePlanAsync(GaertnereiplanDto dto, CancellationToken ct = default);

    /// <summary>Legt bei Id == 0 einen neuen Lagerort mit IstFeld = true an (inkl. Code-Vergabe), sonst Update.</summary>
    Task<FeldDto> SpeichereFeldAsync(int gaertnereiplanId, FeldDto dto, CancellationToken ct = default);

    Task LoescheFeldAsync(int feldId, CancellationToken ct = default);

    /// <summary>Prüft Geometrie (LiegtInnerhalb = Fehler, Ueberlappt = Warnung im Ergebnis, kein Abbruch).</summary>
    Task<SektionSpeichernErgebnisDto> SpeichereSektionAsync(SektionDto dto, CancellationToken ct = default);

    Task LoescheSektionAsync(int sektionId, CancellationToken ct = default);
}

public interface IKulturBuchungService
{
    Task ZugangAsync(KulturZugangDto dto, CancellationToken ct = default);
    Task StufenwechselAsync(StufenwechselDto dto, CancellationToken ct = default);
    Task UmsetzenAsync(UmsetzenDto dto, CancellationToken ct = default);
    Task AusfallAsync(AusfallDto dto, CancellationToken ct = default);
}

public interface IKulturBestandService
{
    /// <summary>Alle Kulturpflanzen (IstKulturpflanze && !Gesperrt), auch ohne aktuellen Bestand (Menge 0).</summary>
    Task<IReadOnlyList<PflanzeUebersichtDto>> LadePflanzenAsync(string? suchtext, CancellationToken ct = default);

    /// <summary>Alle Fundstellen einer Pflanze, sortiert nach Stufe-Reihenfolge, dann Feld, dann Sektion.</summary>
    Task<IReadOnlyList<PflanzenVorkommenDto>> LadeVorkommenAsync(int artikelId, CancellationToken ct = default);

    Task<IReadOnlyList<KulturHistorieZeileDto>> LadeHistorieAsync(int artikelId, int? sektionId, DateOnly? von, DateOnly? bis, CancellationToken ct = default);
}

public interface IVerfuegbarkeitService
{
    Task<VerfuegbarkeitDto> LadeAsync(int artikelId, decimal? benoetigteMenge, CancellationToken ct = default);
    Task<BelegVerfuegbarkeitDto> LadeFuerBelegAsync(int belegId, CancellationToken ct = default);
}
