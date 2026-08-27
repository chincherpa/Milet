using Milet.Application.Abstractions;

namespace Milet.IntegrationTests;

/// <summary>
/// Gemeinsamer "alles erlaubt"-Stub für Tests, die einen Service direkt (ohne DI-Container)
/// konstruieren und keinen RBAC-Guard testen wollen — s. AdminServiceTests für Tests, die den
/// Guard selbst gezielt prüfen.
/// </summary>
internal sealed class AllesErlaubtBerechtigungsService : IBerechtigungsService
{
    public static readonly AllesErlaubtBerechtigungsService Instanz = new();

    public bool HatRecht(string rechtCode) => true;

    public void PruefeRecht(string rechtCode)
    {
    }
}
