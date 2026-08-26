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
}
