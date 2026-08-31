using Milet.Application.Abstractions;

namespace Milet.IntegrationTests;

/// <summary>
/// Gemeinsamer Stub für Tests, die einen Service direkt (ohne DI-Container/Login) konstruieren und
/// eine feste, von Null verschiedene BenutzerId erwarten (z. B. für Lagerbewegung.BenutzerId, s.
/// Plan Phase 9 Task 13/14) — analog AllesErlaubtBerechtigungsService.
/// </summary>
internal sealed class TestCurrentUserService : ICurrentUserService
{
    public static readonly TestCurrentUserService Instanz = new();

    public int? BenutzerId => 42;

    public string BenutzerName => "Testbenutzer";
}
