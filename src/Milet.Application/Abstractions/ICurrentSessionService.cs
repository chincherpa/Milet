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

    void Anmelden(int benutzerId, string benutzerName, string rollenName, IEnumerable<string> rechte);

    void Abmelden();
}
