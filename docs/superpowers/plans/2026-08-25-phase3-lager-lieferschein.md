# Phase 3 „Lager+Lieferschein" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Append-only Lagerbewegungs-Ledger + Bestands-Snapshot, Lieferschein als neuer Beleg-Subtyp (TPH) mit Teillieferung aus dem Auftrag, atomare Bestandsabbuchung beim Buchen (inkl. Seriennummern-Pick), Sammelrechnung aus mehreren Lieferscheinen, Bestandsübersicht mit manueller Korrekturbuchung, und Inventur (Anlegen/Erfassen/Abschließen mit Korrekturbuchungen) — Auftrag→Lieferschein(Teillieferung)→Sammelrechnung End-to-End im UI durchklickbar, Negativbestand hart gesperrt.

**Architektur:** `Lieferschein` wird dünner `Beleg`-Subtyp (TPH, wie `Angebot`/`Auftrag`/`Rechnung` aus Phase 2) — Kopf/Positionen/Nummernkreis/Immutability-Interceptor werden generisch mitgenutzt, keine Parallelstruktur. Bestandsführung ist strikt getrennt vom Beleg-Modell: `Lagerbewegung` (append-only, nie geändert) ist die Quelle der Wahrheit, `ArtikelBestand` (ArtikelId+LagerortId) ein Snapshot, der ausschließlich über ein atomares `UPDATE ... SET Menge = Menge + @delta WHERE ... Menge + @delta >= 0` fortgeschrieben wird (ein SQL-Round-Trip, kein Read-Modify-Write, `betroffeneZeilen == 0` ⇒ Negativsperre). Diese eine Methode (`BestandService.BucheBewegungAsync`, `internal static`) ist der einzige Schreibpfad auf Bestand und wird von Bestandskorrektur, Lieferschein-Buchen und Inventur-Abschluss gemeinsam genutzt (DRY, ein Ort für die Race-Sicherheit). Teillieferung/Sammelrechnung nutzen das bestehende `UrsprungsPositionId`/`OffeneMenge`-Muster aus Phase 2, erweitert um eine explizite Mengenauswahl (`UeberleitenMitAuswahlAsync`) statt der bisherigen Immer-alles-Übernahme, und um einen Mehrfachquellen-Pfad (`UeberleitenMehrereAsync`) für die Sammelrechnung.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server/LocalDB), FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, xUnit v3, Testcontainers.MsSql. Kein neues Paket nötig (alles bereits aus Phase 0–2 vorhanden).

**Spec:** `d:\Projects\Milet\PLAN.md` (Abschnitte „Datenmodell (Kern)" → „Lager: Append-only-Ledger + Snapshot", „Geschäftsprozesse" Punkte 2+3 (Teillieferung, Sammelrechnung), Phasen-Tabelle Zeile „3 Lager+Lieferschein"). Konventionen recherchiert aus bestehendem Phase-1/2-Code (`KleinstammServices`, `BelegService`, `BelegUeberleitungService`, `RechnungBuchenService`, `NumberRangeService`, `BelegEditViewModelBase`) — jede Abweichung davon ist unten explizit begründet.

## Global Constraints

- Neue Aggregate Roots (`Lagerort`, `Seriennummer`, `Inventur`, `Lieferschein` via `Beleg`, `ArtikelBestand`): `IHasRowVersion` (`byte[] RowVersion`), `Lagerort`/`Seriennummer`/`Inventur` zusätzlich `AuditableEntity`. Kind-Entities ohne eigenes RowVersion (hängen an ihrem Parent, wie `BelegPosition`/`BelegSteuerSumme` in Phase 2): `InventurPosition`, `BelegPositionSeriennummer`. **Ausnahme laut PLAN.md-Spec:** `Lagerbewegung` ist bewusst append-only und trägt eigene `Zeitpunkt`/`BenutzerId`-Felder statt `AuditableEntity` (nie geändert, also keine `GeaendertAm/Von`-Semantik nötig) und **kein** RowVersion (nie per EF-Update angefasst, nur `Add`).
- Jede Service-Methode öffnet eigenen `IDbContextFactory<MiletDbContext>`-Context; Reads `AsNoTracking()`; Speichern nutzt `SaveChangesTranslatingConcurrencyAsync`/`SaveChangesDeletingAsync` (bestehende Extensions in `ConcurrencyHelper.cs`) wo Concurrency/FK-Konflikte auftreten können.
- DTOs: `sealed record` mit `init`-Properties (oder positional record für reine Read-DTOs wie in Phase 2 bei `ArtikelVerkaufLookupDto`), alle DTOs eines Moduls in einer `Dtos.cs`, alle Validatoren in einer `Validators.cs`, alle Service-Interfaces in einer `I<Modul>Services.cs` — exakt wie `Stammdaten`/`Verkauf`-Module.
- Decimal-Präzisionen (verbindlich): `Menge`-Felder (`Lagerbewegung.Menge`, `ArtikelBestand.Menge`, `InventurPosition.SollMenge/IstMenge`) `decimal(18,3)` (gleiche Präzision wie `BelegPosition.Menge`).
- Rundung: nicht relevant für Lagerbuchungen (keine Geldbeträge) — nur `BelegPosition.LagerortId`-Erweiterung bringt keine neue Rundungslogik.
- Lieferschein-Nummer wird **beim ersten Speichern** vergeben (wie Angebot/Auftrag) — anders als Rechnung, die erst beim Buchen ihre Nummer bekommt (PLAN.md §Status-Workflow gilt weiterhin nur für Rechnung). Der Nummernkreis `"LS"` existiert bereits in `StammdatenSeed.cs` (aus Phase 2 vorausschauend angelegt, hier zum ersten Mal genutzt).
- `Lieferschein` ist ein `Beleg`-Subtyp (TPH) — die bestehende Immutability-Sperre (`BelegImmutabilityInterceptor`, prüft `EntityEntry<Beleg>`) greift automatisch für gebuchte Lieferscheine, ohne Änderung am Interceptor.
- Bestandsabbuchung ist **race-sicher per atomarem SQL-UPDATE**, nie Read-Modify-Write; Negativbestand ist hart gesperrt (wirft `InvalidOperationException`), kein Override in v1 (anders als die für Phase 4 vorgesehene "weiche" EK-Preis-Abweichungswarnung bei Wareneingang — nicht verwechseln).
- `dotnet` explizit über `%USERPROFILE%\.dotnet\dotnet.exe` aufrufen (PATH zeigt auf leere Install). Jedes Testprojekt einzeln ausführen (MTP-Modus).
- Migrationen ausschließlich über `Milet.Tools.Migrator` anwenden.
- Deutsche Bezeichner für alles Fachliche, englische für rein technische Infrastruktur — wie bisher.
- **Bewusst außerhalb dieses Plans (spätere Phase):** Lieferschein-PDF (Phase-3-Abnahmekriterien in PLAN.md verlangen es nicht; QuestPDF-Erweiterung ist Folgearbeit), Wareneingang/`LagerbewegungTyp.Wareneingang` (Phase 4 — Enum bekommt den Wert erst dort, analog zum in Phase 2 dokumentierten Vorgehen bei `LieferantId`), Mehrsprachigkeit von Lagerort-Auswahl pro Position (v1: **ein** Lagerort pro Lieferschein, am Auftrag-Überleitungsdialog gewählt, nicht pro Position editierbar).

---

### Task 1: Domain — Lagerort, Lagerbewegung, ArtikelBestand + LagerbewegungTyp

**Files:**
- Create: `src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs`
- Create: `src/Milet.Domain/Entities/Lager/Lagerort.cs`
- Create: `src/Milet.Domain/Entities/Lager/Lagerbewegung.cs`
- Create: `src/Milet.Domain/Entities/Lager/ArtikelBestand.cs`

**Interfaces:**
- Consumes: `AuditableEntity`, `IHasRowVersion` (`src/Milet.Domain/Common/`), `Artikel` (`src/Milet.Domain/Entities/Stammdaten/Artikel.cs`).
- Produces: `Lagerort`, `Lagerbewegung`, `ArtikelBestand`, `LagerbewegungTyp` — von Task 6 (EF-Configurations), Task 9 (`BestandService`), Task 12 (`LieferscheinBuchenService`), Task 13 (`InventurService`) konsumiert.

- [ ] **Step 1: `LagerbewegungTyp` anlegen**

`src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs`:
```csharp
namespace Milet.Domain.Entities.Lager;

/// <summary>Wareneingang folgt erst in Phase 4 — hier bewusst noch nicht als Wert angelegt (analog LieferantId in Phase 2).</summary>
public enum LagerbewegungTyp
{
    Korrektur = 0,
    Lieferung = 1,
    InventurKorrektur = 2,
}
```

- [ ] **Step 2: `Lagerort`**

`src/Milet.Domain/Entities/Lager/Lagerort.cs`:
```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Lagerort : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 3: `Lagerbewegung` (append-only)**

`src/Milet.Domain/Entities/Lager/Lagerbewegung.cs`:
```csharp
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Entities.Lager;

/// <summary>Append-only Ledger — wird nie geändert, nur eingefügt. Quelle der Wahrheit für Bestand.</summary>
public class Lagerbewegung
{
    public int Id { get; set; }

    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }

    public LagerbewegungTyp Typ { get; set; }

    /// <summary>Signiert: positiv = Zugang, negativ = Abgang.</summary>
    public decimal Menge { get; set; }

    public int? BelegPositionId { get; set; }
    public BelegPosition? BelegPosition { get; set; }

    public int? SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }

    public DateTime Zeitpunkt { get; set; }
    public int? BenutzerId { get; set; }
}
```

- [ ] **Step 4: `ArtikelBestand` (Snapshot)**

`src/Milet.Domain/Entities/Lager/ArtikelBestand.cs`:
```csharp
using Milet.Domain.Common;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

/// <summary>Snapshot je Artikel+Lagerort — wird ausschließlich über ein atomares SQL-UPDATE fortgeschrieben, nie per Read-Modify-Write.</summary>
public class ArtikelBestand : IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }
    public decimal Menge { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs src/Milet.Domain/Entities/Lager/Lagerort.cs src/Milet.Domain/Entities/Lager/Lagerbewegung.cs src/Milet.Domain/Entities/Lager/ArtikelBestand.cs
git commit -m "Lager-Grundentities: Lagerort, Lagerbewegung (Ledger), ArtikelBestand (Snapshot)"
```

---

### Task 2: Domain — Seriennummer, BelegPositionSeriennummer, Inventur, InventurPosition

**Files:**
- Create: `src/Milet.Domain/Entities/Lager/SeriennummerStatus.cs`
- Create: `src/Milet.Domain/Entities/Lager/Seriennummer.cs`
- Create: `src/Milet.Domain/Entities/Lager/BelegPositionSeriennummer.cs`
- Create: `src/Milet.Domain/Entities/Lager/InventurStatus.cs`
- Create: `src/Milet.Domain/Entities/Lager/Inventur.cs`
- Create: `src/Milet.Domain/Entities/Lager/InventurPosition.cs`

**Interfaces:**
- Consumes: `AuditableEntity`, `IHasRowVersion`, `Artikel`, `Lagerort` (Task 1), `BelegPosition` (`src/Milet.Domain/Entities/Verkauf/BelegPosition.cs`).
- Produces: `Seriennummer`, `SeriennummerStatus`, `BelegPositionSeriennummer`, `Inventur`, `InventurStatus`, `InventurPosition` — von Task 6 (EF-Configurations), Task 11 (`SeriennummernService`), Task 12 (`LieferscheinBuchenService`), Task 13 (`InventurService`) konsumiert.

- [ ] **Step 1: `SeriennummerStatus` + `Seriennummer`**

`src/Milet.Domain/Entities/Lager/SeriennummerStatus.cs`:
```csharp
namespace Milet.Domain.Entities.Lager;

public enum SeriennummerStatus
{
    AufLager = 0,
    Ausgeliefert = 1,
    Retourniert = 2,
}
```

`src/Milet.Domain/Entities/Lager/Seriennummer.cs`:
```csharp
using Milet.Domain.Common;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

public class Seriennummer : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public string Nummer { get; set; } = string.Empty;
    public SeriennummerStatus Status { get; set; } = SeriennummerStatus.AufLager;

    /// <summary>Nur gesetzt, solange Status == AufLager.</summary>
    public int? LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 2: `BelegPositionSeriennummer` (Junction, Lieferschein-Position ↔ ausgelieferte Seriennummer)**

`src/Milet.Domain/Entities/Lager/BelegPositionSeriennummer.cs`:
```csharp
using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Entities.Lager;

public class BelegPositionSeriennummer
{
    public int Id { get; set; }
    public int BelegPositionId { get; set; }
    public BelegPosition? BelegPosition { get; set; }
    public int SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }
}
```

- [ ] **Step 3: `InventurStatus` + `Inventur` + `InventurPosition`**

`src/Milet.Domain/Entities/Lager/InventurStatus.cs`:
```csharp
namespace Milet.Domain.Entities.Lager;

public enum InventurStatus
{
    Offen = 0,
    Abgeschlossen = 1,
}
```

`src/Milet.Domain/Entities/Lager/Inventur.cs`:
```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Lager;

public class Inventur : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int LagerortId { get; set; }
    public Lagerort? Lagerort { get; set; }
    public DateOnly Datum { get; set; }
    public InventurStatus Status { get; set; } = InventurStatus.Offen;
    public List<InventurPosition> Positionen { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
```

`src/Milet.Domain/Entities/Lager/InventurPosition.cs`:
```csharp
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Domain.Entities.Lager;

public class InventurPosition
{
    public int Id { get; set; }
    public int InventurId { get; set; }
    public Inventur? Inventur { get; set; }
    public int ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    /// <summary>Eingefroren beim Anlegen der Inventur (aktueller ArtikelBestand.Menge zu diesem Zeitpunkt).</summary>
    public decimal SollMenge { get; set; }

    /// <summary>Null solange nicht gezählt.</summary>
    public decimal? IstMenge { get; set; }
}
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Entities/Lager/SeriennummerStatus.cs src/Milet.Domain/Entities/Lager/Seriennummer.cs src/Milet.Domain/Entities/Lager/BelegPositionSeriennummer.cs src/Milet.Domain/Entities/Lager/InventurStatus.cs src/Milet.Domain/Entities/Lager/Inventur.cs src/Milet.Domain/Entities/Lager/InventurPosition.cs
git commit -m "Seriennummer-Tracking + Inventur-Domainmodell"
```

---

### Task 3: Domain — Lieferschein als Beleg-Subtyp + BelegPosition.LagerortId

**Files:**
- Modify: `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Lieferschein.cs`
- Modify: `src/Milet.Domain/Entities/Verkauf/BelegPosition.cs`

**Interfaces:**
- Consumes: `Beleg`, `BelegTyp`, `BelegPosition` (Phase 2), `Lagerort` (Task 1).
- Produces: `BelegTyp.Lieferschein`, `Lieferschein : Beleg`, `BelegPosition.LagerortId` — von Task 6 (TPH-Discriminator, EF-Config), Task 7 (Beleg-Switches), Task 12 (`LieferscheinBuchenService`), Task 16/17 (UI) konsumiert.

- [ ] **Step 1: `BelegTyp` um `Lieferschein` erweitern**

Modify `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs` — vollständiger neuer Inhalt:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public enum BelegTyp
{
    Angebot = 0,
    Auftrag = 1,
    Rechnung = 2,
    Lieferschein = 3,
}
```

- [ ] **Step 2: `Lieferschein`-Subklasse (dünn, wie `Angebot`/`Auftrag`/`Rechnung`)**

`src/Milet.Domain/Entities/Verkauf/Lieferschein.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Lieferschein : Beleg;
```

- [ ] **Step 3: `BelegPosition.LagerortId` ergänzen**

Modify `src/Milet.Domain/Entities/Verkauf/BelegPosition.cs` — nach der Zeile `public int? SteuerSchluessel { get; set; }` einfügen:
```csharp
    /// <summary>Nur bei Lieferschein-Positionen gesetzt — Ziel-Lagerort für die Bestandsabbuchung beim Buchen.</summary>
    public int? LagerortId { get; set; }
    public Domain.Entities.Lager.Lagerort? Lagerort { get; set; }
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Entities/Verkauf/BelegTyp.cs src/Milet.Domain/Entities/Verkauf/Lieferschein.cs src/Milet.Domain/Entities/Verkauf/BelegPosition.cs
git commit -m "Lieferschein als Beleg-TPH-Subtyp + BelegPosition.LagerortId"
```

---

### Task 4: Application — Lager-DTOs + Validatoren + Tests

**Files:**
- Create: `src/Milet.Application/Lager/Dtos.cs`
- Create: `src/Milet.Application/Lager/Validators.cs`
- Test: `tests/Milet.Application.Tests/LagerValidatorTests.cs`

**Interfaces:**
- Consumes: `SeriennummerStatus`, `InventurStatus` (`Milet.Domain.Entities.Lager`, Task 1/2).
- Produces: `LagerortDto`, `ArtikelBestandDto`, `BestandskorrekturDto`, `SeriennummerDto`, `InventurPositionDto`, `InventurDto`, `LagerortValidator`, `BestandskorrekturValidator` — von Task 5 (Service-Interfaces), Task 10/11/13 (Infrastructure-Services), Task 14/15/18 (UI) konsumiert.

- [ ] **Step 1: DTOs**

`src/Milet.Application/Lager/Dtos.cs`:
```csharp
using Milet.Domain.Entities.Lager;

namespace Milet.Application.Lager;

public sealed record LagerortDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Bezeichnung { get; init; } = string.Empty;
    public bool Aktiv { get; init; } = true;
    public byte[] RowVersion { get; init; } = [];
}

public sealed record ArtikelBestandDto(
    int ArtikelId,
    string Artikelnummer,
    string ArtikelBezeichnung,
    bool HatSeriennummern,
    int LagerortId,
    string LagerortBezeichnung,
    decimal Menge,
    decimal? Mindestbestand);

public sealed record BestandskorrekturDto
{
    public int ArtikelId { get; init; }
    public int LagerortId { get; init; }
    public decimal MengeDelta { get; init; }
    public string Grund { get; init; } = string.Empty;
}

public sealed record SeriennummerDto(int Id, int ArtikelId, string Nummer, SeriennummerStatus Status, int? LagerortId);

public sealed record InventurPositionDto(int Id, int ArtikelId, string Artikelnummer, string ArtikelBezeichnung, decimal SollMenge, decimal? IstMenge);

