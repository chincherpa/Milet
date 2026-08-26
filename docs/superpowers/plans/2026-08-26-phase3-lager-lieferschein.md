# Phase 3 „Lager+Lieferschein" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lagerführung als Append-only-Ledger (`Lagerbewegung`) mit transaktional mitgeführtem `ArtikelBestand`-Snapshot, der Lieferschein als vierter Belegtyp inkl. Teillieferung (Auftrag→Lieferschein mit Mengenauswahl), Bestandsabbuchung beim Buchen, Sammelrechnung (mehrere Lieferscheine → eine Rechnung), Seriennummern und Inventur — Auftrag→Teillieferung→Restlieferung→Sammelrechnung im UI durchklickbar, Ledger und Snapshot beweisbar konsistent.

**Architektur:** `Lieferschein` wird ein weiterer dünner TPH-Subtyp von `Beleg` — kein neues Belegmodell. Der Belegfluss läuft weiter über `BelegPosition.UrsprungsPositionId`; Teillieferung ist damit reine Mengenauswahl beim Überleiten, keine neue Struktur. Bestandsführung strikt zweigeteilt: `Lagerbewegung` ist die Wahrheit (append-only, nie Update/Delete), `ArtikelBestand` ist ein in derselben Transaktion per atomarem `UPDATE … SET Menge = Menge + @delta … OUTPUT inserted.Menge` fortgeschriebener Lesecache. Schichten- und Namenskonventionen 1:1 aus Phase 1/2 (Plain Services, DTOs als `sealed record`, FluentValidation, `IDbContextFactory` + `AsNoTracking`, `SaveChangesTranslatingConcurrencyAsync`, ViewModels via `[ObservableProperty]`/`[RelayCommand]`, `INavigationAware`).

**Tech Stack:** .NET 10, EF Core 10 (SQL Server/LocalDB), FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, QuestPDF 2026.7.3 (Community License), xUnit v3, Testcontainers.MsSql + **neu** LocalDB-Fallback.

**Spec:** `PLAN.md` (Abschnitte „Lager: Append-only-Ledger + Snapshot", „Geschäftsprozesse" Punkte 2+3, Phasen-Tabelle Zeile „3 Lager+Lieferschein", Risiko 6 „Business-Races"). Bestandscode recherchiert (`BelegService`, `BelegUeberleitungService`, `RechnungBuchenService`, `NumberRangeService`, `BelegEditViewModelBase`, `KleinstammPage`) — jede Abweichung von den dort etablierten Mustern ist unten begründet.

---

## Vorbefunde aus dem Bestandscode (vor Task 1 lesen)

Drei Dinge, die Phase 3 zwingend anfasst und die vorher niemandem aufgefallen sind:

1. **Die Offene-Mengen-Prüfung in `BelegUeberleitungService` schützt nicht gegen Races.** Der Kommentar dort behauptet „Prüfung explizit in derselben Transaktion — Schutz gegen Race zweier gleichzeitiger Überleitungen". Unter dem Default-Isolationslevel READ COMMITTED lesen zwei parallele Überleitungen desselben Auftrags beide „nichts geliefert" und schreiben beide einen Vollbeleg → Überlieferung. In Phase 2 war das folgenlos (Angebot→Auftrag ist Vollkopie, doppelte Aufträge fallen sofort auf); mit Teillieferung wird es ein echter Bestandsfehler. Fix in Task 9: Sperrzeile auf dem **Quellbeleg** (`SELECT Id FROM Belege WITH (UPDLOCK, ROWLOCK) WHERE Id = @id`) als erste Anweisung der Transaktion — serialisiert alle Überleitungen aus demselben Beleg, ohne die übrige Last zu berühren.

2. **`ErlaubteUebergaenge` ist ein `Dictionary<BelegTyp, BelegTyp>` — ein Ziel je Quelltyp.** Phase 3 braucht aus `Auftrag` zwei Ziele (`Lieferschein` regulär, `Rechnung` direkt für Dienstleistung) und aus `Lieferschein` eines (`Rechnung`). Wird in Task 9 zu einem `HashSet<(BelegTyp Quelle, BelegTyp Ziel)>`.

3. **Der Nummernkreis-Seed ist nicht per Code idempotent.** `StammdatenSeed` legt Nummernkreise nur an, wenn die Tabelle *komplett leer* ist (`if (!await db.Nummernkreise.AnyAsync(ct))`). Bestehende Datenbanken bekommen den in Task 6 ergänzten `INV`-Kreis damit nie. Muss in Task 6 auf „je Code prüfen und einzeln nachlegen" umgebaut werden — sonst schlägt der Inventur-Abschluss auf jeder bereits migrierten DB mit „Nummernkreis 'INV' existiert nicht" fehl. (`LS` ist bereits geseedet und braucht nichts.)

---

## Global Constraints

- **Ledger ist unveränderlich.** `Lagerbewegung` wird ausschließlich per `Add` geschrieben. Kein Codepfad ändert oder löscht eine Bewegung; Korrekturen sind Gegenbuchungen. Ein Interceptor sichert das ab (Task 7), analog zu `BelegImmutabilityInterceptor`.
- **Snapshot nie per Read-Modify-Write.** `ArtikelBestand.Menge` wird nur über das atomare Delta-UPDATE aus Task 7 fortgeschrieben — niemals über `entity.Menge = entity.Menge - x` im Change Tracker. Verstöße gegen diese Regel sind der Bug, den PLAN.md §Risiko 6 explizit benennt.
- **Eine Transaktion je Nutzeraktion.** Lieferschein buchen = Mengenprüfung + Ledger-Inserts + Snapshot-Updates + Seriennummern-Statuswechsel + Belegstatus in *einer* `BeginTransactionAsync`. Rollback lässt keinen Teilzustand zurück.
- Neue Aggregate Roots (`Inventur`) erhalten `AuditableEntity` + `IHasRowVersion`; Kinder (`InventurPosition`, `BelegPositionSeriennummer`) sind einfache POCOs ohne RowVersion. `Lagerbewegung` erbt `AuditableEntity` (wer/wann ist GoBD-relevant), aber **kein** `IHasRowVersion` — eine append-only-Zeile hat keinen Konflikt.
- Decimal-Präzisionen: `Menge`-artige Felder (`Lagerbewegung.Menge`, `ArtikelBestand.Menge`, `InventurPosition.SollMenge/IstMenge`) `decimal(18,3)` — identisch zu `BelegPosition.Menge`. Keine Preise im Lagermodul (Bewertung ist nicht Phase 3).
- DTOs: `sealed record` mit `init`-Properties, alle DTOs eines Moduls in `Dtos.cs`, Validatoren in `Validators.cs`, Interfaces in `ILagerServices.cs` — exakt wie `Stammdaten`/`Verkauf`.
- Lieferschein-Nummer wird wie Angebot/Auftrag **beim ersten Speichern** vergeben (Nummernkreis `LS`), nicht beim Buchen — die §14-UStG-Lückenlosigkeit betrifft nur Rechnungen.
- Gebuchter Lieferschein ist unveränderlich; der bestehende `BelegImmutabilityInterceptor` greift ohne Änderung, weil er auf `Beleg` (Basisklasse) läuft.
- `dotnet` explizit über `%USERPROFILE%\.dotnet\dotnet.exe` aufrufen (PATH zeigt auf leere Install, siehe STATUS.md). Jedes Testprojekt einzeln aufrufen (MTP-Modus).
- Migrationen ausschließlich über `Milet.Tools.Migrator` anwenden.
- Deutsche Bezeichner für Fachliches, gemischt-englisch für Infrastruktur — wie bisher.

---

### Task 0: Test-Infrastruktur — LocalDB-Fallback für Integrationstests

**Warum zuerst:** Phase 3 steht und fällt mit Transaktionstests (Ledger=Snapshot, Teillieferungs-Race, Negativsperre). Auf der Entwicklungsmaschine gibt es kein Docker, alle Testcontainers-Tests werden übersprungen — die Kernaussagen dieser Phase wären damit nie tatsächlich verifiziert, nur behauptet. LocalDB ist vorhanden und für diese Tests ausreichend (echtes SQL Server, echte Transaktionen, echte Sperren; nur nicht containerisiert).

**Files:**
- Create: `tests/Milet.IntegrationTests/Infrastruktur/SqlTestDatabase.cs`
- Modify: `tests/Milet.IntegrationTests/NumberRangeServiceTests.cs`
- Modify: `tests/Milet.IntegrationTests/RechnungBuchenServiceTests.cs`

**Interfaces:**
- Produces: `SqlTestDatabase` (async-disposable Testdatenbank + `IDbContextFactory<MiletDbContext>`) — von Task 7, 8, 13 konsumiert.

- [ ] **Step 1: `SqlTestDatabase` anlegen**

Verhalten: Docker verfügbar → Testcontainers wie bisher. Sonst LocalDB verfügbar → eigene Datenbank `MiletTest_{Guid:N}` auf `(localdb)\MSSQLLocalDB`, am Ende `DROP DATABASE`. Sonst → `Assert.Skip`.

`tests/Milet.IntegrationTests/Infrastruktur/SqlTestDatabase.cs`:
```csharp
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Milet.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests.Infrastruktur;

/// <summary>
/// Stellt eine echte SQL-Server-Datenbank für Integrationstests bereit: bevorzugt Testcontainers,
/// ersatzweise LocalDB (Entwicklungsmaschine ohne Docker, siehe STATUS.md). Erst wenn beides fehlt,
/// wird der Test übersprungen — die Transaktions-/Sperr-Invarianten dieser Phase sollen nicht
/// stillschweigend ungetestet bleiben.
/// </summary>
public sealed class SqlTestDatabase : IAsyncDisposable
{
    private const string LocalDbMaster =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";

    private MsSqlContainer? _container;
    private string? _localDbName;

    public string ConnectionString { get; private set; } = string.Empty;
    public DbContextOptions<MiletDbContext> Options { get; private set; } = null!;
    public IDbContextFactory<MiletDbContext> Factory { get; private set; } = null!;

    /// <param name="interceptors">Interceptors, die der Produktivcode ebenfalls registriert (siehe <c>DependencyInjection</c>).</param>
    public static async Task<SqlTestDatabase> StarteAsync(params IInterceptor[] interceptors)
    {
        var db = new SqlTestDatabase();

        if (DockerVerfuegbar())
        {
            db._container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await db._container.StartAsync();
            db.ConnectionString = db._container.GetConnectionString();
        }
        else if (await LocalDbVerfuegbarAsync())
        {
            db._localDbName = $"MiletTest_{Guid.NewGuid():N}";
            await FuehreAufMasterAus($"CREATE DATABASE [{db._localDbName}]");
            db.ConnectionString =
                $@"Server=(localdb)\MSSQLLocalDB;Database={db._localDbName};Integrated Security=true;TrustServerCertificate=true";
        }
        else
        {
            Assert.Skip("Weder Docker noch LocalDB verfügbar — Integrationstest übersprungen.");
        }

        var builder = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(db.ConnectionString);
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        db.Options = builder.Options;
        db.Factory = new TestDbContextFactory(db.Options);

        await using var ctx = new MiletDbContext(db.Options);
        await ctx.Database.EnsureCreatedAsync();
        return db;
    }

    public MiletDbContext NeuerContext() => new(Options);

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (_localDbName is not null)
        {
            SqlConnection.ClearAllPools();
            await FuehreAufMasterAus(
                $"ALTER DATABASE [{_localDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_localDbName}]");
        }
    }

    private static async Task FuehreAufMasterAus(string sql)
    {
        await using var connection = new SqlConnection(LocalDbMaster);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> LocalDbVerfuegbarAsync()
    {
        try
        {
            await using var connection = new SqlConnection(LocalDbMaster);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            return process is not null && process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<MiletDbContext> options) : IDbContextFactory<MiletDbContext>
    {
        public MiletDbContext CreateDbContext() => new(options);
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
```

