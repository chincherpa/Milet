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

    /// <summary>Sachkonto für GEWÄHRTE Skonti (Zahlungseingang eines Debitors) — Erlösschmälerung.
    /// 0 = nicht gepflegt, dann greift das Standardkonto des Kontenrahmens (SKR03 8736, SKR04 4736).
    /// Bewusst nach Buchungsrichtung benannt statt nach dem Kontenrahmen-Begriff: die Zuordnung zur
    /// Debitor-/Kreditor-Zahlung ist damit ohne Buchhaltungswissen eindeutig.</summary>
    public int SkontoKontoDebitorNr { get; set; }

    /// <summary>Sachkonto für ERHALTENE Skonti (Zahlungsausgang an einen Kreditor) — Aufwandsminderung.
    /// 0 = nicht gepflegt, dann greift das Standardkonto des Kontenrahmens (SKR03 3736, SKR04 5736).</summary>
    public int SkontoKontoKreditorNr { get; set; }
}
