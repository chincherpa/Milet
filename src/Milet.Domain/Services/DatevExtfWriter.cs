using System.Globalization;
using System.Text;

namespace Milet.Domain.Services;

/// <summary>Formatiert Buchungszeilen als DATEV-EXTF-Buchungsstapel-CSV (Formatkennzeichen "EXTF").
///
/// Bildet die zentralen, buchhalterisch relevanten Spalten des offiziellen DATEV-Formats ab
/// (Umsatz, Soll/Haben, Konto, Gegenkonto, BU-Schlüssel, Belegdatum, Belegfeld 1, Buchungstext) —
/// bewusst kein vollständiger Nachbau der ca. 125 offiziellen Spalten (s. PLAN.md Risiko 5:
/// "eng scopen, früh mit Steuerberater validieren"). Byte-genau reproduzierbar (Golden-File-Test),
/// aber NICHT gegen einen echten DATEV-Import geprüft — das bleibt ein offener, nur vom Nutzer mit
/// eigenem Steuerberater durchführbarer Schritt (analog Graph-Mail in Phase 5).
///
/// Reine, IO-freie Formatierungslogik — kein DB-Zugriff, daher hier in der Domain-Schicht statt in
/// Infrastructure (die die Buchungszeilen erst aus gebuchten Belegen zusammenstellt).</summary>
public static class DatevExtfWriter
{
    private const string ZeilenEnde = "\r\n";

    public static string Schreiben(DatevExportKopf kopf, IReadOnlyList<DatevBuchungszeile> zeilen)
    {
        ArgumentNullException.ThrowIfNull(kopf);
        ArgumentNullException.ThrowIfNull(zeilen);

        var sb = new StringBuilder();
        sb.Append(FormatkennzeichenZeile(kopf)).Append(ZeilenEnde);
        sb.Append(SpaltenueberschriftenZeile()).Append(ZeilenEnde);
        foreach (var zeile in zeilen)
        {
            sb.Append(Datenzeile(zeile)).Append(ZeilenEnde);
        }

        return sb.ToString();
    }

    private static string FormatkennzeichenZeile(DatevExportKopf kopf)
    {
        var felder = new[]
        {
            Text("EXTF"),
            "700",
            "21",
            Text("Buchungsstapel"),
            "13",
            kopf.ErzeugtAm.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture),
            "",
            Text("Milet"),
            "",
            "",
            kopf.BeraterNr.ToString(CultureInfo.InvariantCulture),
            kopf.MandantNr.ToString(CultureInfo.InvariantCulture),
            kopf.WirtschaftsjahrBeginn.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            kopf.SachkontenLaenge.ToString(CultureInfo.InvariantCulture),
            kopf.DatumVon.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            kopf.DatumBis.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            Text(kopf.Bezeichnung),
            "",
            "1",
            "0",
            "0",
            Text("EUR"),
        };
        return string.Join(';', felder);
    }

    private static string SpaltenueberschriftenZeile()
    {
        string[] spalten =
        [
            "Umsatz (ohne Soll/Haben-Kz)",
            "Soll/Haben-Kennzeichen",
            "WKZ Umsatz",
            "Konto",
            "Gegenkonto (ohne BU-Schlüssel)",
            "BU-Schlüssel",
            "Belegdatum",
            "Belegfeld 1",
            "Buchungstext",
        ];
        return string.Join(';', spalten.Select(Text));
    }

    private static string Datenzeile(DatevBuchungszeile zeile)
    {
        var felder = new[]
        {
            FormatBetrag(zeile.Umsatz),
            Text(zeile.SollHaben.ToString()),
            Text("EUR"),
            zeile.Konto.ToString(CultureInfo.InvariantCulture),
            zeile.Gegenkonto.ToString(CultureInfo.InvariantCulture),
            zeile.BuSchluessel?.ToString(CultureInfo.InvariantCulture) ?? "",
            zeile.Belegdatum.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            Text(Kuerzen(zeile.Belegfeld1, 36)),
            Text(Kuerzen(zeile.Buchungstext, 60)),
        };
        return string.Join(';', felder);
    }

    private static string FormatBetrag(decimal betrag) =>
        Math.Abs(betrag).ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Kuerzen(string wert, int maxLaenge) =>
        wert.Length <= maxLaenge ? wert : wert[..maxLaenge];

    private static string Text(string wert) => $"\"{wert.Replace("\"", "\"\"")}\"";
}
