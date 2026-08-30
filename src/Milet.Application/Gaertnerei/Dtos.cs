namespace Milet.Application.Gaertnerei;

public sealed record KulturstufeDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public int Reihenfolge { get; init; }
    public bool IstVerkaufsfaehig { get; init; }
    public string FarbeHex { get; init; } = "#4CAF50";
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
}

public sealed record SektionDto
{
    public int Id { get; init; }
    public int LagerortId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public decimal PosXMeter { get; init; }
    public decimal PosYMeter { get; init; }
    public decimal BreiteMeter { get; init; }
    public decimal HoeheMeter { get; init; }
    public decimal FlaecheQm => BreiteMeter * HoeheMeter;
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>Ein Feld ist ein Lagerort mit Geometrie (E2) — dieses DTO trägt zusätzlich seine Sektionen,
/// weil Grundriss-Editor und Pflanzenübersicht immer beides zusammen brauchen.</summary>
public sealed record FeldDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public decimal PosXMeter { get; init; }
    public decimal PosYMeter { get; init; }
    public decimal BreiteMeter { get; init; }
    public decimal HoeheMeter { get; init; }
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
    public IReadOnlyList<SektionDto> Sektionen { get; init; } = [];
}

public sealed record GaertnereiplanDto
{
    public int Id { get; init; }
    public string Bezeichnung { get; init; } = string.Empty;
    public decimal BreiteMeter { get; init; }
    public decimal HoeheMeter { get; init; }
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
    public IReadOnlyList<FeldDto> Felder { get; init; } = [];
}

/// <summary>Ergebnis eines Sektion-Speicherns — Überlappung ist bewusst eine Warnung, kein Abbruch (E11).</summary>
public sealed record SektionSpeichernErgebnisDto(SektionDto Sektion, IReadOnlyList<string> Warnungen);

public sealed record MengeJeStufeDto(int KulturstufeId, string StufeBezeichnung, string FarbeHex, decimal Menge);

/// <summary>Eine Zeile der Pflanzenübersicht (links) — auch Kulturpflanzen ohne aktuellen Bestand erscheinen mit Menge 0.</summary>
public sealed record PflanzeUebersichtDto(
    int ArtikelId,
    string Artikelnummer,
    string Bezeichnung,
    string? BotanischerName,
    decimal GesamtMenge,
    IReadOnlyList<MengeJeStufeDto> JeStufe);

/// <summary>Eine Fundstelle einer Pflanze — Basis für das Highlighting im Grundriss.</summary>
public sealed record PflanzenVorkommenDto(
    int FeldId,
    string FeldBezeichnung,
    int SektionId,
    string SektionBezeichnung,
    int KulturstufeId,
    string StufeBezeichnung,
    string FarbeHex,
    decimal Menge);

public enum VerfuegbarkeitAmpel
{
    /// <summary>Verkaufsfähig frei verfügbar ≥ benötigte Menge.</summary>
    Gruen,

    /// <summary>Verkaufsfähiger Bestand reicht nicht, aber es steht etwas in einer nicht-verkaufsfähigen Stufe.</summary>
    Gelb,

    /// <summary>Gar kein Bestand — weder verkaufsfähig noch in Anzucht.</summary>
    Rot,
}

/// <summary>E8: beratend, nicht sperrend. Reserviert = Summe offener Auftragsmengen dieses Artikels (berechnet, nicht gespeichert).</summary>
public sealed record VerfuegbarkeitDto(
    int ArtikelId,
    decimal VerkaufsfaehigGesamt,
    decimal Reserviert,
    decimal Frei,
    VerfuegbarkeitAmpel Ampel,
    IReadOnlyList<PflanzenVorkommenDto> Fundstellen,
    IReadOnlyList<MengeJeStufeDto> NichtVerkaufsfaehig);

/// <summary>Ampel je Position eines Belegs (Auftrag/Angebot) plus eine Gesamtampel (die schlechteste Einzelampel).</summary>
public sealed record BelegVerfuegbarkeitDto(int BelegId, VerfuegbarkeitAmpel GesamtAmpel, IReadOnlyList<VerfuegbarkeitDto> JePosition);

public sealed record KulturHistorieZeileDto(
    DateTime Zeitpunkt,
    string Typ,
    decimal Menge,
    string? FeldBezeichnung,
    string? SektionBezeichnung,
    string? StufeBezeichnung,
    string? BelegNummer);

public sealed record KulturZugangDto
{
    public int ArtikelId { get; init; }
    public int FeldId { get; init; }
    public int? SektionId { get; init; }
    public int KulturstufeId { get; init; }
    public decimal Menge { get; init; }
    public DateOnly Datum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Bemerkung { get; init; }
}

public sealed record StufenwechselDto
{
    public int ArtikelId { get; init; }
    public int VonFeldId { get; init; }
    public int? VonSektionId { get; init; }
    public int VonKulturstufeId { get; init; }
    public int NachFeldId { get; init; }
    public int? NachSektionId { get; init; }
    public int NachKulturstufeId { get; init; }
    public decimal Menge { get; init; }
    public DateOnly Datum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Bemerkung { get; init; }
}

/// <summary>Reiner Ortswechsel — Stufe bleibt gleich (im Unterschied zu <see cref="StufenwechselDto"/>).</summary>
public sealed record UmsetzenDto
{
    public int ArtikelId { get; init; }
    public int VonFeldId { get; init; }
    public int? VonSektionId { get; init; }
    public int NachFeldId { get; init; }
    public int? NachSektionId { get; init; }
    public int KulturstufeId { get; init; }
    public decimal Menge { get; init; }
    public DateOnly Datum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Bemerkung { get; init; }
}

public sealed record AusfallDto
{
    public int ArtikelId { get; init; }
    public int FeldId { get; init; }
    public int? SektionId { get; init; }
    public int KulturstufeId { get; init; }
    public decimal Menge { get; init; }
    public DateOnly Datum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Bemerkung { get; init; }
}
