using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milet.Infrastructure;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Seed;

var connectionStringOverride = Environment.GetEnvironmentVariable("MILET_CONNECTIONSTRING")
    ?? args.FirstOrDefault(a => a.StartsWith("--connection=", StringComparison.Ordinal))?["--connection=".Length..];

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true);

if (connectionStringOverride is not null)
{
    builder.Configuration.AddInMemoryCollection([new("ConnectionStrings:Milet", connectionStringOverride)]);
}

if (builder.Configuration.GetConnectionString("Milet") is null)
{
    throw new InvalidOperationException(
        "Keine Verbindungszeichenfolge. ConnectionStrings:Milet in appsettings.json setzen " +
        "oder MILET_CONNECTIONSTRING als Umgebungsvariable.");
}

builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

var dbFactory = host.Services.GetRequiredService<IDbContextFactory<MiletDbContext>>();
await using var db = await dbFactory.CreateDbContextAsync();

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

await AdminSeed.ApplyAsync(db);
Console.WriteLine($"RBAC-Grunddaten (Rechte, Administrator-Rolle, Erstbenutzer '{AdminSeed.StandardAdminBenutzername}') geprüft/angelegt.");

var dummyAngelegt = await DummyDatenSeed.ApplyAsync(host.Services);
Console.WriteLine(dummyAngelegt
    ? "Testdaten (Kunden, Lieferanten, Artikel, Angebote/Aufträge/Rechnungen) angelegt."
    : "Testdaten bereits vorhanden — übersprungen.");

return 0;