- [ ] **Step 2: `Microsoft.Data.SqlClient` sicherstellen**

Prüfen, ob `tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj` `Microsoft.Data.SqlClient` transitiv über `Microsoft.EntityFrameworkCore.SqlServer` bekommt (Regelfall). Nur falls der `SqlConnection`-Typ nicht auflösbar ist: `<PackageReference Include="Microsoft.Data.SqlClient" />` ergänzen und Version in `Directory.Packages.props` pinnen.

- [ ] **Step 3: Bestehende Tests auf `SqlTestDatabase` umstellen**

`NumberRangeServiceTests` und `RechnungBuchenServiceTests`: die je eigene `InitializeAsync`-Container-Logik, `DockerVerfuegbar()` und die private `TestDbContextFactory` durch je ein `SqlTestDatabase.StarteAsync(...)` ersetzen (bei `RechnungBuchenServiceTests` mit `new BelegImmutabilityInterceptor()` als Argument). Testkörper und Assertions bleiben unverändert.

- [ ] **Step 4: Tests laufen lassen — jetzt erwartungsgemäß NICHT mehr übersprungen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: Alle bisher übersprungenen Testcontainers-Tests laufen jetzt gegen LocalDB **durch** (Passed, nicht Skipped). Schlägt einer davon fachlich fehl, ist das ein bisher verdeckter echter Fehler — vor dem Weitermachen klären, nicht wegskippen.

- [ ] **Step 5: Commit**

```bash
git add tests/Milet.IntegrationTests
git commit -m "Integrationstests: LocalDB-Fallback statt Skip, wenn kein Docker verfügbar"
```

---

### Task 1: Domain — Lager-Entities (Lagerort, Lagerbewegung, ArtikelBestand, Seriennummer)

**Files:**
- Create: `src/Milet.Domain/Entities/Stammdaten/Lagerort.cs`
- Create: `src/Milet.Domain/Entities/Lager/Lagerbewegung.cs`
- Create: `src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs`
- Create: `src/Milet.Domain/Entities/Lager/ArtikelBestand.cs`
- Create: `src/Milet.Domain/Entities/Lager/Seriennummer.cs`
- Create: `src/Milet.Domain/Entities/Lager/SeriennummerStatus.cs`
- Create: `src/Milet.Domain/Entities/Lager/BelegPositionSeriennummer.cs`

**Interfaces:**
- Consumes: `AuditableEntity`, `IHasRowVersion` (`src/Milet.Domain/Common/`), `Artikel` (`Entities/Stammdaten/`), `BelegPosition` (`Entities/Verkauf/`).
- Produces: alle Lager-Entities — von Task 4 (DTOs), 6 (EF), 7/8/10/11 (Services) konsumiert.

- [ ] **Step 1: `Lagerort` (Stammdaten, nicht Lager — analog `Einheit`)**

```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Stammdaten;

public class Lagerort : AuditableEntity
{
    public int Id { get; set; }
    public string Kuerzel { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    /// <summary>Vorbelegung im Belegeditor und Ziel aller Buchungen ohne explizite Lagerortwahl.</summary>
    public bool IstStandard { get; set; }
    public bool Gesperrt { get; set; }
}
```
Kein `IHasRowVersion` — wie die übrigen Kleinstamm-Entities (`Einheit`, `Versandart`), deren Löschkonflikte über `SaveChangesDeletingAsync` abgefangen werden.

- [ ] **Step 2: `LagerbewegungTyp`**

```csharp
namespace Milet.Domain.Entities.Lager;

public enum LagerbewegungTyp
{
    Warenausgang = 0,
    Wareneingang = 1,
    Inventurkorrektur = 2,
    Storno = 3,
}
```
`Umlagerung` bewusst nicht — käme als Paar aus Ab-/Zugang und ist in Phase 3 nicht gefordert.

- [ ] **Step 3: `Lagerbewegung` (append-only)**

```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

/// <summary>
/// Append-only-Ledger: die Wahrheit über den Bestand. Zeilen werden nie geändert oder gelöscht —
/// Korrekturen sind Gegenbuchungen (durchgesetzt von <c>LagerbewegungImmutabilityInterceptor</c>).
/// </summary>
public class Lagerbewegung : AuditableEntity
{
    public int Id { get; set; }

    public int ArtikelId { get; set; }
    public Entities.Stammdaten.Artikel? Artikel { get; set; }

    public int LagerortId { get; set; }
    public Entities.Stammdaten.Lagerort? Lagerort { get; set; }

    /// <summary>Signiert: negativ = Abgang, positiv = Zugang.</summary>
    public decimal Menge { get; set; }

    public LagerbewegungTyp Typ { get; set; }

    /// <summary>Herkunft der Bewegung, falls belegbasiert (Lieferschein/Wareneingang).</summary>
    public int? BelegPositionId { get; set; }
    public Entities.Verkauf.BelegPosition? BelegPosition { get; set; }

    /// <summary>Herkunft der Bewegung, falls aus einem Inventurabschluss.</summary>
    public int? InventurPositionId { get; set; }

    public int? SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }

    public DateTime Zeitpunkt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: `ArtikelBestand` (Snapshot)**

```csharp
namespace Milet.Domain.Entities.Lager;

/// <summary>
/// Aus dem Ledger abgeleiteter Lesecache (Artikel × Lagerort). Wird ausschließlich über das
/// atomare Delta-UPDATE in <c>BestandsBuchungen</c> fortgeschrieben — nie über den Change Tracker.
/// </summary>
public class ArtikelBestand
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Entities.Stammdaten.Artikel? Artikel { get; set; }
    public int LagerortId { get; set; }
    public Entities.Stammdaten.Lagerort? Lagerort { get; set; }
    public decimal Menge { get; set; }
}
```
Kein `RowVersion`: die Korrektheit kommt aus dem atomaren UPDATE und der X-Sperre innerhalb der Buchungstransaktion, nicht aus optimistischer Concurrency. Ein RowVersion hier würde zu Konflikt-Dialogen bei Vorgängen führen, die serialisiert ohnehin korrekt sind.

- [ ] **Step 5: `Seriennummer` + Status + Junction**

```csharp
namespace Milet.Domain.Entities.Lager;

public enum SeriennummerStatus
{
    AufLager = 0,
    Ausgeliefert = 1,
    Retourniert = 2,
}
```

```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Seriennummer : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Entities.Stammdaten.Artikel? Artikel { get; set; }
    public string Nummer { get; set; } = string.Empty;
    public SeriennummerStatus Status { get; set; } = SeriennummerStatus.AufLager;
    public int? LagerortId { get; set; }
    public Entities.Stammdaten.Lagerort? Lagerort { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
```

```csharp
namespace Milet.Domain.Entities.Lager;

/// <summary>Zuordnung gepickter Seriennummern zur Lieferscheinposition (n:m).</summary>
public class BelegPositionSeriennummer
{
    public int BelegPositionId { get; set; }
    public Entities.Verkauf.BelegPosition? BelegPosition { get; set; }
    public int SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }
}
```

- [ ] **Step 6: Build**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: grün.

- [ ] **Step 7: Commit**

```bash
git add src/Milet.Domain/Entities
git commit -m "Domain: Lager-Entities (Lagerort, Lagerbewegung, ArtikelBestand, Seriennummer)"
```

---

### Task 2: Domain — Inventur + Differenzlogik + Tests

**Files:**
- Create: `src/Milet.Domain/Entities/Lager/Inventur.cs`
- Create: `src/Milet.Domain/Entities/Lager/InventurPosition.cs`
- Create: `src/Milet.Domain/Entities/Lager/InventurStatus.cs`
- Create: `src/Milet.Domain/Services/InventurRechner.cs`
- Create: `tests/Milet.Domain.Tests/InventurRechnerTests.cs`

**Interfaces:**
- Produces: `Inventur`, `InventurPosition`, `InventurStatus`, `InventurRechner.BerechneKorrekturen` — von Task 11 (InventurService) konsumiert.

- [ ] **Step 1: Entities**

```csharp
namespace Milet.Domain.Entities.Lager;

