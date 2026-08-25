namespace Milet.Application.Lager;

public interface ILagerortService
{
    Task<IReadOnlyList<LagerortDto>> SucheAsync(string? suchtext, CancellationToken ct = default);
    Task<LagerortDto> SpeichereAsync(LagerortDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IBestandService
{
    Task<IReadOnlyList<ArtikelBestandDto>> SucheAsync(string? suchtext, CancellationToken ct = default);

    /// <summary>Bucht eine manuelle Korrektur (z. B. Erstbestückung, Schwund) — atomar, wirft bei negativem Ergebnisbestand.</summary>
    Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default);
}

public interface ISeriennummernService
{
    Task<IReadOnlyList<SeriennummerDto>> SucheAsync(int? artikelId, CancellationToken ct = default);
    Task<IReadOnlyList<SeriennummerDto>> AufLagerAsync(int artikelId, CancellationToken ct = default);

    /// <summary>Manuelle Neuerfassung (z. B. Erstbestückung serialisierter Artikel) — bucht implizit +1 Bestand am angegebenen Lagerort.</summary>
    Task ErfasseAsync(int artikelId, int lagerortId, string nummer, CancellationToken ct = default);
}

public interface IInventurService
{
    Task<IReadOnlyList<InventurDto>> SucheAsync(CancellationToken ct = default);
    Task<InventurDto> LadeAsync(int id, CancellationToken ct = default);

    /// <summary>Legt eine neue Inventur an und friert SollMenge je lagerfähigem Artikel aus dem aktuellen ArtikelBestand ein.</summary>
    Task<InventurDto> NeueInventurAsync(int lagerortId, CancellationToken ct = default);

    Task ErfasseIstMengeAsync(int inventurPositionId, decimal istMenge, CancellationToken ct = default);

    /// <summary>Bucht für jede Position mit Ist≠Soll eine Korrekturbuchung (InventurKorrektur) und setzt Status Abgeschlossen — eine Transaktion.</summary>
    Task<InventurDto> AbschliessenAsync(int inventurId, CancellationToken ct = default);
}
