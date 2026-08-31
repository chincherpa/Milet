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
        SkontoDebitorKontoNr = f.SkontoDebitorKontoNr,
        SkontoKreditorKontoNr = f.SkontoKreditorKontoNr,
    };

    public static void ApplyTo(this FibuKonfigurationDto dto, FibuKonfiguration entity)
    {
        entity.Kontenrahmen = dto.Kontenrahmen;
        entity.BeraterNr = dto.BeraterNr;
        entity.MandantNr = dto.MandantNr;
        entity.WirtschaftsjahrBeginnMonat = dto.WirtschaftsjahrBeginnMonat;
        entity.SachkontenLaenge = dto.SachkontenLaenge;
        entity.BankkontoNr = dto.BankkontoNr;
        entity.SkontoDebitorKontoNr = dto.SkontoDebitorKontoNr;
        entity.SkontoKreditorKontoNr = dto.SkontoKreditorKontoNr;
    }

    public static RechtDto ToDto(this Recht r) => new()
    {
        Id = r.Id,
        Code = r.Code,
        Bezeichnung = r.Bezeichnung,
    };

    public static RolleDto ToDto(this Rolle r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Beschreibung = r.Beschreibung,
        RechteCodes = r.Rechte.Select(x => x.Code).ToList(),
        RowVersion = r.RowVersion,
    };

    public static BenutzerDto ToDto(this Benutzer b) => new()
    {
        Id = b.Id,
        Benutzername = b.Benutzername,
        Anzeigename = b.Anzeigename,
        Email = b.Email,
        RolleId = b.RolleId,
        RollenName = b.Rolle?.Name,
        Aktiv = b.Aktiv,
        GesperrtBis = b.GesperrtBis,
        PasswortWechselErforderlich = b.PasswortWechselErforderlich,
        RowVersion = b.RowVersion,
    };

    public static AuditLogDto ToDto(this AuditLog a) => new()
    {
        Id = a.Id,
        Zeitpunkt = a.Zeitpunkt,
        BenutzerName = a.BenutzerName,
        EntityName = a.EntityName,
        EntityId = a.EntityId,
        Aktion = a.Aktion,
        Aenderungen = a.Aenderungen,
    };
}
