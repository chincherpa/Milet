namespace Milet.Application.Abstractions;

/// <summary>
/// Service-seitiger RBAC-Guard: Application-Services rufen <see cref="PruefeRecht"/> am
/// Anfang mutierender Methoden auf (analog zur expliziten FluentValidation am Methodenanfang).
/// Wirft <see cref="Milet.Application.Common.KeinZugriffException"/>, wenn das Recht fehlt.
/// </summary>
public interface IBerechtigungsService
{
    void PruefeRecht(string rechtCode);

    bool HatRecht(string rechtCode);
}
