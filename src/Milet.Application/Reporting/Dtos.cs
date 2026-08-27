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
