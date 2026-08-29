namespace Milet.Domain.Services;

/// <summary>
/// Verteilt einen gewährten/erhaltenen Skontobetrag auf die Steuergruppen des zugrunde liegenden Belegs.
/// Nötig für die Gegenbuchung des Skontos im DATEV-Buchungsstapel: das Skontokonto wird je Steuerschlüssel
/// bebucht, weil DATEV die Umsatzsteuerkorrektur aus dem Steuerschlüssel der Buchungszeile ableitet. Eine
/// Rechnung mit 19 % und 7 % würde bei einer einzigen Skontozeile die Steuerkorrektur auf einen der beiden
/// Sätze verfälschen.
///
/// Verteilt wird proportional zum Bruttoanteil der Gruppe; die Rundungsdifferenz trägt die größte Gruppe,
/// damit die Summe der Zeilen exakt dem Skontobetrag entspricht (analog zur Begründung in SteuerRechner:
/// gerundet wird auf der Summe, nicht je Zeile).
/// </summary>
public static class SkontoAufteilung
{
    public readonly record struct Gruppe(int? SteuerSchluessel, decimal Brutto);

    public readonly record struct Anteil(int? SteuerSchluessel, decimal Betrag);

    public static IReadOnlyList<Anteil> AufSteuergruppen(decimal skontoBetrag, IReadOnlyList<Gruppe> gruppen)
    {
        ArgumentNullException.ThrowIfNull(gruppen);
        if (skontoBetrag <= 0m) return [];

        var relevante = gruppen.Where(g => g.Brutto > 0m).ToList();
        var summeBrutto = relevante.Sum(g => g.Brutto);

        // Kein verwertbarer Steuerbezug (keine Steuersummen am Beleg oder Bruttosumme 0): eine Zeile ohne
        // Steuerschlüssel. Der Betrag geht dann ungeteilt aufs Skontokonto — die Steuerkorrektur muss in
        // diesem Ausnahmefall die Buchhaltung selbst vornehmen.
        if (relevante.Count == 0 || summeBrutto <= 0m)
        {
            return [new Anteil(null, skontoBetrag)];
        }

        if (relevante.Count == 1)
        {
            return [new Anteil(relevante[0].SteuerSchluessel, skontoBetrag)];
        }

        var anteile = relevante
            .Select(g => new Anteil(g.SteuerSchluessel, Math.Round(skontoBetrag * g.Brutto / summeBrutto, 2, MidpointRounding.ToEven)))
            .ToList();

        var differenz = skontoBetrag - anteile.Sum(a => a.Betrag);
        if (differenz != 0m)
        {
            var groessteIndex = 0;
            for (var i = 1; i < relevante.Count; i++)
            {
                if (relevante[i].Brutto > relevante[groessteIndex].Brutto) groessteIndex = i;
            }

            anteile[groessteIndex] = anteile[groessteIndex] with { Betrag = anteile[groessteIndex].Betrag + differenz };
        }

        return anteile.Where(a => a.Betrag != 0m).ToList();
    }
}
