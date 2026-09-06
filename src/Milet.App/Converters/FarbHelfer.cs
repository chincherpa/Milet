using Windows.UI;

namespace Milet.App.Converters;

/// <summary>Gemeinsame Farblogik für die benutzerdefinierten Farben aus den Stammdaten
/// (<c>Kulturstufe.FarbeHex</c>, Format "#RRGGBB", per Validator erzwungen).
///
/// Lag vorher zweimal vor: einmal mit try/catch im <see cref="HexColorToBrushConverter"/>, einmal ohne
/// in <c>PflanzenUebersichtPage.xaml.cs</c> — dort warf ein fehlerhafter Wert wie "#GGGGGG" beim
/// Zeichnen eine FormatException.</summary>
internal static class FarbHelfer
{
    /// <summary>Wandelt "#RRGGBB" in eine Farbe. Liefert false bei null, leer, falscher Länge oder
    /// ungültigen Hex-Ziffern — Aufrufer setzen dann ihre eigene Ersatzfarbe.</summary>
    public static bool VersucheHexZuFarbe(string? hex, byte alpha, out Color farbe)
    {
        farbe = default;

        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
        {
            return false;
        }

        if (!byte.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        farbe = Color.FromArgb(alpha, r, g, b);
        return true;
    }

    /// <summary>Relative Luminanz nach WCAG 2.1 (sRGB-Kanäle linearisieren, dann gewichtet summieren).
    /// Ergebnis zwischen 0 (Schwarz) und 1 (Weiß).</summary>
    public static double RelativeLuminanz(Color farbe)
        => 0.2126 * Linearisiere(farbe.R) + 0.7152 * Linearisiere(farbe.G) + 0.0722 * Linearisiere(farbe.B);

    private static double Linearisiere(byte kanal)
    {
        var anteil = kanal / 255.0;
        return anteil <= 0.04045 ? anteil / 12.92 : Math.Pow((anteil + 0.055) / 1.055, 2.4);
    }
}
