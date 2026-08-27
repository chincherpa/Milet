using System.Globalization;
using System.Text;

namespace Milet.Application.Common;

/// <summary>Generischer CSV-Export für die Reporting-Auswertungen (Excel-tauglich: Semikolon-getrennt,
/// UTF-8 mit BOM, de-DE-Dezimaltrennzeichen) — bewusst getrennt vom DATEV-spezifischen
/// <c>DatevExtfWriter</c> in Milet.Domain, der ein anderes, fest vorgegebenes Format schreibt.
/// Reine Formatierungslogik ohne IO/DB-Abhängigkeit, daher in der Application-Schicht (WinUI-
/// ViewModels dürfen sie direkt aufrufen, ohne die Infrastructure-Schicht zu referenzieren).</summary>
public static class CsvWriter
{
    public static byte[] Schreiben(IReadOnlyList<string> spalten, IEnumerable<IReadOnlyList<object?>> zeilen)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', spalten.Select(Feld))).Append("\r\n");
        foreach (var zeile in zeilen)
        {
            sb.Append(string.Join(';', zeile.Select(Feld))).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var inhalt = Encoding.UTF8.GetBytes(sb.ToString());
        var ergebnis = new byte[preamble.Length + inhalt.Length];
        preamble.CopyTo(ergebnis, 0);
        inhalt.CopyTo(ergebnis, preamble.Length);
        return ergebnis;
    }

    private static string Feld(object? wert)
    {
        var text = wert switch
        {
            null => "",
            decimal d => d.ToString("F2", CultureInfo.GetCultureInfo("de-DE")),
            DateOnly d => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            DateTime d => d.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            _ => wert.ToString() ?? "",
        };
        return text.Contains(';') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
