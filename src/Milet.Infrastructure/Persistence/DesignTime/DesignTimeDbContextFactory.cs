using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Milet.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Ermöglicht "dotnet ef" ohne die WinUI-App als Startprojekt.
/// Verbindungszeichenfolge über Umgebungsvariable MILET_CONNECTIONSTRING überschreibbar.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MiletDbContext>
{
    public MiletDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MILET_CONNECTIONSTRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=Milet;Trusted_Connection=True";

        var options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MiletDbContext(options);
    }
}
