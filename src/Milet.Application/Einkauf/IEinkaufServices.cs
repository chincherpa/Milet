namespace Milet.Application.Einkauf;

public interface IEinkaufLookupService
{
    Task<EinkaufLookups> LadeLookupsAsync(CancellationToken ct = default);
}

public interface IBestellVorschlagService
{
    /// <summary>Alle lagerfähigen, nicht gesperrten Artikel mit gesetztem Mindestbestand, deren aktueller
    /// Gesamtbestand (über alle Lagerorte) den Mindestbestand unterschreitet.</summary>
    Task<IReadOnlyList<BestellVorschlagPositionDto>> ErmittleVorschlaegeAsync(CancellationToken ct = default);
}

public interface IWareneingangBuchenService
{
    /// <summary>Bucht: positive Lagerbewegung je Artikelposition (BestandService.BucheBewegungAsync), legt bei
    /// serialisierten Artikeln neue Seriennummern an, setzt Status Gebucht — eine Transaktion.</summary>
    Task<Verkauf.BelegDto> BuchenAsync(
        int wareneingangId, IReadOnlyDictionary<int, IReadOnlyList<string>> neueSeriennummernJePosition, CancellationToken ct = default);
}

public interface IEingangsrechnungBuchenService
{
    /// <summary>Legt einen Kreditor-Offenen-Posten an; vergleicht die Rechnungssumme mit der Summe des
    /// ursprünglichen Wareneingangs und meldet eine Abweichung als Soft-Warnung im Ergebnis (kein Blocker,
    /// der OP entsteht in jedem Fall).</summary>
    Task<EingangsrechnungBuchenErgebnisDto> BuchenAsync(int eingangsrechnungId, CancellationToken ct = default);
}
