using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexus.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Ermöglicht "dotnet ef" ohne die WinUI-App als Startprojekt.
/// Verbindungszeichenfolge über Umgebungsvariable NEXUS_CONNECTIONSTRING überschreibbar.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NexusDbContext>
{
    public NexusDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("NEXUS_CONNECTIONSTRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=Nexus;Trusted_Connection=True";

        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NexusDbContext(options);
    }
}
