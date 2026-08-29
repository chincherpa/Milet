using System.Text;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>Baut den DATEV-EXTF-Buchungsstapel aus gebuchten Rechnungen/Eingangsrechnungen und
/// Zahlungen eines Zeitraums (s. Klassenkommentar DatevExtfWriter für den Scope-Vorbehalt zum Format
/// selbst). Bewusste Vereinfachung: Belege ohne gepflegtes Debitoren-/Kreditorenkonto beim Kunden/
/// Lieferanten bzw. Steuersätze ohne gepflegtes Erlös-/Aufwandskonto erzeugen keine Buchungszeile und
/// werden NICHT als exportiert markiert — sie erscheinen bei der nächsten Vorschau/Export wieder,
/// sobald die Kontenzuordnung nachgepflegt ist (kein stiller Datenverlust).
///
/// Aus demselben Grund markiert <see cref="ExportierenAsync"/> nichts: die Markierung ist ein eigener
/// Schritt (<see cref="MarkiereAlsExportiertAsync"/>), den der Aufrufer erst ausführt, wenn die Datei
/// wirklich geschrieben ist. Vorher committete die Markierung, bevor die CSV überhaupt erzeugt war —
/// scheiterte danach das Schreiben (volle Platte, Netzlaufwerk weg), galten die Belege als exportiert
/// und tauchten nie wieder auf.</summary>
public sealed class DatevExportService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IDatevExportService
{
    static DatevExportService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public async Task<DatevExportVorschauDto> VorschauAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, bankkonto, kontenrahmen) = await LadeAsync(db, von, bis, ct);

        var zeilen = new List<DatevBuchungszeile>();
        BuildeRechnungszeilen(rechnungen, mwStKonten, zeilen);
        BuildeEingangsrechnungszeilen(eingangsrechnungen, mwStKonten, zeilen);
        BuildeZahlungszeilen(zahlungen, bankkonto, kontenrahmen, zeilen);

        var summeUmsatz = zeilen.Sum(z => z.Umsatz);
        return new DatevExportVorschauDto(rechnungen.Count, eingangsrechnungen.Count, zahlungen.Count, zeilen.Count, summeUmsatz);
    }

    public async Task<DatevExportErgebnisDto> ExportierenAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Finanzen);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, bankkonto, kontenrahmen) = await LadeAsync(db, von, bis, ct);

        var zeilen = new List<DatevBuchungszeile>();
        var exportierteBelegIds = new List<int>();
        var exportierteZahlungIds = new List<int>();

        foreach (var rechnung in rechnungen)
        {
            var vorher = zeilen.Count;
            BuildeRechnungszeilen([rechnung], mwStKonten, zeilen);
            if (zeilen.Count > vorher) exportierteBelegIds.Add(rechnung.Id);
        }

        foreach (var eingangsrechnung in eingangsrechnungen)
        {
            var vorher = zeilen.Count;
            BuildeEingangsrechnungszeilen([eingangsrechnung], mwStKonten, zeilen);
            if (zeilen.Count > vorher) exportierteBelegIds.Add(eingangsrechnung.Id);
        }

        foreach (var zahlung in zahlungen)
        {
            var vorher = zeilen.Count;
            BuildeZahlungszeilen([zahlung], bankkonto, kontenrahmen, zeilen);
            if (zeilen.Count > vorher) exportierteZahlungIds.Add(zahlung.Id);
        }

        var konfiguration = await db.FibuKonfiguration.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);
        var kopf = new DatevExportKopf
        {
            BeraterNr = konfiguration?.BeraterNr ?? 0,
            MandantNr = konfiguration?.MandantNr ?? 0,
            WirtschaftsjahrBeginn = BerechneWirtschaftsjahrBeginn(von, konfiguration?.WirtschaftsjahrBeginnMonat ?? 1),
            SachkontenLaenge = konfiguration?.SachkontenLaenge ?? 4,
            DatumVon = von,
            DatumBis = bis,
            Bezeichnung = $"Milet Buchungsstapel {von:dd.MM.yyyy}-{bis:dd.MM.yyyy}",
            ErzeugtAm = DateTime.UtcNow,
        };
        var csv = DatevExtfWriter.Schreiben(kopf, zeilen);
        var bytes = Encoding.GetEncoding(1252).GetBytes(csv);
        var dateiname = $"EXTF_Buchungsstapel_{von:yyyyMMdd}_{bis:yyyyMMdd}.csv";

        return new DatevExportErgebnisDto(bytes, dateiname, zeilen.Count, exportierteBelegIds, exportierteZahlungIds);
    }

    /// <summary>
    /// Beginn des Wirtschaftsjahres, in dem der Exportzeitraum liegt. Bei einem abweichenden
    /// Wirtschaftsjahr (z. B. ab Juli) und einem Export für März 2027 ist das der 01.07.2026 — schlicht
    /// <c>von.Year</c> zu nehmen ergäbe den 01.07.2027 und damit ein Datum NACH dem Exportzeitraum.
    /// </summary>
    private static DateOnly BerechneWirtschaftsjahrBeginn(DateOnly von, int beginnMonat)
    {
        var jahr = von.Month < beginnMonat ? von.Year - 1 : von.Year;
        return new DateOnly(jahr, beginnMonat, 1);
    }

    public async Task MarkiereAlsExportiertAsync(
        IReadOnlyList<int> belegIds, IReadOnlyList<int> zahlungIds, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Finanzen);
        if (belegIds.Count == 0 && zahlungIds.Count == 0) return;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        var jetzt = DateTime.UtcNow;
        // ExportiertAm == null in der Bedingung: hat zwischenzeitlich ein anderer Export denselben Vorgang
        // festgeschrieben, bleibt dessen Zeitstempel stehen.
        if (belegIds.Count > 0)
        {
            await db.Belege.Where(b => belegIds.Contains(b.Id) && b.ExportiertAm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExportiertAm, jetzt), ct);
        }
        if (zahlungIds.Count > 0)
        {
            await db.Zahlungen.Where(z => zahlungIds.Contains(z.Id) && z.ExportiertAm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(z => z.ExportiertAm, jetzt), ct);
        }

        await transaktion.CommitAsync(ct);
    }

    private static async Task<(
        List<Rechnung> Rechnungen, List<Eingangsrechnung> Eingangsrechnungen, List<Zahlung> Zahlungen,
        Dictionary<int, (int? Erloes, int? Aufwand)> MwStKonten, int Bankkonto, Kontenrahmen Kontenrahmen)>
        LadeAsync(MiletDbContext db, DateOnly von, DateOnly bis, CancellationToken ct)
    {
        var rechnungen = await db.Rechnungen.AsNoTracking()
            .Include(b => b.Steuersummen)
            .Include(b => b.Kunde)
            .Where(b => b.Status == BelegStatus.Gebucht && b.BelegDatum >= von && b.BelegDatum <= bis && b.ExportiertAm == null)
            .ToListAsync(ct);

        var eingangsrechnungen = await db.Eingangsrechnungen.AsNoTracking()
            .Include(b => b.Steuersummen)
            .Include(b => b.Lieferant)
            .Where(b => b.Status == BelegStatus.Gebucht && b.BelegDatum >= von && b.BelegDatum <= bis && b.ExportiertAm == null)
            .ToListAsync(ct);

        // Zuordnungen samt zugrunde liegendem Beleg: die Skonto-Gegenbuchung braucht die Steuersummen des
        // ausgeglichenen Belegs, um je Steuerschlüssel zu buchen (s. BuildeZahlungszeilen).
        var zahlungen = await db.Zahlungen.AsNoTracking()
            .Include(z => z.Kunde)
            .Include(z => z.Lieferant)
            .Include(z => z.Zuordnungen).ThenInclude(zu => zu.OffenerPosten!).ThenInclude(op => op.Beleg!).ThenInclude(b => b.Steuersummen)
            .Where(z => z.Zahlungsdatum >= von && z.Zahlungsdatum <= bis && z.ExportiertAm == null)
            .ToListAsync(ct);

        var mwStKonten = await db.MwStSaetze.AsNoTracking()
            .Where(m => m.SteuerSchluessel != null)
            .ToDictionaryAsync(m => m.SteuerSchluessel!.Value, m => (m.ErloeskontoNr, m.AufwandskontoNr), ct);

        var konfiguration = await db.FibuKonfiguration.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);

        return (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, konfiguration?.BankkontoNr ?? 0,
            konfiguration?.Kontenrahmen ?? Kontenrahmen.Skr03);
    }

    private static void BuildeRechnungszeilen(
        IReadOnlyList<Beleg> rechnungen, Dictionary<int, (int? Erloes, int? Aufwand)> mwStKonten, List<DatevBuchungszeile> zeilen)
    {
        foreach (var rechnung in rechnungen)
        {
            var debitorenkonto = rechnung.Kunde?.DebitorenkontoNr;
            if (debitorenkonto is not > 0) continue;

            foreach (var steuersumme in rechnung.Steuersummen)
            {
                var erloeskonto = steuersumme.SteuerSchluessel.HasValue && mwStKonten.TryGetValue(steuersumme.SteuerSchluessel.Value, out var konten)
                    ? konten.Erloes
                    : null;
                if (erloeskonto is not > 0) continue;

                var betrag = steuersumme.NettoSumme + steuersumme.MwStBetrag;
                if (betrag == 0) continue;

                zeilen.Add(new DatevBuchungszeile
                {
                    Umsatz = betrag,
                    SollHaben = 'S',
                    Konto = debitorenkonto.Value,
                    Gegenkonto = erloeskonto.Value,
                    BuSchluessel = steuersumme.SteuerSchluessel,
                    Belegdatum = rechnung.BelegDatum,
                    Belegfeld1 = rechnung.BelegNummer,
                    Buchungstext = $"Rechnung {rechnung.BelegNummer}",
                });
            }
        }
    }

    private static void BuildeEingangsrechnungszeilen(
        IReadOnlyList<Beleg> eingangsrechnungen, Dictionary<int, (int? Erloes, int? Aufwand)> mwStKonten, List<DatevBuchungszeile> zeilen)
    {
        foreach (var eingangsrechnung in eingangsrechnungen)
        {
            var kreditorenkonto = eingangsrechnung.Lieferant?.KreditorenkontoNr;
            if (kreditorenkonto is not > 0) continue;

            foreach (var steuersumme in eingangsrechnung.Steuersummen)
            {
                var aufwandskonto = steuersumme.SteuerSchluessel.HasValue && mwStKonten.TryGetValue(steuersumme.SteuerSchluessel.Value, out var konten)
                    ? konten.Aufwand
                    : null;
                if (aufwandskonto is not > 0) continue;

                var betrag = steuersumme.NettoSumme + steuersumme.MwStBetrag;
                if (betrag == 0) continue;

                zeilen.Add(new DatevBuchungszeile
                {
                    Umsatz = betrag,
                    SollHaben = 'H',
                    Konto = kreditorenkonto.Value,
                    Gegenkonto = aufwandskonto.Value,
                    BuSchluessel = steuersumme.SteuerSchluessel,
                    Belegdatum = eingangsrechnung.BelegDatum,
                    Belegfeld1 = string.IsNullOrEmpty(eingangsrechnung.ExterneReferenz) ? eingangsrechnung.BelegNummer : eingangsrechnung.ExterneReferenz,
                    Buchungstext = $"Eingangsrechnung {eingangsrechnung.BelegNummer}",
                });
            }
        }
    }

    /// <summary>
    /// Je Zahlung eine Bankzeile über den tatsächlich geflossenen Betrag (<c>Zahlung.Gesamtbetrag</c>, ohne
    /// Skonto — er muss dem Kontoauszug entsprechen) und, wenn Skonto gewährt/erhalten wurde, dessen
    /// Gegenbuchung aufs Skontokonto. Ohne diese zweite Zeile wäre der Stapel nicht ausgeglichen: der
    /// Debitor würde um 100 entlastet, die Bank aber nur um 98 belastet.
    /// </summary>
    private static void BuildeZahlungszeilen(
        IReadOnlyList<Zahlung> zahlungen, int bankkonto, Kontenrahmen kontenrahmen, List<DatevBuchungszeile> zeilen)
    {
        if (bankkonto <= 0) return;

        foreach (var zahlung in zahlungen)
        {
            var personenkonto = zahlung.Typ == OffenerPostenTyp.Debitor ? zahlung.Kunde?.DebitorenkontoNr : zahlung.Lieferant?.KreditorenkontoNr;
            if (personenkonto is not > 0) continue;

            var skontoGesamt = zahlung.Zuordnungen.Sum(z => z.SkontoBetrag);
            if (zahlung.Gesamtbetrag == 0 && skontoGesamt == 0) continue;

            // Zahlungseingang (Debitor): Bank-Zugang = Soll. Zahlungsausgang (Kreditor): Bank-Abgang = Haben.
            var sollHaben = zahlung.Typ == OffenerPostenTyp.Debitor ? 'S' : 'H';
            var buchungstext = zahlung.Typ == OffenerPostenTyp.Debitor ? "Zahlungseingang" : "Zahlungsausgang";

            if (zahlung.Gesamtbetrag != 0)
            {
                zeilen.Add(new DatevBuchungszeile
                {
                    Umsatz = zahlung.Gesamtbetrag,
                    SollHaben = sollHaben,
                    Konto = bankkonto,
                    Gegenkonto = personenkonto.Value,
                    Belegdatum = zahlung.Zahlungsdatum,
                    Belegfeld1 = zahlung.Referenz ?? string.Empty,
                    Buchungstext = buchungstext,
                });
            }

            if (skontoGesamt == 0) continue;

            var skontokonto = SkontoKonto(kontenrahmen, zahlung.Typ);
            foreach (var zuordnung in zahlung.Zuordnungen.Where(z => z.SkontoBetrag > 0))
            {
                var steuergruppen = (zuordnung.OffenerPosten?.Beleg?.Steuersummen ?? [])
                    .Select(st => new SkontoAufteilung.Gruppe(st.SteuerSchluessel, st.NettoSumme + st.MwStBetrag))
                    .ToList();

                foreach (var anteil in SkontoAufteilung.AufSteuergruppen(zuordnung.SkontoBetrag, steuergruppen))
                {
                    zeilen.Add(new DatevBuchungszeile
                    {
                        Umsatz = anteil.Betrag,
                        // Gleiche Richtung wie die Bankzeile: das Skonto ersetzt den Restbetrag, der sonst
                        // über die Bank geflossen wäre.
                        SollHaben = sollHaben,
                        Konto = skontokonto,
                        Gegenkonto = personenkonto.Value,
                        // Steuerschlüssel des ausgeglichenen Belegs — DATEV leitet daraus die
                        // Umsatzsteuerkorrektur des Skontos ab.
                        BuSchluessel = anteil.SteuerSchluessel,
                        Belegdatum = zahlung.Zahlungsdatum,
                        Belegfeld1 = zuordnung.OffenerPosten?.Beleg?.BelegNummer ?? zahlung.Referenz ?? string.Empty,
                        Buchungstext = zahlung.Typ == OffenerPostenTyp.Debitor ? "Gewährtes Skonto" : "Erhaltenes Skonto",
                    });
                }
            }
        }
    }

    /// <summary>
    /// Sammelkonto für gewährte/erhaltene Skonti im jeweiligen Standardkontenrahmen (SKR03: 8736/3736,
    /// SKR04: 4736/5736).
    ///
    /// Bewusst hier verdrahtet und nicht konfigurierbar: die Skontokonten gehören fachlich neben
    /// <c>BankkontoNr</c> in die FibuKonfiguration, das ist aber eine Schemaänderung. Bis dahin ist ein
    /// Standardkonto, das der Steuerberater umschlüsseln kann, die deutlich kleinere Übel-Variante
    /// gegenüber einem unausgeglichenen Buchungsstapel (s. STATUS.md, offene Punkte).
    /// </summary>
    private static int SkontoKonto(Kontenrahmen kontenrahmen, OffenerPostenTyp typ) => (kontenrahmen, typ) switch
    {
        (Kontenrahmen.Skr04, OffenerPostenTyp.Debitor) => 4736,
        (Kontenrahmen.Skr04, _) => 5736,
        (_, OffenerPostenTyp.Debitor) => 8736,
        (_, _) => 3736,
    };
}
