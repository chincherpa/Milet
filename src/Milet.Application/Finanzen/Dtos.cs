using Milet.Domain.Entities.Finanzen;

namespace Milet.Application.Finanzen;

public sealed record OffenePostenFilterDto(
    OffenerPostenTyp? Typ = null,
    OffenerPostenStatus? Status = null,
    bool NurUeberfaellige = false);

public sealed record OffenePostenDto(
    int Id,
    int BelegId,
    string BelegNummer,
    int? KundeId,
    int? LieferantId,
    string PartnerName,
    OffenerPostenTyp Typ,
    decimal Betrag,
    decimal OffenerBetrag,
    DateOnly Faelligkeit,
    int TageUeberfaellig,
    int Mahnstufe,
    bool Mahnsperre,
    OffenerPostenStatus Status,
    byte[] RowVersion);

public sealed record SkontoVorschlagDto(decimal SkontoBetrag, decimal ZuZahlenderBetrag);

public sealed record ZahlungZuordnungDto(int OffenerPostenId, decimal Betrag, decimal SkontoBetrag, byte[] RowVersion);

public sealed record ZahlungDto(
    int Id,
    int? KundeId,
    int? LieferantId,
    OffenerPostenTyp Typ,
    DateOnly Zahlungsdatum,
    string? Zahlungsart,
    string? Referenz,
    IReadOnlyList<ZahlungZuordnungDto> Zuordnungen);
