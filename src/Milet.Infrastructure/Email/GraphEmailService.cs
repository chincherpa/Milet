using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using Milet.Application.Abstractions;

namespace Milet.Infrastructure.Email;

/// <summary>
/// Echter E-Mail-Versand über Microsoft Graph, Sign-In über MSAL mit WAM-Broker (Windows Account Manager).
/// Nur unter Windows mit registrierter Entra-App nutzbar (s. GraphSettings) — funktional in dieser
/// Cloud-Session nicht verifizierbar, siehe Phase-5-Plan „Verifikations-Realität dieser Session".
/// Token-Cache: rein in-memory für die Laufzeit der IPublicClientApplication-Singleton-Instanz — reicht
/// für v1 (ein interaktiver Login pro App-Sitzung), kein persistenter Cache über Neustarts hinweg.
/// </summary>
public sealed class GraphEmailService(IPublicClientApplication app, IWindowHandleProvider windowHandleProvider) : IEmailService
{
    private static readonly string[] Scopes = ["Mail.Send"];

    public async Task SendeMailMitAnhangAsync(
        string empfaenger, string betreff, string text, byte[] anhang, string anhangDateiname,
        CancellationToken ct = default)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(new MsalAccessTokenProvider(app, windowHandleProvider));
        var graphClient = new GraphServiceClient(authProvider);

        var message = new Message
        {
            Subject = betreff,
            Body = new ItemBody { ContentType = BodyType.Text, Content = text },
            ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = empfaenger } }],
            Attachments = [new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = anhangDateiname,
                ContentBytes = anhang,
            }],
        };

        await graphClient.Me.SendMail.PostAsync(
            new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody { Message = message, SaveToSentItems = true },
            cancellationToken: ct);
    }

    private sealed class MsalAccessTokenProvider(IPublicClientApplication app, IWindowHandleProvider windowHandleProvider)
        : Microsoft.Kiota.Abstractions.Authentication.IAccessTokenProvider
    {
        public Microsoft.Kiota.Abstractions.Authentication.AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var accounts = await app.GetAccountsAsync();
            try
            {
                var result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                var result = await app.AcquireTokenInteractive(Scopes)
                    .WithParentActivityOrWindow(windowHandleProvider.GetHandle())
                    .ExecuteAsync(cancellationToken);
                return result.AccessToken;
            }
        }
    }
}
