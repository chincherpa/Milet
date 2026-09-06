using Milet.Application.Stammdaten;

namespace Milet.Application.Admin;

public sealed record FirmenstammDto
{
    public string Firmenname { get; init; } = string.Empty;
    public AdresseDto Adresse { get; init; } = new();
    public string? UStIdNr { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
}

public sealed record FibuKonfigurationDto
{
    public Milet.Domain.Entities.Admin.Kontenrahmen Kontenrahmen { get; init; } = Milet.Domain.Entities.Admin.Kontenrahmen.Skr03;
    public int BeraterNr { get; init; }
    public int MandantNr { get; init; }
    public int WirtschaftsjahrBeginnMonat { get; init; } = 1;
    public int SachkontenLaenge { get; init; } = 4;
    public int BankkontoNr { get; init; }

    /// <summary>0 = nicht gepflegt, dann greift das Standardkonto des Kontenrahmens (s. Entity).</summary>
    public int SkontoKontoDebitorNr { get; init; }
    public int SkontoKontoKreditorNr { get; init; }
}

public sealed record RechtDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
}

public sealed record RolleDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Beschreibung { get; init; }
    public IReadOnlyList<string> RechteCodes { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}

public sealed record BenutzerDto
{
    public int Id { get; init; }
    public string Benutzername { get; init; } = string.Empty;
    public string Anzeigename { get; init; } = string.Empty;
    public string? Email { get; init; }

    /// <summary>Nur beim Anlegen/Zurücksetzen gesetzt (Klartext) — wird nie aus der DB zurückgegeben.</summary>
    public string? NeuesPasswort { get; init; }

    public int RolleId { get; init; }
    public string? RollenName { get; init; }
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>Ergebnis einer erfolgreichen Anmeldung — s. <see cref="Abstractions.ICurrentSessionService"/>.</summary>
public sealed record BenutzerSessionDto
{
    public int BenutzerId { get; init; }
    public string BenutzerName { get; init; } = string.Empty;
    public string RollenName { get; init; } = string.Empty;
    public IReadOnlyList<string> Rechte { get; init; } = [];
}

/// <summary>Ergebnis eines Anmeldeversuchs. Ein einfaches <c>BenutzerSessionDto?</c> reicht nicht mehr:
/// zwischen „abgelehnt" und „Passwort stimmt, aber der Zugang ist vorübergehend gesperrt" muss die
/// Oberfläche unterscheiden können, ohne dass der abgelehnte Fall irgendetwas über die Existenz des
/// Benutzers verrät.</summary>
public sealed record AnmeldeErgebnisDto
{
    public BenutzerSessionDto? Session { get; init; }

    /// <summary>Gesetzt, wenn das Passwort stimmt, der Zugang aber wegen zu vieler Fehlversuche bis zu
    /// diesem Zeitpunkt gesperrt ist. Dann ist <see cref="Session"/> null.</summary>
    public DateTime? GesperrtBis { get; init; }

    /// <summary>Anmeldung erfolgreich, aber das Passwort muss vor dem Weiterarbeiten gewechselt werden
    /// (Erstbenutzer aus dem Seed, administratives Zurücksetzen).</summary>
    public bool PasswortAenderungErforderlich { get; init; }

    public static AnmeldeErgebnisDto Fehlgeschlagen() => new();

    public static AnmeldeErgebnisDto Gesperrt(DateTime bis) => new() { GesperrtBis = bis };

    public static AnmeldeErgebnisDto Erfolgreich(BenutzerSessionDto session, bool passwortAenderungErforderlich) =>
        new() { Session = session, PasswortAenderungErforderlich = passwortAenderungErforderlich };
}

public sealed record AuditLogDto
{
    public long Id { get; init; }
    public DateTime Zeitpunkt { get; init; }
    public string BenutzerName { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Aktion { get; init; } = string.Empty;
    public string? Aenderungen { get; init; }
}

/// <summary>Filter für die AuditLog-Ansicht (alle Felder optional/additiv).</summary>
public sealed record AuditLogFilterDto
{
    public string? EntityName { get; init; }
    public DateTime? Von { get; init; }
    public DateTime? Bis { get; init; }
}
