using Milet.Domain.Entities.Lager;

namespace Milet.Application.Lager;

public sealed record LagerortDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
}

public sealed record ArtikelBestandDto(
    int ArtikelId,
    string Artikelnummer,
    string ArtikelBezeichnung,
    bool HatSeriennummern,
    int LagerortId,
    string LagerortBezeichnung,
    decimal Menge,
    decimal? Mindestbestand);

public sealed record BestandskorrekturDto
{
    public int ArtikelId { get; init; }
    public int LagerortId { get; init; }
    public decimal MengeDelta { get; init; }
    public string Grund { get; init; } = string.Empty;
}

public sealed record SeriennummerDto(int Id, int ArtikelId, string Nummer, SeriennummerStatus Status, int? LagerortId);

public sealed record InventurPositionDto(int Id, int ArtikelId, string Artikelnummer, string ArtikelBezeichnung, decimal SollMenge, decimal? IstMenge);

public sealed record InventurDto
{
    public int Id { get; init; }
    public int LagerortId { get; init; }
    public string LagerortBezeichnung { get; init; } = string.Empty;
    public DateOnly Datum { get; init; }
    public InventurStatus Status { get; init; } = InventurStatus.Offen;
    public IReadOnlyList<InventurPositionDto> Positionen { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}
