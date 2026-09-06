using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Domain.Services;
using Milet.Infrastructure;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Seed;

var connectionStringOverride = Environment.GetEnvironmentVariable("MILET_CONNECTIONSTRING")
    ?? args.FirstOrDefault(a => a.StartsWith("--connection=", StringComparison.Ordinal))?["--connection=".Length..];

// Testdaten sind OPT-IN. Vorher lief DummyDatenSeed bedingungslos, und sein eigenes Gate ("noch keine
// Artikel vorhanden") trifft genau den Zustand einer frischen PRODUKTIVdatenbank. Der Seed legt dabei nicht
// nur erfundene Stammdaten an, sondern durchläuft die echte Buchungspipeline und BUCHT drei Rechnungen:
// die verbrauchen RE-{Jahr}-0001..0003 aus dem lückenlosen Rechnungsnummernkreis (§14 UStG), sind als
// gebuchte Belege durch den BelegImmutabilityInterceptor unveränderlich und mangels Storno über die
// Anwendung nie wieder zu entfernen. Der dokumentierte Produktiv-Deployment-Schritt (docs/deployment.md § 2)
// ist genau dieser Migratorlauf — der Standardweg darf die Datenbank nicht verunreinigen.
var mitTestdaten = Environment.GetEnvironmentVariable("MILET_SEED_TESTDATEN") == "1"
    || args.Contains("--mit-testdaten", StringComparer.Ordinal);

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

// Das Initialpasswort steht im öffentlichen Quellcode (AdminSeed) und ist damit jedem bekannt. Der Wechsel
// wird seit dem 2026-09-06 beim ersten Login ERZWUNGEN (Benutzer.PasswortAenderungErforderlich, gesetzt vom
// AdminSeed) — der Hinweis bleibt trotzdem, weil er dem Betreiber sagt, dass der Erstlogin noch aussteht.
var standardAdmin = await db.Benutzer.AsNoTracking()
    .FirstOrDefaultAsync(b => b.Benutzername == AdminSeed.StandardAdminBenutzername);
if (standardAdmin is not null && PasswortHasher.Verify(AdminSeed.StandardAdminPasswort, standardAdmin.PasswortHash))
{
    Console.WriteLine();
    Console.WriteLine($"  HINWEIS: Benutzer '{AdminSeed.StandardAdminBenutzername}' hat noch das dokumentierte "
        + "Initialpasswort. Die Anmeldung verlangt beim ersten Login einen Wechsel, bevor die Anwendung "
        + "nutzbar ist — bis dahin ist das Passwort über das Repository öffentlich bekannt.");
    Console.WriteLine();
}

// DummyDatenSeed läuft bewusst über die echten Application-Services (s. Klassenkommentar dort) — und die
// prüfen seit Phase 7 RBAC. Der Migrator hat keine Anmeldung: ohne diese technische Sitzung scheitert der
// erste Migratorlauf auf einer leeren Datenbank mit KeinZugriffException('Stammdaten'). Die Sitzung wird
// erst hier geöffnet, nachdem Migrationen und Grunddaten durch sind — Schema und Rechtekatalog stehen dann.
// BenutzerId bewusst nullable durchgereicht: existiert der Standard-Admin nicht mehr (umbenannt/gelöscht),
// schrieben ErstelltVonId und die AuditLog-Zeilen sonst eine 0, die auf keinen Benutzer zeigt. Es gibt
// keinen FK auf diese Spalte, es knallt also nicht — die Herkunft wäre nur nicht mehr auflösbar.
var sitzung = host.Services.GetRequiredService<ICurrentSessionService>();
sitzung.Anmelden(standardAdmin?.Id, "Migrator", "Administrator", RechtCodes.Alle);

if (mitTestdaten)
{
    var dummyAngelegt = await DummyDatenSeed.ApplyAsync(host.Services);
    Console.WriteLine(dummyAngelegt
        ? "Testdaten (Kunden, Lieferanten, Artikel, Angebote/Aufträge/Rechnungen) angelegt."
        : "Testdaten bereits vorhanden — übersprungen.");
}
else
{
    Console.WriteLine("Testdaten übersprungen (Standard). Für Entwicklung/Demo anfordern mit "
        + "--mit-testdaten oder MILET_SEED_TESTDATEN=1.");
}

return 0;
