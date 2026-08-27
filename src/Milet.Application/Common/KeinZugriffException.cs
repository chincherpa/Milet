namespace Milet.Application.Common;

/// <summary>
/// Der angemeldete Benutzer besitzt nicht das für diese Aktion erforderliche Recht (RBAC-Guard).
/// UI-Standardreaktion: Fehlerdialog, keine Aktion ausgeführt.
/// </summary>
public sealed class KeinZugriffException(string rechtCode)
    : Exception($"Für diese Aktion fehlt das Recht \"{rechtCode}\".")
{
    public string RechtCode { get; } = rechtCode;
}