public enum InventurStatus
{
    Erfassung = 0,
    Abgeschlossen = 1,
}
```

```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Inventur : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string InventurNummer { get; set; } = string.Empty;
    public DateOnly Stichtag { get; set; }
    public int LagerortId { get; set; }
    public Entities.Stammdaten.Lagerort? Lagerort { get; set; }
    public InventurStatus Status { get; set; } = InventurStatus.Erfassung;
    public DateTime? AbgeschlossenAm { get; set; }
    public List<InventurPosition> Positionen { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
```

```csharp
namespace Milet.Domain.Entities.Lager;

public class InventurPosition
{
    public int Id { get; set; }
    public int InventurId { get; set; }
    public Inventur? Inventur { get; set; }
    public int ArtikelId { get; set; }
    public Entities.Stammdaten.Artikel? Artikel { get; set; }

    /// <summary>Bei Anlage der Inventur eingefroren — spätere Bewegungen ändern den Soll-Wert nicht.</summary>
    public decimal SollMenge { get; set; }

    /// <summary>Gezählter Wert; null = noch nicht gezählt (zählt beim Abschluss nicht als 0).</summary>
    public decimal? IstMenge { get; set; }
}
```

Das Einfrieren der `SollMenge` ist der Grund, warum die Inventur kein reiner View auf `ArtikelBestand` sein kann: Zählung und Abschluss liegen zeitlich auseinander, und die Differenz muss gegen den Stand *bei Anlage* gebildet werden.

- [ ] **Step 2: `InventurRechner` (reine Domain-Logik, ohne DB)**

```csharp
using Milet.Domain.Entities.Lager;

namespace Milet.Domain.Services;

public static class InventurRechner
{
    /// <summary>
    /// Liefert je gezählter Position die zu buchende Korrekturmenge (Ist − Soll). Nicht gezählte
    /// Positionen (<c>IstMenge is null</c>) und Nulldifferenzen erzeugen keine Buchung.
    /// </summary>
    public static IReadOnlyList<(int InventurPositionId, int ArtikelId, decimal Korrekturmenge)> BerechneKorrekturen(
        IEnumerable<InventurPosition> positionen)
    {
        ArgumentNullException.ThrowIfNull(positionen);
        return positionen
            .Where(p => p.IstMenge is not null)
            .Select(p => (p.Id, p.ArtikelId, Korrekturmenge: p.IstMenge!.Value - p.SollMenge))
            .Where(x => x.Korrekturmenge != 0)
            .ToList();
    }
}
```

- [ ] **Step 3: Tests schreiben (TDD — vor Step 2 rot sehen ist erlaubt, aber nicht erzwungen)**

`tests/Milet.Domain.Tests/InventurRechnerTests.cs` — mindestens diese Fälle:
1. `IstMenge` größer `SollMenge` → positive Korrektur.
2. `IstMenge` kleiner `SollMenge` → negative Korrektur.
3. `IstMenge == SollMenge` → keine Korrektur in der Liste.
4. `IstMenge is null` → keine Korrektur (nicht gezählt ≠ null gezählt) — der fachlich wichtigste Fall.
5. Nachkommastellen: `SollMenge = 2.5m`, `IstMenge = 2.125m` → exakt `-0.375m` (keine Rundung im Rechner; `decimal(18,3)` deckt drei Stellen).

- [ ] **Step 4: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj`
Expected: alle grün (bisher 14 + 5 neue).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain tests/Milet.Domain.Tests
git commit -m "Domain: Inventur-Entities + InventurRechner mit Tests"
```

---

### Task 3: Domain — Lieferschein als vierter Belegtyp

**Files:**
- Modify: `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Lieferschein.cs`
- Modify: `src/Milet.Domain/Entities/Verkauf/Beleg.cs`

**Interfaces:**
- Produces: `BelegTyp.Lieferschein`, `Lieferschein`, `Beleg.LagerortId` — von Task 6, 8, 9 konsumiert.

- [ ] **Step 1: Enum erweitern**

```csharp
public enum BelegTyp
{
    Angebot = 0,
    Auftrag = 1,
    Rechnung = 2,
    Lieferschein = 3,
}
```
Wert 3 anhängen, **nicht** einsortieren — der Discriminator ist zwar ein String (`nameof`), aber `BelegTyp` wird an mehreren Stellen als `int` durch DTOs gereicht; Umnummerieren würde bestehende Belege umdeuten.

- [ ] **Step 2: Subtyp**

```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Lieferschein : Beleg;
```

- [ ] **Step 3: `Beleg` um Lagerort ergänzen**

In `Beleg.cs` ergänzen:
```csharp
    /// <summary>Nur Lieferschein (und ab Phase 4 Wareneingang): Lager, gegen das gebucht wird.</summary>
    public int? LagerortId { get; set; }
    public Domain.Entities.Stammdaten.Lagerort? Lagerort { get; set; }
```
Auf der Basisklasse statt auf `Lieferschein`, weil TPH ohnehin eine Tabelle ist und Phase 4 (Wareneingang) dieselbe Spalte braucht — eine Spalte, zwei Subtypen, nullable für die übrigen.

- [ ] **Step 4: Build**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: grün. Der Rest der Solution bricht an dieser Stelle noch an den `switch`-Ausdrücken über `BelegTyp` (`BelegService`, `BelegUeberleitungService`, `VerkaufMapping`, `PdfService`) — das ist erwartet und wird in Task 8/9/12 behoben. Falls die Build-Reihenfolge stört: Solution-Build erst nach Task 9 erwarten.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Entities/Verkauf
git commit -m "Domain: Lieferschein als vierter Beleg-Subtyp + LagerortId am Beleg"
```

---

### Task 4: Application — Lager-DTOs, Service-Interfaces, Validatoren

**Files:**
- Create: `src/Milet.Application/Lager/Dtos.cs`
- Create: `src/Milet.Application/Lager/ILagerServices.cs`
- Create: `src/Milet.Application/Lager/Validators.cs`
- Create: `tests/Milet.Application.Tests/LagerValidatorTests.cs`
- Modify: `src/Milet.Application/Stammdaten/Dtos.cs` (`LagerortDto`)
- Modify: `src/Milet.Application/Stammdaten/IStammdatenServices.cs` (`ILagerorteService`)
- Modify: `src/Milet.Application/Stammdaten/Validators.cs` (`LagerortValidator`)

**Interfaces:**
- Consumes: `LookupDto` (`src/Milet.Application/Stammdaten/Dtos.cs`).
- Produces: alle Lager-DTOs und -Interfaces — von Task 8, 10, 11 (Implementierungen) und 15–17 (ViewModels) konsumiert.

- [ ] **Step 1: `LagerortDto` + `ILagerorteService` + Validator im Stammdaten-Modul**

`Lagerort` gehört fachlich zum Kleinstamm und folgt exakt dem Muster von `VersandartDto`/`IVersandartenService` (Liste/Speichere/Lösche, kein RowVersion). Genau dort einsortieren, keine Sonderlocke.

```csharp
public sealed record LagerortDto
{
    public int Id { get; init; }
    public string Kuerzel { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public bool IstStandard { get; init; }
    public bool Gesperrt { get; init; }
}
```
Validator: `Kuerzel` `NotEmpty().MaximumLength(10)`, `Bezeichnung` `NotEmpty().MaximumLength(100)`.

- [ ] **Step 2: Lager-DTOs**

`src/Milet.Application/Lager/Dtos.cs`:
```csharp
namespace Milet.Application.Lager;

/// <summary>Zeile der Bestandsübersicht (Artikel × Lagerort) inkl. Mindestbestand-Warnung.</summary>
public sealed record BestandDto
{
    public int ArtikelId { get; init; }
    public string Artikelnummer { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public int LagerortId { get; init; }
    public string LagerortKuerzel { get; init; } = string.Empty;
    public decimal Menge { get; init; }
    public string? EinheitKuerzel { get; init; }
    public decimal? Mindestbestand { get; init; }
    public bool UnterMindestbestand => Mindestbestand is { } m && Menge < m;
}

public sealed record LagerbewegungDto
{
    public int Id { get; init; }
    public DateTime Zeitpunkt { get; init; }
    public int ArtikelId { get; init; }
    public string Artikelnummer { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public string LagerortKuerzel { get; init; } = string.Empty;
    public decimal Menge { get; init; }
    public Domain.Entities.Lager.LagerbewegungTyp Typ { get; init; }
    public string? BelegNummer { get; init; }
    public string? Seriennummer { get; init; }
    public string? BenutzerName { get; init; }
}

public sealed record SeriennummerDto
{
    public int Id { get; init; }
    public int ArtikelId { get; init; }
    public string ArtikelAnzeige { get; init; } = string.Empty;
    public string Nummer { get; init; } = string.Empty;
    public Domain.Entities.Lager.SeriennummerStatus Status { get; init; }
    public int? LagerortId { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>Eine Zeile im Teillieferungs-Dialog: was ist noch offen, wie viel soll übernommen werden.</summary>
public sealed record OffenePositionDto
{
    public int PositionId { get; init; }
    public int PositionsNr { get; init; }
    public string Bezeichnung { get; init; } = string.Empty;
    public string? EinheitKuerzel { get; init; }
    public decimal Menge { get; init; }
    public decimal BereitsUebernommen { get; init; }
    public decimal OffeneMenge { get; init; }
    public bool HatSeriennummern { get; init; }
}

/// <summary>Nutzerauswahl aus dem Teillieferungs-Dialog. <c>Menge</c> ≤ zugehörige <c>OffeneMenge</c>.</summary>
public sealed record UeberleitungsAuswahlDto
{
    public int QuellPositionId { get; init; }
    public decimal Menge { get; init; }
    /// <summary>Gepickte Seriennummern; Anzahl muss <c>Menge</c> entsprechen, wenn der Artikel seriennummernpflichtig ist.</summary>
    public IReadOnlyList<int> SeriennummerIds { get; init; } = [];
}

public sealed record InventurPositionDto
{
    public int Id { get; init; }
    public int ArtikelId { get; init; }
    public string Artikelnummer { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public decimal SollMenge { get; init; }
    public decimal? IstMenge { get; init; }
    public decimal? Differenz => IstMenge is { } ist ? ist - SollMenge : null;
}

public sealed record InventurDto
{
    public int Id { get; init; }
    public string InventurNummer { get; init; } = string.Empty;
    public DateOnly Stichtag { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public int LagerortId { get; init; }
    public string LagerortKuerzel { get; init; } = string.Empty;
    public Domain.Entities.Lager.InventurStatus Status { get; init; }
    public DateTime? AbgeschlossenAm { get; init; }
    public IReadOnlyList<InventurPositionDto> Positionen { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}
```

- [ ] **Step 3: Service-Interfaces**

`src/Milet.Application/Lager/ILagerServices.cs`:
```csharp
namespace Milet.Application.Lager;

public interface IBestandService
{
    Task<IReadOnlyList<BestandDto>> SucheAsync(string? suchtext, int? lagerortId, bool nurUnterMindestbestand, CancellationToken ct = default);
    Task<IReadOnlyList<LagerbewegungDto>> LadeBewegungenAsync(int artikelId, int? lagerortId, CancellationToken ct = default);
    /// <summary>Konsistenzprüfung: vergleicht den Snapshot mit der Ledger-Summe und meldet Abweichungen (leer = konsistent).</summary>
    Task<IReadOnlyList<BestandDto>> PruefeKonsistenzAsync(CancellationToken ct = default);
}

public interface ISeriennummernService
{
    Task<IReadOnlyList<SeriennummerDto>> SucheAsync(int? artikelId, Domain.Entities.Lager.SeriennummerStatus? status, string? suchtext, CancellationToken ct = default);
    /// <summary>Manuelles Anlegen von Seriennummern für Bestandsware (bis Wareneingang in Phase 4 das übernimmt).</summary>
    Task<SeriennummerDto> SpeichereAsync(SeriennummerDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface ILieferscheinBuchenService
{
    /// <summary>
    /// Bucht den Lieferschein in einer Transaktion: Mengen erneut prüfen, Ledger schreiben,
    /// Snapshot atomar fortschreiben, Seriennummern auf Ausgeliefert setzen, Status auf Gebucht.
    /// </summary>
    Task<Verkauf.BelegDto> BuchenAsync(int lieferscheinId, CancellationToken ct = default);
}

public interface IInventurService
{
    Task<IReadOnlyList<InventurDto>> SucheAsync(string? suchtext, CancellationToken ct = default);
    Task<InventurDto> LadeAsync(int id, CancellationToken ct = default);
    /// <summary>Legt die Inventur an und friert je Artikel des Lagerorts die aktuelle Soll-Menge ein.</summary>
    Task<InventurDto> AnlegenAsync(DateOnly stichtag, int lagerortId, CancellationToken ct = default);
    /// <summary>Speichert gezählte Mengen (nur im Status Erfassung).</summary>
    Task<InventurDto> ErfasseAsync(int inventurId, IReadOnlyList<InventurPositionDto> positionen, CancellationToken ct = default);
    /// <summary>Bucht alle Differenzen als Inventurkorrektur und schließt die Inventur — eine Transaktion.</summary>
    Task<InventurDto> AbschliessenAsync(int inventurId, CancellationToken ct = default);
}
```

Die Überleitungs-Erweiterung (`LadeOffenePositionenAsync`, Auswahl-Parameter, Sammelrechnung) gehört ins Verkauf-Modul und steht in Task 5.

- [ ] **Step 4: Validatoren + Tests**

`src/Milet.Application/Lager/Validators.cs`:
- `SeriennummerValidator`: `Nummer` `NotEmpty().MaximumLength(50)`, `ArtikelId` `GreaterThan(0)`.
- `InventurPositionValidator`: `IstMenge` `GreaterThanOrEqualTo(0).When(p => p.IstMenge is not null)`.
- `UeberleitungsAuswahlValidator`: `Menge` `GreaterThan(0)`, `QuellPositionId` `GreaterThan(0)`.

`tests/Milet.Application.Tests/LagerValidatorTests.cs` — mindestens: negative `IstMenge` abgelehnt, `IstMenge = null` akzeptiert, `IstMenge = 0` akzeptiert (echte Nullzählung), leere Seriennummer abgelehnt, `Menge = 0` in der Auswahl abgelehnt.

- [ ] **Step 5: Tests + Commit**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj`

```bash
git add src/Milet.Application tests/Milet.Application.Tests
git commit -m "Application: Lager-DTOs, Service-Interfaces und Validatoren"
```

---

### Task 5: Application — Überleitungs-Interface für Teillieferung und Sammelrechnung

**Files:**
- Modify: `src/Milet.Application/Verkauf/IVerkaufServices.cs`

**Interfaces:**
- Produces: erweitertes `IBelegUeberleitungService` — von Task 9 (Implementierung) und 15/16 (UI) konsumiert.

- [ ] **Step 1: Interface erweitern**

```csharp
public interface IBelegUeberleitungService
{
    /// <summary>Offene Mengen des Quellbelegs — Datenbasis des Teillieferungs-/Teilfakturierungs-Dialogs.</summary>
    Task<IReadOnlyList<Lager.OffenePositionDto>> LadeOffenePositionenAsync(int quellBelegId, CancellationToken ct = default);

    /// <summary>
    /// Erzeugt aus <paramref name="quellBelegId"/> einen Folgebeleg vom Typ <paramref name="zielTyp"/>.
    /// <paramref name="auswahl"/> = null übernimmt alle offenen Positionen vollständig (bisheriges Verhalten);
    /// andernfalls genau die gewählten Positionen mit den gewählten Mengen (Teillieferung/Teilfakturierung).
    /// </summary>
    Task<BelegDto> UeberleitenAsync(
        int quellBelegId,
        Domain.Entities.Verkauf.BelegTyp zielTyp,
        IReadOnlyList<Lager.UeberleitungsAuswahlDto>? auswahl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fasst mehrere gebuchte Lieferscheine desselben Kunden mit identischer Zahlungsbedingung
    /// zu einer Rechnung zusammen (Sammelrechnung).
    /// </summary>
    Task<BelegDto> SammelrechnungAsync(IReadOnlyList<int> lieferscheinIds, CancellationToken ct = default);
}
```

Der Default-Wert `auswahl = null` hält alle bestehenden Aufrufstellen (`BelegEditViewModelBase.UeberleitenAsync`) quellkompatibel — Angebot→Auftrag bleibt unverändert Vollkopie.

- [ ] **Step 2: Build + Commit**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Application/Milet.Application.csproj`

```bash
git add src/Milet.Application/Verkauf/IVerkaufServices.cs
git commit -m "Application: Überleitungs-Interface um Mengenauswahl und Sammelrechnung erweitert"
```

---

### Task 6: Infrastructure — EF-Configurations, DbContext, Migration, Seed

**Files:**
- Create: `src/Milet.Infrastructure/Persistence/Configurations/LagerortConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/LagerbewegungConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/ArtikelBestandConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/SeriennummerConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/BelegPositionSeriennummerConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/InventurConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs` (Discriminator `Lieferschein`, `LagerortId`)
- Modify: `src/Milet.Infrastructure/Persistence/MiletDbContext.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs`
- Create: Migration `LagerLieferschein`

- [ ] **Step 1: Configurations**

`LagerortConfiguration`: Tabelle `Lagerorte`, `Kuerzel` `HasMaxLength(10).IsRequired()` + **unique index**, `Bezeichnung` `HasMaxLength(100).IsRequired()`. Gefilterter Unique-Index auf `IstStandard`, damit es genau einen Standard-Lagerort geben kann:
```csharp
b.HasIndex(x => x.IstStandard).IsUnique().HasFilter("[IstStandard] = 1");
```

`LagerbewegungConfiguration`: Tabelle `Lagerbewegungen`, `Menge` `HasPrecision(18, 3)`, FKs auf `Artikel`/`Lagerort`/`BelegPosition`/`Seriennummer` alle `OnDelete(DeleteBehavior.Restrict)` — eine Bewegung darf durch keinen Kaskadenlöschvorgang verschwinden. Index `(ArtikelId, LagerortId)` für die Konsistenzabfrage, Index auf `Zeitpunkt` für die Bewegungsliste.

`ArtikelBestandConfiguration`: Tabelle `ArtikelBestaende`, `Menge` `HasPrecision(18, 3)`, **Unique-Index `(ArtikelId, LagerortId)`** — trägt das Upsert aus Task 7, ohne ihn ist die Insert-Race nicht erkennbar:
```csharp
b.HasIndex(x => new { x.ArtikelId, x.LagerortId }).IsUnique();
```

`SeriennummerConfiguration`: Tabelle `Seriennummern`, `Nummer` `HasMaxLength(50).IsRequired()`, Unique-Index `(ArtikelId, Nummer)`, `RowVersion().IsRowVersion()`.

`BelegPositionSeriennummerConfiguration`: Tabelle `BelegPositionSeriennummern`, zusammengesetzter Schlüssel `HasKey(x => new { x.BelegPositionId, x.SeriennummerId })`, beide FKs `Restrict`.

`InventurConfiguration` (beide Entities in einer Datei, wie `KleinstammServices` es für Verwandtes vormacht): Tabellen `Inventuren`/`InventurPositionen`, `InventurNummer` `HasMaxLength(20).IsRequired()` + Unique-Index, `SollMenge`/`IstMenge` `HasPrecision(18, 3)`, `HasMany(x => x.Positionen).WithOne(p => p.Inventur).OnDelete(DeleteBehavior.Cascade)`, `RowVersion`.

- [ ] **Step 2: `BelegConfiguration` erweitern**

```csharp
        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung))
            .HasValue<Lieferschein>(nameof(BelegTyp.Lieferschein));
```
plus:
```csharp
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
```
Der bestehende gefilterte Unique-Index über `("BelegTyp", BelegNummer)` deckt den neuen Typ automatisch mit ab.

- [ ] **Step 3: DbContext-Sets**

```csharp
    public DbSet<Lagerort> Lagerorte => Set<Lagerort>();
    public DbSet<Milet.Domain.Entities.Verkauf.Lieferschein> Lieferscheine => Set<Milet.Domain.Entities.Verkauf.Lieferschein>();
    public DbSet<Milet.Domain.Entities.Lager.Lagerbewegung> Lagerbewegungen => Set<Milet.Domain.Entities.Lager.Lagerbewegung>();
    public DbSet<Milet.Domain.Entities.Lager.ArtikelBestand> ArtikelBestaende => Set<Milet.Domain.Entities.Lager.ArtikelBestand>();
    public DbSet<Milet.Domain.Entities.Lager.Seriennummer> Seriennummern => Set<Milet.Domain.Entities.Lager.Seriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.BelegPositionSeriennummer> BelegPositionSeriennummern => Set<Milet.Domain.Entities.Lager.BelegPositionSeriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.Inventur> Inventuren => Set<Milet.Domain.Entities.Lager.Inventur>();
    public DbSet<Milet.Domain.Entities.Lager.InventurPosition> InventurPositionen => Set<Milet.Domain.Entities.Lager.InventurPosition>();
```

- [ ] **Step 4: Seed reparieren und erweitern (siehe Vorbefund 3)**

`StammdatenSeed`: Die Nummernkreis-Sektion von „alles oder nichts" auf „je Code nachlegen" umbauen:
```csharp
        var gewuenschteKreise = new[]
        {
            new Nummernkreis { Code = "KD", NaechsteNummer = 10001, Format = "KD-{0}" },
            // … bestehende unverändert …
            new Nummernkreis { Code = "INV", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "INV-{1}-{0:0000}" },
        };

        var vorhandeneCodes = await db.Nummernkreise.Select(n => n.Code).ToListAsync(ct);
        foreach (var kreis in gewuenschteKreise.Where(k => !vorhandeneCodes.Contains(k.Code)))
        {
            db.Nummernkreise.Add(kreis);
        }
```
Zusätzlich Standard-Lagerort seeden, ebenfalls idempotent:
```csharp
        if (!await db.Lagerorte.AnyAsync(ct))
        {
            db.Lagerorte.Add(new Lagerort { Kuerzel = "HAUPT", Bezeichnung = "Hauptlager", IstStandard = true });
        }
```

- [ ] **Step 5: Migration erzeugen und anwenden**

```bash
"$USERPROFILE/.dotnet/dotnet.exe" ef migrations add LagerLieferschein \
  --project src/Milet.Infrastructure --startup-project src/Milet.Tools.Migrator
"$USERPROFILE/.dotnet/dotnet.exe" run --project src/Milet.Tools.Migrator
```
Expected: Migration legt `Lagerorte`, `Lagerbewegungen`, `ArtikelBestaende`, `Seriennummern`, `BelegPositionSeriennummern`, `Inventuren`, `InventurPositionen` an und ergänzt `Belege.LagerortId`. Generiertes Migrations-C# vor dem Anwenden lesen: keine `DropTable`/`DropColumn` auf Bestandstabellen, keine Neuanlage bestehender Indizes.

Verifikation per `sqlcmd` (Erinnerung aus STATUS.md: `SET QUOTED_IDENTIFIER ON;` voranstellen):
```sql
SELECT Kuerzel, IstStandard FROM Lagerorte;
SELECT Code, NaechsteNummer FROM Nummernkreise WHERE Code IN ('LS','INV');
```

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure
git commit -m "Infrastructure: EF-Mappings für Lager/Lieferschein/Inventur, Migration LagerLieferschein, Seed je Nummernkreis idempotent"
```

---

### Task 7: Infrastructure — atomare Bestandsfortschreibung + Ledger-Immutability

**Der Kern dieser Phase.** Alles andere ist Verdrahtung.

**Files:**
- Create: `src/Milet.Infrastructure/Services/BestandsBuchungen.cs`
- Create: `src/Milet.Infrastructure/Persistence/Interceptors/LagerbewegungImmutabilityInterceptor.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`
- Create: `tests/Milet.IntegrationTests/BestandsBuchungenTests.cs`

**Interfaces:**
- Consumes: `MiletDbContext`, `SqlTestDatabase` (Task 0).
- Produces: `BestandsBuchungen.BucheAsync` — von Task 8 (Lieferschein buchen) und 11 (Inventurabschluss) konsumiert.

- [ ] **Step 1: `BestandsBuchungen`**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

/// <summary>
/// Schreibt den <c>ArtikelBestand</c>-Snapshot fort — ausschließlich über ein atomares Delta-UPDATE
/// (PLAN.md §Lager, §Risiko 6). Kein Read-Modify-Write: die Zeile wird im UPDATE selbst gesperrt und
/// bleibt bis zum Commit der umgebenden Transaktion gesperrt, wodurch konkurrierende Buchungen auf
/// denselben Artikel/Lagerort serialisieren.
/// Muss innerhalb einer bereits geöffneten Transaktion des übergebenen Contexts aufgerufen werden.
/// </summary>
internal static class BestandsBuchungen
{
    private const int UniqueIndexVerletzung = 2601;
    private const int UniqueConstraintVerletzung = 2627;

    /// <summary>
    /// Verrechnet <paramref name="delta"/> (negativ = Abgang) und liefert den neuen Bestand.
    /// Wirft, wenn <paramref name="negativErlaubt"/> false ist und der Bestand negativ würde —
    /// die umgebende Transaktion rollt damit die gesamte Buchung zurück.
    /// </summary>
    public static async Task<decimal> BucheAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal delta, bool negativErlaubt, CancellationToken ct)
    {
        var neueMenge = await UpdateAsync(db, artikelId, lagerortId, delta, ct);

        if (neueMenge is null)
        {
            try
            {
                await db.Database.ExecuteSqlAsync(
                    $"INSERT INTO ArtikelBestaende (ArtikelId, LagerortId, Menge) VALUES ({artikelId}, {lagerortId}, {delta})",
                    ct);
                neueMenge = delta;
            }
            catch (SqlException ex) when (ex.Number is UniqueIndexVerletzung or UniqueConstraintVerletzung)
            {
                // Zwei Buchungen haben die Zeile gleichzeitig angelegt — die andere hat gewonnen,
                // jetzt existiert sie und das UPDATE greift.
                neueMenge = await UpdateAsync(db, artikelId, lagerortId, delta, ct)
                    ?? throw new InvalidOperationException(
                        $"Bestandszeile für Artikel {artikelId}/Lagerort {lagerortId} konnte nicht fortgeschrieben werden.");
            }
        }

        if (!negativErlaubt && neueMenge < 0)
        {
            throw new InvalidOperationException(
                $"Buchung würde den Bestand auf {neueMenge:0.###} senken. Negative Bestände sind nicht zulässig.");
        }

        return neueMenge.Value;
    }

    private static async Task<decimal?> UpdateAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal delta, CancellationToken ct)
    {
        var ergebnis = await db.Database.SqlQuery<decimal>(
                $"""
                 UPDATE ArtikelBestaende
                 SET Menge = Menge + {delta}
                 OUTPUT inserted.Menge AS Value
                 WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId}
                 """)
            .ToListAsync(ct);

        return ergebnis.Count == 0 ? null : ergebnis[0];
    }
}
```

Bewusst **kein** `MERGE`: SQL Server hat mit MERGE unter Nebenläufigkeit bekannte Deadlock- und Unique-Verletzungs-Fallstricke; UPDATE-dann-INSERT-mit-Retry ist das robustere Muster. Die `OUTPUT inserted.Menge`-Variante liefert den neuen Stand ohne zweiten Roundtrip und ohne Lesekonsistenz-Annahme — sie ist gleichzeitig die Grundlage der Negativsperre. `SqlQuery<decimal>` verlangt die Spaltenbenennung `Value`.

- [ ] **Step 2: `LagerbewegungImmutabilityInterceptor`**

Analog `BelegImmutabilityInterceptor`, aber für jede `Lagerbewegung`-Entry im Zustand `Modified` oder `Deleted`:
```csharp
throw new InvalidOperationException(
    "Lagerbewegungen sind unveränderlich (Append-only-Ledger). Korrekturen sind als Gegenbuchung zu erfassen.");
```

- [ ] **Step 3: DI**

In `DependencyInjection.AddInfrastructure`: `services.AddSingleton<LagerbewegungImmutabilityInterceptor>();` und in `AddInterceptors(...)` als drittes Argument ergänzen.

- [ ] **Step 4: Integrationstest — die Ledger-Invariante**

`tests/Milet.IntegrationTests/BestandsBuchungenTests.cs`, drei Tests:

1. `ParalleleAbgaenge_SnapshotBleibtGleichLedgerSumme` — Startbestand 1000, dann 50 parallele Buchungen à −1 (jede in eigener Transaktion, jede mit passender `Lagerbewegung`). Assert: `ArtikelBestand.Menge == 950` **und** `ArtikelBestand.Menge == SUM(Lagerbewegungen.Menge)`. Das ist die Invariante aus PLAN.md, an der die ganze Phase hängt.
2. `ParalleleErstbuchungen_ErzeugenGenauEineBestandszeile` — kein Startbestand, 20 parallele Zugänge à +1 auf denselben Artikel/Lagerort. Assert: genau eine Zeile in `ArtikelBestaende`, Menge 20. Deckt den Insert-Race-Pfad ab.
3. `Abgang_UnterNull_WirftUndLaesstBestandUnveraendert` — Startbestand 1, Abgang −5 mit `negativErlaubt: false`. Assert: `InvalidOperationException`, Bestand danach unverändert 1, keine Bewegung geschrieben (Rollback vollständig).

Setup über `SqlTestDatabase.StarteAsync(new LagerbewegungImmutabilityInterceptor())`.

- [ ] **Step 5: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: alle drei **Passed** (nicht Skipped — dafür war Task 0 da). Test 1 oder 2 rot heißt: die Buchungslogik ist falsch, nicht der Test flaky. Nicht mit Retry übergehen.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure tests/Milet.IntegrationTests
git commit -m "Infrastructure: atomare Bestandsfortschreibung (UPDATE/OUTPUT + Insert-Retry), Negativsperre, Ledger-Immutability"
```

---

### Task 8: Infrastructure — LieferscheinBuchenService

**Files:**
- Create: `src/Milet.Infrastructure/Services/LieferscheinBuchenService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `BestandsBuchungen` (Task 7), `ILieferscheinBuchenService` (Task 4), `BelegPosition.OffeneMenge` (bestehend).
- Produces: `LieferscheinBuchenService` — von Task 15 (UI) konsumiert.

- [ ] **Step 1: Service implementieren**

Ablauf, streng in dieser Reihenfolge, alles in **einer** Transaktion:

1. Transaktion öffnen.
2. Lieferschein mit `Positionen` laden; `NotFoundException` wenn weg.
3. Guard: `Status == Entwurf`, sonst `InvalidOperationException("Lieferschein '…' ist bereits gebucht.")`.
4. Guard: `Positionen.Count > 0`.
5. Guard: `LagerortId is not null`, sonst „Lagerort ist erforderlich."
6. **Mengenprüfung in der Transaktion wiederholen** (PLAN.md §Geschäftsprozesse 2): Für jede Position mit `UrsprungsPositionId` die Quellposition **mit `UPDLOCK` sperren** und die offene Menge inklusive aller *anderen* Folgepositionen neu berechnen. Überschreitung → `InvalidOperationException` mit Positionsnummer und Restmenge. Ohne diese Sperre ist die Prüfung wertlos (Vorbefund 1).
   ```csharp
   var quellIds = lieferschein.Positionen.Where(p => p.UrsprungsPositionId is not null)
                                         .Select(p => p.UrsprungsPositionId!.Value).Distinct().ToList();
   if (quellIds.Count > 0)
   {
       await db.Database.ExecuteSqlAsync(
           $"SELECT Id FROM BelegPositionen WITH (UPDLOCK, ROWLOCK) WHERE Id IN (SELECT value FROM STRING_SPLIT({string.Join(',', quellIds)}, ','))", ct);
   }
   ```
   (Alternative ohne `STRING_SPLIT`, wenn die Lesbarkeit leidet: je Quell-Id ein einzelnes gesperrtes `SELECT` — bei den zu erwartenden Positionszahlen unkritisch.)
7. Je Position mit `PositionsTyp == Artikel` und Artikel mit `IstLagerartikel`:
   - `Lagerbewegung` anlegen (`Menge = -position.Menge`, `Typ = Warenausgang`, `BelegPositionId`, `LagerortId`, `Zeitpunkt = DateTime.UtcNow`).
   - `await BestandsBuchungen.BucheAsync(db, artikelId, lagerortId, -menge, negativErlaubt: false, ct)`.
   - Nicht-Lagerartikel (`IstLagerartikel == false`) überspringen — Dienstleistungspositionen erzeugen keine Bewegung.
8. Seriennummern: Für jede Position eines Artikels mit `HatSeriennummern` die über `BelegPositionSeriennummern` zugeordneten Seriennummern laden. Guards: Anzahl == `Menge` (ganzzahlig), jede im Status `AufLager`. Dann `Status = Ausgeliefert`, `LagerortId = null` und je Seriennummer die zugehörige `Lagerbewegung.SeriennummerId` setzen. Fehlende/falsche Anzahl → `InvalidOperationException("Position {Nr}: {n} von {m} Seriennummern zugeordnet.")`.
9. `lieferschein.Status = BelegStatus.Gebucht`.
10. Vollständig gelieferte Quell-Aufträge: Wenn nach dieser Buchung alle Positionen des Quellauftrags offene Menge 0 haben, Quellauftrag auf `Erledigt` setzen (gleiche Regel wie in `BelegUeberleitungService`, hier aber erst beim Buchen — ein Entwurfs-Lieferschein hat noch nichts geliefert).
11. `SaveChangesAsync` + `CommitAsync`, DTO zurück.

- [ ] **Step 2: DI**

`services.AddScoped<ILieferscheinBuchenService, LieferscheinBuchenService>();`

- [ ] **Step 3: Build**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure
git commit -m "LieferscheinBuchenService: Mengenprüfung unter Sperre, Ledger, Snapshot, Seriennummern in einer Transaktion"
```

---

### Task 9: Infrastructure — Überleitung mit Mengenauswahl + Sammelrechnung

**Files:**
- Modify: `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`
- Modify: `src/Milet.Infrastructure/Services/BelegService.cs` (Lieferschein in den `switch`-Ausdrücken)
- Modify: `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs` (`Lieferschein` im Typ-`switch`)

- [ ] **Step 1: Übergangstabelle ersetzen (Vorbefund 2)**

```csharp
    private static readonly HashSet<(BelegTyp Quelle, BelegTyp Ziel)> ErlaubteUebergaenge =
    [
        (BelegTyp.Angebot, BelegTyp.Auftrag),
        (BelegTyp.Auftrag, BelegTyp.Lieferschein),
        (BelegTyp.Auftrag, BelegTyp.Rechnung),      // Dienstleistung ohne Warenbewegung
        (BelegTyp.Lieferschein, BelegTyp.Rechnung),
    ];
```
`TypVon`, `NeueInstanz` und `NummernkreisCode` je um `Lieferschein` (`"LS"`) ergänzen. Dieselben drei `switch`-Ausdrücke existieren in `BelegService` — dort identisch nachziehen.

- [ ] **Step 2: Quellbeleg sperren (Vorbefund 1)**

Erste Anweisung *innerhalb* der Transaktion, vor dem Laden:
```csharp
        // Serialisiert konkurrierende Überleitungen aus demselben Beleg. Ohne diese Sperre sehen zwei
        // parallele Teillieferungen beide dieselbe offene Menge und überliefern (READ COMMITTED).
        await db.Database.ExecuteSqlAsync(
            $"SELECT Id FROM Belege WITH (UPDLOCK, ROWLOCK) WHERE Id = {quellBelegId}", ct);
```
Den irreführenden Alt-Kommentar („Schutz gegen Race") ersetzen.

- [ ] **Step 3: `LadeOffenePositionenAsync`**

Lädt Quellbeleg + Positionen, dazu alle Folgepositionen, und liefert je Artikelposition `Menge`, `BereitsUebernommen` (Summe der Folgemengen), `OffeneMenge` und `HatSeriennummern` (aus `Artikel`). Positionen mit offener Menge 0 werden mitgeliefert, aber vom Dialog ausgegraut — der Nutzer soll sehen, was bereits geliefert ist.

- [ ] **Step 4: `UeberleitenAsync` um `auswahl` erweitern**

- `auswahl is null` → bisheriges Verhalten unverändert (alle offenen Mengen).
- `auswahl` gesetzt → nur die genannten Quellpositionen; je Position `menge = auswahl.Menge`, geprüft gegen die unter Sperre neu berechnete offene Menge. Überschreitung → `InvalidOperationException` mit Positionsnummer, offener und angeforderter Menge.
- Bei `zielTyp == Lieferschein`: `zielBeleg.LagerortId` = Standard-Lagerort (`Lagerorte.FirstOrDefault(l => l.IstStandard)`); fehlt der, `InvalidOperationException("Kein Standard-Lagerort definiert.")`.
- Bei `zielTyp == Lieferschein` und `auswahl` mit `SeriennummerIds`: `BelegPositionSeriennummern`-Zuordnungen anlegen. Prüfung auf Status `AufLager` passiert hier (früh, für gute Fehlermeldungen) **und** noch einmal beim Buchen (Task 8, verbindlich) — der Beleg kann zwischen Anlage und Buchen liegen.
- Der bestehende Vollständigkeits-Check (`quellVollstaendigUebernommen` → Quellbeleg `Erledigt`) gilt für Angebot→Auftrag weiter. Für Auftrag→Lieferschein **nicht** setzen: ein Auftrag ist erst erledigt, wenn geliefert *gebucht* wurde (Task 8, Schritt 10).

- [ ] **Step 5: `SammelrechnungAsync`**

```
1. Transaktion öffnen.
2. Alle genannten Lieferscheine mit Positionen laden; jeden per UPDLOCK sperren (aufsteigend nach Id
   sperren — feste Reihenfolge verhindert Deadlocks bei überlappenden Auswahlen).
3. Guards: mindestens einer; alle Status == Gebucht ("Nur gebuchte Lieferscheine können fakturiert werden.");
   alle gleiche KundeId; alle gleiche Zahlungsbedingung (ZielTage/SkontoTage/SkontoProzent).
4. Neue Rechnung: BelegNummer leer (erst beim Buchen), Kopf-Snapshots vom ersten Lieferschein,
   BelegDatum heute, LagerortId null.
5. Positionen: je Lieferscheinposition mit offener Menge > 0 eine Rechnungsposition mit
   UrsprungsPositionId = Lieferscheinposition.Id. Vor jeder Gruppe eine Freitextposition
   "Lieferschein {Nummer} vom {Datum}" (PositionsTyp.Freitext) — ohne diese Trennzeile ist die
   Sammelrechnung für den Kunden nicht nachvollziehbar.
6. Steuersummen und Kopfsummen über SteuerRechner (nicht selbst summieren).
7. SaveChanges + Commit, DTO zurück.
```
Beachten: Freitextpositionen dürfen nicht in `SteuerRechner.BerechneSteuersummen` einfließen — der Filter auf `PositionsTyp.Artikel` dort erledigt das bereits, aber der Validator (`BelegPositionValidator`) verlangt `Bezeichnung` `NotEmpty` und `ArtikelId` nur bei Artikelpositionen; die Trennzeilen müssen also `Menge = 0`, `Einzelpreis = 0` tragen und dürfen die `Menge > 0`-Regel nicht verletzen. **Konsequenz:** `BelegPositionValidator.Menge` muss auf `GreaterThan(0).When(p => p.PositionsTyp == PositionsTyp.Artikel)` eingeschränkt werden — sonst scheitert jede Sammelrechnung an der Validierung. Diese Änderung gehört in denselben Commit, inkl. eines Validator-Tests „Freitextposition mit Menge 0 ist gültig".

- [ ] **Step 6: Build + bestehende Tests**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build Milet.slnx` (ohne WinUI-App: `--project`-weise, App separat mit `-r win-x64`)
Run: alle drei Testprojekte einzeln.
Expected: grün; insbesondere dürfen die Phase-2-Tests (`RechnungBuchenServiceTests`, PDF-Smoke) nicht brechen.

- [ ] **Step 7: Commit**

```bash
git add src/Milet.Infrastructure src/Milet.Application tests
git commit -m "Überleitung: Mengenauswahl (Teillieferung), Auftrag->Lieferschein->Rechnung, Sammelrechnung, Quellbeleg-Sperre"
```

---

### Task 10: Infrastructure — BestandService + SeriennummernService + LagerorteService

**Files:**
- Create: `src/Milet.Infrastructure/Services/BestandService.cs`
- Create: `src/Milet.Infrastructure/Services/SeriennummernService.cs`
- Create: `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs`
- Modify: `src/Milet.Infrastructure/Services/KleinstammServices.cs` (`LagerorteService`)
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: `BestandService`**

- `SucheAsync`: Join `ArtikelBestaende` × `Artikel` × `Lagerorte`, `AsNoTracking()`, Filter über Artikelnummer/Bezeichnung (`EF.Functions.Like`), optionaler Lagerort, optional nur `Menge < Artikel.Mindestbestand`. Sortiert nach Artikelnummer, `Take(500)` wie die übrigen Listen.
- `LadeBewegungenAsync`: Bewegungen eines Artikels, absteigend nach `Zeitpunkt`, mit Belegnummer (über `BelegPosition.Beleg`) und Seriennummer, `Take(500)`.
- `PruefeKonsistenzAsync`: gruppierte Ledger-Summe je (`ArtikelId`, `LagerortId`) gegen den Snapshot; liefert nur die Abweichungen. Diese Methode ist die Laufzeit-Entsprechung des Invariantentests aus Task 7 und wird in Task 17 als Button „Bestand prüfen" angeboten. Sie **korrigiert nicht** — Ableiten des Snapshots aus dem Ledger ist ein bewusster Administrationsschritt und gehört nicht hinter einen Listen-Button.

- [ ] **Step 2: `SeriennummernService`**

Muster exakt wie `ArtikelPreiseService` in `KleinstammServices.cs`: `SucheAsync`/`SpeichereAsync`/`LoescheAsync`, Validierung über `SeriennummerValidator`, Speichern über `SaveChangesTranslatingConcurrencyAsync` (hat RowVersion), Löschen über `SaveChangesDeletingAsync`. Zusätzliche Guards:
- Speichern nur, wenn der Artikel `HatSeriennummern` — sonst `InvalidOperationException("Artikel '…' ist nicht seriennummernpflichtig.")`.
- Löschen nur im Status `AufLager` und ohne `BelegPositionSeriennummern`-Zuordnung.

- [ ] **Step 3: `LagerorteService`**

In `KleinstammServices.cs` ergänzen, 1:1 nach dem Vorbild `VersandartenService`. Zusatzregel: Beim Speichern mit `IstStandard = true` alle anderen Lagerorte auf `false` setzen (der gefilterte Unique-Index aus Task 6 würde sonst einen sperrigen DB-Fehler werfen statt der erwarteten Umschaltung).

- [ ] **Step 4: DI + Build + Commit**

```csharp
services.AddScoped<IBestandService, BestandService>();
services.AddScoped<ISeriennummernService, SeriennummernService>();
services.AddScoped<ILagerorteService, LagerorteService>();
```

```bash
git commit -m "Infrastructure: BestandService (inkl. Konsistenzprüfung), SeriennummernService, LagerorteService"
```

---

### Task 11: Infrastructure — InventurService

**Files:**
- Create: `src/Milet.Infrastructure/Services/InventurService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `InventurRechner` (Task 2), `BestandsBuchungen` (Task 7), `INumberRangeService` (Code `INV`).

- [ ] **Step 1: `AnlegenAsync`**

In einer Transaktion: Nummer über `INV`-Kreis, dann je Artikel mit `IstLagerartikel` eine `InventurPosition` mit `SollMenge` = aktueller Snapshot-Stand am gewählten Lagerort (0, wenn keine Bestandszeile existiert), `IstMenge = null`. Guard: keine zweite offene Inventur je Lagerort (`Status == Erfassung` bereits vorhanden → `InvalidOperationException`) — zwei parallele Zählungen desselben Lagers ergeben widersprüchliche Korrekturen.

- [ ] **Step 2: `ErfasseAsync`**

Guard `Status == Erfassung`. Nur `IstMenge` der übergebenen Positionen übernehmen; `SollMenge`, `ArtikelId` und Positionsbestand bleiben unangetastet (eingefroren). Validierung je Position über `InventurPositionValidator`. Speichern mit `SaveChangesTranslatingConcurrencyAsync`.

- [ ] **Step 3: `AbschliessenAsync` — eine Transaktion**

```
1. Transaktion öffnen, Inventur mit Positionen laden.
2. Guard: Status == Erfassung ("Inventur ist bereits abgeschlossen.").
3. Korrekturen = InventurRechner.BerechneKorrekturen(positionen).
4. Je Korrektur:
   - Lagerbewegung { Menge = Korrekturmenge, Typ = Inventurkorrektur,
                     InventurPositionId = …, LagerortId = inventur.LagerortId }
   - BestandsBuchungen.BucheAsync(db, artikelId, lagerortId, korrekturmenge,
                                  negativErlaubt: true, ct)
     → hier bewusst negativErlaubt: true. Eine Zählung ist die Wahrheit; sie darf nicht an
       einer Plausibilitätsregel scheitern, die für Warenausgänge sinnvoll ist.
5. Status = Abgeschlossen, AbgeschlossenAm = DateTime.UtcNow.
6. SaveChanges + Commit.
```
Nicht gezählte Positionen (`IstMenge is null`) erzeugen keine Bewegung — der Bestand bleibt, wie er ist. Das ist der Unterschied zwischen „nicht gezählt" und „null gezählt" und der Grund für Test 4 aus Task 2.

- [ ] **Step 4: DI + Build + Commit**

```bash
git commit -m "InventurService: Soll-Snapshot bei Anlage, Erfassung, Abschluss als Korrekturbuchung"
```

---

### Task 12: Infrastructure — Lieferschein-PDF (ohne Preise)

**Files:**
- Modify: `src/Milet.Infrastructure/Pdf/BelegPdfDocument.cs`
- Modify: `src/Milet.Infrastructure/Pdf/PdfService.cs`
- Modify: `tests/Milet.IntegrationTests/BelegPdfDocumentTests.cs`

- [ ] **Step 1: `BelegPdfDocument` um `mitPreisen` erweitern**

Konstruktor: `BelegPdfDocument(BelegDto beleg, FirmenstammDto firma, string dokumenttitel, bool mitPreisen = true)`.
Bei `mitPreisen == false`: Spalten „Preis", „Rabatt%", „Gesamt" und der komplette Summenblock entfallen; die Lieferadresse (`LieferadresseSnapshot`) wird statt der Rechnungsadresse gedruckt. Ein Lieferschein mit Preisen ist ein Fehler, kein Stilfrage — er landet beim Empfänger der Ware.

Zusätzlich bei Lieferschein: Spalte „Seriennummern" nur drucken, wenn mindestens eine Position welche trägt. Dafür braucht `BelegPositionDto` ein Feld `IReadOnlyList<string> Seriennummern { get; init; } = []`, das `VerkaufMapping` aus `BelegPositionSeriennummern` füllt (nur beim Laden mit Positionen).

- [ ] **Step 2: `PdfService` — Titel und Preisflag**

```csharp
        var (titel, mitPreisen) = beleg.BelegTyp switch
        {
            BelegTyp.Angebot => ("Angebot", true),
            BelegTyp.Auftrag => ("Auftragsbestätigung", true),
            BelegTyp.Rechnung => ("Rechnung", true),
            BelegTyp.Lieferschein => ("Lieferschein", false),
            _ => throw new ArgumentOutOfRangeException(nameof(belegId)),
        };
```

- [ ] **Step 3: Smoke-Test ergänzen**

In `BelegPdfDocumentTests` einen vierten Test: Lieferschein rendert ohne Exception, Ergebnis > 1 KB, und — als echte Zusicherung statt reinem Smoke — der extrahierte Dokumententext enthält **keinen** der Preiswerte. Reicht die vorhandene Testinfrastruktur für Textextraktion nicht, stattdessen gegen das Dokumentmodell assertieren (`mitPreisen: false` erzeugt Tabelle mit 3 statt 6 Spalten) — kein Pixel-Diff, wie in PLAN.md §Verifikation festgelegt.

- [ ] **Step 4: Tests + Commit**

```bash
git commit -m "PDF: Lieferschein-Dokument ohne Preise, mit Lieferadresse und Seriennummern"
```

---

### Task 13: Integrationstests — Teillieferungs-Race, Lieferschein-Buchung, Inventurabschluss

**Files:**
- Create: `tests/Milet.IntegrationTests/LieferscheinBuchenServiceTests.cs`
- Create: `tests/Milet.IntegrationTests/InventurServiceTests.cs`

Diese Tests sind der Grund, warum Task 0 zuerst kam. Sie müssen tatsächlich laufen, nicht skippen.

- [ ] **Step 1: `LieferscheinBuchenServiceTests`**

1. `Teillieferung_BuchtNurGelieferteMenge` — Auftrag über 10 Stück, Lieferschein über 4, buchen. Assert: Bestand −4, genau eine `Lagerbewegung` mit Menge −4, offene Menge der Auftragsposition = 6, Auftrag weiterhin **nicht** `Erledigt`.
2. `Restlieferung_SetztAuftragAufErledigt` — zweiter Lieferschein über 6, buchen. Assert: offene Menge 0, Auftrag `Erledigt`, Bestand −10.
3. `ZweiParalleleUeberleitungen_UeberliefernNicht` — der Kern-Race: aus demselben Auftrag (10 Stück, davon 8 offen) zwei parallele `UeberleitenAsync` mit je 5 Stück. Assert: genau eine gelingt vollständig **oder** beide zusammen übernehmen höchstens die offene Menge; in keinem Fall Summe > 8. Ohne die `UPDLOCK`-Sperre aus Task 9/Step 2 schlägt dieser Test fehl — er ist die Regression für Vorbefund 1.
4. `BuchenOhneBestand_WirftUndBuchtNichts` — Negativsperre auf Belegebene: Assert `InvalidOperationException`, Lieferschein danach weiterhin `Entwurf`, keine Bewegung, Bestand unverändert.
5. `Seriennummernpflicht_OhneZuordnung_Wirft` — Artikel mit `HatSeriennummern`, Lieferschein ohne Zuordnung → wirft; mit korrekter Anzahl → Seriennummern danach `Ausgeliefert`.
6. `GebuchterLieferschein_AenderungWirftImmutabilityFehler` — wie der bestehende Rechnungs-Test, für den neuen Subtyp.

- [ ] **Step 2: `InventurServiceTests`**

1. `Abschluss_BuchtDifferenzenUndSchliesst` — Soll 10, Ist 7 → Bewegung −3, Bestand 7, Status `Abgeschlossen`.
2. `NichtGezaehlt_ErzeugtKeineBewegung` — eine Position ohne `IstMenge` bleibt bestandswirkungsfrei.
3. `Abschluss_ZweitesMal_Wirft`.
4. `LedgerSummeGleichSnapshot_NachInventur` — die Invariante hält auch nach Korrekturbuchungen.

- [ ] **Step 3: Laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: alle Passed. Fehlschlag in Test 3 (Race) heißt Sperrlogik prüfen — nicht den Test lockern.

- [ ] **Step 4: Commit**

```bash
git commit -m "Integrationstests: Teillieferung, Überlieferungs-Race, Negativsperre, Seriennummern, Inventurabschluss"
```

---

### Task 14: App — Lieferadresse im Belegeditor editierbar

**Files:**
- Modify: `src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs`
- Modify: `src/Milet.App/Views/Verkauf/*EditPage.xaml` (4×, inkl. neuer Lieferschein-Seite aus Task 15)
- Modify: `src/Milet.Infrastructure/Services/BelegService.cs`

Der in STATUS.md notierte bewusste Phase-2-Vereinfachungsschritt („Lieferadresse immer 1:1 aus Kundenstamm") endet hier: ein Lieferschein an eine abweichende Lieferanschrift ist der Normalfall, nicht die Ausnahme.

- [ ] **Step 1: ViewModel**

Sechs Properties (`LieferadresseName1`, `Name2`, `Strasse`, `Plz`, `Ort`, `Land`) plus `[RelayCommand] LieferadresseVomKundenUebernehmen`. Beim Laden aus `BelegDto.LieferadresseSnapshot` füllen, beim Speichern zurückschreiben.

- [ ] **Step 2: `BelegService.SpeichereAsync`**

Bei `dto.Id != 0` die Lieferadresse aus dem DTO übernehmen (heute wird sie beim Update gar nicht angefasst). Bei Neuanlage weiterhin aus dem Kundenstamm vorbelegen, außer das DTO trägt bereits eine gefüllte Lieferadresse.

- [ ] **Step 3: XAML**

Ein `Expander` „Lieferadresse" unter dem Kopfbereich, innerhalb des bestehenden `ContentControl`-Wrappers, der über `IstBearbeitbar` gesperrt wird (Phase-2-Bugfix nicht rückgängig machen — siehe STATUS.md).

- [ ] **Step 4: Build + Commit**

```bash
git commit -m "Belegeditor: Lieferadresse editierbar statt fix aus dem Kundenstamm"
```

---

### Task 15: App — Lieferscheine (Liste, Editor, Teillieferungs-Dialog)

**Files:**
- Create: `src/Milet.App/ViewModels/Verkauf/LieferscheinListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Verkauf/LieferscheinEditViewModel.cs`
- Create: `src/Milet.App/Views/Verkauf/LieferscheinListPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/LieferscheinEditPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/TeillieferungDialog.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/ViewModels/Verkauf/TeillieferungViewModel.cs`
- Modify: `src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs`
- Modify: `src/Milet.App/Shell/ShellPage.xaml` + `.xaml.cs`
- Modify: `src/Milet.App/App.xaml.cs`

- [ ] **Step 1: Liste + Editor nach bestehendem Muster**

`LieferscheinListViewModel`/`LieferscheinListPage` sind eine wörtliche Kopie von `AuftragListViewModel`/`AuftragListPage` mit `BelegTyp.Lieferschein`; `LieferscheinEditViewModel` erbt `BelegEditViewModelBase` wie `AuftragEditViewModel`, mit `buchenService: null` (der Lieferschein hat einen eigenen Buchen-Service, siehe Step 3). Spalte „Lagerort" in der Liste ergänzen. Keine Abweichung vom Muster erfinden.

- [ ] **Step 2: `BelegEditViewModelBase` — Teillieferungs-Dialog vor der Überleitung**

`UeberleitenAsync` erweitern: Ist der Zieltyp `Lieferschein` oder `Rechnung` (aus Lieferschein), zuerst `LadeOffenePositionenAsync` aufrufen und den Dialog zeigen; bricht der Nutzer ab, passiert nichts. Andernfalls die Auswahl an `UeberleitenAsync(..., auswahl)` übergeben. Angebot→Auftrag bleibt ohne Dialog (Vollkopie).

`ZeigtUeberleitenButton`/`UeberleitenButtonText` um den Lieferschein erweitern:
```csharp
    public string UeberleitenButtonText => _typ switch
    {
        BelegTyp.Angebot => "→ Auftrag",
        BelegTyp.Auftrag => "→ Lieferschein",
        BelegTyp.Lieferschein => "→ Rechnung",
        _ => string.Empty,
    };
```
Auftrag→Rechnung direkt (Dienstleistung) als zweiter Button „→ Rechnung (ohne Lieferung)", nur bei `BelegTyp.Auftrag` sichtbar.

- [ ] **Step 3: Buchen-Pfad für den Lieferschein**

`BelegEditViewModelBase` kennt bisher nur `IRechnungBuchenService`. Sauberste Variante ohne Umbau der Basisklasse: ein optionales `Func<int, CancellationToken, Task<BelegDto>>`-Delegate `_buchenDelegate`, das `RechnungEditViewModel` mit `IRechnungBuchenService.BuchenAsync` und `LieferscheinEditViewModel` mit `ILieferscheinBuchenService.BuchenAsync` befüllt. `ZeigtBuchenButton` prüft dann das Delegate statt des Rechnungs-Service. Der Alternativweg (zweiter nullable Service im Basiskonstruktor) verdoppelt die Konstruktor-Parameter aller vier Editoren — nicht nehmen.

- [ ] **Step 4: `TeillieferungDialog`**

`ContentDialog` (XamlRoot wie in `DialogService`), `ListView` über `OffenePositionDto` mit editierbarer Spalte „Liefermenge" (`NumberBox` + `DecimalToDoubleConverter`, vorbelegt mit `OffeneMenge`), Zeilen mit `OffeneMenge == 0` deaktiviert. Bei seriennummernpflichtigen Artikeln eine zweite Ebene: Auswahl aus den `AufLager`-Seriennummern des Artikels (`ListView`, `SelectionMode="Multiple"`), Primärbutton erst aktiv, wenn Anzahl == Liefermenge. Rückgabe: `IReadOnlyList<UeberleitungsAuswahlDto>`.

**Achtung UI-Automation** (STATUS.md, Phase-1-Abnahme): `NumberBox` mit `x:Bind TwoWay` committet erst bei echtem Fokusverlust. Im Dialog vor dem Primärbutton-Handler explizit `Focus()` auf ein anderes Element setzen, sonst geht die zuletzt getippte Menge verloren — bei Mausbedienung unauffällig, bei Tastatur/Automation ein Datenverlust.

- [ ] **Step 5: Navigation + DI**

`ShellPage.xaml`: Menüpunkt „Lieferscheine" (Tag `lieferscheine`, Icon `Send`) im Verkauf-Untermenü zwischen Aufträgen und Rechnungen. `ShellPage.xaml.cs`: zwei `Register<…>`-Aufrufe + `case "lieferscheine"`. `App.xaml.cs`: beide neuen ViewModels als `AddTransient`.

- [ ] **Step 6: Build + Commit**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -r win-x64`

```bash
git commit -m "App: Lieferschein-Liste/-Editor, Teillieferungs-Dialog, Buchen-Pfad für Lieferscheine"
```

---

### Task 16: App — Sammelrechnung aus der Lieferschein-Liste

**Files:**
- Modify: `src/Milet.App/ViewModels/Verkauf/LieferscheinListViewModel.cs`
- Modify: `src/Milet.App/Views/Verkauf/LieferscheinListPage.xaml`

- [ ] **Step 1: Mehrfachauswahl**

`ListView.SelectionMode="Multiple"`. Da `x:Bind` `SelectedItems` nicht TwoWay bindet, das `SelectionChanged`-Event im Code-behind an eine `ObservableCollection<BelegDto> Ausgewaehlte` des ViewModels weiterreichen — der etablierte WinUI-Weg; keine Behelfskonstruktion mit Checkbox-Spalte.

- [ ] **Step 2: Command**

`[RelayCommand] SammelrechnungAsync`: Guards clientseitig für gute Meldungen (mindestens 2 Belege, alle `Gebucht`, alle gleicher Kunde), dann `SammelrechnungAsync(ids)`, danach zur Rechnungsliste navigieren. Serverseitige Guards bleiben verbindlich (Task 9) — die UI-Prüfung ersetzt sie nicht.

- [ ] **Step 3: Build + Commit**

```bash
git commit -m "App: Sammelrechnung aus mehreren gebuchten Lieferscheinen"
```

---

### Task 17: App — Lager-Menü (Bestand, Seriennummern, Inventur) + Lagerorte-Tab

**Files:**
- Create: `src/Milet.App/ViewModels/Lager/BestandViewModel.cs`
- Create: `src/Milet.App/ViewModels/Lager/SeriennummernViewModel.cs`
- Create: `src/Milet.App/ViewModels/Lager/InventurViewModel.cs`
- Create: `src/Milet.App/Views/Lager/BestandPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Lager/SeriennummernPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Lager/InventurPage.xaml` (+ `.xaml.cs`)
- Modify: `src/Milet.App/Views/Stammdaten/KleinstammPage.xaml` + `KleinstammViewModel.cs` (6. Pivot-Tab „Lagerorte")
- Modify: `src/Milet.App/Shell/ShellPage.xaml` + `.xaml.cs`, `src/Milet.App/App.xaml.cs`

- [ ] **Step 1: Bestandsübersicht**

Liste nach dem Muster `ArtikelListPage`: Suchfeld, Lagerort-`ComboBox`, `CheckBox` „nur unter Mindestbestand", Spalten Artikelnummer/Bezeichnung/Lagerort/Menge/Einheit/Mindestbestand. Zeilen unter Mindestbestand farblich hervorheben (`UnterMindestbestand` → Converter auf `Foreground`). Zweiter Bereich: Bewegungen des gewählten Artikels (`LadeBewegungenAsync`). Button „Bestand prüfen" ruft `PruefeKonsistenzAsync` und zeigt entweder „Snapshot und Ledger stimmen überein" oder die Abweichungsliste.

- [ ] **Step 2: Seriennummern**

Master-Detail wie `KleinstammPage`-Tabs: Liste (Filter Artikel/Status/Suchtext) + Formular (Artikel-`ComboBox`, Nummer, Lagerort). **`ArtikelId` als `int?`** und Reset auf `null`, nie auf `0` — das ist exakt der Absturz, der in Phase 1 bei den Staffelpreisen auftrat (`Selector.SelectedValue` mit `SelectedValuePath="Id"` und nicht existierender Id 0 → `NullReferenceException` im generierten Binding-Code). Ebenso: Auswahl **vor** dem Neuladen der Liste zurücksetzen.

- [ ] **Step 3: Inventur**

Liste der Inventuren + Detail: „Neu" (Stichtag + Lagerort → `AnlegenAsync`), Positionsgitter mit editierbarer `IstMenge` und Differenzspalte, Buttons „Zählung speichern" (`ErfasseAsync`) und „Abschließen" (`AbschliessenAsync`, mit Bestätigungsdialog inkl. Anzahl der zu buchenden Differenzen). Nach Abschluss ist alles schreibgeschützt (`Status != Erfassung` → `IsEnabled=false` am Wrapper-`ContentControl`, nicht am äußeren `ScrollViewer` — Phase-2-Lehre).

- [ ] **Step 4: Lagerorte-Tab in den Einstellungen**

Sechster Pivot-Tab in `KleinstammPage.xaml`, gleiche 3-Spalten-Geometrie wie die bestehenden fünf (`380` / `360` / `*` — die Layout-Korrektur aus Phase 1 nicht wieder aufbrechen), `KleinstammViewModel` um den Abschnitt erweitern.

- [ ] **Step 5: Menü aktivieren**

In `ShellPage.xaml` `<NavigationViewItem Content="Lager" Tag="lager" Icon="Library" IsEnabled="False" />` durch ein Untermenü ersetzen (Bestand / Seriennummern / Inventur), `IsEnabled="False"` entfernen; Registrierungen und `case`-Zweige nachziehen; drei `AddTransient` in `App.xaml.cs`.

- [ ] **Step 6: Build + Commit**

```bash
git commit -m "App: Lager-Menü mit Bestandsübersicht, Seriennummern und Inventur; Lagerorte in den Einstellungen"
```

---

### Task 18: Live-UI-Abnahme + Dokumentation

Nach dem Muster der Phase-1-/Phase-2-Abnahmen: per UIAutomation gegen die laufende App, jedes Ergebnis per `sqlcmd` gegen die LocalDB gegengeprüft (nicht nur „hat nicht abgestürzt"). Erinnerung aus STATUS.md: `SET QUOTED_IDENTIFIER ON;` vor `DELETE`/`UPDATE` auf `Belege`; deutsche Locale erwartet **Komma** als Dezimaltrennzeichen in `NumberBox`.

- [ ] **Step 1: Vorbereitung**

Migrator laufen lassen, Standard-Lagerort und `INV`-Nummernkreis in der DB verifizieren. Testartikel mit `IstLagerartikel = true`, Mindestbestand setzen; Anfangsbestand über eine abgeschlossene Inventur erzeugen (das ist gleichzeitig der erste Testfall und der einzige Weg, ohne Wareneingang Bestand aufzubauen).

- [ ] **Step 2: Durchstich**

1. Inventur anlegen → `IstMenge = 100` erfassen → abschließen. Prüfen: `ArtikelBestaende.Menge = 100`, eine `Lagerbewegung` Typ `Inventurkorrektur`.
2. Auftrag über 10 Stück anlegen und speichern.
3. „→ Lieferschein", im Dialog Menge auf 4 reduzieren. Prüfen: Lieferschein `LS-2026-000x`, 4 Stück, Auftrag noch nicht `Erledigt`.
4. Lieferschein buchen. Prüfen: Bestand 96, eine Bewegung −4, Lieferschein `Gebucht`, PDF ohne Preise (öffnen und ansehen, nicht nur „Dialog kam").
5. Zweiten Lieferschein über die Restmenge 6, buchen. Prüfen: Bestand 90, Auftrag `Erledigt`.
6. Beide Lieferscheine in der Liste markieren → „→ Sammelrechnung". Prüfen: eine Rechnung mit zwei Trennzeilen und 10 Stück in Summe, Summen korrekt.
7. Rechnung buchen. Prüfen: `RE-2026-000x`, Offener Posten mit Betrag = Bruttosumme.
8. Negativsperre: Lieferschein über mehr als den Bestand → Buchen muss mit klarer Meldung scheitern, Bestand unverändert.
9. Bestandsübersicht: Mindestbestand-Hervorhebung sichtbar, „Bestand prüfen" meldet Konsistenz.
10. Seriennummernpflichtiger Artikel: zwei Seriennummern erfassen, Lieferschein über 2 mit Pick, buchen → beide `Ausgeliefert`, auf dem PDF gedruckt.

- [ ] **Step 3: Testdaten entfernen**

Alle in der Abnahme angelegten Belege, Bewegungen, Bestände, Seriennummern und Inventuren wieder löschen (Reihenfolge: Bewegungen → Bestände → Belegpositionen/Belege → Stammdaten). Gebuchte Belege lassen sich fachlich nicht löschen — für die Abnahme direkt per `sqlcmd` entfernen und im Commit vermerken, dass es sich um Testdaten handelte.

- [ ] **Step 4: STATUS.md und PLAN.md fortschreiben**

`STATUS.md`: Abschnitt „Phase 3 — Lager+Lieferschein ✅" mit Domain/Application/Infrastructure/App-Gliederung wie bei Phase 2, den gefundenen Bugs (mindestens: Race in der Überleitung, nicht-idempotenter Nummernkreis-Seed) und dem Verifikationsstand. Unter „Offen" Phase 3 streichen. Unter „Bekannte Risiken" den Docker-Punkt aktualisieren: Integrationstests laufen jetzt per LocalDB-Fallback tatsächlich.
`PLAN.md`: „Stand"-Zeile am Ende auf Phase 4 als nächsten Schritt setzen.

- [ ] **Step 5: Commit**

```bash
git commit -m "Phase 3 (Lager+Lieferschein) live abgenommen: Teillieferung, Sammelrechnung, Inventur, Seriennummern"
```

---

## Self-Review (Spec-Abdeckung gegen PLAN.md §„3 Lager+Lieferschein")

| Spec-Punkt (PLAN.md) | Abgedeckt in |
|---|---|
| `Lagerbewegung` append-only mit Typ, BelegPositionId, SeriennummerId, Zeitpunkt, Benutzer | Task 1 (Entity), Task 7 (Interceptor), `AuditableEntity` liefert Benutzer |
| `ArtikelBestand`-Snapshot, Update in derselben Transaktion via atomarem Delta-UPDATE | Task 7 (`BestandsBuchungen`), Task 8/11 (Aufrufer) |
| Kein Read-Modify-Write-Race | Task 7 Step 1 + Integrationstest 1/2 |
| Konsistenzjob leitet Snapshot bei Bedarf aus Ledger neu ab | Task 10 (`PruefeKonsistenzAsync`) — **meldet** Abweichungen; das automatische Neu-Ableiten ist bewusst nicht implementiert (Administrationsaufgabe, kein Listen-Button) |
| Teillieferung mit offenen Mengen | Task 5/9 (Auswahl), Task 15 (Dialog), Task 13 Test 1/2 |
| Offene-Mengen-Prüfung in der Transaktion wiederholen | Task 8 Schritt 6 + Task 9 Step 2 (Sperre) + Task 13 Test 3 |
| Bestandsabbuchung beim Buchen des Lieferscheins | Task 8 |
| Negativsperre | Task 7 (`negativErlaubt`), Task 13 Test 4 |
| Sammelrechnung (mehrere Lieferscheine, gleicher Kunde/Zahlungsbedingung) | Task 9 Step 5, Task 16 |
| `Seriennummer` (AufLager/Ausgeliefert/Retourniert), Junction beim Lieferschein | Task 1, Task 8 Schritt 8, Task 10, Task 15 Step 4, Task 17 Step 2 |
| `Inventur` + `InventurPosition`, SollMenge eingefroren, Abschluss bucht Differenzen | Task 2, Task 11, Task 17 Step 3, Task 13 |
| Bestandsübersicht | Task 17 Step 1 |
| Testbar am Ende: „Teillieferung korrekt; Ledger=Snapshot (Integrationstest); Negativsperre" | Task 13 (alle drei), lauffähig gemacht durch Task 0 |

**Bewusst nicht in Phase 3:**
- **Retoure/Warenrücknahme** (`SeriennummerStatus.Retourniert` existiert, wird aber erst mit der Gutschrift in Phase 5 gesetzt) — PLAN.md ordnet die Gutschrift Phase 5 zu.
- **Umlagerung zwischen Lagerorten** — in PLAN.md nicht gefordert; das Datenmodell trägt sie (zwei Bewegungen), die UI kommt bei Bedarf später.
- **Lagerbewertung / Durchschnittspreise** — gehört zu Finanzen/Reporting, nicht zur Mengenführung.
- **Automatisches Neu-Ableiten des Snapshots aus dem Ledger** — siehe Tabelle oben.
- **Wareneingang** — Phase 4. Konsequenz für Phase 3: Bestand entsteht ausschließlich über die Inventur (Task 18 Step 1 nutzt genau das). Das ist umständlich, aber ehrlich; ein „Bestand direkt setzen"-Feld wäre eine Hintertür am Ledger vorbei und würde die Invariante dieser Phase untergraben.
