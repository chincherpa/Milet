using Milet.Domain.Common;

namespace Milet.Domain.Entities.Stammdaten;

public class Artikel : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    public string Artikelnummer { get; set; } = string.Empty;

    public string Bezeichnung { get; set; } = string.Empty;

    public string? Beschreibung { get; set; }

    public int EinheitId { get; set; }
    public Einheit? Einheit { get; set; }

    public int MwStSatzId { get; set; }
    public MwStSatz? MwStSatz { get; set; }

    public decimal Einkaufspreis { get; set; }

    /// <summary>Netto-Listenverkaufspreis; Basis der Preisfindung.</summary>
    public decimal Listenpreis { get; set; }

    public decimal? Gewicht { get; set; }

    public string? Ean { get; set; }

    public bool IstLagerartikel { get; set; } = true;

    public bool HatSeriennummern { get; set; }

    public decimal? Mindestbestand { get; set; }

    public bool Gesperrt { get; set; }

    /// <summary>Kulturpflanze statt Handelsware — steuert, ob eine Bestandszeile eine `KulturstufeId` braucht (E1).</summary>
    public bool IstKulturpflanze { get; set; }
    public string? BotanischerName { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
