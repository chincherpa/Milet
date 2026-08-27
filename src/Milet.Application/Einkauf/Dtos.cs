namespace Milet.Application.Einkauf;

public sealed record LieferantEinkaufLookupDto(int Id, string Anzeige, int? ZahlungsbedingungId);

public sealed record ArtikelEinkaufLookupDto(
    int Id,
    string Anzeige,
    string Bezeichnung,
    decimal Einkaufspreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel,
    bool HatSeriennummern);

public sealed record EinkaufLookups(
    IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten,
    IReadOnlyList<ArtikelEinkaufLookupDto> Artikel);

/// <summary>Ein lagerfähiger, nicht gesperrter Artikel mit Mindestbestand, dessen Gesamtbestand
/// (über alle Lagerorte) den Mindestbestand unterschreitet.</summary>
public sealed record BestellVorschlagPositionDto(
    int ArtikelId,
    string Artikelnummer,
    string Bezeichnung,
    decimal AktuellerBestand,
    decimal Mindestbestand,
    decimal VorschlagsMenge,
    decimal Einkaufspreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel);

/// <summary>Ergebnis des Eingangsrechnung-Buchens: der Kreditor-OP wird IMMER angelegt (kein Blocker);
/// BetragWeichtAb ist eine reine Soft-Warnung für die UI (siehe Architektur-Entscheidung 7).</summary>
public sealed record EingangsrechnungBuchenErgebnisDto(
    Verkauf.BelegDto Beleg,
    bool BetragWeichtAb,
    decimal ErwarteterBetrag,
    decimal AbweichungBetrag);
