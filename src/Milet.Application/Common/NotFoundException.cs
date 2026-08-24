namespace Milet.Application.Common;

public sealed class NotFoundException(string entitaet, object id)
    : Exception($"{entitaet} (Id {id}) wurde nicht gefunden.")
{
    public string Entitaet { get; } = entitaet;

    public object Id { get; } = id;
}
