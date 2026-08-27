using System.Text;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Finanzen;
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
/// sobald die Kontenzuordnung nachgepflegt ist (kein stiller Datenverlust).</summary>
public sealed class DatevExportService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    IBerechtigungsService berechtigung) : IDatevExportService
{
    static DatevExportService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public async Task<DatevExportVorschauDto> VorschauAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, bankkonto) = await LadeAsync(db, von, bis, ct);

        var zeilen = new List<DatevBuchungszeile>();
        BuildeRechnungszeilen(rechnungen, mwStKonten, zeilen);
        BuildeEingangsrechnungszeilen(eingangsrechnungen, mwStKonten, zeilen);
        BuildeZahlungszeilen(zahlungen, bankkonto, zeilen);

        var summeUmsatz = zeilen.Sum(z => z.Umsatz);
        return new DatevExportVorschauDto(rechnungen.Count, eingangsrechnungen.Count, zahlungen.Count, zeilen.Count, summeUmsatz);
    }

    public async Task<DatevExportErgebnisDto> ExportierenAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        berechtigung.PruefeRecht(RechtCodes.Finanzen);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        var (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, bankkonto) = await LadeAsync(db, von, bis, ct);

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
            BuildeZahlungszeilen([zahlung], bankkonto, zeilen);
            if (zeilen.Count > vorher) exportierteZahlungIds.Add(zahlung.Id);
        }

        var jetzt = DateTime.UtcNow;
        if (exportierteBelegIds.Count > 0)
        {
            await db.Belege.Where(b => exportierteBelegIds.Contains(b.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExportiertAm, jetzt), ct);
        }
        if (exportierteZahlungIds.Count > 0)
        {
            await db.Zahlungen.Where(z => exportierteZahlungIds.Contains(z.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(z => z.ExportiertAm, jetzt), ct);
        }

        await transaktion.CommitAsync(ct);

        var konfiguration = await db.FibuKonfiguration.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);
        var kopf = new DatevExportKopf
        {
            BeraterNr = konfiguration?.BeraterNr ?? 0,
            MandantNr = konfiguration?.MandantNr ?? 0,
            WirtschaftsjahrBeginn = new DateOnly(von.Year, konfiguration?.WirtschaftsjahrBeginnMonat ?? 1, 1),
            SachkontenLaenge = konfiguration?.SachkontenLaenge ?? 4,
            DatumVon = von,
            DatumBis = bis,
            Bezeichnung = $"Milet Buchungsstapel {von:dd.MM.yyyy}-{bis:dd.MM.yyyy}",
            ErzeugtAm = jetzt,
        };
        var csv = DatevExtfWriter.Schreiben(kopf, zeilen);
        var bytes = Encoding.GetEncoding(1252).GetBytes(csv);
        var dateiname = $"EXTF_Buchungsstapel_{von:yyyyMMdd}_{bis:yyyyMMdd}.csv";

        return new DatevExportErgebnisDto(bytes, dateiname, zeilen.Count);
    }

    private static async Task<(
        List<Rechnung> Rechnungen, List<Eingangsrechnung> Eingangsrechnungen, List<Zahlung> Zahlungen,
        Dictionary<int, (int? Erloes, int? Aufwand)> MwStKonten, int Bankkonto)>
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

        var zahlungen = await db.Zahlungen.AsNoTracking()
            .Include(z => z.Kunde)
            .Include(z => z.Lieferant)
            .Where(z => z.Zahlungsdatum >= von && z.Zahlungsdatum <= bis && z.ExportiertAm == null)
            .ToListAsync(ct);

        var mwStKonten = await db.MwStSaetze.AsNoTracking()
            .Where(m => m.SteuerSchluessel != null)
            .ToDictionaryAsync(m => m.SteuerSchluessel!.Value, m => (m.ErloeskontoNr, m.AufwandskontoNr), ct);

        var konfiguration = await db.FibuKonfiguration.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);

        return (rechnungen, eingangsrechnungen, zahlungen, mwStKonten, konfiguration?.BankkontoNr ?? 0);
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

    private static void BuildeZahlungszeilen(IReadOnlyList<Zahlung> zahlungen, int bankkonto, List<DatevBuchungszeile> zeilen)
    {
        if (bankkonto <= 0) return;

        foreach (var zahlung in zahlungen)
        {
            if (zahlung.Gesamtbetrag == 0) continue;

            var personenkonto = zahlung.Typ == OffenerPostenTyp.Debitor ? zahlung.Kunde?.DebitorenkontoNr : zahlung.Lieferant?.KreditorenkontoNr;
            if (personenkonto is not > 0) continue;

            zeilen.Add(new DatevBuchungszeile
            {
                Umsatz = zahlung.Gesamtbetrag,
                // Zahlungseingang (Debitor): Bank-Zugang = Soll. Zahlungsausgang (Kreditor): Bank-Abgang = Haben.
                SollHaben = zahlung.Typ == OffenerPostenTyp.Debitor ? 'S' : 'H',
                Konto = bankkonto,
                Gegenkonto = personenkonto.Value,
                Belegdatum = zahlung.Zahlungsdatum,
                Belegfeld1 = zahlung.Referenz ?? string.Empty,
                Buchungstext = zahlung.Typ == OffenerPostenTyp.Debitor ? "Zahlungseingang" : "Zahlungsausgang",
            });
        }
    }
}
