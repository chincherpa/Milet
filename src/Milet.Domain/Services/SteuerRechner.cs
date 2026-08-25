using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Services;

public static class SteuerRechner
{
    public static decimal BerechnePosition(decimal menge, decimal einzelpreis, decimal rabattProzent)
    {
        var brutto = menge * einzelpreis;
        var nachRabatt = brutto * (1 - rabattProzent / 100m);
        return Math.Round(nachRabatt, 2, MidpointRounding.ToEven);
    }

    public static IReadOnlyList<BelegSteuerSumme> BerechneSteuersummen(IEnumerable<BelegPosition> positionen)
    {
        ArgumentNullException.ThrowIfNull(positionen);
        return positionen
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel)
            .GroupBy(p => (p.MwStSatzWert, p.SteuerSchluessel))
            .Select(g =>
            {
                var netto = Math.Round(g.Sum(p => p.GesamtNetto), 2, MidpointRounding.ToEven);
                var mwst = Math.Round(netto * g.Key.MwStSatzWert / 100m, 2, MidpointRounding.ToEven);
                return new BelegSteuerSumme
                {
                    MwStSatzWert = g.Key.MwStSatzWert,
                    SteuerSchluessel = g.Key.SteuerSchluessel,
                    NettoSumme = netto,
                    MwStBetrag = mwst,
                };
            })
            .ToList();
    }

    public static (decimal Netto, decimal MwSt, decimal Brutto) BerechneKopfsummen(IReadOnlyList<BelegSteuerSumme> steuersummen)
    {
        ArgumentNullException.ThrowIfNull(steuersummen);
        var netto = Math.Round(steuersummen.Sum(s => s.NettoSumme), 2, MidpointRounding.ToEven);
        var mwst = Math.Round(steuersummen.Sum(s => s.MwStBetrag), 2, MidpointRounding.ToEven);
        return (netto, mwst, netto + mwst);
    }
}
