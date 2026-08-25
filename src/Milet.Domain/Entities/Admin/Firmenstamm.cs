using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Admin;

/// <summary>Genau ein Datensatz (Id = 1) — Firmendaten für Briefkopf/PDF-Ausgabe.</summary>
public class Firmenstamm
{
    public int Id { get; set; }
    public string Firmenname { get; set; } = string.Empty;
    public Adresse Adresse { get; set; } = new();
    public string? UStIdNr { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Iban { get; set; }
    public string? Bic { get; set; }
}
