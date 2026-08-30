using Milet.Application.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Application.Verkauf;

public sealed record BelegPositionDto
{
    public int Id { get; init; }
    public int PositionsNr { get; init; }
    public PositionsTyp PositionsTyp { get; init; } = PositionsTyp.Artikel;
    public int? ArtikelId { get; init; }
    public string Bezeichnung { get; init; } = string.Empty;
    public string? EinheitKuerzel { get; init; }
    public decimal Menge { get; init; }
    public decimal Einzelpreis { get; init; }
    public decimal RabattProzent { get; init; }
    public int? MwStSatzId { get; init; }
    public decimal MwStSatzWert { get; init; }
    public int? SteuerSchluessel { get; init; }
    public int? LagerortId { get; init; }

    /// <summary>Nur Lieferschein/Wareneingang — bestimmt, gegen welche Bestandszeile gebucht wird (Phase 8, E9).</summary>
    public int? SektionId { get; init; }
    public int? KulturstufeId { get; init; }

    public decimal GesamtNetto { get; init; }
    public int? UrsprungsPositionId { get; init; }
}

/// <summary>Sektion/Kulturstufe für eine einzelne Zielposition bei UeberleitenMitAuswahlAsync (Phase 8, E9).</summary>
public sealed record BelegPositionDimensionenDto(int? SektionId, int? KulturstufeId);

public sealed record BelegDto
{
    public int Id { get; init; }
    public BelegTyp BelegTyp { get; init; }
    public string BelegNummer { get; init; } = string.Empty;
    public DateOnly BelegDatum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public int KundeId { get; init; }
    public string KundeAnzeige { get; init; } = string.Empty;
    public int? LieferantId { get; init; }
    public string LieferantAnzeige { get; init; } = string.Empty;
    public AdresseDto RechnungsadresseSnapshot { get; init; } = new();
    public AdresseDto LieferadresseSnapshot { get; init; } = new();
    public int ZahlungsbedingungZielTage { get; init; }
    public int? ZahlungsbedingungSkontoTage { get; init; }
    public decimal? ZahlungsbedingungSkontoProzent { get; init; }
    public BelegStatus Status { get; init; } = BelegStatus.Entwurf;
    public decimal SummeNetto { get; init; }
    public decimal SummeMwSt { get; init; }
    public decimal SummeBrutto { get; init; }
    public DateOnly? Faelligkeit { get; init; }
    public DateOnly? Leistungsdatum { get; init; }
    public string? Kopftext { get; init; }
    public string? Fusstext { get; init; }
    public string? ExterneReferenz { get; init; }
    public IReadOnlyList<BelegPositionDto> Positionen { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>Reicheres Lookup als das generische <see cref="LookupDto"/> — trägt Defaultwerte für neue Belegpositionen.</summary>
public sealed record ArtikelVerkaufLookupDto(
    int Id,
    string Anzeige,
    /// <summary>Reine Artikelbezeichnung ohne Artikelnummer-Präfix — für Belegpositionen/Druck (im Gegensatz zu <see cref="Anzeige"/>, das für ComboBoxen gedacht ist).</summary>
    string Bezeichnung,
    decimal Listenpreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel,
    bool HatSeriennummern);

public sealed record KundeVerkaufLookupDto(
    int Id,
    string Anzeige,
    int? ZahlungsbedingungId,
    int? PreislisteId,
    decimal RabattProzent);

public sealed record VerkaufLookups(
    IReadOnlyList<KundeVerkaufLookupDto> Kunden,
    IReadOnlyList<ArtikelVerkaufLookupDto> Artikel,
    IReadOnlyList<LookupDto> Zahlungsbedingungen);

public sealed record PreisErgebnisDto(decimal Einzelpreis, decimal RabattProzent);

/// <summary>Offene (noch nicht überführte) Menge einer Quellposition — Grundlage für den Teillieferungs-Dialog.</summary>
public sealed record OffenePositionDto(int PositionId, string Bezeichnung, string? EinheitKuerzel, decimal OffeneMenge);
