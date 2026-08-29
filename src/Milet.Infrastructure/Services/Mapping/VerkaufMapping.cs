using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Services.Mapping;

internal static class VerkaufMapping
{
    public static BelegPositionDto ToDto(this BelegPosition p) => new()
    {
        Id = p.Id,
        PositionsNr = p.PositionsNr,
        PositionsTyp = p.PositionsTyp,
        ArtikelId = p.ArtikelId,
        Bezeichnung = p.Bezeichnung,
        EinheitKuerzel = p.EinheitKuerzel,
        Menge = p.Menge,
        Einzelpreis = p.Einzelpreis,
        RabattProzent = p.RabattProzent,
        MwStSatzId = p.MwStSatzId,
        MwStSatzWert = p.MwStSatzWert,
        SteuerSchluessel = p.SteuerSchluessel,
        LagerortId = p.LagerortId,
        GesamtNetto = p.GesamtNetto,
        UrsprungsPositionId = p.UrsprungsPositionId,
    };

    public static BelegDto ToDto(this Beleg b, bool mitPositionen)
    {
        var typ = BelegTypErweiterung.TypVon(b);

        return new BelegDto
        {
            Id = b.Id,
            BelegTyp = typ,
            BelegNummer = b.BelegNummer,
            BelegDatum = b.BelegDatum,
            KundeId = b.KundeId ?? 0,
            KundeAnzeige = b.Kunde is null ? string.Empty : $"{b.Kunde.Kundennummer} — {b.Kunde.Adresse.Name1}",
            LieferantId = b.LieferantId,
            LieferantAnzeige = b.Lieferant is null ? string.Empty : $"{b.Lieferant.Lieferantennummer} — {b.Lieferant.Adresse.Name1}",
            RechnungsadresseSnapshot = b.RechnungsadresseSnapshot.ToDto(),
            LieferadresseSnapshot = b.LieferadresseSnapshot.ToDto(),
            ZahlungsbedingungZielTage = b.ZahlungsbedingungZielTage,
            ZahlungsbedingungSkontoTage = b.ZahlungsbedingungSkontoTage,
            ZahlungsbedingungSkontoProzent = b.ZahlungsbedingungSkontoProzent,
            Status = b.Status,
            SummeNetto = b.SummeNetto,
            SummeMwSt = b.SummeMwSt,
            SummeBrutto = b.SummeBrutto,
            Faelligkeit = b.Faelligkeit,
            Leistungsdatum = b.Leistungsdatum,
            Kopftext = b.Kopftext,
            Fusstext = b.Fusstext,
            ExterneReferenz = b.ExterneReferenz,
            Positionen = mitPositionen ? b.Positionen.OrderBy(p => p.PositionsNr).Select(p => p.ToDto()).ToList() : [],
            RowVersion = b.RowVersion,
        };
    }
}
