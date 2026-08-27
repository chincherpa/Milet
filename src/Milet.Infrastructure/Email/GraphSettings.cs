namespace Milet.Infrastructure.Email;

/// <summary>Gebunden aus appsettings.json Sektion "Graph". Alle drei Felder sind Pflicht, damit
/// GraphEmailService registriert wird (siehe DependencyInjection.AddInfrastructure).</summary>
public sealed class GraphSettings
{
    public const string SectionName = "Graph";

    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
