namespace Milet.Application.Admin;

public interface IBenutzerverwaltungService
{
    Task<IReadOnlyList<BenutzerDto>> ListeAsync(CancellationToken ct = default);

    Task<BenutzerDto> LadeAsync(int id, CancellationToken ct = default);

    /// <summary>Id 0 = Neuanlage — <c>NeuesPasswort</c> ist dann Pflicht.</summary>
    Task<BenutzerDto> SpeichereAsync(BenutzerDto dto, CancellationToken ct = default);

    Task PasswortZuruecksetzenAsync(int benutzerId, string neuesPasswort, CancellationToken ct = default);

    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IRollenverwaltungService
{
    Task<IReadOnlyList<RolleDto>> ListeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RechtDto>> AlleRechteAsync(CancellationToken ct = default);

    Task<RolleDto> SpeichereAsync(RolleDto dto, CancellationToken ct = default);

    Task LoescheAsync(int id, CancellationToken ct = default);
}

/// <summary>Login vor Shell (s. PLAN.md "RBAC"). Falsche Zugangsdaten und ein deaktivierter Benutzer sind
/// nach außen ununterscheidbar (kein User-Enumeration-Leck); eine aktive Sperre wird nur offengelegt, wenn
/// das Passwort stimmt.</summary>
public interface IAuthService
{
    Task<AnmeldeErgebnisDto> AnmeldenAsync(string benutzername, string passwort, CancellationToken ct = default);

    /// <summary>Selbstbedienungspfad direkt nach dem Login (erzwungener Wechsel) — autorisiert über das alte
    /// Passwort, nicht über ein Recht: eine Sitzung gibt es zu diesem Zeitpunkt noch nicht.</summary>
    Task PasswortAendernAsync(string benutzername, string altesPasswort, string neuesPasswort, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task<IReadOnlyList<AuditLogDto>> ListeAsync(AuditLogFilterDto? filter = null, CancellationToken ct = default);
}
