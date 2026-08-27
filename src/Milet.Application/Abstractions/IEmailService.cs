namespace Milet.Application.Abstractions;

/// <summary>
/// Abstraktion über den tatsächlichen E-Mail-Versand (Microsoft Graph, MSAL/WAM). Implementierungen
/// werfen <see cref="Common.EmailNichtKonfiguriertException"/>, wenn keine Graph-Konfiguration vorhanden ist —
/// das blockiert nie den Rest der Anwendung (siehe PLAN.md Risiko 4).
/// </summary>
public interface IEmailService
{
    Task SendeMailMitAnhangAsync(
        string empfaenger, string betreff, string text, byte[] anhang, string anhangDateiname,
        CancellationToken ct = default);
}
