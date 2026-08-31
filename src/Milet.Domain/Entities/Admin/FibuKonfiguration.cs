namespace Milet.Domain.Entities.Admin;

/// <summary>Genau ein Datensatz (Id = 1) — Konfiguration für den DATEV-EXTF-Export
/// (Buchungsstapel), analog <see cref="Firmenstamm"/>.</summary>
public class FibuKonfiguration
{
    public int Id { get; set; }

    public Kontenrahmen Kontenrahmen { get; set; } = Kontenrahmen.Skr03;

    public int BeraterNr { get; set; }
    public int MandantNr { get; set; }

    /// <summary>Monat 1–12, in dem das Wirtschaftsjahr beginnt (i. d. R. 1 = Kalenderjahr).</summary>
    public int WirtschaftsjahrBeginnMonat { get; set; } = 1;

    /// <summary>Länge der Sachkonten im Kontenrahmen (SKR03/04 typisch 4, teils 5).</summary>
    public int SachkontenLaenge { get; set; } = 4;

    /// <summary>Sachkonto für Zahlungseingänge/-ausgänge (Bank/Kasse), Gegenkonto beim Zahlungs-Export.</summary>
    public int BankkontoNr { get; set; }

    /// <summary>Sammelkonto für gewährtes Skonto (Debitor). NULL = Standardkonto des Kontenrahmens
    /// (SKR03 8736, SKR04 4736) wird verwendet, s. DatevExportService.</summary>
    public int? SkontoDebitorKontoNr { get; set; }

    /// <summary>Sammelkonto für erhaltenes Skonto (Kreditor). NULL = Standardkonto des Kontenrahmens
    /// (SKR03 3736, SKR04 5736) wird verwendet, s. DatevExportService.</summary>
    public int? SkontoKreditorKontoNr { get; set; }
}
