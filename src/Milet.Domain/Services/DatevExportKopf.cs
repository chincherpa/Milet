namespace Milet.Domain.Services;

/// <summary>Kopfdaten für die DATEV-EXTF-Formatkennzeichenzeile — aus <c>FibuKonfiguration</c> +
/// dem gewählten Exportzeitraum zusammengestellt.</summary>
public sealed class DatevExportKopf
{
    public required int BeraterNr { get; init; }
    public required int MandantNr { get; init; }
    public required DateOnly WirtschaftsjahrBeginn { get; init; }
    public required int SachkontenLaenge { get; init; }
    public required DateOnly DatumVon { get; init; }
    public required DateOnly DatumBis { get; init; }
    public required string Bezeichnung { get; init; }
    public required DateTime ErzeugtAm { get; init; }
}
