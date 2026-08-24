namespace Nexus.Domain.ValueObjects;

/// <summary>
/// Adress-Werttyp. Wird als EF Owned Type eingebettet (Kunde, Lieferant, später Beleg-Snapshots).
/// </summary>
public sealed class Adresse
{
    public string Name1 { get; set; } = string.Empty;

    public string? Name2 { get; set; }

    public string Strasse { get; set; } = string.Empty;

    public string Plz { get; set; } = string.Empty;

    public string Ort { get; set; } = string.Empty;

    public string Land { get; set; } = "DE";

    public Adresse Kopie() => (Adresse)MemberwiseClone();
}
