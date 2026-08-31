namespace Milet.Domain.Entities.Verkauf;

/// <summary>Dünner TPH-Subtyp wie <see cref="Rechnung"/>. Entsteht heute ausschließlich als automatische
/// Storno-Gutschrift (s. StornoService, <see cref="Beleg.StorniertenBelegId"/> gesetzt) — eine fachliche
/// Gutschrift ohne Storno-Bezug (Retoure/Kulanz) ist in der Datenbank vorgesehen (Feld bleibt dann NULL),
/// aber noch nicht über einen eigenen Erfassungsweg erzeugbar.</summary>
public sealed class Gutschrift : Beleg;
