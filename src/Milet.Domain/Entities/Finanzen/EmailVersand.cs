namespace Milet.Domain.Entities.Finanzen;

/// <summary>Protokoll jedes Versandversuchs (Erfolg wie Fehlschlag) — insert-only, kein RowVersion nötig.
/// Genau eines von BelegId/MahnungId ist gesetzt (DB-CHECK-Constraint, analog Beleg.KundeId/LieferantId).</summary>
public class EmailVersand
{
    public int Id { get; set; }

    public int? BelegId { get; set; }
    public Entities.Verkauf.Beleg? Beleg { get; set; }

    public int? MahnungId { get; set; }
    public Mahnung? Mahnung { get; set; }

    public string Empfaenger { get; set; } = string.Empty;
    public string Betreff { get; set; } = string.Empty;
    public DateTime GesendetAm { get; set; }
    public bool Erfolgreich { get; set; }
    public string? Fehlermeldung { get; set; }
}
