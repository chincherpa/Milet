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

public sealed record MahnstufeDto(int Id, int Stufe, int Karenztage, decimal Gebuehr, string? Mahntext);

/// <summary>Ein OP, für den laut MahnSelektionService eine Mahnung fällig ist.</summary>
public sealed record MahnKandidatDto(
    int OffenerPostenId, int BelegId, string BelegNummer, decimal OffenerBetrag, DateOnly Faelligkeit,
    int AktuelleMahnstufe, int NaechsteMahnstufe);

public sealed record MahnlaufGruppeDto(int KundeId, string KundenName, IReadOnlyList<MahnKandidatDto> Kandidaten);

public sealed record MahnungPositionDto(int OffenerPostenId, string BelegNummerSnapshot, decimal OffenerBetragSnapshot);

public sealed record MahnungDto(
    int Id, int KundeId, string KundenName, DateOnly MahnDatum, int Mahnstufe, decimal Gebuehr,
    decimal Gesamtbetrag, IReadOnlyList<MahnungPositionDto> Positionen);
