using Milet.Application.Abstractions;

namespace Milet.Infrastructure.Services;

/// <summary>
/// Platzhalter bis zum Login/RBAC in Phase 7 — meldet einen technischen "System"-Benutzer.
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public int? BenutzerId => null;

    public string BenutzerName => "System";
}
