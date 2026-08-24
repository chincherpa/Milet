namespace Milet.Application.Common;

/// <summary>
/// Der Datensatz wurde zwischenzeitlich von einem anderen Benutzer geändert oder gelöscht.
/// UI-Standardreaktion: Dialog "neu laden?".
/// </summary>
public sealed class ConcurrencyConflictException(string entitaet, object id, Exception? innerException = null)
    : Exception($"{entitaet} (Id {id}) wurde zwischenzeitlich geändert oder gelöscht.", innerException)
{
    public string Entitaet { get; } = entitaet;

    public object Id { get; } = id;
}
