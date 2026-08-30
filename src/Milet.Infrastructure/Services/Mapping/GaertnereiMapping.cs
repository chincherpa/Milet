using Milet.Application.Gaertnerei;
using Milet.Domain.Entities.Gaertnerei;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Services.Mapping;

internal static class GaertnereiMapping
{
    public static KulturstufeDto ToDto(this Kulturstufe k) => new()
    {
        Id = k.Id,
        Code = k.Code,
        Bezeichnung = k.Bezeichnung,
        Reihenfolge = k.Reihenfolge,
        IstVerkaufsfaehig = k.IstVerkaufsfaehig,
        FarbeHex = k.FarbeHex,
        Aktiv = k.Aktiv,
        RowVersion = k.RowVersion,
    };

    public static SektionDto ToDto(this Sektion s) => new()
    {
        Id = s.Id,
        LagerortId = s.LagerortId,
        Code = s.Code,
        Bezeichnung = s.Bezeichnung,
        PosXMeter = s.PosXMeter,
        PosYMeter = s.PosYMeter,
        BreiteMeter = s.BreiteMeter,
        HoeheMeter = s.HoeheMeter,
        Aktiv = s.Aktiv,
        RowVersion = s.RowVersion,
    };

    public static FeldDto ToDto(this Lagerort feld, IEnumerable<Sektion> sektionen) => new()
    {
        Id = feld.Id,
        Code = feld.Code,
        Bezeichnung = feld.Bezeichnung,
        PosXMeter = feld.PosXMeter ?? 0,
        PosYMeter = feld.PosYMeter ?? 0,
        BreiteMeter = feld.BreiteMeter ?? 0,
        HoeheMeter = feld.HoeheMeter ?? 0,
        Aktiv = feld.Aktiv,
        RowVersion = feld.RowVersion,
        Sektionen = sektionen.Select(s => s.ToDto()).ToList(),
    };

    public static GaertnereiplanDto ToDto(this Gaertnereiplan plan, IEnumerable<FeldDto> felder) => new()
    {
        Id = plan.Id,
        Bezeichnung = plan.Bezeichnung,
        BreiteMeter = plan.BreiteMeter,
        HoeheMeter = plan.HoeheMeter,
        Aktiv = plan.Aktiv,
        RowVersion = plan.RowVersion,
        Felder = felder.ToList(),
    };
}
