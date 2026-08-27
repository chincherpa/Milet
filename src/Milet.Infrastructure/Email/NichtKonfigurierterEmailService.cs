using Milet.Application.Abstractions;
using Milet.Application.Common;

namespace Milet.Infrastructure.Email;

/// <summary>Fallback, solange keine Graph-Konfiguration vorhanden ist (s. DependencyInjection.AddInfrastructure).
/// Blockiert nie den Rest der Anwendung — nur der "E-Mail senden"-Button in der UI meldet einen Fehler.</summary>
public sealed class NichtKonfigurierterEmailService : IEmailService
{
    public Task SendeMailMitAnhangAsync(
        string empfaenger, string betreff, string text, byte[] anhang, string anhangDateiname,
        CancellationToken ct = default) =>
        throw new EmailNichtKonfiguriertException();
}
