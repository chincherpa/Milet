namespace Milet.Application.Stammdaten;

public sealed record AdresseDto
{
    public string Name1 { get; init; } = string.Empty;
    public string? Name2 { get; init; }
    public string Strasse { get; init; } = string.Empty;
    public string Plz { get; init; } = string.Empty;
    public string Ort { get; init; } = string.Empty;
    public string Land { get; init; } = "DE";
}

/// <summary>Einfacher Lookup-Eintrag für ComboBoxen.</summary>
public sealed record LookupDto(int Id, string Anzeige);

public sealed record KundeDto
{
    public int Id { get; init; }
    public string Kundennummer { get; init; } = string.Empty;
    public AdresseDto Adresse { get; init; } = new();
    public string? Ansprechpartner { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? EmailRechnung { get; init; }
    public string? UStIdNr { get; init; }
    public int? ZahlungsbedingungId { get; init; }
    public int? PreislisteId { get; init; }
    public decimal RabattProzent { get; init; }
    public decimal? Kreditlimit { get; init; }
    public bool Liefersperre { get; init; }
    public int? DebitorenkontoNr { get; init; }
    public string? Notiz { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record LieferantDto
{
    public int Id { get; init; }
    public string Lieferantennummer { get; init; } = string.Empty;
    public AdresseDto Adresse { get; init; } = new();
    public string? Ansprechpartner { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? UStIdNr { get; init; }
    public int? ZahlungsbedingungId { get; init; }
    public int? KreditorenkontoNr { get; init; }
    public string? Notiz { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record ArtikelDto
{
    public int Id { get; init; }
    public string Artikelnummer { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public string? Beschreibung { get; init; }
    public int EinheitId { get; init; }
    public string? EinheitKuerzel { get; init; }
    public int MwStSatzId { get; init; }
    public decimal Einkaufspreis { get; init; }
    public decimal Listenpreis { get; init; }
    public decimal? Gewicht { get; init; }
    public string? Ean { get; init; }
    public bool IstLagerartikel { get; init; } = true;
    public bool HatSeriennummern { get; init; }
    public decimal? Mindestbestand { get; init; }
    public bool Gesperrt { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record StammdatenLookups(
    IReadOnlyList<LookupDto> Einheiten,
    IReadOnlyList<LookupDto> MwStSaetze,
    IReadOnlyList<LookupDto> Zahlungsbedingungen,
    IReadOnlyList<LookupDto> Versandarten,
    IReadOnlyList<LookupDto> Preislisten);
