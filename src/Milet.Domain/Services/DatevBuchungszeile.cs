namespace Milet.Domain.Services;

/// <summary>Eine Buchungszeile für den DATEV-EXTF-Export — reines Datenobjekt, von
/// <see cref="DatevExtfWriter"/> in eine Zeile der Buchungsstapel-CSV übersetzt.</summary>
public sealed class DatevBuchungszeile
{
    /// <summary>Immer positiv — die Buchungsrichtung steht in <see cref="SollHaben"/>.</summary>
    public required decimal Umsatz { get; init; }

    public required char SollHaben { get; init; }

    public required int Konto { get; init; }

    public required int Gegenkonto { get; init; }

    /// <summary>DATEV-Steuerschlüssel (z. B. 3 = 19 % USt), leer wenn nicht zutreffend (z. B. Zahlung).</summary>
    public int? BuSchluessel { get; init; }

    public required DateOnly Belegdatum { get; init; }

    /// <summary>Rechnungs-/Belegnummer, max. 36 Zeichen laut DATEV-Vorgabe.</summary>
    public required string Belegfeld1 { get; init; }

    public required string Buchungstext { get; init; }
}
