namespace Nexus.Domain.Common;

/// <summary>
/// Aggregate Roots mit optimistischer Concurrency (SQL Server rowversion).
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}
