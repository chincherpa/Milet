using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Services.Mapping;

internal static class LagerMapping
{
    public static LagerortDto ToDto(this Lagerort l) => new()
    {
        Id = l.Id,
        Code = l.Code,
        Bezeichnung = l.Bezeichnung,
        Aktiv = l.Aktiv,
        RowVersion = l.RowVersion,
    };

    public static ArtikelBestandDto ToDto(this ArtikelBestand b) => new(
        b.ArtikelId,
        b.Artikel!.Artikelnummer,
        b.Artikel.Bezeichnung,
        b.Artikel.HatSeriennummern,
        b.LagerortId,
        b.Lagerort!.Bezeichnung,
        b.Menge,
        b.Artikel.Mindestbestand);

    public static SeriennummerDto ToDto(this Seriennummer s) => new(s.Id, s.ArtikelId, s.Nummer, s.Status, s.LagerortId);

    public static InventurPositionDto ToDto(this InventurPosition p) =>
        new(p.Id, p.ArtikelId, p.Artikel!.Artikelnummer, p.Artikel.Bezeichnung, p.SollMenge, p.IstMenge);

    public static InventurDto ToDto(this Inventur i, bool mitPositionen) => new()
    {
        Id = i.Id,
        LagerortId = i.LagerortId,
        LagerortBezeichnung = i.Lagerort?.Bezeichnung ?? string.Empty,
        Datum = i.Datum,
        Status = i.Status,
        Positionen = mitPositionen ? i.Positionen.OrderBy(p => p.ArtikelId).Select(p => p.ToDto()).ToList() : [],
        RowVersion = i.RowVersion,
    };
}
