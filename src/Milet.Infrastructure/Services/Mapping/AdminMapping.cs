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
}
