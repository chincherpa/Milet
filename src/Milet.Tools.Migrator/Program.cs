using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Seed;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var connectionString = Environment.GetEnvironmentVariable("MILET_CONNECTIONSTRING")
    ?? args.FirstOrDefault(a => a.StartsWith("--connection=", StringComparison.Ordinal))?["--connection=".Length..]
    ?? configuration.GetConnectionString("Milet")
    ?? throw new InvalidOperationException(
        "Keine Verbindungszeichenfolge. ConnectionStrings:Milet in appsettings.json setzen " +
        "oder MILET_CONNECTIONSTRING als Umgebungsvariable.");

var options = new DbContextOptionsBuilder<MiletDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var db = new MiletDbContext(options);

Console.WriteLine("Milet Migrator");
Console.WriteLine($"Ziel: {db.Database.GetDbConnection().DataSource} / {db.Database.GetDbConnection().Database}");

var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count == 0)
{
    Console.WriteLine("Datenbank ist aktuell — keine ausstehenden Migrationen.");
}
else
{
    Console.WriteLine($"Ausstehende Migrationen ({pending.Count}):");
    foreach (var migration in pending)
    {
        Console.WriteLine($"  - {migration}");
    }

    await db.Database.MigrateAsync();
    Console.WriteLine("Migrationen erfolgreich angewendet.");
}

await StammdatenSeed.ApplyAsync(db);
Console.WriteLine("Grunddaten (Einheiten, MwSt-Sätze, Zahlungsbedingungen, Nummernkreise) geprüft/angelegt.");

return 0;
