namespace Milet.Application.Abstractions;

/// <summary>
/// Erweitert <see cref="ICurrentUserService"/> um den Login-/Rechte-Zustand (Phase 7).
/// Implementierung hält den angemeldeten Benutzer für die Lebensdauer der App-Instanz
/// (Singleton in Milet.App), nicht pro DbContext.
/// </summary>
public interface ICurrentSessionService : ICurrentUserService
{
    bool IstAngemeldet { get; }

    string? RollenName { get; }

    IReadOnlySet<string> Rechte { get; }

    bool HatRecht(string rechtCode);

    /// <summary>Meldet einen Benutzer an. <paramref name="benutzerId"/> ist nullable für technische
    /// Sitzungen ohne echten Benutzerdatensatz (Migrator) — 0 wäre dort eine Id, die auf niemanden zeigt
    /// und in ErstelltVonId/AuditLog landen würde.
    ///
    /// Die Rechte werden hier eingefroren und nicht nachgelesen: entzieht ein Administrator einem bereits
    /// angemeldeten Benutzer ein Recht, wirkt das erst nach dessen Neuanmeldung. Bewusste Entscheidung für
    /// eine Desktop-Anwendung mit prozessweiter Sitzung — ein Nachlesen bei jedem HatRecht-Aufruf hieße eine
    /// DB-Abfrage in einer synchronen Methode (Sync-over-Async, im Projekt durchgängig vermieden).</summary>
    void Anmelden(int? benutzerId, string benutzerName, string rollenName, IEnumerable<string> rechte);

    void Abmelden();
}
