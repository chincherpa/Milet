namespace Milet.Application.Abstractions;

/// <summary>
/// Prüft beim Start, ob das DB-Schema auf dem Stand der App ist (s. PLAN.md "Deployment":
/// "App prüft SchemaVersion beim Start"). Migrationen werden ausschließlich über
/// Milet.Tools.Migrator angewendet — die App selbst migriert nie.
/// </summary>
public interface ISchemaVersionService
{
    Task<bool> IstAktuellAsync(CancellationToken ct = default);
}
