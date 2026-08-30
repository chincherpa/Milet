namespace Milet.Application.Reporting;

public sealed record UmsatzJeKundeDto(
    int KundeId, string KundeNummer, string KundeName, int AnzahlRechnungen, decimal SummeNetto, decimal SummeBrutto);

public sealed record UmsatzJeArtikelDto(
    int ArtikelId, string ArtikelNummer, string Bezeichnung, decimal Menge, decimal SummeNetto);

public sealed record UmsatzJeMonatDto(int Jahr, int Monat, decimal SummeNetto, decimal SummeBrutto);

public sealed record ArtikelbewegungDto(
    DateTime Zeitpunkt, string ArtikelNummer, string ArtikelBezeichnung, string LagerortCode,
    decimal Menge, string Typ, string? BelegNummer);

public sealed record TopArtikelDto(
    int ArtikelId, string ArtikelNummer, string Bezeichnung, decimal Menge, decimal SummeNetto);

public sealed record OffenerAuftragDto(
    int BelegId, string BelegNummer, DateOnly BelegDatum, string KundeName, decimal SummeBrutto, decimal OffeneMenge);

/// <summary>Menge je Pflanze × Stufe × Feld × Sektion (Phase 8) — eine Zeile je existierender Bestandszeile.</summary>
public sealed record KulturbestandZeileDto(
    int ArtikelId, string Artikelnummer, string Bezeichnung, string? BotanischerName,
    int FeldId, string FeldBezeichnung, int SektionId, string SektionBezeichnung,
    int KulturstufeId, string KulturstufeBezeichnung, decimal Menge);

/// <summary>Ausfallquote je Pflanze und Stufe im Zeitraum — die betriebswirtschaftlich interessanteste Zahl
/// der Phase (steuert, wie viel man ansetzen muss, um eine Verkaufsmenge zu erreichen).</summary>
public sealed record AusfallquoteZeileDto(
    int ArtikelId, string Artikelnummer, string Bezeichnung,
    int KulturstufeId, string KulturstufeBezeichnung,
    decimal SummeZugaenge, decimal SummeAusfall, decimal AusfallquoteProzent);

/// <summary>Belegte Fläche je Feld (Summe der Sektionsflächen mit Bestand) gegen Gesamtfläche.</summary>
public sealed record FlaechenbelegungZeileDto(
    int FeldId, string FeldBezeichnung, decimal GesamtflaecheQm, decimal BelegteFlaecheQm, decimal BelegungsProzent);