public sealed record InventurDto
{
    public int Id { get; init; }
    public int LagerortId { get; init; }
    public string LagerortBezeichnung { get; init; } = string.Empty;
    public DateOnly Datum { get; init; }
    public InventurStatus Status { get; init; } = InventurStatus.Offen;
    public IReadOnlyList<InventurPositionDto> Positionen { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}
```

- [ ] **Step 2: Validatoren**

`src/Milet.Application/Lager/Validators.cs`:
```csharp
using FluentValidation;

namespace Milet.Application.Lager;

public sealed class LagerortValidator : AbstractValidator<LagerortDto>
{
    public LagerortValidator()
    {
        RuleFor(l => l.Code).NotEmpty().MaximumLength(10);
        RuleFor(l => l.Bezeichnung).NotEmpty().MaximumLength(100);
    }
}

public sealed class BestandskorrekturValidator : AbstractValidator<BestandskorrekturDto>
{
    public BestandskorrekturValidator()
    {
        RuleFor(k => k.ArtikelId).GreaterThan(0);
        RuleFor(k => k.LagerortId).GreaterThan(0);
        RuleFor(k => k.MengeDelta).NotEqual(0m).WithMessage("Mengenänderung darf nicht 0 sein.");
        RuleFor(k => k.Grund).NotEmpty().MaximumLength(200);
    }
}
```

- [ ] **Step 3: Validator-Tests**

`tests/Milet.Application.Tests/LagerValidatorTests.cs`:
```csharp
using Milet.Application.Lager;

namespace Milet.Application.Tests;

public class LagerValidatorTests
{
    [Fact]
    public void Lagerort_OhneCode_Fehler()
    {
        var dto = new LagerortDto { Code = "", Bezeichnung = "Hauptlager" };
        Assert.False(new LagerortValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Lagerort_GueltigeDaten_KeinFehler()
    {
        var dto = new LagerortDto { Code = "HL", Bezeichnung = "Hauptlager" };
        Assert.True(new LagerortValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_MengeDeltaNull_Fehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 0, Grund = "Inventur" };
        Assert.False(new BestandskorrekturValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_OhneGrund_Fehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 5, Grund = "" };
        Assert.False(new BestandskorrekturValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Bestandskorrektur_GueltigeDaten_KeinFehler()
    {
        var dto = new BestandskorrekturDto { ArtikelId = 1, LagerortId = 1, MengeDelta = 10, Grund = "Erstbestückung" };
        Assert.True(new BestandskorrekturValidator().Validate(dto).IsValid);
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj`
Expected: alle PASS (bestehende 14 + 5 neue = 19).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Application/Lager/ tests/Milet.Application.Tests/LagerValidatorTests.cs
git commit -m "Lager-DTOs und Validatoren (Lagerort, Bestandskorrektur, Seriennummer, Inventur)"
```

---

### Task 5: Application — Lager-Service-Interfaces + Verkauf-Erweiterungen (Teillieferung, Sammelrechnung, Lieferschein-Buchen)

**Files:**
- Create: `src/Milet.Application/Lager/ILagerServices.cs`
- Modify: `src/Milet.Application/Verkauf/Dtos.cs`
- Modify: `src/Milet.Application/Verkauf/IVerkaufServices.cs`

**Interfaces:**
- Consumes: `LagerortDto`, `ArtikelBestandDto`, `BestandskorrekturDto`, `SeriennummerDto`, `InventurDto` (Task 4); `BelegDto`, `BelegTyp` (Phase 2).
- Produces: `ILagerortService`, `IBestandService`, `ISeriennummernService`, `IInventurService`, `OffenePositionDto`, erweitertes `BelegPositionDto` (mit `LagerortId`), erweitertes `ArtikelVerkaufLookupDto` (mit `HatSeriennummern`), erweitertes `IBelegUeberleitungService`, neues `ILieferscheinBuchenService` — von Task 7–13 (Infrastructure) implementiert, von Task 14–19 (App) konsumiert.

- [ ] **Step 1: `Milet.Application/Verkauf/Dtos.cs` erweitern**

Modify `src/Milet.Application/Verkauf/Dtos.cs`:

In `BelegPositionDto`, nach `public int? SteuerSchluessel { get; init; }` einfügen:
```csharp
    public int? LagerortId { get; init; }
```

`ArtikelVerkaufLookupDto`-Definition ersetzen durch (ein zusätzliches positional Argument am Ende, alle bestehenden Aufrufer in `VerkaufLookupService` müssen den neuen Parameter mitgeben — siehe Task 7):
```csharp
/// <summary>Reicheres Lookup als das generische <see cref="LookupDto"/> — trägt Defaultwerte für neue Belegpositionen.</summary>
public sealed record ArtikelVerkaufLookupDto(
    int Id,
    string Anzeige,
    /// <summary>Reine Artikelbezeichnung ohne Artikelnummer-Präfix — für Belegpositionen/Druck (im Gegensatz zu <see cref="Anzeige"/>, das für ComboBoxen gedacht ist).</summary>
    string Bezeichnung,
    decimal Listenpreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel,
    bool HatSeriennummern);
```

Am Ende der Datei (nach `PreisErgebnisDto`) neu hinzufügen:
```csharp
/// <summary>Offene (noch nicht überführte) Menge einer Quellposition — Grundlage für den Teillieferungs-Dialog.</summary>
public sealed record OffenePositionDto(int PositionId, string Bezeichnung, string? EinheitKuerzel, decimal OffeneMenge);
```

- [ ] **Step 2: `Milet.Application/Verkauf/IVerkaufServices.cs` erweitern**

Modify `src/Milet.Application/Verkauf/IVerkaufServices.cs` — `IBelegUeberleitungService` ersetzen durch:
```csharp
public interface IBelegUeberleitungService
{
    Task<BelegDto> UeberleitenAsync(int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);

    /// <summary>Offene Menge je Position des Quellbelegs — Grundlage für die Auswahl im Teillieferungs-Dialog.</summary>
    Task<IReadOnlyList<OffenePositionDto>> LadeOffenePositionenAsync(int quellBelegId, CancellationToken ct = default);

    /// <summary>Wie <see cref="UeberleitenAsync"/>, aber mit expliziter (ggf. reduzierter) Menge je Quellposition statt automatisch voller offener Menge — Basis der Teillieferung. <paramref name="lagerortId"/> nur bei zielTyp Lieferschein erforderlich.</summary>
    Task<BelegDto> UeberleitenMitAuswahlAsync(
        int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp,
        IReadOnlyDictionary<int, decimal> mengenJePosition, int? lagerortId, CancellationToken ct = default);

    /// <summary>Führt mehrere Quellbelege (z. B. mehrere Lieferscheine gleichen Kunden) in einen Zielbeleg zusammen — Basis der Sammelrechnung.</summary>
    Task<BelegDto> UeberleitenMehrereAsync(IReadOnlyList<int> quellBelegIds, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);
}
```

Nach `IRechnungBuchenService` neu hinzufügen:
```csharp
public interface ILieferscheinBuchenService
{
    /// <summary>Bucht: prüft/bucht Bestand atomar je Artikelposition, verknüpft ausgewählte Seriennummern, setzt Status Gebucht — eine Transaktion.</summary>
    Task<BelegDto> BuchenAsync(
        int lieferscheinId, IReadOnlyDictionary<int, IReadOnlyList<int>> seriennummernJePosition, CancellationToken ct = default);
}
```

- [ ] **Step 3: `Milet.Application/Lager/ILagerServices.cs` anlegen**

`src/Milet.Application/Lager/ILagerServices.cs`:
```csharp
namespace Milet.Application.Lager;

public interface ILagerortService
{
    Task<IReadOnlyList<LagerortDto>> SucheAsync(string? suchtext, CancellationToken ct = default);
    Task<LagerortDto> SpeichereAsync(LagerortDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IBestandService
{
    Task<IReadOnlyList<ArtikelBestandDto>> SucheAsync(string? suchtext, CancellationToken ct = default);

    /// <summary>Bucht eine manuelle Korrektur (z. B. Erstbestückung, Schwund) — atomar, wirft bei negativem Ergebnisbestand.</summary>
    Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default);
}

public interface ISeriennummernService
{
    Task<IReadOnlyList<SeriennummerDto>> SucheAsync(int? artikelId, CancellationToken ct = default);
    Task<IReadOnlyList<SeriennummerDto>> AufLagerAsync(int artikelId, CancellationToken ct = default);

    /// <summary>Manuelle Neuerfassung (z. B. Erstbestückung serialisierter Artikel) — bucht implizit +1 Bestand am angegebenen Lagerort.</summary>
    Task ErfasseAsync(int artikelId, int lagerortId, string nummer, CancellationToken ct = default);
}

public interface IInventurService
{
    Task<IReadOnlyList<InventurDto>> SucheAsync(CancellationToken ct = default);
    Task<InventurDto> LadeAsync(int id, CancellationToken ct = default);

    /// <summary>Legt eine neue Inventur an und friert SollMenge je lagerfähigem Artikel aus dem aktuellen ArtikelBestand ein.</summary>
    Task<InventurDto> NeueInventurAsync(int lagerortId, CancellationToken ct = default);

    Task ErfasseIstMengeAsync(int inventurPositionId, decimal istMenge, CancellationToken ct = default);

    /// <summary>Bucht für jede Position mit Ist≠Soll eine Korrekturbuchung (InventurKorrektur) und setzt Status Abgeschlossen — eine Transaktion.</summary>
    Task<InventurDto> AbschliessenAsync(int inventurId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Application/Milet.Application.csproj`
Expected: Fehler erwartet — `VerkaufLookupService` (Infrastructure) instanziiert `ArtikelVerkaufLookupDto` noch mit der alten 8-Parameter-Signatur. Das ist normal; wird in Task 7 behoben. Für diesen Task genügt: `Milet.Application`-Projekt selbst (ohne Infrastructure-Abhängigkeit) baut fehlerfrei.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Application/Lager/ILagerServices.cs src/Milet.Application/Verkauf/Dtos.cs src/Milet.Application/Verkauf/IVerkaufServices.cs
git commit -m "Application-Interfaces: Lager-Services, Teillieferung/Sammelrechnung/Lieferschein-Buchen"
```

---

### Task 6: Infrastructure — EF-Configurations Lager-Entities + DbContext + Migration + Seed (Hauptlagerort)

**Files:**
- Create: `src/Milet.Infrastructure/Persistence/Configurations/LagerortConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/LagerbewegungConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/ArtikelBestandConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/SeriennummerConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/BelegPositionSeriennummerConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/InventurConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/InventurPositionConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Configurations/BelegPositionConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/MiletDbContext.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs`

**Interfaces:**
- Consumes: alle Lager-Entities (Task 1/2), `Lieferschein`/`BelegPosition.LagerortId` (Task 3).
- Produces: `MiletDbContext.Lagerorte`/`.Lagerbewegungen`/`.ArtikelBestaende`/`.Seriennummern`/`.BelegPositionSeriennummern`/`.Inventuren`/`.InventurPositionen`/`.Lieferscheine` DbSets, TPH-Discriminator-Eintrag für `Lieferschein`, Seed-Datensatz `"HL"` (Hauptlager) — von allen folgenden Infrastructure-Tasks konsumiert.

- [ ] **Step 1: `LagerortConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/LagerortConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class LagerortConfiguration : IEntityTypeConfiguration<Lagerort>
{
    public void Configure(EntityTypeBuilder<Lagerort> b)
    {
        b.ToTable("Lagerorte");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 2: `LagerbewegungConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/LagerbewegungConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class LagerbewegungConfiguration : IEntityTypeConfiguration<Lagerbewegung>
{
    public void Configure(EntityTypeBuilder<Lagerbewegung> b)
    {
        b.ToTable("Lagerbewegungen");
        b.HasKey(x => x.Id);
        b.Property(x => x.Menge).HasPrecision(18, 3);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BelegPosition).WithMany().HasForeignKey(x => x.BelegPositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seriennummer).WithMany().HasForeignKey(x => x.SeriennummerId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ArtikelId, x.LagerortId });
    }
}
```

- [ ] **Step 3: `ArtikelBestandConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/ArtikelBestandConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class ArtikelBestandConfiguration : IEntityTypeConfiguration<ArtikelBestand>
{
    public void Configure(EntityTypeBuilder<ArtikelBestand> b)
    {
        b.ToTable("ArtikelBestaende");
        b.HasKey(x => x.Id);
        b.Property(x => x.Menge).HasPrecision(18, 3);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ArtikelId, x.LagerortId }).IsUnique();

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 4: `SeriennummerConfiguration` + `BelegPositionSeriennummerConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/SeriennummerConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class SeriennummerConfiguration : IEntityTypeConfiguration<Seriennummer>
{
    public void Configure(EntityTypeBuilder<Seriennummer> b)
    {
        b.ToTable("Seriennummern");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nummer).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.ArtikelId, x.Nummer }).IsUnique();

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

`src/Milet.Infrastructure/Persistence/Configurations/BelegPositionSeriennummerConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegPositionSeriennummerConfiguration : IEntityTypeConfiguration<BelegPositionSeriennummer>
{
    public void Configure(EntityTypeBuilder<BelegPositionSeriennummer> b)
    {
        b.ToTable("BelegPositionSeriennummern");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.BelegPosition).WithMany().HasForeignKey(x => x.BelegPositionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Seriennummer).WithMany().HasForeignKey(x => x.SeriennummerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BelegPositionId, x.SeriennummerId }).IsUnique();
    }
}
```

- [ ] **Step 5: `InventurConfiguration` + `InventurPositionConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/InventurConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class InventurConfiguration : IEntityTypeConfiguration<Inventur>
{
    public void Configure(EntityTypeBuilder<Inventur> b)
    {
        b.ToTable("Inventuren");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Positionen).WithOne(p => p.Inventur).HasForeignKey(p => p.InventurId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

`src/Milet.Infrastructure/Persistence/Configurations/InventurPositionConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class InventurPositionConfiguration : IEntityTypeConfiguration<InventurPosition>
{
    public void Configure(EntityTypeBuilder<InventurPosition> b)
    {
        b.ToTable("InventurPositionen");
        b.HasKey(x => x.Id);
        b.Property(x => x.SollMenge).HasPrecision(18, 3);
        b.Property(x => x.IstMenge).HasPrecision(18, 3);
        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 6: `BelegConfiguration` — Discriminator um Lieferschein erweitern**

Modify `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs` — Zeile
```csharp
        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung));
```
ersetzen durch:
```csharp
        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung))
            .HasValue<Lieferschein>(nameof(BelegTyp.Lieferschein));
```

- [ ] **Step 7: `BelegPositionConfiguration` — `LagerortId`-FK ergänzen**

Modify `src/Milet.Infrastructure/Persistence/Configurations/BelegPositionConfiguration.cs` — nach der Zeile `b.HasOne<MwStSatz>().WithMany().HasForeignKey(x => x.MwStSatzId).OnDelete(DeleteBehavior.Restrict);` einfügen:
```csharp
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
```
und den `using`-Block um `using Milet.Domain.Entities.Lager;` ergänzen.

- [ ] **Step 8: `MiletDbContext` — DbSets ergänzen**

Modify `src/Milet.Infrastructure/Persistence/MiletDbContext.cs` — nach `public DbSet<Milet.Domain.Entities.Admin.Firmenstamm> Firmenstamm => Set<Milet.Domain.Entities.Admin.Firmenstamm>();` einfügen:
```csharp
    public DbSet<Milet.Domain.Entities.Verkauf.Lieferschein> Lieferscheine => Set<Milet.Domain.Entities.Verkauf.Lieferschein>();
    public DbSet<Milet.Domain.Entities.Lager.Lagerort> Lagerorte => Set<Milet.Domain.Entities.Lager.Lagerort>();
    public DbSet<Milet.Domain.Entities.Lager.Lagerbewegung> Lagerbewegungen => Set<Milet.Domain.Entities.Lager.Lagerbewegung>();
    public DbSet<Milet.Domain.Entities.Lager.ArtikelBestand> ArtikelBestaende => Set<Milet.Domain.Entities.Lager.ArtikelBestand>();
    public DbSet<Milet.Domain.Entities.Lager.Seriennummer> Seriennummern => Set<Milet.Domain.Entities.Lager.Seriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.BelegPositionSeriennummer> BelegPositionSeriennummern => Set<Milet.Domain.Entities.Lager.BelegPositionSeriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.Inventur> Inventuren => Set<Milet.Domain.Entities.Lager.Inventur>();
    public DbSet<Milet.Domain.Entities.Lager.InventurPosition> InventurPositionen => Set<Milet.Domain.Entities.Lager.InventurPosition>();
```

- [ ] **Step 9: Seed — Hauptlagerort**

Modify `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs` — vor `if (!await db.Firmenstamm.AnyAsync(ct))` einfügen:
```csharp
        if (!await db.Lagerorte.AnyAsync(ct))
        {
            db.Lagerorte.Add(new Milet.Domain.Entities.Lager.Lagerort { Code = "HL", Bezeichnung = "Hauptlager", Aktiv = true });
        }

```

- [ ] **Step 10: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: Fehler in `VerkaufMapping.cs`/`BelegService.cs`/`BelegUeberleitungService.cs`/`VerkaufLookupService.cs` (unvollständige Switches / alte `ArtikelVerkaufLookupDto`-Signatur) — normal, wird in Task 7 behoben. Für diesen Task genügt: keine Fehler in den neu erstellten/geänderten Configuration-Dateien selbst (per `dotnet build` Fehlerliste gezielt auf Task-6-Dateien prüfen).

- [ ] **Step 11: Commit**

```bash
git add src/Milet.Infrastructure/Persistence/Configurations/LagerortConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/LagerbewegungConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/ArtikelBestandConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/SeriennummerConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/BelegPositionSeriennummerConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/InventurConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/InventurPositionConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/BelegPositionConfiguration.cs src/Milet.Infrastructure/Persistence/MiletDbContext.cs src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs
git commit -m "EF-Configurations Lager-Entities + Lieferschein-Discriminator + Hauptlagerort-Seed"
```

**Hinweis:** `dotnet ef migrations add` kompiliert das volle Infrastructure-Projekt und schlägt daher erst NACH Task 7 fehlerfrei durch (die dort behobenen Switches gehören zum selben Build). Die eigentliche Migration wird deshalb als letzter Schritt von Task 7 erzeugt, nicht hier.

---

### Task 7: Infrastructure — Lieferschein in bestehenden Beleg-Switches + Teillieferung (UeberleitenMitAuswahlAsync)

**Files:**
- Modify: `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs`
- Modify: `src/Milet.Infrastructure/Services/VerkaufLookupService.cs`
- Modify: `src/Milet.Infrastructure/Services/BelegService.cs`
- Modify: `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`

**Interfaces:**
- Consumes: `Lieferschein`, `BelegTyp.Lieferschein`, `BelegPosition.LagerortId` (Task 3), `OffenePositionDto`, erweitertes `IBelegUeberleitungService` (Task 5), `db.Lieferscheine` (Task 6).
- Produces: `IBelegUeberleitungService.LadeOffenePositionenAsync`/`.UeberleitenMitAuswahlAsync` implementiert — von Task 16 (`AuftragEditViewModel`) konsumiert. `BelegService`/`VerkaufMapping` unterstützen `BelegTyp.Lieferschein` generisch für Suche/Laden/Speichern/Löschen — von Task 17 (`LieferscheinListViewModel`/`LieferscheinEditViewModel`) konsumiert.

- [ ] **Step 1: `VerkaufMapping.cs` — Lieferschein im Typ-Switch, LagerortId in Positions-DTO**

Modify `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs`:

In `ToDto(this BelegPosition p)`, nach `SteuerSchluessel = p.SteuerSchluessel,` einfügen:
```csharp
        LagerortId = p.LagerortId,
```

In `ToDto(this Beleg b, bool mitPositionen)`, den Typ-Switch ersetzen durch:
```csharp
        var typ = b switch
        {
            Angebot => BelegTyp.Angebot,
            Auftrag => BelegTyp.Auftrag,
            Rechnung => BelegTyp.Rechnung,
            Lieferschein => BelegTyp.Lieferschein,
            _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
        };
```

- [ ] **Step 2: `VerkaufLookupService.cs` — `HatSeriennummern` im Artikel-Lookup**

Modify `src/Milet.Infrastructure/Services/VerkaufLookupService.cs` — den `Select`-Aufruf für `artikel` ersetzen durch:
```csharp
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => !a.Gesperrt)
            .OrderBy(a => a.Artikelnummer)
            .Select(a => new ArtikelVerkaufLookupDto(
                a.Id,
                $"{a.Artikelnummer} — {a.Bezeichnung}",
                a.Bezeichnung,
                a.Listenpreis,
                a.MwStSatzId,
                a.MwStSatz!.Satz,
                a.MwStSatz.SteuerSchluessel,
                a.Einheit!.Kuerzel,
                a.HatSeriennummern))
            .ToListAsync(ct);
```

- [ ] **Step 3: `BelegService.cs` — Lieferschein in `SetFuerTyp`/`NeueInstanz`/`NummernkreisCode`, `LagerortId` in Positions-Diffing**

Modify `src/Milet.Infrastructure/Services/BelegService.cs` — die drei privaten Switch-Helfer ersetzen durch:
```csharp
    private static IQueryable<Beleg> SetFuerTyp(MiletDbContext db, BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => db.Angebote,
        BelegTyp.Auftrag => db.Auftraege,
        BelegTyp.Rechnung => db.Rechnungen,
        BelegTyp.Lieferschein => db.Lieferscheine,
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };
```

In `AktualisierePositionen`, in beiden Zweigen (bestehende Position aktualisieren UND neue Position anlegen) nach `SteuerSchluessel = dtoPos.SteuerSchluessel,` (bzw. der entsprechenden Zuweisung `bestehend.SteuerSchluessel = dtoPos.SteuerSchluessel;`) ergänzen:
- Im `bestehend`-Zweig: `bestehend.LagerortId = dtoPos.LagerortId;`
- Im `new BelegPosition { ... }`-Zweig: `LagerortId = dtoPos.LagerortId,`

- [ ] **Step 4: `BelegUeberleitungService.cs` — Mehrfach-Zielübergänge, Lieferschein-Switches, Teillieferung**

Modify `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`:

`ErlaubteUebergaenge` ersetzen durch (Auftrag erlaubt jetzt zwei Ziele — direkte Rechnung für Dienstleistungen ODER Lieferschein für Warenlieferung; Lieferschein selbst leitet nur in Rechnung über):
```csharp
    private static readonly Dictionary<BelegTyp, BelegTyp[]> ErlaubteUebergaenge = new()
    {
        [BelegTyp.Angebot] = [BelegTyp.Auftrag],
        [BelegTyp.Auftrag] = [BelegTyp.Rechnung, BelegTyp.Lieferschein],
        [BelegTyp.Lieferschein] = [BelegTyp.Rechnung],
    };
```

`TypVon`/`NeueInstanz`/`NummernkreisCode` je um den Lieferschein-Fall erweitern (identisch zu Task 7 Step 3 in `BelegService.cs`):
```csharp
    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        Lieferschein => BelegTyp.Lieferschein,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };
```

In `UeberleitenAsync`, die Übergangsprüfung ersetzen durch (neu: ein Lieferschein muss gebucht — also physisch ausgeliefert und bestandswirksam — sein, bevor er in eine Rechnung überführt werden darf):
```csharp
        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");
        if (quellTyp == BelegTyp.Lieferschein && quellBeleg.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"Lieferschein '{quellBeleg.BelegNummer}' muss erst gebucht werden, bevor er berechnet werden kann.");
```

Am Ende der Methode die Zeile
```csharp
        if (quellVollstaendigUebernommen && quellBeleg.Status == BelegStatus.Entwurf)
            quellBeleg.Status = BelegStatus.Erledigt;
```
ersetzen durch (ein Lieferschein ist beim Überleiten `Gebucht`, nicht `Entwurf` — die ursprüngliche Bedingung hätte ihn nie auf `Erledigt` gesetzt):
```csharp
        if (quellVollstaendigUebernommen && quellBeleg.Status is BelegStatus.Entwurf or BelegStatus.Gebucht)
            quellBeleg.Status = BelegStatus.Erledigt;
```

Nach `UeberleitenAsync` zwei neue Methoden ergänzen:
```csharp
    public async Task<IReadOnlyList<OffenePositionDto>> LadeOffenePositionenAsync(int quellBelegId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var quellBeleg = await db.Belege.AsNoTracking().Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellPositionIds = quellBeleg.Positionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        return quellBeleg.Positionen
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel)
            .Select(p => new OffenePositionDto(p.Id, p.Bezeichnung, p.EinheitKuerzel, BelegPosition.OffeneMenge(p, folgepositionen)))
            .Where(p => p.OffeneMenge > 0)
            .ToList();
    }

    /// <summary>Wie <see cref="UeberleitenAsync"/>, aber mit expliziter Menge je Quellposition (Teillieferung) statt automatisch voller offener Menge.
    /// Bewusst als eigene Methode statt Parametrisierung von <see cref="UeberleitenAsync"/> — beide Pfade sind klar genug getrennt (voll vs. Auswahl),
    /// eine gemeinsame Abstraktion würde hier mehr Indirektion als Nutzen bringen.</summary>
    public async Task<BelegDto> UeberleitenMitAuswahlAsync(
        int quellBelegId, BelegTyp zielTyp, IReadOnlyDictionary<int, decimal> mengenJePosition, int? lagerortId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quellBeleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Kunde)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");

        if (zielTyp == BelegTyp.Lieferschein)
        {
            if (lagerortId is null)
                throw new InvalidOperationException("Lagerort ist für die Lieferschein-Erstellung erforderlich.");
            if (quellBeleg.Kunde?.Liefersperre == true)
                throw new InvalidOperationException($"Kunde '{quellBeleg.Kunde.Kundennummer}' hat Liefersperre.");
        }

        var quellPositionIds = quellBeleg.Positionen.Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && quellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var zielBeleg = NeueInstanz(zielTyp);
        zielBeleg.BelegNummer = zielTyp == BelegTyp.Rechnung
            ? string.Empty
            : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(zielTyp), ct);
        zielBeleg.BelegDatum = DateOnly.FromDateTime(DateTime.Today);
        zielBeleg.KundeId = quellBeleg.KundeId;
        zielBeleg.RechnungsadresseSnapshot = quellBeleg.RechnungsadresseSnapshot.Kopie();
        zielBeleg.LieferadresseSnapshot = quellBeleg.LieferadresseSnapshot.Kopie();
        zielBeleg.ZahlungsbedingungZielTage = quellBeleg.ZahlungsbedingungZielTage;
        zielBeleg.ZahlungsbedingungSkontoTage = quellBeleg.ZahlungsbedingungSkontoTage;
        zielBeleg.ZahlungsbedingungSkontoProzent = quellBeleg.ZahlungsbedingungSkontoProzent;
        zielBeleg.Kopftext = quellBeleg.Kopftext;
        zielBeleg.Fusstext = quellBeleg.Fusstext;

        var quellVollstaendigUebernommen = true;
        var positionsNr = 1;
        foreach (var quellPosition in quellBeleg.Positionen.OrderBy(p => p.PositionsNr))
        {
            if (quellPosition.PositionsTyp != PositionsTyp.Artikel) continue;

            var offeneMenge = BelegPosition.OffeneMenge(quellPosition, folgepositionen);
            if (!mengenJePosition.TryGetValue(quellPosition.Id, out var gewaehlteMenge) || gewaehlteMenge <= 0)
            {
                if (offeneMenge > 0) quellVollstaendigUebernommen = false;
                continue;
            }

            // Erneute Prüfung in derselben Transaktion — Schutz gegen Race zweier gleichzeitiger Teillieferungen aus demselben Auftrag.
            if (gewaehlteMenge > offeneMenge)
                throw new InvalidOperationException(
                    $"Position {quellPosition.PositionsNr}: gewählte Menge ({gewaehlteMenge}) übersteigt offene Menge ({offeneMenge}).");

            if (gewaehlteMenge < offeneMenge) quellVollstaendigUebernommen = false;

            zielBeleg.Positionen.Add(new BelegPosition
            {
                PositionsNr = positionsNr++,
                PositionsTyp = PositionsTyp.Artikel,
                ArtikelId = quellPosition.ArtikelId,
                Bezeichnung = quellPosition.Bezeichnung,
                EinheitKuerzel = quellPosition.EinheitKuerzel,
                Menge = gewaehlteMenge,
                Einzelpreis = quellPosition.Einzelpreis,
                RabattProzent = quellPosition.RabattProzent,
                MwStSatzId = quellPosition.MwStSatzId,
                MwStSatzWert = quellPosition.MwStSatzWert,
                SteuerSchluessel = quellPosition.SteuerSchluessel,
                GesamtNetto = SteuerRechner.BerechnePosition(gewaehlteMenge, quellPosition.Einzelpreis, quellPosition.RabattProzent),
                UrsprungsPositionId = quellPosition.Id,
                LagerortId = zielTyp == BelegTyp.Lieferschein ? lagerortId : null,
            });
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine Positionen zum Überleiten ausgewählt.");

        var steuersummen = SteuerRechner.BerechneSteuersummen(zielBeleg.Positionen);
        zielBeleg.Steuersummen = steuersummen.ToList();
        (zielBeleg.SummeNetto, zielBeleg.SummeMwSt, zielBeleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(zielBeleg);

        if (quellVollstaendigUebernommen && quellBeleg.Status == BelegStatus.Entwurf)
            quellBeleg.Status = BelegStatus.Erledigt;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return zielBeleg.ToDto(mitPositionen: true);
    }
```

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler (Task 5/6 offene Switch-Fehler jetzt behoben — `UeberleitenMehrereAsync` aus `IBelegUeberleitungService` fehlt noch, das ist Task 8; falls der Compiler das schon als Fehler zeigt, ist das erwartet und wird dort behoben — für DIESEN Build-Check zählt nur: keine Fehler in den in Step 1–4 geänderten Dateien).

- [ ] **Step 6: Migration erzeugen und anwenden**

Run:
```bash
cd src/Milet.Tools.Migrator
"$USERPROFILE/.dotnet/dotnet.exe" tool run dotnet-ef migrations add LagerLieferschein --project ../Milet.Infrastructure --startup-project .
"$USERPROFILE/.dotnet/dotnet.exe" run --project .
```
Expected: Migration erzeugt, `dotnet run` wendet sie auf die LocalDB `Milet` an und legt den Hauptlagerort-Seed an (Konsolenausgabe „Migrationen erfolgreich angewendet." + Grunddaten-Zeile).

- [ ] **Step 7: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs src/Milet.Infrastructure/Services/VerkaufLookupService.cs src/Milet.Infrastructure/Services/BelegService.cs src/Milet.Infrastructure/Services/BelegUeberleitungService.cs src/Milet.Infrastructure/Persistence/Migrations/
git commit -m "Lieferschein in Beleg-Switches integriert; Teillieferung (UeberleitenMitAuswahlAsync) + LadeOffenePositionenAsync; Migration LagerLieferschein"
```

---

### Task 8: Infrastructure — Sammelüberleitung (mehrere Lieferscheine → eine Rechnung) + Integrationstest

**Files:**
- Modify: `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`
- Test: `tests/Milet.IntegrationTests/BelegUeberleitungServiceTests.cs`

**Interfaces:**
- Consumes: `ErlaubteUebergaenge`/`TypVon`/`NeueInstanz`/`NummernkreisCode` (Task 7).
- Produces: `IBelegUeberleitungService.UeberleitenMehrereAsync` implementiert — von Task 17 (`LieferscheinListViewModel`) konsumiert.

- [ ] **Step 1: `UeberleitenMehrereAsync` implementieren**

Modify `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs` — nach `UeberleitenMitAuswahlAsync` ergänzen:
```csharp
    /// <summary>Führt mehrere Quellbelege gleichen Kunden/gleicher Zahlungsbedingung in einen Zielbeleg zusammen (Sammelrechnung).</summary>
    public async Task<BelegDto> UeberleitenMehrereAsync(IReadOnlyList<int> quellBelegIds, BelegTyp zielTyp, CancellationToken ct = default)
    {
        if (quellBelegIds.Count == 0)
            throw new InvalidOperationException("Mindestens ein Quellbeleg erforderlich.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quellBelege = await db.Belege.Include(b => b.Positionen)
            .Where(b => quellBelegIds.Contains(b.Id))
            .ToListAsync(ct);
        if (quellBelege.Count != quellBelegIds.Count)
            throw new NotFoundException(nameof(Beleg), string.Join(",", quellBelegIds));

        var ersterBeleg = quellBelege[0];
        var ersteZahlungsbedingung = (ersterBeleg.ZahlungsbedingungZielTage, ersterBeleg.ZahlungsbedingungSkontoTage, ersterBeleg.ZahlungsbedingungSkontoProzent);
        foreach (var beleg in quellBelege)
        {
            var typ = TypVon(beleg);
            if (!ErlaubteUebergaenge.TryGetValue(typ, out var erlaubteZiele) || !erlaubteZiele.Contains(zielTyp))
                throw new InvalidOperationException($"Überleitung von {typ} nach {zielTyp} wird nicht unterstützt.");
            if (typ == BelegTyp.Lieferschein && beleg.Status != BelegStatus.Gebucht)
                throw new InvalidOperationException($"Lieferschein '{beleg.BelegNummer}' muss erst gebucht werden, bevor er berechnet werden kann.");
            if (beleg.KundeId != ersterBeleg.KundeId)
                throw new InvalidOperationException("Sammelüberleitung nur für Belege desselben Kunden möglich.");
            if ((beleg.ZahlungsbedingungZielTage, beleg.ZahlungsbedingungSkontoTage, beleg.ZahlungsbedingungSkontoProzent) != ersteZahlungsbedingung)
                throw new InvalidOperationException("Sammelüberleitung nur für Belege derselben Zahlungsbedingung möglich.");
        }

        var alleQuellPositionIds = quellBelege.SelectMany(b => b.Positionen).Select(p => p.Id).ToList();
        var folgepositionen = await db.BelegPositionen.AsNoTracking()
            .Where(p => p.UrsprungsPositionId != null && alleQuellPositionIds.Contains(p.UrsprungsPositionId.Value))
            .ToListAsync(ct);

        var zielBeleg = NeueInstanz(zielTyp);
        zielBeleg.BelegNummer = zielTyp == BelegTyp.Rechnung
            ? string.Empty
            : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(zielTyp), ct);
        zielBeleg.BelegDatum = DateOnly.FromDateTime(DateTime.Today);
        zielBeleg.KundeId = ersterBeleg.KundeId;
        zielBeleg.RechnungsadresseSnapshot = ersterBeleg.RechnungsadresseSnapshot.Kopie();
        zielBeleg.LieferadresseSnapshot = ersterBeleg.LieferadresseSnapshot.Kopie();
        zielBeleg.ZahlungsbedingungZielTage = ersterBeleg.ZahlungsbedingungZielTage;
        zielBeleg.ZahlungsbedingungSkontoTage = ersterBeleg.ZahlungsbedingungSkontoTage;
        zielBeleg.ZahlungsbedingungSkontoProzent = ersterBeleg.ZahlungsbedingungSkontoProzent;

        var positionsNr = 1;
        foreach (var quellBeleg in quellBelege.OrderBy(b => b.BelegDatum).ThenBy(b => b.Id))
        {
            var quellVollstaendigUebernommen = true;
            foreach (var quellPosition in quellBeleg.Positionen.OrderBy(p => p.PositionsNr))
            {
                var menge = quellPosition.PositionsTyp == PositionsTyp.Artikel
                    ? BelegPosition.OffeneMenge(quellPosition, folgepositionen)
                    : quellPosition.Menge;

                if (quellPosition.PositionsTyp == PositionsTyp.Artikel && menge <= 0)
                    continue;
                if (quellPosition.PositionsTyp == PositionsTyp.Artikel && menge < quellPosition.Menge)
                    quellVollstaendigUebernommen = false;

                zielBeleg.Positionen.Add(new BelegPosition
                {
                    PositionsNr = positionsNr++,
                    PositionsTyp = quellPosition.PositionsTyp,
                    ArtikelId = quellPosition.ArtikelId,
                    Bezeichnung = quellPosition.Bezeichnung,
                    EinheitKuerzel = quellPosition.EinheitKuerzel,
                    Menge = menge,
                    Einzelpreis = quellPosition.Einzelpreis,
                    RabattProzent = quellPosition.RabattProzent,
                    MwStSatzId = quellPosition.MwStSatzId,
                    MwStSatzWert = quellPosition.MwStSatzWert,
                    SteuerSchluessel = quellPosition.SteuerSchluessel,
                    GesamtNetto = SteuerRechner.BerechnePosition(menge, quellPosition.Einzelpreis, quellPosition.RabattProzent),
                    UrsprungsPositionId = quellPosition.Id,
                });
            }

            if (quellVollstaendigUebernommen && quellBeleg.Status is BelegStatus.Entwurf or BelegStatus.Gebucht)
                quellBeleg.Status = BelegStatus.Erledigt;
        }

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");

        var steuersummen = SteuerRechner.BerechneSteuersummen(zielBeleg.Positionen);
        zielBeleg.Steuersummen = steuersummen.ToList();
        (zielBeleg.SummeNetto, zielBeleg.SummeMwSt, zielBeleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        db.Add(zielBeleg);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return zielBeleg.ToDto(mitPositionen: true);
    }
```

- [ ] **Step 2: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler — `IBelegUeberleitungService` jetzt vollständig implementiert.

- [ ] **Step 3: Integrationstest**

`tests/Milet.IntegrationTests/BelegUeberleitungServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BelegUeberleitungServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new BelegImmutabilityInterceptor())
            .Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        db.Nummernkreise.AddRange(
            new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" });
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
        _kundeId = kunde.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeuerGebuchterLieferscheinAsync(decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = $"LS-TEST-{Guid.NewGuid():N}"[..15],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            Status = BelegStatus.Gebucht,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = menge, Einzelpreis = 10m, GesamtNetto = menge * 10m, MwStSatzWert = 19m }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);
        return lieferschein.Id;
    }

    [Fact]
    public async Task UeberleitenMehrereAsync_ZweiLieferscheineGleicherKunde_ErgibtEineSammelrechnung()
    {
        var ct = TestContext.Current.CancellationToken;
        var ls1 = await NeuerGebuchterLieferscheinAsync(3, ct);
        var ls2 = await NeuerGebuchterLieferscheinAsync(5, ct);

        var service = new BelegUeberleitungService(_factory, new NumberRangeService(_factory));
        var rechnung = await service.UeberleitenMehrereAsync([ls1, ls2], BelegTyp.Rechnung, ct);

        Assert.Equal(2, rechnung.Positionen.Count);
        Assert.Equal(80m, rechnung.SummeNetto);

        await using var db = new MiletDbContext(_options);
        Assert.Equal(BelegStatus.Erledigt, (await db.Belege.FirstAsync(b => b.Id == ls1, ct)).Status);
        Assert.Equal(BelegStatus.Erledigt, (await db.Belege.FirstAsync(b => b.Id == ls2, ct)).Status);
    }

    [Fact]
    public async Task UeberleitenMehrereAsync_NichtGebuchterLieferschein_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = "LS-ENTWURF",
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            Status = BelegStatus.Entwurf,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 10m, GesamtNetto = 10m, MwStSatzWert = 19m }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);

        var service = new BelegUeberleitungService(_factory, new NumberRangeService(_factory));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UeberleitenMehrereAsync([lieferschein.Id], BelegTyp.Rechnung, ct));
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: PASS (oder sauberer Skip ohne Docker — beide Fälle sind Erfolg, siehe `STATUS.md`-Hinweis zu Testcontainers auf dieser Maschine).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Infrastructure/Services/BelegUeberleitungService.cs tests/Milet.IntegrationTests/BelegUeberleitungServiceTests.cs
git commit -m "Sammelüberleitung (mehrere Lieferscheine -> eine Rechnung) + Integrationstest"
```

---

### Task 9: Infrastructure — BestandService (atomare Buchung) + Negativsperre-/Ledger-Integrationstest

**Files:**
- Create: `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs`
- Create: `src/Milet.Infrastructure/Services/BestandService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`
- Test: `tests/Milet.IntegrationTests/BestandServiceTests.cs`

**Interfaces:**
- Consumes: `ArtikelBestand`, `Lagerbewegung`, `LagerbewegungTyp` (Task 1), `IBestandService`, `BestandskorrekturDto`, `ArtikelBestandDto` (Task 4/5).
- Produces: `IBestandService` implementiert; `BestandService.BucheBewegungAsync(MiletDbContext, int artikelId, int lagerortId, decimal mengeDelta, LagerbewegungTyp typ, int? belegPositionId, CancellationToken)` — `internal static`, einziger Schreibpfad auf `ArtikelBestand`/`Lagerbewegung` — von Task 12 (`LieferscheinBuchenService`) und Task 13 (`InventurService`) innerhalb ihrer eigenen Transaktionen wiederverwendet. `LagerMapping.ToDto`-Erweiterungen — von Task 10/11/13 konsumiert.

- [ ] **Step 1: `LagerMapping.cs` — Basis-Mapping-Datei anlegen**

`src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs`:
```csharp
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Services.Mapping;

internal static class LagerMapping
{
    public static LagerortDto ToDto(this Lagerort l) => new()
    {
        Id = l.Id,
        Code = l.Code,
        Bezeichnung = l.Bezeichnung,
        Aktiv = l.Aktiv,
        RowVersion = l.RowVersion,
    };

    public static ArtikelBestandDto ToDto(this ArtikelBestand b) => new(
        b.ArtikelId,
        b.Artikel!.Artikelnummer,
        b.Artikel.Bezeichnung,
        b.Artikel.HatSeriennummern,
        b.LagerortId,
        b.Lagerort!.Bezeichnung,
        b.Menge,
        b.Artikel.Mindestbestand);
}
```

- [ ] **Step 2: `BestandService` mit atomarer Buchung**

`src/Milet.Infrastructure/Services/BestandService.cs`:
```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BestandService(IDbContextFactory<MiletDbContext> dbContextFactory) : IBestandService
{
    private static readonly BestandskorrekturValidator Validator = new();

    public async Task<IReadOnlyList<ArtikelBestandDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.ArtikelBestaende.AsNoTracking().Include(b => b.Artikel).Include(b => b.Lagerort).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b => EF.Functions.Like(b.Artikel!.Bezeichnung, $"%{s}%") || EF.Functions.Like(b.Artikel!.Artikelnummer, $"%{s}%"));
        }
        var liste = await query.OrderBy(b => b.Artikel!.Artikelnummer).ToListAsync(ct);
        return liste.Select(b => b.ToDto()).ToList();
    }

    public async Task KorrigiereAsync(BestandskorrekturDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BucheBewegungAsync(db, dto.ArtikelId, dto.LagerortId, dto.MengeDelta, LagerbewegungTyp.Korrektur, belegPositionId: null, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Einziger Schreibpfad auf Bestand — ein atomares UPDATE (kein Read-Modify-Write), Negativbestand ist hart gesperrt.
    /// Läuft innerhalb der Transaktion des Aufrufers (Aufrufer öffnet/committet); wiederverwendbar von Bestandskorrektur, Lieferschein-Buchen, Inventur-Abschluss.</summary>
    internal static async Task BucheBewegungAsync(
        MiletDbContext db, int artikelId, int lagerortId, decimal mengeDelta,
        LagerbewegungTyp typ, int? belegPositionId, CancellationToken ct)
    {
        var betroffeneZeilen = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE ArtikelBestaende SET Menge = Menge + {mengeDelta}
             WHERE ArtikelId = {artikelId} AND LagerortId = {lagerortId} AND Menge + {mengeDelta} >= 0
             """, ct);

        if (betroffeneZeilen == 0)
        {
            if (mengeDelta < 0)
                throw new InvalidOperationException("Nicht genügend Bestand vorhanden — Buchung würde negativen Bestand erzeugen.");

            db.ArtikelBestaende.Add(new ArtikelBestand { ArtikelId = artikelId, LagerortId = lagerortId, Menge = mengeDelta });
        }

        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            ArtikelId = artikelId,
            LagerortId = lagerortId,
            Typ = typ,
            Menge = mengeDelta,
            BelegPositionId = belegPositionId,
            Zeitpunkt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: DI-Registrierung**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IPdfService, Pdf.PdfService>();` einfügen:
```csharp
        services.AddScoped<IBestandService, BestandService>();
```
`using Milet.Application.Lager;` zum `using`-Block ergänzen.

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Integrationstest — Negativsperre, parallele Buchung, Ledger=Snapshot**

`tests/Milet.IntegrationTests/BestandServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BestandServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private int _artikelId;
    private int _lagerortId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(_container.GetConnectionString()).Options;

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();
        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        db.AddRange(einheit, mwst);
        await db.SaveChangesAsync();
        var artikel = new Artikel { Artikelnummer = "ART-TEST", Bezeichnung = "Testartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(artikel, lagerort);
        await db.SaveChangesAsync();
        _artikelId = artikel.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task Korrektur_UnzureichenderBestand_WirftNegativsperre()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -1, Grund = "Test" }, ct));
    }

    [Fact]
    public async Task Korrektur_PositivGefolgtVonNegativUeberBestand_LetzeBuchungWirftBestandBleibtKonsistent()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BestandService(new TestDbContextFactory(_options));

        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 10, Grund = "Erstbestückung" }, ct);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -15, Grund = "Zu viel" }, ct));

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(10m, bestand.Menge);
    }

    [Fact]
    public async Task ParalleleBuchungen_LedgerSummeGleichSnapshot_NieNegativ()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = new TestDbContextFactory(_options);
        var service = new BestandService(factory);

        await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = 100, Grund = "Start" }, ct);

        // 30 parallele Abbuchungen à 5 = 150 angefragt, nur 100 verfügbar -> ein Teil muss mit Negativsperre scheitern, Rest darf nie unter 0 fallen.
        var aufgaben = Enumerable.Range(0, 30).Select(async _ =>
        {
            try { await service.KorrigiereAsync(new() { ArtikelId = _artikelId, LagerortId = _lagerortId, MengeDelta = -5, Grund = "Parallel" }, ct); return true; }
            catch (InvalidOperationException) { return false; }
        });
        var ergebnisse = await Task.WhenAll(aufgaben);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        var ledgerSumme = await db.Lagerbewegungen.Where(l => l.ArtikelId == _artikelId && l.LagerortId == _lagerortId).SumAsync(l => l.Menge, ct);

        Assert.True(bestand.Menge >= 0);
        Assert.Equal(ledgerSumme, bestand.Menge);
        Assert.Equal(100m - 5m * ergebnisse.Count(erfolg => erfolg), bestand.Menge);
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
```

- [ ] **Step 6: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: PASS (oder sauberer Skip ohne Docker).

- [ ] **Step 7: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs src/Milet.Infrastructure/Services/BestandService.cs src/Milet.Infrastructure/DependencyInjection.cs tests/Milet.IntegrationTests/BestandServiceTests.cs
git commit -m "BestandService: atomare Bestandsbuchung (kein Read-Modify-Write), Negativsperre, Ledger=Snapshot-Integrationstests"
```

---

### Task 10: Infrastructure — LagerortService (CRUD, Kleinstamm-Muster)

**Files:**
- Create: `src/Milet.Infrastructure/Services/LagerortService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Lagerort`, `LagerortDto`, `LagerortValidator`, `LagerMapping.ToDto(Lagerort)` (Task 1/4/9).
- Produces: `ILagerortService` implementiert — von Task 14 (Kleinstamm-Tab), Task 16 (Teillieferungs-Dialog), Task 17 (Lieferschein-Editor, indirekt über bereits geladene Lagerortliste) konsumiert.

- [ ] **Step 1: `LagerortService`**

`src/Milet.Infrastructure/Services/LagerortService.cs`:
```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class LagerortService(IDbContextFactory<MiletDbContext> dbContextFactory) : ILagerortService
{
    private static readonly LagerortValidator Validator = new();

    public async Task<IReadOnlyList<LagerortDto>> SucheAsync(string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Lagerorte.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(l => EF.Functions.Like(l.Code, $"%{s}%") || EF.Functions.Like(l.Bezeichnung, $"%{s}%"));
        }
        var liste = await query.OrderBy(l => l.Code).ToListAsync(ct);
        return liste.Select(l => l.ToDto()).ToList();
    }

    public async Task<LagerortDto> SpeichereAsync(LagerortDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Lagerort lagerort;
        if (dto.Id == 0)
        {
            lagerort = new Lagerort();
            db.Add(lagerort);
        }
        else
        {
            lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == dto.Id, ct)
                ?? throw new Application.Common.NotFoundException(nameof(Lagerort), dto.Id);
            db.Entry(lagerort).Property(l => l.RowVersion).OriginalValue = dto.RowVersion;
        }

        lagerort.Code = dto.Code;
        lagerort.Bezeichnung = dto.Bezeichnung;
        lagerort.Aktiv = dto.Aktiv;

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Lagerort), lagerort.Id, ct);
        return lagerort.ToDto();
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Lagerort), id);
        db.Remove(lagerort);
        await db.SaveChangesDeletingAsync(nameof(Lagerort), id, ct);
    }
}
```

- [ ] **Step 2: DI-Registrierung**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IBestandService, BestandService>();` einfügen:
```csharp
        services.AddScoped<ILagerortService, LagerortService>();
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/LagerortService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "LagerortService (CRUD, Kleinstamm-Muster)"
```

---

### Task 11: Infrastructure — SeriennummernService

**Files:**
- Modify: `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs`
- Create: `src/Milet.Infrastructure/Services/SeriennummernService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Seriennummer`, `SeriennummerStatus` (Task 2), `SeriennummerDto`, `ISeriennummernService` (Task 4/5), `BestandService.BucheBewegungAsync` (Task 9).
- Produces: `ISeriennummernService` implementiert — von Task 12 (`LieferscheinBuchenService`) und Task 15/17 (UI: Erfassung, Auswahl-Dialog beim Buchen) konsumiert.

- [ ] **Step 1: `LagerMapping.cs` — `SeriennummerDto`-Mapping ergänzen**

Modify `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs` — am Ende der Klasse ergänzen:
```csharp
    public static SeriennummerDto ToDto(this Seriennummer s) => new(s.Id, s.ArtikelId, s.Nummer, s.Status, s.LagerortId);
```

- [ ] **Step 2: `SeriennummernService`**

`src/Milet.Infrastructure/Services/SeriennummernService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class SeriennummernService(IDbContextFactory<MiletDbContext> dbContextFactory) : ISeriennummernService
{
    public async Task<IReadOnlyList<SeriennummerDto>> SucheAsync(int? artikelId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Seriennummern.AsNoTracking().AsQueryable();
        if (artikelId is { } id) query = query.Where(s => s.ArtikelId == id);
        var liste = await query.OrderBy(s => s.Nummer).ToListAsync(ct);
        return liste.Select(s => s.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<SeriennummerDto>> AufLagerAsync(int artikelId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var liste = await db.Seriennummern.AsNoTracking()
            .Where(s => s.ArtikelId == artikelId && s.Status == SeriennummerStatus.AufLager)
            .OrderBy(s => s.Nummer)
            .ToListAsync(ct);
        return liste.Select(s => s.ToDto()).ToList();
    }

    public async Task ErfasseAsync(int artikelId, int lagerortId, string nummer, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            throw new InvalidOperationException("Seriennummer darf nicht leer sein.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (await db.Seriennummern.AnyAsync(s => s.ArtikelId == artikelId && s.Nummer == nummer, ct))
            throw new InvalidOperationException($"Seriennummer '{nummer}' ist für diesen Artikel bereits erfasst.");

        db.Seriennummern.Add(new Seriennummer { ArtikelId = artikelId, Nummer = nummer, Status = SeriennummerStatus.AufLager, LagerortId = lagerortId });
        await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, 1m, LagerbewegungTyp.Korrektur, belegPositionId: null, ct);
        await transaction.CommitAsync(ct);
    }
}
```

- [ ] **Step 3: DI-Registrierung**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<ILagerortService, LagerortService>();` einfügen:
```csharp
        services.AddScoped<ISeriennummernService, SeriennummernService>();
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs src/Milet.Infrastructure/Services/SeriennummernService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "SeriennummernService: Suche, AufLager-Abfrage, manuelle Erfassung mit Bestandsbuchung"
```

---

### Task 12: Infrastructure — LieferscheinBuchenService + Integrationstest

**Files:**
- Create: `src/Milet.Infrastructure/Services/LieferscheinBuchenService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`
- Test: `tests/Milet.IntegrationTests/LieferscheinBuchenServiceTests.cs`

**Interfaces:**
- Consumes: `Lieferschein`, `BelegPositionSeriennummer`, `Seriennummer`, `BestandService.BucheBewegungAsync` (Task 1/2/9), `ILieferscheinBuchenService` (Task 5).
- Produces: `ILieferscheinBuchenService` implementiert — von Task 17 (`LieferscheinEditViewModel`) konsumiert.

- [ ] **Step 1: `LieferscheinBuchenService`**

`src/Milet.Infrastructure/Services/LieferscheinBuchenService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class LieferscheinBuchenService(IDbContextFactory<MiletDbContext> dbContextFactory) : ILieferscheinBuchenService
{
    public async Task<BelegDto> BuchenAsync(
        int lieferscheinId, IReadOnlyDictionary<int, IReadOnlyList<int>> seriennummernJePosition, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var lieferschein = await db.Lieferscheine.Include(l => l.Positionen)
            .FirstOrDefaultAsync(l => l.Id == lieferscheinId, ct)
            ?? throw new NotFoundException(nameof(Lieferschein), lieferscheinId);

        if (lieferschein.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Lieferschein '{lieferschein.BelegNummer}' ist bereits gebucht.");
        if (lieferschein.Positionen.Count == 0)
            throw new InvalidOperationException("Lieferschein ohne Positionen kann nicht gebucht werden.");

        foreach (var position in lieferschein.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId)
                throw new InvalidOperationException($"Position {position.PositionsNr}: Artikel oder Lagerort fehlt.");

            var artikel = await db.Artikel.AsNoTracking().FirstAsync(a => a.Id == artikelId, ct);

            // Bestand VOR der Seriennummern-Prüfung abbuchen: eine einzige atomare Buchung entscheidet über Verfügbarkeit
            // (kein separater Read-Modify-Write-Check davor, siehe BestandService.BucheBewegungAsync).
            await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, -position.Menge, LagerbewegungTyp.Lieferung, position.Id, ct);

            if (artikel.HatSeriennummern)
            {
                if (!seriennummernJePosition.TryGetValue(position.Id, out var gewaehlt) || gewaehlt.Count != position.Menge)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: es müssen genau {position.Menge} Seriennummer(n) ausgewählt werden.");

                var seriennummern = await db.Seriennummern
                    .Where(s => gewaehlt.Contains(s.Id) && s.ArtikelId == artikelId && s.Status == SeriennummerStatus.AufLager)
                    .ToListAsync(ct);

                if (seriennummern.Count != gewaehlt.Count)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: eine oder mehrere gewählte Seriennummern sind nicht mehr verfügbar.");

                foreach (var seriennummer in seriennummern)
                {
                    seriennummer.Status = SeriennummerStatus.Ausgeliefert;
                    seriennummer.LagerortId = null;
                    db.BelegPositionSeriennummern.Add(new BelegPositionSeriennummer { BelegPositionId = position.Id, SeriennummerId = seriennummer.Id });
                }
            }
        }

        lieferschein.Status = BelegStatus.Gebucht;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return lieferschein.ToDto(mitPositionen: true);
    }
}
```

- [ ] **Step 2: DI-Registrierung**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<ISeriennummernService, SeriennummernService>();` einfügen:
```csharp
        services.AddScoped<ILieferscheinBuchenService, LieferscheinBuchenService>();
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Integrationstest**

`tests/Milet.IntegrationTests/LieferscheinBuchenServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class LieferscheinBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _kundeId;
    private int _artikelId;
    private int _artikelSerialisiertId;
    private int _lagerortId;

    public async ValueTask InitializeAsync()
    {
        if (!DockerVerfuegbar())
            Assert.Skip("Docker nicht verfügbar — Testcontainers-Integrationstest übersprungen.");

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<MiletDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new BelegImmutabilityInterceptor())
            .Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, kunde, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelSerial = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Serienartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id, HatSeriennummern = true };
        db.AddRange(artikel, artikelSerial);
        await db.SaveChangesAsync();

        _kundeId = kunde.Id;
        _artikelId = artikel.Id;
        _artikelSerialisiertId = artikelSerial.Id;
        _lagerortId = lagerort.Id;

        await BestandService.BucheBewegungAsync(db, _artikelId, _lagerortId, 20m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
        var s1 = new Seriennummer { ArtikelId = _artikelSerialisiertId, Nummer = "SN-1", Status = SeriennummerStatus.AufLager, LagerortId = _lagerortId };
        var s2 = new Seriennummer { ArtikelId = _artikelSerialisiertId, Nummer = "SN-2", Status = SeriennummerStatus.AufLager, LagerortId = _lagerortId };
        db.AddRange(s1, s2);
        await db.SaveChangesAsync();
        await BestandService.BucheBewegungAsync(db, _artikelSerialisiertId, _lagerortId, 2m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<Lieferschein> NeuerLieferscheinAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync(k => k.Id == _kundeId, ct);
        var lieferschein = new Lieferschein
        {
            BelegNummer = $"LS-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Test", Menge = menge, Einzelpreis = 10m, GesamtNetto = menge * 10m,
                MwStSatzWert = 19m, ArtikelId = artikelId, LagerortId = _lagerortId,
            }],
        };
        db.Add(lieferschein);
        await db.SaveChangesAsync(ct);
        return lieferschein;
    }

    [Fact]
    public async Task BuchenAsync_AusreichenderBestand_BuchtAbUndSetztGebucht()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelId, 5, ct);
        var service = new LieferscheinBuchenService(_factory);

        var gebucht = await service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct);

        Assert.Equal(BelegStatus.Gebucht, gebucht.Status);
        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(15m, bestand.Menge);
    }

    [Fact]
    public async Task BuchenAsync_UnzureichenderBestand_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelId, 100, ct);
        var service = new LieferscheinBuchenService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelMitAuswahl_VerknuepftSeriennummernUndSetztAusgeliefert()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelSerialisiertId, 2, ct);
        var positionId = lieferschein.Positionen[0].Id;

        await using var seedDb = new MiletDbContext(_options);
        var seriennummerIds = await seedDb.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).Select(s => s.Id).ToListAsync(ct);

        var service = new LieferscheinBuchenService(_factory);
        await service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>> { [positionId] = seriennummerIds }, ct);

        await using var db = new MiletDbContext(_options);
        Assert.All(await db.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).ToListAsync(ct),
            s => Assert.Equal(SeriennummerStatus.Ausgeliefert, s.Status));
        Assert.Equal(2, await db.BelegPositionSeriennummern.CountAsync(b => b.BelegPositionId == positionId, ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelOhneAuswahl_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferschein = await NeuerLieferscheinAsync(_artikelSerialisiertId, 2, ct);
        var service = new LieferscheinBuchenService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(lieferschein.Id, new Dictionary<int, IReadOnlyList<int>>(), ct));
    }

    [Fact]
    public async Task ParallelesBuchen_MehrererLieferscheine_NieNegativerBestand()
    {
        var ct = TestContext.Current.CancellationToken;
        var lieferscheine = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => NeuerLieferscheinAsync(_artikelId, 5, ct)));
        var service = new LieferscheinBuchenService(_factory);

        var ergebnisse = await Task.WhenAll(lieferscheine.Select(async l =>
        {
            try { await service.BuchenAsync(l.Id, new Dictionary<int, IReadOnlyList<int>>(), ct); return true; }
            catch (InvalidOperationException) { return false; }
        }));

        // 8 x 5 = 40 angefragt, nur 20 verfügbar -> maximal 4 dürfen erfolgreich sein.
        Assert.True(ergebnisse.Count(erfolg => erfolg) <= 4);

        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.True(bestand.Menge >= 0);
    }

    private static bool DockerVerfuegbar()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
        public Task<MiletDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
```

- [ ] **Step 5: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: PASS (oder sauberer Skip ohne Docker).

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Services/LieferscheinBuchenService.cs src/Milet.Infrastructure/DependencyInjection.cs tests/Milet.IntegrationTests/LieferscheinBuchenServiceTests.cs
git commit -m "LieferscheinBuchenService: atomare Bestandsabbuchung + Seriennummern-Pick + Buchungsstatus, eine Transaktion"
```

---

### Task 13: Infrastructure — InventurService

**Files:**
- Modify: `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs`
- Create: `src/Milet.Infrastructure/Services/InventurService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Inventur`, `InventurPosition`, `InventurStatus`, `LagerbewegungTyp.InventurKorrektur` (Task 2), `BestandService.BucheBewegungAsync` (Task 9), `IInventurService` (Task 5).
- Produces: `IInventurService` implementiert — von Task 18 (Inventur-UI) konsumiert.

- [ ] **Step 1: `LagerMapping.cs` — Inventur-Mappings ergänzen**

Modify `src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs` — am Ende der Klasse ergänzen:
```csharp
    public static InventurPositionDto ToDto(this InventurPosition p) =>
        new(p.Id, p.ArtikelId, p.Artikel!.Artikelnummer, p.Artikel.Bezeichnung, p.SollMenge, p.IstMenge);

    public static InventurDto ToDto(this Inventur i, bool mitPositionen) => new()
    {
        Id = i.Id,
        LagerortId = i.LagerortId,
        LagerortBezeichnung = i.Lagerort?.Bezeichnung ?? string.Empty,
        Datum = i.Datum,
        Status = i.Status,
        Positionen = mitPositionen ? i.Positionen.OrderBy(p => p.ArtikelId).Select(p => p.ToDto()).ToList() : [],
        RowVersion = i.RowVersion,
    };
```

- [ ] **Step 2: `InventurService`**

`src/Milet.Infrastructure/Services/InventurService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class InventurService(IDbContextFactory<MiletDbContext> dbContextFactory) : IInventurService
{
    public async Task<IReadOnlyList<InventurDto>> SucheAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var liste = await db.Inventuren.AsNoTracking().Include(i => i.Lagerort)
            .OrderByDescending(i => i.Datum).ThenByDescending(i => i.Id).ToListAsync(ct);
        return liste.Select(i => i.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<InventurDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var inventur = await db.Inventuren.AsNoTracking().Include(i => i.Lagerort)
            .Include(i => i.Positionen).ThenInclude(p => p.Artikel)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(Inventur), id);
        return inventur.ToDto(mitPositionen: true);
    }

    public async Task<InventurDto> NeueInventurAsync(int lagerortId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var lagerort = await db.Lagerorte.FirstOrDefaultAsync(l => l.Id == lagerortId, ct)
            ?? throw new NotFoundException(nameof(Lagerort), lagerortId);

        var bestaende = await db.ArtikelBestaende.AsNoTracking().Where(b => b.LagerortId == lagerortId).ToListAsync(ct);
        var lagerfaehigeArtikel = await db.Artikel.AsNoTracking()
            .Where(a => a.IstLagerartikel && !a.Gesperrt).ToListAsync(ct);

        if (lagerfaehigeArtikel.Count == 0)
            throw new InvalidOperationException("Keine lagerfähigen Artikel für eine Inventur vorhanden.");

        var inventur = new Inventur { LagerortId = lagerortId, Lagerort = lagerort, Datum = DateOnly.FromDateTime(DateTime.Today), Status = InventurStatus.Offen };
        foreach (var artikel in lagerfaehigeArtikel)
        {
            var soll = bestaende.FirstOrDefault(b => b.ArtikelId == artikel.Id)?.Menge ?? 0m;
            inventur.Positionen.Add(new InventurPosition { ArtikelId = artikel.Id, Artikel = artikel, SollMenge = soll });
        }

        db.Add(inventur);
        await db.SaveChangesAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }

    public async Task ErfasseIstMengeAsync(int inventurPositionId, decimal istMenge, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var position = await db.InventurPositionen.Include(p => p.Inventur)
            .FirstOrDefaultAsync(p => p.Id == inventurPositionId, ct)
            ?? throw new NotFoundException(nameof(InventurPosition), inventurPositionId);
        if (position.Inventur!.Status != InventurStatus.Offen)
            throw new InvalidOperationException("Inventur ist bereits abgeschlossen.");

        position.IstMenge = istMenge;
        await db.SaveChangesAsync(ct);
    }

    public async Task<InventurDto> AbschliessenAsync(int inventurId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var inventur = await db.Inventuren.Include(i => i.Positionen).Include(i => i.Lagerort)
            .FirstOrDefaultAsync(i => i.Id == inventurId, ct)
            ?? throw new NotFoundException(nameof(Inventur), inventurId);
        if (inventur.Status != InventurStatus.Offen)
            throw new InvalidOperationException("Inventur ist bereits abgeschlossen.");

        foreach (var position in inventur.Positionen.Where(p => p.IstMenge.HasValue && p.IstMenge != p.SollMenge))
        {
            var delta = position.IstMenge!.Value - position.SollMenge;
            await BestandService.BucheBewegungAsync(db, position.ArtikelId, inventur.LagerortId, delta, LagerbewegungTyp.InventurKorrektur, null, ct);
        }

        inventur.Status = InventurStatus.Abgeschlossen;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await db.Entry(inventur).Collection(i => i.Positionen).Query().Include(p => p.Artikel).LoadAsync(ct);
        return inventur.ToDto(mitPositionen: true);
    }
}
```

- [ ] **Step 3: DI-Registrierung**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<ILieferscheinBuchenService, LieferscheinBuchenService>();` einfügen:
```csharp
        services.AddScoped<IInventurService, InventurService>();
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/LagerMapping.cs src/Milet.Infrastructure/Services/InventurService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "InventurService: Anlegen (SollMenge einfrieren), Ist-Erfassung, Abschluss mit Korrekturbuchungen"
```

---

### Task 14: App — Lagerorte-Tab in KleinstammPage (CRUD)

**Files:**
- Modify: `src/Milet.App/ViewModels/Stammdaten/KleinstammViewModel.cs`
- Modify: `src/Milet.App/Views/Stammdaten/KleinstammPage.xaml`

**Interfaces:**
- Consumes: `ILagerortService`, `LagerortDto` (Task 5/10).
- Produces: Lagerort-Verwaltung im UI — Grundlage für Task 16 (Teillieferungs-Dialog lädt Lagerorte über denselben Service, unabhängig von dieser UI) und Task 15 (Bestandsübersicht referenziert Lagerorte per Anzeige).

**Warum in `KleinstammPage` statt eigener Seite:** `Lagerort` ist eine kleine reine Stammdaten-Lookup-Tabelle ohne eigenen Workflow (wie `Einheit`/`Versandart`) — passt exakt in das bestehende Pivot-Tab-Muster, eine weitere Master-Detail-Seite nur dafür wäre unnötige Duplikation.

- [ ] **Step 1: `KleinstammViewModel.cs` — Konstruktor + Lagerort-Abschnitt**

Modify `src/Milet.App/ViewModels/Stammdaten/KleinstammViewModel.cs` — Feld, Konstruktor-Parameter und Aufruf ergänzen:
```csharp
    private readonly Milet.Application.Lager.ILagerortService _lagerortService;
```
im Konstruktor als letzter Parameter (nach `IDialogService dialogService`):
```csharp
        Milet.Application.Lager.ILagerortService lagerortService)
```
im Konstruktor-Body:
```csharp
        _lagerortService = lagerortService;
```
und im Konstruktor-Body nach `_ = ArtikelLookupsLadenAsync();`:
```csharp
        _ = LagerortenLadenAsync();
```

Am Ende der Klasse (nach dem letzten `StaffelpreisLoeschenAsync`-Block, vor der schließenden `}`) einen neuen Abschnitt ergänzen:
```csharp
    // ---- Lagerorte ----

    [ObservableProperty]
    public partial IReadOnlyList<Milet.Application.Lager.LagerortDto> LagerorteListe { get; set; } = [];

    [ObservableProperty]
    public partial Milet.Application.Lager.LagerortDto? LagerortAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string LagerortCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LagerortBezeichnung { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LagerortAktiv { get; set; } = true;

    [ObservableProperty]
    public partial string? LagerortFehler { get; set; }

    partial void OnLagerortAusgewaehltChanged(Milet.Application.Lager.LagerortDto? value)
    {
        LagerortFehler = null;
        LagerortCode = value?.Code ?? string.Empty;
        LagerortBezeichnung = value?.Bezeichnung ?? string.Empty;
        LagerortAktiv = value?.Aktiv ?? true;
    }

    [RelayCommand]
    private async Task LagerortenLadenAsync() => LagerorteListe = await _lagerortService.SucheAsync(null);

    [RelayCommand]
    private void LagerortNeu() => LagerortAusgewaehlt = null;

    [RelayCommand]
    private async Task LagerortSpeichernAsync()
    {
        LagerortFehler = null;
        var dto = new Milet.Application.Lager.LagerortDto
        {
            Id = LagerortAusgewaehlt?.Id ?? 0,
            Code = LagerortCode,
            Bezeichnung = LagerortBezeichnung,
            Aktiv = LagerortAktiv,
            RowVersion = LagerortAusgewaehlt?.RowVersion ?? [],
        };

        try
        {
            await _lagerortService.SpeichereAsync(dto);
            await LagerortenLadenAsync();
            LagerortNeu();
        }
        catch (ValidationException ex)
        {
            LagerortFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            LagerortFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LagerortLoeschenAsync()
    {
        if (LagerortAusgewaehlt is not { } lagerort)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Lagerort löschen", $"Lagerort '{lagerort.Bezeichnung}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            await _lagerortService.LoescheAsync(lagerort.Id);
            LagerortNeu();
            await LagerortenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }
```

- [ ] **Step 2: `KleinstammPage.xaml` — sechster Tab**

Modify `src/Milet.App/Views/Stammdaten/KleinstammPage.xaml` — direkt vor dem schließenden `</Pivot>` einen neuen `PivotItem` einfügen (gleiches Muster wie der „Einheiten"-Tab):
```xml
            <PivotItem Header="Lagerorte">
                <Grid ColumnSpacing="24">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="380" />
                        <ColumnDefinition Width="360" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>

                    <ListView Grid.Column="0" Style="{StaticResource MasterListStyle}" ItemsSource="{x:Bind ViewModel.LagerorteListe, Mode=OneWay}" SelectedItem="{x:Bind ViewModel.LagerortAusgewaehlt, Mode=TwoWay}" SelectionMode="Single">
                        <ListView.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" Spacing="12" Padding="4">
                                    <TextBlock Text="{Binding Code}" Width="60" FontWeight="SemiBold" />
                                    <TextBlock Text="{Binding Bezeichnung}" />
                                </StackPanel>
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>

                    <StackPanel Grid.Column="1" Spacing="12">
                        <InfoBar IsOpen="{x:Bind ViewModel.LagerortFehler, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}" Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.LagerortFehler, Mode=OneWay}" />
                        <TextBox Header="Code *" Text="{x:Bind ViewModel.LagerortCode, Mode=TwoWay}" />
                        <TextBox Header="Bezeichnung *" Text="{x:Bind ViewModel.LagerortBezeichnung, Mode=TwoWay}" />
                        <CheckBox Content="Aktiv" IsChecked="{x:Bind ViewModel.LagerortAktiv, Mode=TwoWay}" />
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <Button Content="Neu" Command="{x:Bind ViewModel.LagerortNeuCommand}" />
                            <Button Content="Speichern" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.LagerortSpeichernCommand}" />
                            <Button Content="Löschen" Command="{x:Bind ViewModel.LagerortLoeschenCommand}" />
                        </StackPanel>
                    </StackPanel>
                </Grid>
            </PivotItem>
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.App/ViewModels/Stammdaten/KleinstammViewModel.cs src/Milet.App/Views/Stammdaten/KleinstammPage.xaml
git commit -m "Lagerorte-Tab in Kleinstamm-Settings (CRUD)"
```

---

### Task 15: App — Bestandsübersicht + Seriennummern-Erfassung (eine Seite, zwei Detail-Modi)

**Files:**
- Create: `src/Milet.App/ViewModels/Lager/BestandUebersichtViewModel.cs`
- Create: `src/Milet.App/Views/Lager/BestandUebersichtPage.xaml`
- Create: `src/Milet.App/Views/Lager/BestandUebersichtPage.xaml.cs`

**Interfaces:**
- Consumes: `IBestandService`, `ISeriennummernService`, `ILagerortService` (Task 5/9/10/11), `IDialogService` (bestehend).
- Produces: `BestandUebersichtViewModel`, `BestandUebersichtPage` — Registrierung in App.xaml.cs/ShellPage folgt zentral in Task 19.

**Warum eine Seite statt zwei:** Bestandskorrektur (normale Artikel) und Seriennummern-Erfassung (serialisierte Artikel) sind beides Wege, Erstbestand ins System zu bringen — je nachdem, ob der ausgewählte Artikel `HatSeriennummern` ist, zeigt die Detailspalte das passende Formular. Eine gemeinsame Master-Liste vermeidet eine dritte fast-identische List-Seite.

- [ ] **Step 1: `BestandUebersichtViewModel`**

`src/Milet.App/ViewModels/Lager/BestandUebersichtViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class BestandUebersichtViewModel : ObservableObject
{
    private readonly IBestandService _bestandService;
    private readonly ISeriennummernService _seriennummernService;
    private readonly ILagerortService _lagerortService;
    private readonly IDialogService _dialogService;

    public BestandUebersichtViewModel(
        IBestandService bestandService, ISeriennummernService seriennummernService,
        ILagerortService lagerortService, IDialogService dialogService)
    {
        _bestandService = bestandService;
        _seriennummernService = seriennummernService;
        _lagerortService = lagerortService;
        _dialogService = dialogService;
        _ = LadenAsync();
        _ = LagerorteLadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelBestandDto> Bestaende { get; set; } = [];
    [ObservableProperty] public partial ArtikelBestandDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LagerortDto> Lagerorte { get; set; } = [];

    [ObservableProperty] public partial decimal KorrekturMengeDelta { get; set; }
    [ObservableProperty] public partial string KorrekturGrund { get; set; } = string.Empty;
    [ObservableProperty] public partial string? KorrekturFehler { get; set; }

    [ObservableProperty] public partial IReadOnlyList<SeriennummerDto> SeriennummernAufLager { get; set; } = [];
    [ObservableProperty] public partial string NeueSeriennummer { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SeriennummerFehler { get; set; }

    public Microsoft.UI.Xaml.Visibility ZeigtSeriennummernPanel =>
        Ausgewaehlt is { HatSeriennummern: true } ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility ZeigtKorrekturPanel =>
        Ausgewaehlt is { HatSeriennummern: false } ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    partial void OnAusgewaehltChanged(ArtikelBestandDto? value)
    {
        KorrekturFehler = null;
        SeriennummerFehler = null;
        NeueSeriennummer = string.Empty;
        KorrekturMengeDelta = 0;
        KorrekturGrund = string.Empty;
        SeriennummernAufLager = [];
        OnPropertyChanged(nameof(ZeigtSeriennummernPanel));
        OnPropertyChanged(nameof(ZeigtKorrekturPanel));
        if (value is { HatSeriennummern: true }) _ = SeriennummernLadenAsync(value.ArtikelId);
    }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Bestaende = await _bestandService.SucheAsync(Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private async Task LagerorteLadenAsync() => Lagerorte = await _lagerortService.SucheAsync(null);

    private async Task SeriennummernLadenAsync(int artikelId) => SeriennummernAufLager = await _seriennummernService.AufLagerAsync(artikelId);

    [RelayCommand]
    private async Task KorrekturBuchenAsync()
    {
        if (Ausgewaehlt is not { } bestand) return;
        KorrekturFehler = null;
        try
        {
            await _bestandService.KorrigiereAsync(new BestandskorrekturDto
            {
                ArtikelId = bestand.ArtikelId,
                LagerortId = bestand.LagerortId,
                MengeDelta = KorrekturMengeDelta,
                Grund = KorrekturGrund,
            });
            KorrekturMengeDelta = 0;
            KorrekturGrund = string.Empty;
            await LadenAsync();
        }
        catch (ValidationException ex)
        {
            KorrekturFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            KorrekturFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SeriennummerErfassenAsync()
    {
        if (Ausgewaehlt is not { } bestand) return;
        SeriennummerFehler = null;
        try
        {
            await _seriennummernService.ErfasseAsync(bestand.ArtikelId, bestand.LagerortId, NeueSeriennummer);
            NeueSeriennummer = string.Empty;
            await SeriennummernLadenAsync(bestand.ArtikelId);
            await LadenAsync();
        }
        catch (Exception ex)
        {
            SeriennummerFehler = ex.Message;
        }
    }
}
```

- [ ] **Step 2: `BestandUebersichtPage.xaml`**

`src/Milet.App/Views/Lager/BestandUebersichtPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Lager.BestandUebersichtPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Padding="24" ColumnSpacing="24">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="360" />
        </Grid.ColumnDefinitions>

        <Grid Grid.Column="0" RowSpacing="12">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Text="Bestandsübersicht" Style="{StaticResource TitleTextBlockStyle}" />

            <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8">
                <TextBox Width="300" PlaceholderText="Suche (Artikel)…" Text="{x:Bind ViewModel.Suchtext, Mode=TwoWay}" />
                <Button Content="Suchen" Command="{x:Bind ViewModel.LadenCommand}" />
                <ProgressRing IsActive="{x:Bind ViewModel.LaedtGerade, Mode=OneWay}" Width="24" Height="24" />
            </StackPanel>

            <ListView Grid.Row="2"
                ItemsSource="{x:Bind ViewModel.Bestaende, Mode=OneWay}"
                SelectedItem="{x:Bind ViewModel.Ausgewaehlt, Mode=TwoWay}"
                SelectionMode="Single">
                <ListView.HeaderTemplate>
                    <DataTemplate>
                        <Grid Padding="8,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="120" /><ColumnDefinition Width="260" />
                                <ColumnDefinition Width="160" /><ColumnDefinition Width="100" />
                                <ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Artikelnr." FontWeight="SemiBold" />
                            <TextBlock Grid.Column="1" Text="Bezeichnung" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="2" Text="Lagerort" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="3" Text="Menge" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="4" Text="Mindest" FontWeight="SemiBold" />
                        </Grid>
                    </DataTemplate>
                </ListView.HeaderTemplate>
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="8,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="120" /><ColumnDefinition Width="260" />
                                <ColumnDefinition Width="160" /><ColumnDefinition Width="100" />
                                <ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{Binding Artikelnummer}" />
                            <TextBlock Grid.Column="1" Text="{Binding ArtikelBezeichnung}" />
                            <TextBlock Grid.Column="2" Text="{Binding LagerortBezeichnung}" />
                            <TextBlock Grid.Column="3" Text="{Binding Menge}" />
                            <TextBlock Grid.Column="4" Text="{Binding Mindestbestand}" />
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </Grid>

        <StackPanel Grid.Column="1" Spacing="12">
            <StackPanel Spacing="12" Visibility="{x:Bind ViewModel.ZeigtKorrekturPanel, Mode=OneWay}">
                <TextBlock Text="Bestandskorrektur" Style="{StaticResource SubtitleTextBlockStyle}" />
                <InfoBar IsOpen="{x:Bind ViewModel.KorrekturFehler, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}" Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.KorrekturFehler, Mode=OneWay}" />
                <NumberBox Header="Mengenänderung (+ / -)" Value="{x:Bind ViewModel.KorrekturMengeDelta, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" SpinButtonPlacementMode="Compact" />
                <TextBox Header="Grund *" Text="{x:Bind ViewModel.KorrekturGrund, Mode=TwoWay}" />
                <Button Content="Buchen" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.KorrekturBuchenCommand}" />
            </StackPanel>

            <StackPanel Spacing="12" Visibility="{x:Bind ViewModel.ZeigtSeriennummernPanel, Mode=OneWay}">
                <TextBlock Text="Seriennummern (auf Lager)" Style="{StaticResource SubtitleTextBlockStyle}" />
                <InfoBar IsOpen="{x:Bind ViewModel.SeriennummerFehler, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}" Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.SeriennummerFehler, Mode=OneWay}" />
                <ListView MaxHeight="200" ItemsSource="{x:Bind ViewModel.SeriennummernAufLager, Mode=OneWay}">
                    <ListView.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Nummer}" />
                        </DataTemplate>
                    </ListView.ItemTemplate>
                </ListView>
                <TextBox Header="Neue Seriennummer" Text="{x:Bind ViewModel.NeueSeriennummer, Mode=TwoWay}" />
                <Button Content="Erfassen" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.SeriennummerErfassenCommand}" />
            </StackPanel>
        </StackPanel>
    </Grid>
</Page>
```

- [ ] **Step 3: `BestandUebersichtPage.xaml.cs`**

`src/Milet.App/Views/Lager/BestandUebersichtPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class BestandUebersichtPage : Page
{
    public BestandUebersichtViewModel ViewModel { get; }
    public BestandUebersichtPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BestandUebersichtViewModel>();
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.App/ViewModels/Lager/BestandUebersichtViewModel.cs src/Milet.App/Views/Lager/BestandUebersichtPage.xaml src/Milet.App/Views/Lager/BestandUebersichtPage.xaml.cs
git commit -m "Bestandsübersicht + Seriennummern-Erfassung (App): eine Seite, Detail-Panel je nach Artikeltyp"
```

---

### Task 16: App — BelegEditViewModelBase-Erweiterung + AuftragEditViewModel „→ Lieferschein" + Teillieferungs-Dialog

**Files:**
- Modify: `src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs`
- Modify: `src/Milet.App/ViewModels/Verkauf/AuftragEditViewModel.cs`
- Modify: `src/Milet.App/Views/Verkauf/AuftragEditPage.xaml`
- Create: `src/Milet.App/Views/Lager/TeillieferungDialog.xaml`
- Create: `src/Milet.App/Views/Lager/TeillieferungDialog.xaml.cs`

**Interfaces:**
- Consumes: `IBelegUeberleitungService.LadeOffenePositionenAsync`/`.UeberleitenMitAuswahlAsync` (Task 7), `ILagerortService` (Task 10), `OffenePositionDto`, `LagerortDto`.
- Produces: `BelegEditViewModelBase.Id`/`.UeberleitungService` als `protected` (statt `private _id`/`_ueberleitungService`) — von `AuftragEditViewModel` und allen bestehenden Subklassen (`AngebotEditViewModel`, `RechnungEditViewModel` — unverändertes Verhalten, nur andere Sichtbarkeit) genutzt. `TeillieferungDialog` — eigenständiger `ContentDialog`, von `AuftragEditViewModel` instanziiert.

**Warum diese Umbenennung statt einer neuen Abstraktion:** Die einzigen zwei Dinge, die eine neue „→ Lieferschein"-Aktion vom Auftrag aus braucht, sind die aktuelle Beleg-Id und den `IBelegUeberleitungService` — beide existieren bereits in der Basisklasse, nur `private`. `protected` statt einer neuen Indirektionsebene ist die kleinste Änderung, die den bestehenden Vertrag der Basisklasse nicht bricht (alle drei Subklassen kompilieren unverändert weiter).

- [ ] **Step 1: `BelegEditViewModelBase.cs` — `_id`/`_ueberleitungService` zu `protected Id`/`UeberleitungService`**

Modify `src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs` — vollständiger neuer Inhalt:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public abstract partial class BelegEditViewModelBase : ObservableObject, INavigationAware
{
    private readonly BelegTyp _typ;
    private readonly IBelegService _belegService;
    private readonly IVerkaufLookupService _lookupService;
    protected readonly IBelegUeberleitungService UeberleitungService;
    private readonly IRechnungBuchenService? _buchenService;
    private readonly IPdfService _pdfService;
    protected readonly INavigationService Navigation;
    protected readonly IDialogService DialogService;

    protected int Id;
    private byte[] _rowVersion = [];
    private int _naechstePositionsNr = 1;

    protected BelegEditViewModelBase(
        BelegTyp typ,
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IRechnungBuchenService? buchenService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
    {
        _typ = typ;
        _belegService = belegService;
        _lookupService = lookupService;
        UeberleitungService = ueberleitungService;
        _buchenService = buchenService;
        _pdfService = pdfService;
        Navigation = navigation;
        DialogService = dialogService;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = "(automatisch)";
    /// <summary>Nullable, exakt der Typ von <c>CalendarDatePicker.Date</c> — vermeidet einen Konverter/Absturz beim x:Bind TwoWay, falls der Nutzer das Datum leert.</summary>
    [ObservableProperty] public partial DateTimeOffset? BelegDatum { get; set; } = DateTimeOffset.Now;
    [ObservableProperty] public partial IReadOnlyList<KundeVerkaufLookupDto> Kunden { get; set; } = [];
    [ObservableProperty] public partial int KundeId { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelVerkaufLookupDto> ArtikelLookups { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial BelegPositionDto? PositionAusgewaehlt { get; set; }
    [ObservableProperty] public partial int? PositionArtikelId { get; set; }
    [ObservableProperty] public partial decimal PositionMenge { get; set; } = 1;
    [ObservableProperty] public partial decimal PositionEinzelpreis { get; set; }
    [ObservableProperty] public partial decimal PositionRabattProzent { get; set; }

    [ObservableProperty] public partial decimal SummeNetto { get; set; }
    [ObservableProperty] public partial decimal SummeMwSt { get; set; }
    [ObservableProperty] public partial decimal SummeBrutto { get; set; }

    [ObservableProperty] public partial BelegStatus Status { get; set; } = BelegStatus.Entwurf;
    [ObservableProperty] public partial DateTimeOffset? Faelligkeit { get; set; }
    [ObservableProperty] public partial string? Kopftext { get; set; }
    [ObservableProperty] public partial string? Fusstext { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; } = true;

    /// <summary>Als <see cref="Microsoft.UI.Xaml.Visibility"/> statt <c>bool</c> — x:Bind konvertiert bool nicht automatisch in Visibility, ein eigener Converter wäre hierfür Overhead.</summary>
    public Microsoft.UI.Xaml.Visibility ZeigtBuchenButton =>
        _buchenService is not null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility ZeigtUeberleitenButton =>
        _typ != BelegTyp.Rechnung ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string UeberleitenButtonText => _typ switch
    {
        BelegTyp.Angebot => "→ Auftrag",
        BelegTyp.Auftrag => "→ Rechnung",
        _ => string.Empty,
    };

    /// <summary>x:Bind-Funktionsbindung für die schreibgeschützte Fälligkeits-Anzeige (nur `RechnungEditPage`).</summary>
    public string FormatiereDatum(DateTimeOffset? wert) => wert?.ToString("dd.MM.yyyy") ?? "–";

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        Id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Kunden = lookups.Kunden;
        ArtikelLookups = lookups.Artikel;

        if (Id == 0)
        {
            IstBearbeitbar = true;
            return;
        }

        var beleg = await _belegService.LadeAsync(Id);
        _rowVersion = beleg.RowVersion;
        BelegNummer = string.IsNullOrEmpty(beleg.BelegNummer) ? "(wird beim Buchen vergeben)" : beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum.ToDateTime(TimeOnly.MinValue);
        KundeId = beleg.KundeId;
        Positionen = new ObservableCollection<BelegPositionDto>(beleg.Positionen);
        _naechstePositionsNr = Positionen.Count == 0 ? 1 : Positionen.Max(p => p.PositionsNr) + 1;
        SummeNetto = beleg.SummeNetto;
        SummeMwSt = beleg.SummeMwSt;
        SummeBrutto = beleg.SummeBrutto;
        Status = beleg.Status;
        Faelligkeit = beleg.Faelligkeit?.ToDateTime(TimeOnly.MinValue);
        Kopftext = beleg.Kopftext;
        Fusstext = beleg.Fusstext;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private async Task PreisVorschlagAsync()
    {
        if (PositionArtikelId is not { } artikelId || KundeId == 0) return;
        var menge = PositionMenge <= 0 ? 1 : PositionMenge;
        var ergebnis = await _lookupService.ErmittlePreisAsync(artikelId, menge, KundeId);
        PositionEinzelpreis = ergebnis.Einzelpreis;
        PositionRabattProzent = ergebnis.RabattProzent;
    }

    [RelayCommand]
    private void PositionHinzufuegen()
    {
        if (PositionArtikelId is not { } artikelId) { Fehlermeldung = "Artikel auswählen."; return; }
        if (PositionMenge <= 0) { Fehlermeldung = "Menge muss größer 0 sein."; return; }
        var artikel = ArtikelLookups.FirstOrDefault(a => a.Id == artikelId);
        if (artikel is null) return;

        Positionen.Add(new BelegPositionDto
        {
            PositionsNr = _naechstePositionsNr++,
            PositionsTyp = PositionsTyp.Artikel,
            ArtikelId = artikel.Id,
            Bezeichnung = artikel.Bezeichnung,
            EinheitKuerzel = artikel.EinheitKuerzel,
            Menge = PositionMenge,
            Einzelpreis = PositionEinzelpreis,
            RabattProzent = PositionRabattProzent,
            MwStSatzId = artikel.MwStSatzId,
            MwStSatzWert = artikel.MwStSatzWert,
            SteuerSchluessel = artikel.SteuerSchluessel,
            GesamtNetto = BerechnePositionsNetto(PositionMenge, PositionEinzelpreis, PositionRabattProzent),
        });

        PositionArtikelId = null;
        PositionMenge = 1;
        PositionEinzelpreis = 0;
        PositionRabattProzent = 0;
        Fehlermeldung = null;
        AktualisiereSummen();
    }

    [RelayCommand]
    private void PositionEntfernen()
    {
        if (PositionAusgewaehlt is not { } position) return;
        Positionen.Remove(position);
        PositionAusgewaehlt = null;
        AktualisiereSummen();
    }

    private void AktualisiereSummen()
    {
        decimal netto = 0, mwst = 0;
        foreach (var gruppe in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel).GroupBy(p => p.MwStSatzWert))
        {
            var gruppenNetto = Math.Round(gruppe.Sum(p => p.GesamtNetto), 2, MidpointRounding.ToEven);
            netto += gruppenNetto;
            mwst += Math.Round(gruppenNetto * gruppe.Key / 100m, 2, MidpointRounding.ToEven);
        }
        SummeNetto = netto;
        SummeMwSt = mwst;
        SummeBrutto = netto + mwst;
    }

    /// <summary>UI-Vorschau — Server berechnet autoritativ neu (siehe Klassenkopf-Kommentar).</summary>
    private static decimal BerechnePositionsNetto(decimal menge, decimal einzelpreis, decimal rabattProzent)
    {
        var brutto = menge * einzelpreis;
        var nachRabatt = brutto * (1 - rabattProzent / 100m);
        return Math.Round(nachRabatt, 2, MidpointRounding.ToEven);
    }

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;
        var dto = new BelegDto
        {
            Id = Id,
            BelegTyp = _typ,
            BelegDatum = DateOnly.FromDateTime((BelegDatum ?? DateTimeOffset.Now).DateTime),
            KundeId = KundeId,
            Kopftext = Kopftext,
            Fusstext = Fusstext,
            Positionen = Positionen.ToList(),
            RowVersion = _rowVersion,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            Id = gespeichert.Id;
            _rowVersion = gespeichert.RowVersion;
            BelegNummer = string.IsNullOrEmpty(gespeichert.BelegNummer) ? "(wird beim Buchen vergeben)" : gespeichert.BelegNummer;
            SummeNetto = gespeichert.SummeNetto;
            SummeMwSt = gespeichert.SummeMwSt;
            SummeBrutto = gespeichert.SummeBrutto;
        }
        catch (ValidationException ex)
        {
            Fehlermeldung = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (ConcurrencyConflictException)
        {
            var neuLaden = await DialogService.BestaetigenAsync(
                "Datensatz geändert", "Dieser Beleg wurde zwischenzeitlich von einem anderen Benutzer geändert. Neu laden?");
            if (neuLaden) await InitAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_buchenService is null || Id == 0) return;
        try
        {
            var gebucht = await _buchenService.BuchenAsync(Id);
            BelegNummer = gebucht.BelegNummer;
            Status = gebucht.Status;
            Faelligkeit = gebucht.Faelligkeit?.ToDateTime(TimeOnly.MinValue);
            IstBearbeitbar = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PdfAsync()
    {
        if (Id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        try
        {
            var pdfBytes = await _pdfService.GeneriereBelegPdfAsync(Id);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            picker.SuggestedFileName = string.IsNullOrEmpty(BelegNummer) ? "Beleg" : BelegNummer.Replace('/', '-');
            picker.FileTypeChoices.Add("PDF-Dokument", [".pdf"]);
            var datei = await picker.PickSaveFileAsync();
            if (datei is null) return;
            await Windows.Storage.FileIO.WriteBytesAsync(datei, pdfBytes);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UeberleitenAsync()
    {
        if (Id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        var zielTyp = _typ switch
        {
            BelegTyp.Angebot => BelegTyp.Auftrag,
            BelegTyp.Auftrag => BelegTyp.Rechnung,
            _ => (BelegTyp?)null,
        };
        if (zielTyp is null) return;

        try
        {
            await UeberleitungService.UeberleitenAsync(Id, zielTyp.Value);
            NavigiereNachUeberleitung(zielTyp.Value);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => NavigiereZurListe();

    protected abstract void NavigiereZurListe();
    protected abstract void NavigiereNachUeberleitung(BelegTyp zielTyp);
}
```

**Hinweis:** `_dialogService` wurde ebenfalls zu `protected DialogService` (umbenannt von `_dialogService`) — wird von `AuftragEditViewModel` in Task 16 Step 2 für den Teillieferungs-Dialog nicht direkt gebraucht (der Dialog nutzt `App.MainWindow.Content.XamlRoot` wie `IDialogService` selbst), ist aber für künftige Subklassen konsequent gleich behandelt wie `Id`/`UeberleitungService`/`Navigation`.

- [ ] **Step 2: `AuftragEditViewModel.cs` — „→ Lieferschein"-Kommando**

Modify `src/Milet.App/ViewModels/Verkauf/AuftragEditViewModel.cs` — vollständiger neuer Inhalt:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.App.Views.Lager;
using Milet.Application.Abstractions;
using Milet.Application.Lager;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AuftragEditViewModel : BelegEditViewModelBase
{
    private readonly ILagerortService _lagerortService;

    public AuftragEditViewModel(
        IBelegService belegService, IVerkaufLookupService lookupService, IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService, INavigationService navigation, IDialogService dialogService, ILagerortService lagerortService)
        : base(BelegTyp.Auftrag, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService)
    {
        _lagerortService = lagerortService;
    }

    [RelayCommand]
    private async Task UeberleitenZuLieferscheinAsync()
    {
        if (Id == 0) { Fehlermeldung = "Auftrag muss erst gespeichert werden."; return; }

        var lagerorte = (await _lagerortService.SucheAsync(null)).Where(l => l.Aktiv).ToList();
        if (lagerorte.Count == 0) { Fehlermeldung = "Kein aktiver Lagerort angelegt."; return; }

        var offenePositionen = await UeberleitungService.LadeOffenePositionenAsync(Id);
        if (offenePositionen.Count == 0) { Fehlermeldung = "Keine offenen Positionen für eine Lieferung vorhanden."; return; }

        var dialog = new TeillieferungDialog(offenePositionen, lagerorte) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await UeberleitungService.UeberleitenMitAuswahlAsync(Id, BelegTyp.Lieferschein, dialog.GewaehlteMengen(), dialog.AusgewaehlterLagerortId);
            Navigation.Navigate<Lager.LieferscheinListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AuftragListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
```

**Hinweis:** `Navigation.Navigate<Lager.LieferscheinListViewModel>()` referenziert das ViewModel aus Task 17 per vollem Namespace-Präfix `Lager.` (Namenskollision vermeiden, falls später ein `Verkauf.LieferscheinListViewModel` existieren sollte — gibt es nicht, aber der Präfix macht die Modulzugehörigkeit explizit sichtbar, analog zu `Milet.Application.Lager.LagerortDto` in Task 14). `using Milet.App.ViewModels.Lager;` zum `using`-Block ergänzen statt des Präfixes ist ebenso zulässig — hier bewusst der Präfix, weil `AuftragEditViewModel` sonst keine weitere Lager-Klasse referenziert.

- [ ] **Step 3: `AuftragEditPage.xaml` — Button ergänzen**

Modify `src/Milet.App/Views/Verkauf/AuftragEditPage.xaml` — analog zu `AngebotEditPage.xaml`, aber mit einem zusätzlichen Button. In der Button-Leiste (`<StackPanel Orientation="Horizontal" Spacing="8">` am Seitenende) nach dem `Überleiten`-Button (`Content="{x:Bind ViewModel.UeberleitenButtonText, ...}"`) einfügen:
```xml
                <Button Content="→ Lieferschein" Command="{x:Bind ViewModel.UeberleitenZuLieferscheinCommand}" />
```

- [ ] **Step 4: `TeillieferungDialog` — XAML**

`src/Milet.App/Views/Lager/TeillieferungDialog.xaml`:
```xml
<ContentDialog
    x:Class="Milet.App.Views.Lager.TeillieferungDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Lieferschein erzeugen"
    PrimaryButtonText="Lieferschein erzeugen"
    CloseButtonText="Abbrechen"
    DefaultButton="Primary">
    <StackPanel Spacing="12" MinWidth="500">
        <ComboBox Header="Lagerort *" HorizontalAlignment="Stretch"
                  ItemsSource="{x:Bind Lagerorte, Mode=OneWay}"
                  SelectedValue="{x:Bind AusgewaehlterLagerortId, Mode=TwoWay}"
                  SelectedValuePath="Id" DisplayMemberPath="Bezeichnung" />
        <TextBlock Text="Zu liefernde Mengen (0 = Position wird nicht mitgeliefert):" />
        <ListView MaxHeight="300" ItemsSource="{x:Bind Zeilen}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="local:TeillieferungZeile">
                    <Grid ColumnSpacing="12" Padding="4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" /><ColumnDefinition Width="100" />
                            <ColumnDefinition Width="120" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind Bezeichnung}" VerticalAlignment="Center" />
                        <TextBlock Grid.Column="1" Text="{x:Bind OffeneMenge}" VerticalAlignment="Center" />
                        <NumberBox Grid.Column="2" Minimum="0" Maximum="{x:Bind OffeneMenge}" SpinButtonPlacementMode="Compact"
                                   Value="{x:Bind GewaehlteMenge, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </StackPanel>
</ContentDialog>
```
Am Kopf der Datei zusätzlich `xmlns:local="using:Milet.App.Views.Lager"` ergänzen (für `x:DataType="local:TeillieferungZeile"`).

- [ ] **Step 5: `TeillieferungDialog` — Code-Behind**

`src/Milet.App/Views/Lager/TeillieferungDialog.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class TeillieferungZeile : ObservableObject
{
    public int PositionId { get; }
    public string Bezeichnung { get; }
    public decimal OffeneMenge { get; }

    [ObservableProperty]
    public partial decimal GewaehlteMenge { get; set; }

    public TeillieferungZeile(OffenePositionDto dto)
    {
        PositionId = dto.PositionId;
        Bezeichnung = dto.EinheitKuerzel is { } einheit ? $"{dto.Bezeichnung} ({einheit})" : dto.Bezeichnung;
        OffeneMenge = dto.OffeneMenge;
        GewaehlteMenge = dto.OffeneMenge;
    }
}

public sealed partial class TeillieferungDialog : ContentDialog
{
    public ObservableCollection<TeillieferungZeile> Zeilen { get; }
    public IReadOnlyList<LagerortDto> Lagerorte { get; }
    public int AusgewaehlterLagerortId { get; set; }

    public TeillieferungDialog(IReadOnlyList<OffenePositionDto> offenePositionen, IReadOnlyList<LagerortDto> lagerorte)
    {
        InitializeComponent();
        Zeilen = new ObservableCollection<TeillieferungZeile>(offenePositionen.Select(p => new TeillieferungZeile(p)));
        Lagerorte = lagerorte;
        AusgewaehlterLagerortId = lagerorte[0].Id;
    }

    public IReadOnlyDictionary<int, decimal> GewaehlteMengen() =>
        Zeilen.Where(z => z.GewaehlteMenge > 0).ToDictionary(z => z.PositionId, z => z.GewaehlteMenge);
}
```

- [ ] **Step 6: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler. **Erwarteter Compile-Fehler an dieser Stelle**, falls Task 17 noch nicht erledigt ist: `Milet.App.ViewModels.Lager.LieferscheinListViewModel` existiert noch nicht — dieser Task referenziert es bereits vorausschauend in `AuftragEditViewModel.NavigiereNachUeberleitung`-Aufrufstelle. Reihenfolge: Task 17 MUSS vor dem finalen Build-Check dieses Tasks existieren, oder der Build-Check wird nach Task 17 nachgeholt (Executor-Hinweis: Tasks 16 und 17 sind gegenseitig abhängig — `TeillieferungDialog` aus Task 16 wird von Task 17 nicht gebraucht, aber `AuftragEditViewModel` aus Task 16 braucht `LieferscheinListViewModel` aus Task 17. Empfehlung: Task 16 Schritte 1–5 committen, Build-Check erst nach Task 17 gemeinsam durchführen.)

- [ ] **Step 7: Commit**

```bash
git add src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs src/Milet.App/ViewModels/Verkauf/AuftragEditViewModel.cs src/Milet.App/Views/Verkauf/AuftragEditPage.xaml src/Milet.App/Views/Lager/TeillieferungDialog.xaml src/Milet.App/Views/Lager/TeillieferungDialog.xaml.cs
git commit -m "AuftragEditViewModel: Teillieferungs-Dialog -> Lieferschein (Menge je Position, Lagerort-Auswahl)"
```

---

### Task 17: App — Lieferschein-Liste (inkl. Sammelrechnung) + -Editor + Seriennummern-Auswahl-Dialog + Buchen

**Files:**
- Create: `src/Milet.App/ViewModels/Lager/LieferscheinListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Lager/LieferscheinEditViewModel.cs`
- Create: `src/Milet.App/Views/Lager/LieferscheinListPage.xaml`
- Create: `src/Milet.App/Views/Lager/LieferscheinListPage.xaml.cs`
- Create: `src/Milet.App/Views/Lager/LieferscheinEditPage.xaml`
- Create: `src/Milet.App/Views/Lager/LieferscheinEditPage.xaml.cs`
- Create: `src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml`
- Create: `src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml.cs`

**Interfaces:**
- Consumes: `IBelegService` (Lieferschein generisch via `BelegTyp.Lieferschein`, Task 7), `IBelegUeberleitungService.UeberleitenMehrereAsync` (Task 8), `ILieferscheinBuchenService` (Task 12), `ISeriennummernService.AufLagerAsync` (Task 11), `IVerkaufLookupService` (Phase 2, `ArtikelVerkaufLookupDto.HatSeriennummern` aus Task 7).
- Produces: `LieferscheinListViewModel`, `LieferscheinEditViewModel`, `SeriennummernAuswahlDialog` — Registrierung folgt zentral in Task 19.

**Warum `LieferscheinEditViewModel` NICHT `BelegEditViewModelBase` erbt:** Ein Lieferschein wird ausschließlich über den Teillieferungs-Dialog (Task 16) erzeugt — seine Positionen sind durch diese Auswahl bereits fixiert, es gibt keinen "Position hinzufügen"-Workflow wie bei Angebot/Auftrag/Rechnung. Der Editor ist daher reine Anzeige + Buchen-Aktion, keine volle Editier-UI. Eine Vererbung von `BelegEditViewModelBase` (das komplette Positions-Add/Remove/Speichern mitbringt) würde hier nur ungenutzte Oberfläche erben — ein eigenes schlankes ViewModel ist klarer als eine Basisklasse mit teilweise deaktivierten Fähigkeiten. Lieferschein-PDF ist bewusst nicht Teil dieses Tasks (siehe Global Constraints).

- [ ] **Step 1: `LieferscheinListViewModel`**

`src/Milet.App/ViewModels/Lager/LieferscheinListViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Lager;

public sealed partial class LieferscheinListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public LieferscheinListViewModel(
        IBelegService belegService, IBelegUeberleitungService ueberleitungService,
        INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService;
        _ueberleitungService = ueberleitungService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    /// <summary>Wird per Code-Behind aus `ListView.SelectionChanged` befüllt (Mehrfachauswahl für Sammelrechnung) — siehe `LieferscheinListPage.xaml.cs`.</summary>
    public List<int> AusgewaehlteIds { get; set; } = [];

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Lieferschein, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private void Bearbeiten() { if (Ausgewaehlt is { } beleg) _navigation.Navigate<LieferscheinEditViewModel>(beleg.Id); }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Lieferschein löschen", $"Lieferschein '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }

    [RelayCommand]
    private async Task SammelrechnungAsync()
    {
        if (AusgewaehlteIds.Count == 0)
        {
            await _dialogService.ZeigeFehlerAsync("Sammelrechnung", "Mindestens einen Lieferschein auswählen.");
            return;
        }

        try
        {
            await _ueberleitungService.UeberleitenMehrereAsync(AusgewaehlteIds, BelegTyp.Rechnung);
            _navigation.Navigate<Verkauf.RechnungListViewModel>();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Sammelrechnung fehlgeschlagen", ex.Message);
        }
    }
}
```

- [ ] **Step 2: `LieferscheinEditViewModel`**

`src/Milet.App/ViewModels/Lager/LieferscheinEditViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Lager;

public sealed partial class LieferscheinEditViewModel : ObservableObject, INavigationAware
{
    private readonly IBelegService _belegService;
    private readonly IVerkaufLookupService _lookupService;
    private readonly ILieferscheinBuchenService _buchenService;
    private readonly Milet.Application.Lager.ISeriennummernService _seriennummernService;
    private readonly INavigationService _navigation;

    private int _id;
    private IReadOnlyList<ArtikelVerkaufLookupDto> _artikelLookups = [];

    public LieferscheinEditViewModel(
        IBelegService belegService, IVerkaufLookupService lookupService, ILieferscheinBuchenService buchenService,
        Milet.Application.Lager.ISeriennummernService seriennummernService, INavigationService navigation)
    {
        _belegService = belegService;
        _lookupService = lookupService;
        _buchenService = buchenService;
        _seriennummernService = seriennummernService;
        _navigation = navigation;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly BelegDatum { get; set; }
    [ObservableProperty] public partial string KundeAnzeige { get; set; } = string.Empty;
    [ObservableProperty] public partial BelegStatus Status { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = LadenAsync();
    }

    private async Task LadenAsync()
    {
        if (_id == 0) return;
        var lookups = await _lookupService.LadeLookupsAsync();
        _artikelLookups = lookups.Artikel;

        var beleg = await _belegService.LadeAsync(_id);
        BelegNummer = beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum;
        KundeAnzeige = beleg.KundeAnzeige;
        Status = beleg.Status;
        Positionen = beleg.Positionen;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        var seriennummernJePosition = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var position in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            var artikel = _artikelLookups.FirstOrDefault(a => a.Id == position.ArtikelId);
            if (artikel is not { HatSeriennummern: true }) continue;

            var verfuegbar = await _seriennummernService.AufLagerAsync(position.ArtikelId!.Value);
            var dialog = new Milet.App.Views.Lager.SeriennummernAuswahlDialog(position, verfuegbar) { XamlRoot = App.MainWindow.Content.XamlRoot };
            var ergebnis = await dialog.ShowAsync();
            if (ergebnis != ContentDialogResult.Primary) return;
            seriennummernJePosition[position.Id] = dialog.Ausgewaehlt();
        }

        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id, seriennummernJePosition);
            Status = gebucht.Status;
            IstBearbeitbar = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<LieferscheinListViewModel>();
}
```

- [ ] **Step 3: `LieferscheinListPage.xaml` + Code-Behind (Mehrfachauswahl für Sammelrechnung)**

`src/Milet.App/Views/Lager/LieferscheinListPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Lager.LieferscheinListPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Padding="24" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Lieferscheine" Style="{StaticResource TitleTextBlockStyle}" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8">
            <TextBox Width="300" PlaceholderText="Suche (Nummer oder Kunde)…" Text="{x:Bind ViewModel.Suchtext, Mode=TwoWay}" />
            <Button Content="Suchen" Command="{x:Bind ViewModel.LadenCommand}" />
            <Button Content="Bearbeiten" Command="{x:Bind ViewModel.BearbeitenCommand}" />
            <Button Content="Löschen" Command="{x:Bind ViewModel.LoeschenCommand}" />
            <Button Content="→ Sammelrechnung" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.SammelrechnungCommand}" />
            <ProgressRing IsActive="{x:Bind ViewModel.LaedtGerade, Mode=OneWay}" Width="24" Height="24" />
        </StackPanel>

        <ListView Grid.Row="2" x:Name="LieferscheineListView"
            ItemsSource="{x:Bind ViewModel.Belege, Mode=OneWay}"
            SelectedItem="{x:Bind ViewModel.Ausgewaehlt, Mode=TwoWay}"
            SelectionMode="Multiple"
            SelectionChanged="LieferscheineListView_SelectionChanged">
            <ListView.HeaderTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="260" /><ColumnDefinition Width="100" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="Nummer" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="Datum" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="Kunde" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="3" Text="Status" FontWeight="SemiBold" />
                    </Grid>
                </DataTemplate>
            </ListView.HeaderTemplate>
            <ListView.ItemTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="260" /><ColumnDefinition Width="100" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding BelegNummer}" />
                        <TextBlock Grid.Column="1" Text="{Binding BelegDatum}" />
                        <TextBlock Grid.Column="2" Text="{Binding KundeAnzeige}" />
                        <TextBlock Grid.Column="3" Text="{Binding Status}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

`src/Milet.App/Views/Lager/LieferscheinListPage.xaml.cs`:
```csharp
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class LieferscheinListPage : Page
{
    public LieferscheinListViewModel ViewModel { get; }
    public LieferscheinListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferscheinListViewModel>();
        InitializeComponent();
    }

    private void LieferscheineListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel.AusgewaehlteIds = ((ListView)sender).SelectedItems.Cast<BelegDto>().Select(b => b.Id).ToList();
}
```

- [ ] **Step 4: `LieferscheinEditPage.xaml` + Code-Behind (Anzeige + Buchen, keine Positionsbearbeitung)**

`src/Milet.App/Views/Lager/LieferscheinEditPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Lager.LieferscheinEditPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <ScrollViewer Padding="24">
        <StackPanel MaxWidth="900" Spacing="16">
            <TextBlock Text="Lieferschein" Style="{StaticResource TitleTextBlockStyle}" />
            <InfoBar IsOpen="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}"
                     Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay}" />

            <Grid ColumnSpacing="16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" /><ColumnDefinition Width="*" /><ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Header="Nummer" IsReadOnly="True" Text="{x:Bind ViewModel.BelegNummer, Mode=OneWay}" />
                <TextBox Grid.Column="1" Header="Kunde" IsReadOnly="True" Text="{x:Bind ViewModel.KundeAnzeige, Mode=OneWay}" />
                <TextBox Grid.Column="2" Header="Status" IsReadOnly="True" Text="{x:Bind ViewModel.Status, Mode=OneWay}" />
            </Grid>

            <TextBlock Text="Positionen" Style="{StaticResource SubtitleTextBlockStyle}" />
            <ListView MaxHeight="300" ItemsSource="{x:Bind ViewModel.Positionen, Mode=OneWay}" SelectionMode="None">
                <ListView.HeaderTemplate>
                    <DataTemplate>
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" /><ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Bezeichnung" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="1" Text="Menge" FontWeight="SemiBold" />
                        </Grid>
                    </DataTemplate>
                </ListView.HeaderTemplate>
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" /><ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{Binding Bezeichnung}" />
                            <TextBlock Grid.Column="1" Text="{Binding Menge}" />
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="Buchen" Style="{StaticResource AccentButtonStyle}" IsEnabled="{x:Bind ViewModel.IstBearbeitbar, Mode=OneWay}" Command="{x:Bind ViewModel.BuchenCommand}" />
                <Button Content="Abbrechen" Command="{x:Bind ViewModel.AbbrechenCommand}" />
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</Page>
```

`src/Milet.App/Views/Lager/LieferscheinEditPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class LieferscheinEditPage : Page
{
    public LieferscheinEditViewModel ViewModel { get; }
    public LieferscheinEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<LieferscheinEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
```

- [ ] **Step 5: `SeriennummernAuswahlDialog`**

`src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml`:
```xml
<ContentDialog
    x:Class="Milet.App.Views.Lager.SeriennummernAuswahlDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Milet.App.Views.Lager"
    Title="Seriennummern auswählen"
    PrimaryButtonText="Übernehmen"
    CloseButtonText="Abbrechen"
    DefaultButton="Primary">
    <StackPanel Spacing="12" MinWidth="400">
        <TextBlock Text="{x:Bind PositionsBezeichnung}" FontWeight="SemiBold" />
        <TextBlock Text="{x:Bind BenoetigteMengeText}" />
        <ListView MaxHeight="300" ItemsSource="{x:Bind Zeilen}" SelectionMode="None">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="local:SeriennummerAuswahlZeile">
                    <CheckBox Content="{x:Bind Nummer}" IsChecked="{x:Bind Ausgewaehlt, Mode=TwoWay}" />
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </StackPanel>
</ContentDialog>
```

`src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Lager;

public sealed partial class SeriennummerAuswahlZeile : ObservableObject
{
    public int Id { get; }
    public string Nummer { get; }

    [ObservableProperty]
    public partial bool Ausgewaehlt { get; set; }

    public SeriennummerAuswahlZeile(SeriennummerDto dto)
    {
        Id = dto.Id;
        Nummer = dto.Nummer;
    }
}

public sealed partial class SeriennummernAuswahlDialog : ContentDialog
{
    public string PositionsBezeichnung { get; }
    public string BenoetigteMengeText { get; }
    public ObservableCollection<SeriennummerAuswahlZeile> Zeilen { get; }

    public SeriennummernAuswahlDialog(BelegPositionDto position, IReadOnlyList<SeriennummerDto> verfuegbar)
    {
        InitializeComponent();
        PositionsBezeichnung = position.Bezeichnung;
        BenoetigteMengeText = $"Benötigt: {position.Menge} Stück";
        Zeilen = new ObservableCollection<SeriennummerAuswahlZeile>(verfuegbar.Select(s => new SeriennummerAuswahlZeile(s)));
    }

    /// <summary>Keine clientseitige Mengen-Validierung — der Server (LieferscheinBuchenService) prüft die exakte Anzahl und liefert bei Abweichung eine verständliche Fehlermeldung.</summary>
    public IReadOnlyList<int> Ausgewaehlt() => Zeilen.Where(z => z.Ausgewaehlt).Select(z => z.Id).ToList();
}
```

- [ ] **Step 6: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler (jetzt inkl. Task 16, dessen `LieferscheinListViewModel`-Referenz hier aufgelöst wird).

- [ ] **Step 7: Commit**

```bash
git add src/Milet.App/ViewModels/Lager/LieferscheinListViewModel.cs src/Milet.App/ViewModels/Lager/LieferscheinEditViewModel.cs src/Milet.App/Views/Lager/LieferscheinListPage.xaml src/Milet.App/Views/Lager/LieferscheinListPage.xaml.cs src/Milet.App/Views/Lager/LieferscheinEditPage.xaml src/Milet.App/Views/Lager/LieferscheinEditPage.xaml.cs src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml src/Milet.App/Views/Lager/SeriennummernAuswahlDialog.xaml.cs
git commit -m "Lieferschein-Liste (inkl. Sammelrechnung) + -Editor (Anzeige+Buchen) + Seriennummern-Auswahl-Dialog"
```

---

### Task 18: App — Inventur-Liste + -Editor (Ist-Erfassung + Abschluss)

**Files:**
- Create: `src/Milet.App/ViewModels/Lager/InventurListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Lager/InventurEditViewModel.cs`
- Create: `src/Milet.App/Views/Lager/InventurListPage.xaml`
- Create: `src/Milet.App/Views/Lager/InventurListPage.xaml.cs`
- Create: `src/Milet.App/Views/Lager/InventurEditPage.xaml`
- Create: `src/Milet.App/Views/Lager/InventurEditPage.xaml.cs`

**Interfaces:**
- Consumes: `IInventurService`, `InventurDto`, `InventurPositionDto` (Task 5/13), `ILagerortService` (Task 10).
- Produces: `InventurListViewModel`, `InventurEditViewModel` — Registrierung folgt zentral in Task 19.

**Warum ein Bulk-„Mengen speichern"-Button statt Auto-Save je Zeile:** Kein bestehendes Muster im Code (auch Kleinstamm speichert je Formular, nicht je Grid-Zelle) legt einen Live-Save je NumberBox nahe — ein Sammel-Speichern-Klick vor „Abschließen" ist einfacher zu verstehen und zu testen als N Einzelaufrufe pro Tastenanschlag.

- [ ] **Step 1: `InventurListViewModel`**

`src/Milet.App/ViewModels/Lager/InventurListViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class InventurListViewModel : ObservableObject
{
    private readonly IInventurService _inventurService;
    private readonly ILagerortService _lagerortService;
    private readonly INavigationService _navigation;

    public InventurListViewModel(IInventurService inventurService, ILagerortService lagerortService, INavigationService navigation)
    {
        _inventurService = inventurService;
        _lagerortService = lagerortService;
        _navigation = navigation;
        _ = LadenAsync();
        _ = LagerorteLadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<InventurDto> Inventuren { get; set; } = [];
    [ObservableProperty] public partial InventurDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LagerortDto> Lagerorte { get; set; } = [];
    [ObservableProperty] public partial int NeueInventurLagerortId { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Inventuren = await _inventurService.SucheAsync(); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand]
    private async Task LagerorteLadenAsync() => Lagerorte = await _lagerortService.SucheAsync(null);

    [RelayCommand]
    private async Task NeueInventurAsync()
    {
        Fehlermeldung = null;
        if (NeueInventurLagerortId == 0) { Fehlermeldung = "Lagerort wählen."; return; }
        try
        {
            var inventur = await _inventurService.NeueInventurAsync(NeueInventurLagerortId);
            _navigation.Navigate<InventurEditViewModel>(inventur.Id);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Bearbeiten() { if (Ausgewaehlt is { } inventur) _navigation.Navigate<InventurEditViewModel>(inventur.Id); }
}
```

- [ ] **Step 2: `InventurEditViewModel`**

`src/Milet.App/ViewModels/Lager/InventurEditViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Lager;
using Milet.Domain.Entities.Lager;

namespace Milet.App.ViewModels.Lager;

public sealed partial class InventurPositionZeile : ObservableObject
{
    public int Id { get; }
    public string Artikelnummer { get; }
    public string ArtikelBezeichnung { get; }
    public decimal SollMenge { get; }

    [ObservableProperty]
    public partial decimal? IstMenge { get; set; }

    public InventurPositionZeile(InventurPositionDto dto)
    {
        Id = dto.Id;
        Artikelnummer = dto.Artikelnummer;
        ArtikelBezeichnung = dto.ArtikelBezeichnung;
        SollMenge = dto.SollMenge;
        IstMenge = dto.IstMenge;
    }
}

public sealed partial class InventurEditViewModel : ObservableObject, INavigationAware
{
    private readonly IInventurService _inventurService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;
    private int _id;

    public InventurEditViewModel(IInventurService inventurService, INavigationService navigation, IDialogService dialogService)
    {
        _inventurService = inventurService;
        _navigation = navigation;
        _dialogService = dialogService;
    }

    [ObservableProperty] public partial string LagerortBezeichnung { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly Datum { get; set; }
    [ObservableProperty] public partial InventurStatus Status { get; set; }
    [ObservableProperty] public partial ObservableCollection<InventurPositionZeile> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstOffen { get; set; }

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = LadenAsync();
    }

    private async Task LadenAsync()
    {
        if (_id == 0) return;
        var inventur = await _inventurService.LadeAsync(_id);
        LagerortBezeichnung = inventur.LagerortBezeichnung;
        Datum = inventur.Datum;
        Status = inventur.Status;
        IstOffen = inventur.Status == InventurStatus.Offen;
        Positionen = new ObservableCollection<InventurPositionZeile>(inventur.Positionen.Select(p => new InventurPositionZeile(p)));
    }

    [RelayCommand]
    private async Task MengenSpeichernAsync()
    {
        Fehlermeldung = null;
        try
        {
            foreach (var zeile in Positionen.Where(z => z.IstMenge.HasValue))
                await _inventurService.ErfasseIstMengeAsync(zeile.Id, zeile.IstMenge!.Value);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AbschliessenAsync()
    {
        var bestaetigt = await _dialogService.BestaetigenAsync("Inventur abschließen", "Inventur abschließen und Korrekturbuchungen für alle erfassten Abweichungen anlegen?");
        if (!bestaetigt) return;

        try
        {
            var inventur = await _inventurService.AbschliessenAsync(_id);
            Status = inventur.Status;
            IstOffen = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<InventurListViewModel>();
}
```

- [ ] **Step 3: `InventurListPage.xaml` + Code-Behind**

`src/Milet.App/Views/Lager/InventurListPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Lager.InventurListPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Padding="24" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Inventuren" Style="{StaticResource TitleTextBlockStyle}" />

        <InfoBar Grid.Row="1" IsOpen="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}" Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay}" />

        <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="8">
            <ComboBox Width="240" PlaceholderText="Lagerort für neue Inventur…"
                      ItemsSource="{x:Bind ViewModel.Lagerorte, Mode=OneWay}"
                      SelectedValue="{x:Bind ViewModel.NeueInventurLagerortId, Mode=TwoWay}"
                      SelectedValuePath="Id" DisplayMemberPath="Bezeichnung" />
            <Button Content="Neue Inventur" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.NeueInventurCommand}" />
            <Button Content="Bearbeiten" Command="{x:Bind ViewModel.BearbeitenCommand}" />
            <ProgressRing IsActive="{x:Bind ViewModel.LaedtGerade, Mode=OneWay}" Width="24" Height="24" />
        </StackPanel>

        <ListView Grid.Row="3"
            ItemsSource="{x:Bind ViewModel.Inventuren, Mode=OneWay}"
            SelectedItem="{x:Bind ViewModel.Ausgewaehlt, Mode=TwoWay}"
            SelectionMode="Single">
            <ListView.HeaderTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="160" />
                            <ColumnDefinition Width="120" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="Lagerort" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="Datum" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="Status" FontWeight="SemiBold" />
                    </Grid>
                </DataTemplate>
            </ListView.HeaderTemplate>
            <ListView.ItemTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="160" />
                            <ColumnDefinition Width="120" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding LagerortBezeichnung}" />
                        <TextBlock Grid.Column="1" Text="{Binding Datum}" />
                        <TextBlock Grid.Column="2" Text="{Binding Status}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

`src/Milet.App/Views/Lager/InventurListPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class InventurListPage : Page
{
    public InventurListViewModel ViewModel { get; }
    public InventurListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<InventurListViewModel>();
        InitializeComponent();
    }
}
```

- [ ] **Step 4: `InventurEditPage.xaml` + Code-Behind**

`src/Milet.App/Views/Lager/InventurEditPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Lager.InventurEditPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Milet.App.ViewModels.Lager">
    <ScrollViewer Padding="24">
        <StackPanel MaxWidth="900" Spacing="16">
            <TextBlock Text="Inventur" Style="{StaticResource TitleTextBlockStyle}" />
            <InfoBar IsOpen="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}"
                     Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay}" />

            <Grid ColumnSpacing="16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" /><ColumnDefinition Width="*" /><ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Header="Lagerort" IsReadOnly="True" Text="{x:Bind ViewModel.LagerortBezeichnung, Mode=OneWay}" />
                <TextBox Grid.Column="1" Header="Datum" IsReadOnly="True" Text="{x:Bind ViewModel.Datum, Mode=OneWay}" />
                <TextBox Grid.Column="2" Header="Status" IsReadOnly="True" Text="{x:Bind ViewModel.Status, Mode=OneWay}" />
            </Grid>

            <TextBlock Text="Positionen" Style="{StaticResource SubtitleTextBlockStyle}" />
            <ListView MaxHeight="400" ItemsSource="{x:Bind ViewModel.Positionen, Mode=OneWay}" SelectionMode="None"
                      IsEnabled="{x:Bind ViewModel.IstOffen, Mode=OneWay}">
                <ListView.HeaderTemplate>
                    <DataTemplate>
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="120" /><ColumnDefinition Width="*" />
                                <ColumnDefinition Width="100" /><ColumnDefinition Width="120" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Artikelnr." FontWeight="SemiBold" />
                            <TextBlock Grid.Column="1" Text="Bezeichnung" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="2" Text="Soll" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="3" Text="Ist" FontWeight="SemiBold" />
                        </Grid>
                    </DataTemplate>
                </ListView.HeaderTemplate>
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="local:InventurPositionZeile">
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="120" /><ColumnDefinition Width="*" />
                                <ColumnDefinition Width="100" /><ColumnDefinition Width="120" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{x:Bind Artikelnummer}" />
                            <TextBlock Grid.Column="1" Text="{x:Bind ArtikelBezeichnung}" />
                            <TextBlock Grid.Column="2" Text="{x:Bind SollMenge}" />
                            <NumberBox Grid.Column="3" SpinButtonPlacementMode="Compact"
                                       Value="{x:Bind IstMenge, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" />
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="Mengen speichern" IsEnabled="{x:Bind ViewModel.IstOffen, Mode=OneWay}" Command="{x:Bind ViewModel.MengenSpeichernCommand}" />
                <Button Content="Abschließen" Style="{StaticResource AccentButtonStyle}" IsEnabled="{x:Bind ViewModel.IstOffen, Mode=OneWay}" Command="{x:Bind ViewModel.AbschliessenCommand}" />
                <Button Content="Abbrechen" Command="{x:Bind ViewModel.AbbrechenCommand}" />
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</Page>
```
**Hinweis Converter:** `IstMenge` ist `decimal?` — `DecimalToDoubleConverter` (Phase 2) passt, da seine `ConvertBack`-Implementierung bereits `Nullable.GetUnderlyingType(targetType)` prüft und bei leerem `NumberBox`-Wert `null` statt `0m` zurückgibt (nullable-fähig ohne Anpassung). `NullableInt32ToDoubleConverter` wäre hier falsch (der ist für `int?`, nicht `decimal?`).

`src/Milet.App/Views/Lager/InventurEditPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Lager;

namespace Milet.App.Views.Lager;

public sealed partial class InventurEditPage : Page
{
    public InventurEditViewModel ViewModel { get; }
    public InventurEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<InventurEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
```

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.App/ViewModels/Lager/InventurListViewModel.cs src/Milet.App/ViewModels/Lager/InventurEditViewModel.cs src/Milet.App/Views/Lager/InventurListPage.xaml src/Milet.App/Views/Lager/InventurListPage.xaml.cs src/Milet.App/Views/Lager/InventurEditPage.xaml src/Milet.App/Views/Lager/InventurEditPage.xaml.cs
git commit -m "Inventur-Liste + -Editor: Anlegen (SollMenge einfrieren), Ist-Erfassung, Abschluss"
```

---

### Task 19: App — Navigation aktivieren (Lager-Menü) + DI-Registrierungen aller neuen ViewModels

**Files:**
- Modify: `src/Milet.App/App.xaml.cs`
- Modify: `src/Milet.App/Shell/ShellPage.xaml`
- Modify: `src/Milet.App/Shell/ShellPage.xaml.cs`

**Interfaces:**
- Consumes: alle in Task 15/17/18 erstellten ViewModels/Pages.
- Produces: vollständig verdrahtete Navigation — App ist danach Ende-zu-Ende durchklickbar (Auftrag → Lieferschein → Buchen → Sammelrechnung, Bestandsübersicht, Inventur).

- [ ] **Step 1: `App.xaml.cs` — ViewModel-Registrierungen**

Modify `src/Milet.App/App.xaml.cs` — `using Milet.App.ViewModels.Lager;` zum `using`-Block ergänzen; nach
```csharp
        builder.Services.AddTransient<RechnungEditViewModel>();
```
einfügen:
```csharp

        builder.Services.AddTransient<BestandUebersichtViewModel>();
        builder.Services.AddTransient<LieferscheinListViewModel>();
        builder.Services.AddTransient<LieferscheinEditViewModel>();
        builder.Services.AddTransient<InventurListViewModel>();
        builder.Services.AddTransient<InventurEditViewModel>();
```

- [ ] **Step 2: `ShellPage.xaml` — Lager-Menü aktivieren**

Modify `src/Milet.App/Shell/ShellPage.xaml` — die Zeile
```xml
            <NavigationViewItem Content="Lager" Tag="lager" Icon="Library" IsEnabled="False" />
```
ersetzen durch:
```xml
            <NavigationViewItem Content="Lager" Tag="lager" Icon="Library">
                <NavigationViewItem.MenuItems>
                    <NavigationViewItem Content="Lieferscheine" Tag="lieferscheine" Icon="Package" />
                    <NavigationViewItem Content="Bestandsübersicht" Tag="bestand" Icon="AllApps" />
                    <NavigationViewItem Content="Inventur" Tag="inventur" Icon="List" />
                </NavigationViewItem.MenuItems>
            </NavigationViewItem>
```

- [ ] **Step 3: `ShellPage.xaml.cs` — Registrierung + Navigation-Switch**

Modify `src/Milet.App/Shell/ShellPage.xaml.cs` — `using Milet.App.ViewModels.Lager;` und `using Milet.App.Views.Lager;` zum `using`-Block ergänzen; nach
```csharp
        _navigation.Register<RechnungEditViewModel, RechnungEditPage>();
```
einfügen:
```csharp

        _navigation.Register<LieferscheinListViewModel, LieferscheinListPage>();
        _navigation.Register<LieferscheinEditViewModel, LieferscheinEditPage>();
        _navigation.Register<BestandUebersichtViewModel, BestandUebersichtPage>();
        _navigation.Register<InventurListViewModel, InventurListPage>();
        _navigation.Register<InventurEditViewModel, InventurEditPage>();
```

Im `switch (item.Tag as string)` in `NavView_SelectionChanged` nach
```csharp
            case "rechnungen":
                _navigation.Navigate<RechnungListViewModel>();
                break;
```
einfügen:
```csharp
            case "lieferscheine":
                _navigation.Navigate<LieferscheinListViewModel>();
                break;
            case "bestand":
                _navigation.Navigate<BestandUebersichtViewModel>();
                break;
            case "inventur":
                _navigation.Navigate<InventurListViewModel>();
                break;
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.App/App.xaml.cs src/Milet.App/Shell/ShellPage.xaml src/Milet.App/Shell/ShellPage.xaml.cs
git commit -m "Lager-Menü aktivieren: Lieferscheine/Bestandsübersicht/Inventur, alle Phase-3-ViewModels registriert"
```

---

### Task 20: Verifikation — vollständiger Build/Test-Durchlauf, STATUS.md aktualisieren

**Files:**
- Modify: `STATUS.md`

**Interfaces:**
- Consumes: alle Tasks 1–19.
- Produces: dokumentierter Abnahmestand für Phase 3, Grundlage für Phase 4.

- [ ] **Step 1: Vollständigen Build ausführen**

Run:
```bash
"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64
```
Expected: 0 Fehler, 0 Warnungen zu unbenutzten Usings o. Ä. in den neuen Dateien.

- [ ] **Step 2: Alle Testprojekte einzeln ausführen**

Run (jedes einzeln, MTP-Modus — siehe Global Constraints):
```bash
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj
```
Expected: Domain weiterhin 14/14 (keine neuen Domain-Unit-Tests in Phase 3 — die neue Logik ist Infrastructure-lastig, per Integrationstest abgedeckt), Application 14+5=19/19, IntegrationTests grün oder sauberer Skip ohne Docker (7 neue Testklassen: `BelegUeberleitungServiceTests`, `BestandServiceTests`, `LieferscheinBuchenServiceTests` — zusätzlich zu den bestehenden 4).

- [ ] **Step 3: Migration auf LocalDB anwenden (falls in Task 7 übersprungen) und Seed prüfen**

Run:
```bash
cd src/Milet.Tools.Migrator
"$USERPROFILE/.dotnet/dotnet.exe" run --project .
```
Expected: „Migrationen erfolgreich angewendet." (oder „Datenbank ist aktuell"), Hauptlagerort-Seed vorhanden. Per `sqlcmd` gegenprüfen (Hinweis aus Phase 2: `SET QUOTED_IDENTIFIER ON;` vor Statements auf `Belege`):
```sql
SELECT Code, Bezeichnung FROM Lagerorte;
SELECT COUNT(*) FROM Belege WHERE BelegTyp = 'Lieferschein';
```

- [ ] **Step 4: Manueller Smoke-Test im UI (App starten, End-to-End durchklicken)**

App starten, dann:
1. Kleinstamm → Lagerorte: Hauptlager sichtbar (Seed), zweiten Lagerort anlegen testweise.
2. Bestandsübersicht: Artikel mit `IstLagerartikel=true` per Bestandskorrektur auf z. B. 20 Stück setzen — Liste aktualisiert sich.
3. Verkauf → Aufträge: neuen Auftrag mit diesem Artikel (Menge 10) anlegen, speichern → „→ Lieferschein" klicken → Teillieferungs-Dialog zeigt offene Menge 10, Lagerort wählen, Menge auf 6 reduzieren → Lieferschein erzeugt (Nummer `LS-2026-000x`), Auftrag bleibt `Entwurf`-artig offen (nicht `Erledigt`, da Teillieferung).
4. Lager → Lieferscheine: neuen Lieferschein öffnen, „Buchen" klicken → Bestand sinkt um 6 (Bestandsübersicht gegenprüfen), Status `Gebucht`.
5. Zweite Teillieferung über denselben Auftrag: „→ Lieferschein" erneut, jetzt nur noch 4 offen → zweiter Lieferschein, buchen.
6. Lager → Lieferscheine: beide Lieferscheine markieren (Mehrfachauswahl), „→ Sammelrechnung" → eine Rechnung mit 2 Positionen (6+4=10 Stück) entsteht, beide Lieferscheine → `Erledigt`.
7. Negativsperre-Check: dritten Lieferschein-Versuch mit mehr Menge als Bestand erzeugen (z. B. über einen zweiten Auftrag + Teillieferung) → Buchen wirft Fehlermeldung, kein Absturz.
8. Lager → Inventur: neue Inventur für Hauptlager anlegen, eine Ist-Menge abweichend vom Soll eintragen, „Mengen speichern" → „Abschließen" → Bestandsübersicht zeigt korrigierten Wert.
9. Seriennummern: Artikel mit `HatSeriennummern=true` in Bestandsübersicht auswählen, zwei Seriennummern erfassen, per Auftrag→Lieferschein liefern, beim Buchen erscheint Seriennummern-Auswahl-Dialog, nach Bestätigung Status der Seriennummern `Ausgeliefert`.

Alle Schritte ohne Absturz, Fehlermeldungen (falls vorhanden — z. B. Negativsperre) erscheinen als lesbare `InfoBar`/Fehlermeldung statt Exception-Dialog. Testdaten nach Verifikation wieder aus der DB entfernen (Muster aus Phase 1/2-Abnahme in `STATUS.md`).

- [ ] **Step 5: `STATUS.md` aktualisieren**

Modify `STATUS.md` — im Abschnitt „Offen" den Phase-3-Eintrag durch einen neuen Abschnitt „### Phase 3 — Lager+Lieferschein ✅" ersetzen (Muster wie der bestehende „### Phase 2 — Verkauf+PDF ✅"-Abschnitt): kurze Zusammenfassung von Domain/Application/Infrastructure/App-Umfang aus diesem Plan, Testergebnisse aus Step 2, Ergebnis des manuellen Smoke-Tests aus Step 4, jeder während der Verifikation tatsächlich gefundene Bug mit Ursache+Fix (wie in den bestehenden Phase-1/2-Abschnitten dokumentiert). „Offen"-Liste auf „Phasen 4–7" reduzieren.

- [ ] **Step 6: Commit**

```bash
git add STATUS.md
git commit -m "Phase 3 (Lager+Lieferschein) verifiziert: Build/Tests grün, End-to-End im UI durchgeklickt"
```

---

## Self-Review

**Spec-Abdeckung gegen PLAN.md §„Lager: Append-only-Ledger + Snapshot" und Phasen-Tabelle Zeile „3 Lager+Lieferschein":**
- „Lagerbewegung (append-only)... ArtikelId, LagerortId, Menge signiert, Typ, BelegPositionId?, SeriennummerId?, Zeitpunkt, BenutzerId" → Task 1, Step 3 — Felder 1:1 übernommen.
- „ArtikelBestand-Snapshot... Update in derselben Transaktion via atomarem UPDATE... kein Read-Modify-Write-Race" → Task 9, `BestandService.BucheBewegungAsync`.
- „Konsistenzjob leitet Snapshot bei Bedarf aus Ledger neu ab" → **bewusst nicht implementiert** (kein in Phase-3-Abnahmekriterien gefordertes Feature, Ledger=Snapshot-Invariante wird stattdessen laufend garantiert statt nachträglich repariert — siehe Integrationstest in Task 9; ein Reparaturjob wäre YAGNI ohne bisher beobachtete Drift).
- „Seriennummer (Status AufLager/Ausgeliefert/Retourniert); Junction BelegPositionSeriennummer beim Lieferschein" → Task 2, Task 12.
- „Inventur + InventurPosition (SollMenge eingefroren, IstMenge); Abschluss bucht Differenzen als Inventurkorrektur" → Task 2, Task 13.
- Geschäftsprozess 2 „Auftrag→Lieferschein: Teillieferungs-Dialog (offene Mengen); Buchen = negative Lagerbewegungen + Seriennummern-Pick + Bestandsupdate in einer Transaktion. Offene-Mengen-Prüfung in der Transaktion wiederholen" → Task 7 (`UeberleitenMitAuswahlAsync`), Task 12 (`LieferscheinBuchenService`), Task 16 (Dialog).
- Geschäftsprozess 3 „Lieferschein→Rechnung inkl. Sammelrechnung (mehrere Lieferscheine gleicher Kunde/Zahlungsbedingung)" → Task 8 (`UeberleitenMehrereAsync`), Task 17 (Mehrfachauswahl-UI).
- Phasentabelle „Testbar am Ende": Teillieferung korrekt (Task 16/manueller Test Step 4.3–4.5), Ledger=Snapshot Integrationstest (Task 9), Negativsperre (Task 9/12, manueller Test Step 4.7).

**Placeholder-Scan:** Keine `TODO`/`TBD`/„später"-Marker; jeder Code-Block enthält vollständige, kompilierbare Implementierung; Wareneingang/Lieferschein-PDF sind explizit als **außerhalb dieses Plans** benannt (Global Constraints), nicht als unfertige Stubs im Code.

**Typ-Konsistenz geprüft:**
- `BestandService.BucheBewegungAsync(MiletDbContext, int, int, decimal, LagerbewegungTyp, int?, CancellationToken)` — identische Signatur in Task 9 (Definition), Task 11 (`SeriennummernService.ErfasseAsync`), Task 12 (`LieferscheinBuchenService.BuchenAsync`), Task 13 (`InventurService.AbschliessenAsync`).
- `ILieferscheinBuchenService.BuchenAsync(int, IReadOnlyDictionary<int, IReadOnlyList<int>>, CancellationToken)` — Interface (Task 5), Implementierung (Task 12), Aufrufer (Task 17 `LieferscheinEditViewModel`) stimmen überein.
- `IBelegUeberleitungService.UeberleitenMitAuswahlAsync(int, BelegTyp, IReadOnlyDictionary<int, decimal>, int?, CancellationToken)` — Interface (Task 5), Implementierung (Task 7), Aufrufer (Task 16 `AuftragEditViewModel`) stimmen überein.
- `ArtikelVerkaufLookupDto` 9-Parameter-Reihenfolge (inkl. `HatSeriennummern` als letztes Argument) — Definition (Task 5), Konstruktion in `VerkaufLookupService` (Task 7), Konsum in `LieferscheinEditViewModel` (Task 17) stimmen überein.
- `BelegPositionDto.LagerortId` — Definition (Task 5), Mapping in `VerkaufMapping.ToDto` (Task 7), Setzen in `UeberleitenMitAuswahlAsync` (Task 7), Anzeige in `LieferscheinEditPage` (Task 17, indirekt über `Positionen`) konsistent.

**Migrations-Reihenfolge:** Task 6 legt Configurations/DbContext/Seed-Quellcode an und committet ihn ohne Migration; Task 7 erzeugt die tatsächliche `dotnet ef migrations add LagerLieferschein` nach Behebung aller Compile-Fehler aus Task 5/6 (explizit in beiden Tasks dokumentiert, um einen fehlschlagenden `dotnet ef`-Aufruf mitten in Task 6 zu vermeiden — `dotnet ef` kompiliert das gesamte Projekt).

