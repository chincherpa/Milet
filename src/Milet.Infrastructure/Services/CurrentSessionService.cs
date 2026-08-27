using Milet.Application.Abstractions;

namespace Milet.Infrastructure.Services;

/// <summary>
/// Hält den Login-Zustand für die Lebensdauer der App-Instanz (Singleton). Vor dem Login
/// (und für Migrator/Hintergrunddienste ohne UI) meldet sie einen technischen "System"-Benutzer
/// ohne Rechte — genau das Verhalten, das vorher der reine Platzhalter <c>SystemCurrentUserService</c>
/// aus Phase 1 hatte.
/// </summary>
public sealed class CurrentSessionService : ICurrentSessionService
{
    private readonly Lock _sperre = new();
    private HashSet<string> _rechte = new(StringComparer.OrdinalIgnoreCase);

    public int? BenutzerId { get; private set; }

    public string BenutzerName { get; private set; } = "System";

    public bool IstAngemeldet { get; private set; }

    public string? RollenName { get; private set; }

    public IReadOnlySet<string> Rechte => _rechte;

    public bool HatRecht(string rechtCode)
    {
        lock (_sperre)
        {
            return _rechte.Contains(rechtCode);
        }
    }

    public void Anmelden(int benutzerId, string benutzerName, string rollenName, IEnumerable<string> rechte)
    {
        lock (_sperre)
        {
            BenutzerId = benutzerId;
            BenutzerName = benutzerName;
            RollenName = rollenName;
            _rechte = new HashSet<string>(rechte, StringComparer.OrdinalIgnoreCase);
            IstAngemeldet = true;
        }
    }

    public void Abmelden()
    {
        lock (_sperre)
        {
            BenutzerId = null;
            BenutzerName = "System";
            RollenName = null;
            _rechte = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IstAngemeldet = false;
        }
    }
}
