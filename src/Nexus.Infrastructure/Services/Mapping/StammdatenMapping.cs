using Nexus.Application.Stammdaten;
using Nexus.Domain.Entities.Stammdaten;
using Nexus.Domain.ValueObjects;

namespace Nexus.Infrastructure.Services.Mapping;

internal static class StammdatenMapping
{
    public static AdresseDto ToDto(this Adresse a) => new()
    {
        Name1 = a.Name1,
        Name2 = a.Name2,
        Strasse = a.Strasse,
        Plz = a.Plz,
        Ort = a.Ort,
        Land = a.Land,
    };

    public static Adresse ToEntity(this AdresseDto dto) => new()
    {
        Name1 = dto.Name1,
        Name2 = dto.Name2,
        Strasse = dto.Strasse,
        Plz = dto.Plz,
        Ort = dto.Ort,
        Land = dto.Land,
    };

    public static KundeDto ToDto(this Kunde k) => new()
    {
        Id = k.Id,
        Kundennummer = k.Kundennummer,
        Adresse = k.Adresse.ToDto(),
        Ansprechpartner = k.Ansprechpartner,
        Telefon = k.Telefon,
        Email = k.Email,
        EmailRechnung = k.EmailRechnung,
        UStIdNr = k.UStIdNr,
        ZahlungsbedingungId = k.ZahlungsbedingungId,
        PreislisteId = k.PreislisteId,
        RabattProzent = k.RabattProzent,
        Kreditlimit = k.Kreditlimit,
        Liefersperre = k.Liefersperre,
        DebitorenkontoNr = k.DebitorenkontoNr,
        Notiz = k.Notiz,
        RowVersion = k.RowVersion,
    };

    public static void ApplyTo(this KundeDto dto, Kunde entity)
    {
        entity.Adresse = dto.Adresse.ToEntity();
        entity.Ansprechpartner = dto.Ansprechpartner;
        entity.Telefon = dto.Telefon;
        entity.Email = dto.Email;
        entity.EmailRechnung = dto.EmailRechnung;
        entity.UStIdNr = dto.UStIdNr;
        entity.ZahlungsbedingungId = dto.ZahlungsbedingungId;
        entity.PreislisteId = dto.PreislisteId;
        entity.RabattProzent = dto.RabattProzent;
        entity.Kreditlimit = dto.Kreditlimit;
        entity.Liefersperre = dto.Liefersperre;
        entity.Notiz = dto.Notiz;
    }

    public static LieferantDto ToDto(this Lieferant l) => new()
    {
        Id = l.Id,
        Lieferantennummer = l.Lieferantennummer,
        Adresse = l.Adresse.ToDto(),
        Ansprechpartner = l.Ansprechpartner,
        Telefon = l.Telefon,
        Email = l.Email,
        UStIdNr = l.UStIdNr,
        ZahlungsbedingungId = l.ZahlungsbedingungId,
        KreditorenkontoNr = l.KreditorenkontoNr,
        Notiz = l.Notiz,
        RowVersion = l.RowVersion,
    };

    public static void ApplyTo(this LieferantDto dto, Lieferant entity)
    {
        entity.Adresse = dto.Adresse.ToEntity();
        entity.Ansprechpartner = dto.Ansprechpartner;
        entity.Telefon = dto.Telefon;
        entity.Email = dto.Email;
        entity.UStIdNr = dto.UStIdNr;
        entity.ZahlungsbedingungId = dto.ZahlungsbedingungId;
        entity.Notiz = dto.Notiz;
    }

    public static ArtikelDto ToDto(this Artikel a) => new()
    {
        Id = a.Id,
        Artikelnummer = a.Artikelnummer,
        Bezeichnung = a.Bezeichnung,
        Beschreibung = a.Beschreibung,
        EinheitId = a.EinheitId,
        EinheitKuerzel = a.Einheit?.Kuerzel,
        MwStSatzId = a.MwStSatzId,
        Einkaufspreis = a.Einkaufspreis,
        Listenpreis = a.Listenpreis,
        Gewicht = a.Gewicht,
        Ean = a.Ean,
        IstLagerartikel = a.IstLagerartikel,
        HatSeriennummern = a.HatSeriennummern,
        Mindestbestand = a.Mindestbestand,
        Gesperrt = a.Gesperrt,
        RowVersion = a.RowVersion,
    };

    public static void ApplyTo(this ArtikelDto dto, Artikel entity)
    {
        entity.Bezeichnung = dto.Bezeichnung;
        entity.Beschreibung = dto.Beschreibung;
        entity.EinheitId = dto.EinheitId;
        entity.MwStSatzId = dto.MwStSatzId;
        entity.Einkaufspreis = dto.Einkaufspreis;
        entity.Listenpreis = dto.Listenpreis;
        entity.Gewicht = dto.Gewicht;
        entity.Ean = dto.Ean;
        entity.IstLagerartikel = dto.IstLagerartikel;
        entity.HatSeriennummern = dto.HatSeriennummern;
        entity.Mindestbestand = dto.Mindestbestand;
        entity.Gesperrt = dto.Gesperrt;
    }
}
