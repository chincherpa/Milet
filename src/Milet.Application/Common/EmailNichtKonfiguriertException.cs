namespace Milet.Application.Common;

/// <summary>
/// Keine Graph-Konfiguration hinterlegt (appsettings.json Sektion "Graph" fehlt) — E-Mail-Versand ist
/// nicht verfügbar. Blockiert nie andere Funktionen (Buchen/PDF/Drucken funktionieren ohne Graph-Config).
/// </summary>
public sealed class EmailNichtKonfiguriertException()
    : Exception("E-Mail-Versand ist nicht konfiguriert. Graph-Konfiguration (ClientId/TenantId/RedirectUri) " +
        "fehlt in appsettings.json (Sektion \"Graph\") — siehe STATUS.md.");
