using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Services.Mapping;

internal static class AdminMapping
{
    public static FirmenstammDto ToDto(this Firmenstamm f) => new()
    {
        Firmenname = f.Firmenname,
        Adresse = f.Adresse.ToDto(),
        UStIdNr = f.UStIdNr,
        Telefon = f.Telefon,
        Email = f.Email,
        Iban = f.Iban,
        Bic = f.Bic,
    };

    public static void ApplyTo(this FirmenstammDto dto, Firmenstamm entity)
    {
        entity.Firmenname = dto.Firmenname;
        entity.Adresse = dto.Adresse.ToEntity();
        entity.UStIdNr = dto.UStIdNr;
        entity.Telefon = dto.Telefon;
        entity.Email = dto.Email;
        entity.Iban = dto.Iban;
        entity.Bic = dto.Bic;
    }

    public static FibuKonfigurationDto ToDto(this FibuKonfiguration f) => new()
    {
        Kontenrahmen = f.Kontenrahmen,
        BeraterNr = f.BeraterNr,
        MandantNr = f.MandantNr,
        WirtschaftsjahrBeginnMonat = f.WirtschaftsjahrBeginnMonat,
        SachkontenLaenge = f.SachkontenLaenge,
        BankkontoNr = f.BankkontoNr,
    };

    public static void ApplyTo(this FibuKonfigurationDto dto, FibuKonfiguration entity)
    {
        entity.Kontenrahmen = dto.Kontenrahmen;
        entity.BeraterNr = dto.BeraterNr;
        entity.MandantNr = dto.MandantNr;
        entity.WirtschaftsjahrBeginnMonat = dto.WirtschaftsjahrBeginnMonat;
        entity.SachkontenLaenge = dto.SachkontenLaenge;
        entity.BankkontoNr = dto.BankkontoNr;
    }
}
