# Phase 2 „Verkauf+PDF" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beleg-TPH-Datenmodell (Angebot/Auftrag/Rechnung) mit Belegeditor, Preisfindung, Überleitung (Angebot→Auftrag→Rechnung), Buchungspipeline (atomare Rechnungsnummer, Immutability, Offener-Posten-Anlage) und QuestPDF-Ausgabe (3 Dokumenttypen) bauen — Angebot→Rechnung End-to-End im UI durchklickbar.

**Architektur:** EF-Core TPH (`Beleg` Basisklasse, dünne Subklassen `Angebot`/`Auftrag`/`Rechnung`), eine `BelegPosition`-Tabelle mit `UrsprungsPositionId`-Selbstreferenz für Belegfluss/offene Mengen, Steuerberechnung gruppiert in `BelegSteuerSumme`. Schichten- und Namenskonventionen 1:1 aus Phase 1 übernommen (Plain Services, DTOs als `record`, FluentValidation, `IDbContextFactory`+`AsNoTracking`, `SaveChangesTranslatingConcurrencyAsync`, ViewModels via `[ObservableProperty]`/`[RelayCommand]`, `INavigationAware`).

**Tech Stack:** .NET 10, EF Core 10 (SQL Server/LocalDB), FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, QuestPDF 2026.7.3 (Community License), xUnit v3, Testcontainers.MsSql.

**Spec:** `d:\Projects\Milet\PLAN.md` (Abschnitte „Datenmodell (Kern)" Beleg-Pattern/Status-Workflow/Nummernkreise, „Geschäftsprozesse" Punkte 1+3, Phasen-Tabelle Zeile „2 Verkauf+PDF"). Konventionen recherchiert aus bestehendem Phase-1-Code (Kunde/Artikel/KleinstammServices) — jede Abweichung davon ist unten explizit begründet.

## Global Constraints

- Neue Entities: `AuditableEntity`+`IHasRowVersion` für Aggregate Roots (`Beleg`, `OffenerPosten`), einfache POCOs sonst (`BelegPosition`, `BelegSteuerSumme` hängen an ihrem Beleg, kein eigenes RowVersion).
- Jede Service-Methode öffnet eigenen `IDbContextFactory<MiletDbContext>`-Context; Reads `AsNoTracking()`; Speichern nutzt `SaveChangesTranslatingConcurrencyAsync` (bestehende Extension in `ConcurrencyHelper.cs`).
- DTOs: `sealed record` mit `init`-Properties, alle DTOs eines Moduls in einer `Dtos.cs`, alle Validatoren in einer `Validators.cs`, alle Service-Interfaces in einer `I<Modul>Services.cs` — exakt wie `Stammdaten`-Modul.
- Decimal-Präzisionen (verbindlich, aus PLAN.md §Beleg-Pattern): `Menge` `decimal(18,3)`, `Einzelpreis`/`Einkaufspreis`-artige Preise `decimal(18,4)`, `RabattProzent`/`MwStSatzWert` `decimal(5,2)`, alle Summenfelder (`GesamtNetto`, `SummeNetto/MwSt/Brutto`, `Betrag`, `OffenerBetrag`) `decimal(18,2)`.
- Rundung: `Math.Round(x, 2, MidpointRounding.ToEven)` an jeder Stelle, an der ein Summenfeld persistiert wird (PLAN.md §Rundung).
- Rechnungsnummer wird **nicht** beim Speichern vergeben, sondern erst beim Buchen (PLAN.md §Status-Workflow) — Angebot/Auftrag bekommen ihre Nummer wie Kunde/Artikel beim ersten Speichern.
- Gebuchte Belege sind unveränderlich (GoBD) — doppelt abgesichert: Service-Guard (klare Fehlermeldung) + `SaveChangesInterceptor` (harte Sperre als Sicherheitsnetz).
- `dotnet` explizit über `%USERPROFILE%\.dotnet\dotnet.exe` aufrufen (PATH zeigt auf leere Install, siehe STATUS.md). Jedes Testprojekt einzeln ausführen (MTP-Modus, mehrere Projekte gleichzeitig scheitern mit "keine Tests gefunden").
- Migrationen ausschließlich über `Milet.Tools.Migrator` anwenden (kein Startup-Projekt = WinUI-App).
- Deutsche Bezeichner für alles Fachliche (Entities, DTOs, Properties, UI-Texte), englische für rein technische Infrastruktur (Interfaces wie gehabt gemischt-deutsch wie bestehender Code: `IBelegService`, `SpeichereAsync` etc.).

**QuestPDF bereits installiert** (Task 0, vorgezogen): `Directory.Packages.props` hat `QuestPDF` `2026.7.3`, `src/Milet.Infrastructure/Milet.Infrastructure.csproj` hat `<PackageReference Include="QuestPDF" />`, `dotnet restore` lief grün.

---

### Task 1: Domain — Enums + Beleg-TPH-Entities + BelegPosition + BelegSteuerSumme

**Files:**
- Create: `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/BelegStatus.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/PositionsTyp.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Beleg.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Angebot.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Auftrag.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Rechnung.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/BelegPosition.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/BelegSteuerSumme.cs`

**Interfaces:**
- Consumes: `AuditableEntity`, `IHasRowVersion` (`src/Milet.Domain/Common/`), `Adresse` (`src/Milet.Domain/ValueObjects/Adresse.cs`, hat bereits `.Kopie()`), `Kunde`, `Artikel`, `MwStSatz` (`src/Milet.Domain/Entities/Stammdaten/`).
- Produces: `Beleg`, `BelegPosition`, `BelegSteuerSumme`, `BelegTyp`, `BelegStatus`, `PositionsTyp` — von Task 2 (SteuerRechner), Task 4 (DTOs/Validators) und allen Folge-Tasks konsumiert.

- [ ] **Step 1: Enums anlegen**

`src/Milet.Domain/Entities/Verkauf/BelegTyp.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public enum BelegTyp
{
    Angebot = 0,
    Auftrag = 1,
    Rechnung = 2,
}
```

`src/Milet.Domain/Entities/Verkauf/BelegStatus.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public enum BelegStatus
{
    Entwurf = 0,
    Gebucht = 1,
    Erledigt = 2,
    Storniert = 3,
}
```

`src/Milet.Domain/Entities/Verkauf/PositionsTyp.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public enum PositionsTyp
{
    Artikel = 0,
    Freitext = 1,
    Zwischensumme = 2,
}
```

- [ ] **Step 2: `Beleg`-Basisklasse (TPH)**

`src/Milet.Domain/Entities/Verkauf/Beleg.cs`:
```csharp
using Milet.Domain.Common;
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Verkauf;

public abstract class Beleg : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Leer bei Entwurf einer Rechnung — erst beim Buchen atomar vergeben.</summary>
    public string BelegNummer { get; set; } = string.Empty;

    public DateOnly BelegDatum { get; set; }

    public int KundeId { get; set; }
    public Domain.Entities.Stammdaten.Kunde? Kunde { get; set; }

    /// <summary>Eingefroren bei Erstellung — spätere Adressänderungen am Kunden wirken nicht rückwirkend.</summary>
    public Adresse RechnungsadresseSnapshot { get; set; } = new();
    public Adresse LieferadresseSnapshot { get; set; } = new();

    /// <summary>Snapshot aus Zahlungsbedingung bei Erstellung.</summary>
    public int ZahlungsbedingungZielTage { get; set; }
    public int? ZahlungsbedingungSkontoTage { get; set; }
    public decimal? ZahlungsbedingungSkontoProzent { get; set; }

    public BelegStatus Status { get; set; } = BelegStatus.Entwurf;

    public decimal SummeNetto { get; set; }
    public decimal SummeMwSt { get; set; }
    public decimal SummeBrutto { get; set; }

    /// <summary>Nur Rechnung: gesetzt beim Buchen (BelegDatum + ZahlungsbedingungZielTage).</summary>
    public DateOnly? Faelligkeit { get; set; }

    public DateOnly? Leistungsdatum { get; set; }

    public string? Kopftext { get; set; }
    public string? Fusstext { get; set; }

    public List<BelegPosition> Positionen { get; set; } = [];
    public List<BelegSteuerSumme> Steuersummen { get; set; } = [];

    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 3: Dünne Subklassen**

`src/Milet.Domain/Entities/Verkauf/Angebot.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Angebot : Beleg;
```

`src/Milet.Domain/Entities/Verkauf/Auftrag.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Auftrag : Beleg;
```

`src/Milet.Domain/Entities/Verkauf/Rechnung.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Rechnung : Beleg;
```

- [ ] **Step 4: `BelegPosition`**

`src/Milet.Domain/Entities/Verkauf/BelegPosition.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public class BelegPosition
{
    public int Id { get; set; }

    public int BelegId { get; set; }
    public Beleg? Beleg { get; set; }

    public int PositionsNr { get; set; }
    public PositionsTyp PositionsTyp { get; set; } = PositionsTyp.Artikel;

    public int? ArtikelId { get; set; }
    public Domain.Entities.Stammdaten.Artikel? Artikel { get; set; }

    /// <summary>Snapshot — spätere Änderungen am Artikelstamm wirken nicht auf gespeicherte Belege.</summary>
    public string Bezeichnung { get; set; } = string.Empty;
    public string? EinheitKuerzel { get; set; }

    public decimal Menge { get; set; }
    public decimal Einzelpreis { get; set; }
    public decimal RabattProzent { get; set; }

    /// <summary>MwSt-Snapshot je Zeile — Satzänderungen wirken nicht rückwirkend.</summary>
    public int? MwStSatzId { get; set; }
    public decimal MwStSatzWert { get; set; }
    public int? SteuerSchluessel { get; set; }

    public decimal GesamtNetto { get; set; }

    /// <summary>Trägt Teillieferung/Teilfakturierung/Sammelrechnung: offene Menge = Menge − Σ referenzierender Folgepositionen.</summary>
    public int? UrsprungsPositionId { get; set; }

    /// <summary>Berechnet die noch nicht überführte Menge dieser Position anhand aller Positionen im System, die auf sie verweisen.</summary>
    public static decimal OffeneMenge(BelegPosition position, IEnumerable<BelegPosition> alle)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(alle);
        var uebernommen = alle.Where(p => p.UrsprungsPositionId == position.Id).Sum(p => p.Menge);
        return position.Menge - uebernommen;
    }
}
```

- [ ] **Step 5: `BelegSteuerSumme`**

`src/Milet.Domain/Entities/Verkauf/BelegSteuerSumme.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public class BelegSteuerSumme
{
    public int Id { get; set; }

    public int BelegId { get; set; }
    public Beleg? Beleg { get; set; }

    public decimal MwStSatzWert { get; set; }
    public int? SteuerSchluessel { get; set; }
    public decimal NettoSumme { get; set; }
    public decimal MwStBetrag { get; set; }
}
```

- [ ] **Step 6: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 7: Commit**

```bash
git add src/Milet.Domain/Entities/Verkauf/
git commit -m "Beleg-TPH-Domainmodell: Angebot/Auftrag/Rechnung, BelegPosition, BelegSteuerSumme"
```

---

### Task 2: Domain — SteuerRechner (Positions-/Summenberechnung) + Tests

**Files:**
- Create: `src/Milet.Domain/Services/SteuerRechner.cs`
- Test: `tests/Milet.Domain.Tests/SteuerRechnerTests.cs`
- Test: `tests/Milet.Domain.Tests/BelegPositionOffeneMengeTests.cs`

**Interfaces:**
- Consumes: `BelegPosition`, `BelegSteuerSumme` (Task 1).
- Produces: `SteuerRechner.BerechnePosition(decimal menge, decimal einzelpreis, decimal rabattProzent) -> decimal` (GesamtNetto, gerundet). `SteuerRechner.BerechneSteuersummen(IEnumerable<BelegPosition> positionen) -> IReadOnlyList<BelegSteuerSumme>`. `SteuerRechner.BerechneKopfsummen(IReadOnlyList<BelegSteuerSumme> steuersummen) -> (decimal Netto, decimal MwSt, decimal Brutto)`. Von Task 8 (`BelegService`) und Task 11 (`RechnungBuchenService`) konsumiert.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`tests/Milet.Domain.Tests/SteuerRechnerTests.cs`:
```csharp
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;

namespace Milet.Domain.Tests;

public class SteuerRechnerTests
{
    [Fact]
    public void BerechnePosition_MengeEinzelpreisRabatt_RundetAufZweiStellen()
    {
        var netto = SteuerRechner.BerechnePosition(menge: 3, einzelpreis: 19.995m, rabattProzent: 10);
        // 3 * 19.995 = 59.985; abzgl. 10% Rabatt = 53.9865 -> ToEven auf 53.99
        Assert.Equal(53.99m, netto);
    }

    [Fact]
    public void BerechneSteuersummen_GruppiertNachSatzUndRundetAmEnde()
    {
        var positionen = new List<BelegPosition>
        {
            new() { MwStSatzWert = 19m, GesamtNetto = 10.005m },
            new() { MwStSatzWert = 19m, GesamtNetto = 10.005m },
            new() { MwStSatzWert = 7m, GesamtNetto = 5.00m },
        };

        var summen = SteuerRechner.BerechneSteuersummen(positionen);

        var satz19 = Assert.Single(summen, s => s.MwStSatzWert == 19m);
        Assert.Equal(20.01m, satz19.NettoSumme);
        Assert.Equal(Math.Round(20.01m * 0.19m, 2, MidpointRounding.ToEven), satz19.MwStBetrag);

        var satz7 = Assert.Single(summen, s => s.MwStSatzWert == 7m);
        Assert.Equal(5.00m, satz7.NettoSumme);
        Assert.Equal(0.35m, satz7.MwStBetrag);
    }

    [Fact]
    public void BerechneKopfsummen_SummiertAlleSteuergruppen()
    {
        var steuersummen = new List<BelegSteuerSumme>
        {
            new() { NettoSumme = 20.01m, MwStBetrag = 3.80m },
            new() { NettoSumme = 5.00m, MwStBetrag = 0.35m },
        };

        var (netto, mwst, brutto) = SteuerRechner.BerechneKopfsummen(steuersummen);

        Assert.Equal(25.01m, netto);
        Assert.Equal(4.15m, mwst);
        Assert.Equal(29.16m, brutto);
    }
}
```

`tests/Milet.Domain.Tests/BelegPositionOffeneMengeTests.cs`:
```csharp
using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Tests;

public class BelegPositionOffeneMengeTests
{
    [Fact]
    public void OffeneMenge_OhneFolgepositionen_IstVolleMenge()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        Assert.Equal(10, BelegPosition.OffeneMenge(position, []));
    }

    [Fact]
    public void OffeneMenge_MitTeilweiserUebernahme_ZiehtAb()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        var folge = new BelegPosition { Id = 2, UrsprungsPositionId = 1, Menge = 4 };
        Assert.Equal(6, BelegPosition.OffeneMenge(position, [folge]));
    }

    [Fact]
    public void OffeneMenge_MitMehrerenFolgepositionen_ZiehtSummeAb()
    {
        var position = new BelegPosition { Id = 1, Menge = 10 };
        var folge1 = new BelegPosition { Id = 2, UrsprungsPositionId = 1, Menge = 4 };
        var folge2 = new BelegPosition { Id = 3, UrsprungsPositionId = 1, Menge = 6 };
        Assert.Equal(0, BelegPosition.OffeneMenge(position, [folge1, folge2]));
    }
}
```

- [ ] **Step 2: Tests laufen lassen, Fehlschlag bestätigen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj`
Expected: Compile-Fehler — `SteuerRechner` existiert nicht.

- [ ] **Step 3: `SteuerRechner` implementieren**

`src/Milet.Domain/Services/SteuerRechner.cs`:
```csharp
using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Services;

public static class SteuerRechner
{
    public static decimal BerechnePosition(decimal menge, decimal einzelpreis, decimal rabattProzent)
    {
        var brutto = menge * einzelpreis;
        var nachRabatt = brutto * (1 - rabattProzent / 100m);
        return Math.Round(nachRabatt, 2, MidpointRounding.ToEven);
    }

    public static IReadOnlyList<BelegSteuerSumme> BerechneSteuersummen(IEnumerable<BelegPosition> positionen)
    {
        ArgumentNullException.ThrowIfNull(positionen);
        return positionen
            .Where(p => p.PositionsTyp == PositionsTyp.Artikel)
            .GroupBy(p => (p.MwStSatzWert, p.SteuerSchluessel))
            .Select(g =>
            {
                var netto = Math.Round(g.Sum(p => p.GesamtNetto), 2, MidpointRounding.ToEven);
                var mwst = Math.Round(netto * g.Key.MwStSatzWert / 100m, 2, MidpointRounding.ToEven);
                return new BelegSteuerSumme
                {
                    MwStSatzWert = g.Key.MwStSatzWert,
                    SteuerSchluessel = g.Key.SteuerSchluessel,
                    NettoSumme = netto,
                    MwStBetrag = mwst,
                };
            })
            .ToList();
    }

    public static (decimal Netto, decimal MwSt, decimal Brutto) BerechneKopfsummen(IReadOnlyList<BelegSteuerSumme> steuersummen)
    {
        ArgumentNullException.ThrowIfNull(steuersummen);
        var netto = Math.Round(steuersummen.Sum(s => s.NettoSumme), 2, MidpointRounding.ToEven);
        var mwst = Math.Round(steuersummen.Sum(s => s.MwStBetrag), 2, MidpointRounding.ToEven);
        return (netto, mwst, netto + mwst);
    }
}
```

- [ ] **Step 4: Tests laufen lassen, Erfolg bestätigen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj`
Expected: alle PASS (bestehende 8 + 6 neue = 14).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Services/SteuerRechner.cs tests/Milet.Domain.Tests/
git commit -m "SteuerRechner: Positions-/Steuergruppen-/Kopfsummen-Berechnung mit Rundungstests"
```

---

### Task 3: Domain — Firmenstamm (Briefkopf-Stammdaten)

**Files:**
- Create: `src/Milet.Domain/Entities/Admin/Firmenstamm.cs`

**Interfaces:**
- Consumes: `Adresse` (`src/Milet.Domain/ValueObjects/Adresse.cs`).
- Produces: `Firmenstamm` — Singleton-Zeile (`Id = 1`), von Task 6 (EF-Configuration), Task 9 (`FirmenstammService`) und Task 14 (PDF-Briefkopf) konsumiert.

- [ ] **Step 1: Entity anlegen**

`src/Milet.Domain/Entities/Admin/Firmenstamm.cs`:
```csharp
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Admin;

/// <summary>Genau ein Datensatz (Id = 1) — Firmendaten für Briefkopf/PDF-Ausgabe.</summary>
public class Firmenstamm
{
    public int Id { get; set; }
    public string Firmenname { get; set; } = string.Empty;
    public Adresse Adresse { get; set; } = new();
    public string? UStIdNr { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Iban { get; set; }
    public string? Bic { get; set; }
}
```

- [ ] **Step 2: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add src/Milet.Domain/Entities/Admin/Firmenstamm.cs
git commit -m "Firmenstamm-Entity für PDF-Briefkopf"
```

---

### Task 4: Application — Verkauf-DTOs + Validatoren

**Files:**
- Create: `src/Milet.Application/Verkauf/Dtos.cs`
- Create: `src/Milet.Application/Verkauf/Validators.cs`
- Test: `tests/Milet.Application.Tests/VerkaufValidatorTests.cs`

**Interfaces:**
- Consumes: `AdresseDto` (`src/Milet.Application/Stammdaten/Dtos.cs`, bereits vorhanden — wiederverwendet, nicht dupliziert), `LookupDto` (dito).
- Produces: `BelegDto`, `BelegPositionDto`, `ArtikelVerkaufLookupDto`, `VerkaufLookups`, `PreisErgebnisDto`, `BelegValidator`, `BelegPositionValidator` — von Task 5 (Service-Interfaces), Task 8 (`BelegService`), Task 9 (`VerkaufLookupService`), Task 16–18 (App-ViewModels) konsumiert.

- [ ] **Step 1: DTOs**

`src/Milet.Application/Verkauf/Dtos.cs`:
```csharp
using Milet.Application.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Application.Verkauf;

public sealed record BelegPositionDto
{
    public int Id { get; init; }
    public int PositionsNr { get; init; }
    public PositionsTyp PositionsTyp { get; init; } = PositionsTyp.Artikel;
    public int? ArtikelId { get; init; }
    public string Bezeichnung { get; init; } = string.Empty;
    public string? EinheitKuerzel { get; init; }
    public decimal Menge { get; init; }
    public decimal Einzelpreis { get; init; }
    public decimal RabattProzent { get; init; }
    public int? MwStSatzId { get; init; }
    public decimal MwStSatzWert { get; init; }
    public int? SteuerSchluessel { get; init; }
    public decimal GesamtNetto { get; init; }
    public int? UrsprungsPositionId { get; init; }
}

public sealed record BelegDto
{
    public int Id { get; init; }
    public BelegTyp BelegTyp { get; init; }
    public string BelegNummer { get; init; } = string.Empty;
    public DateOnly BelegDatum { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public int KundeId { get; init; }
    public string KundeAnzeige { get; init; } = string.Empty;
    public AdresseDto RechnungsadresseSnapshot { get; init; } = new();
    public AdresseDto LieferadresseSnapshot { get; init; } = new();
    public int ZahlungsbedingungZielTage { get; init; }
    public int? ZahlungsbedingungSkontoTage { get; init; }
    public decimal? ZahlungsbedingungSkontoProzent { get; init; }
    public BelegStatus Status { get; init; } = BelegStatus.Entwurf;
    public decimal SummeNetto { get; init; }
    public decimal SummeMwSt { get; init; }
    public decimal SummeBrutto { get; init; }
    public DateOnly? Faelligkeit { get; init; }
    public DateOnly? Leistungsdatum { get; init; }
    public string? Kopftext { get; init; }
    public string? Fusstext { get; init; }
    public IReadOnlyList<BelegPositionDto> Positionen { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>Reicheres Lookup als das generische <see cref="LookupDto"/> — trägt Defaultwerte für neue Belegpositionen.</summary>
public sealed record ArtikelVerkaufLookupDto(
    int Id,
    string Anzeige,
    decimal Listenpreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel);

public sealed record KundeVerkaufLookupDto(
    int Id,
    string Anzeige,
    int? ZahlungsbedingungId,
    int? PreislisteId,
    decimal RabattProzent);

public sealed record VerkaufLookups(
    IReadOnlyList<KundeVerkaufLookupDto> Kunden,
    IReadOnlyList<ArtikelVerkaufLookupDto> Artikel,
    IReadOnlyList<LookupDto> Zahlungsbedingungen);

public sealed record PreisErgebnisDto(decimal Einzelpreis, decimal RabattProzent);
```

- [ ] **Step 2: Validatoren**

`src/Milet.Application/Verkauf/Validators.cs`:
```csharp
using FluentValidation;

namespace Milet.Application.Verkauf;

public sealed class BelegPositionValidator : AbstractValidator<BelegPositionDto>
{
    public BelegPositionValidator()
    {
        RuleFor(p => p.Menge).GreaterThan(0);
        RuleFor(p => p.Einzelpreis).GreaterThanOrEqualTo(0);
        RuleFor(p => p.RabattProzent).InclusiveBetween(0, 100);
        RuleFor(p => p.Bezeichnung).NotEmpty().MaximumLength(200);
        RuleFor(p => p.ArtikelId).NotNull().When(p => p.PositionsTyp == Domain.Entities.Verkauf.PositionsTyp.Artikel);
    }
}

public sealed class BelegValidator : AbstractValidator<BelegDto>
{
    public BelegValidator()
    {
        RuleFor(b => b.KundeId).GreaterThan(0).WithMessage("Kunde ist erforderlich.");
        RuleFor(b => b.BelegDatum).NotEqual(default(DateOnly));
        RuleFor(b => b.Positionen).NotEmpty().WithMessage("Beleg muss mindestens eine Position enthalten.");
        RuleForEach(b => b.Positionen).SetValidator(new BelegPositionValidator());
    }
}
```

- [ ] **Step 3: Validator-Tests**

`tests/Milet.Application.Tests/VerkaufValidatorTests.cs`:
```csharp
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Application.Tests;

public class VerkaufValidatorTests
{
    private static BelegPositionDto GueltigePosition() => new()
    {
        PositionsNr = 1,
        ArtikelId = 1,
        Bezeichnung = "Testartikel",
        Menge = 2,
        Einzelpreis = 10m,
        MwStSatzWert = 19m,
        GesamtNetto = 20m,
    };

    [Fact]
    public void Beleg_OhneKunde_Fehler()
    {
        var dto = new BelegDto { KundeId = 0, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Beleg_OhnePositionen_Fehler()
    {
        var dto = new BelegDto { KundeId = 1, Positionen = [] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Beleg_GueltigeDaten_KeinFehler()
    {
        var dto = new BelegDto { KundeId = 1, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.True(ergebnis.IsValid);
    }

    [Fact]
    public void Position_NegativeMenge_Fehler()
    {
        var dto = GueltigePosition() with { Menge = -1 };
        var ergebnis = new BelegPositionValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Position_ArtikeltypOhneArtikelId_Fehler()
    {
        var dto = GueltigePosition() with { ArtikelId = null, PositionsTyp = PositionsTyp.Artikel };
        var ergebnis = new BelegPositionValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj`
Expected: alle PASS (bestehende 9 + 5 neue = 14).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Application/Verkauf/ tests/Milet.Application.Tests/VerkaufValidatorTests.cs
git commit -m "Verkauf-DTOs und Validatoren (Beleg, BelegPosition, Lookups)"
```

---

### Task 5: Application — Service-Interfaces (Beleg, Lookup, Überleitung, Buchen, PDF, Firmenstamm)

**Files:**
- Create: `src/Milet.Application/Verkauf/IVerkaufServices.cs`
- Create: `src/Milet.Application/Abstractions/IPdfService.cs`
- Create: `src/Milet.Application/Admin/IFirmenstammService.cs`
- Create: `src/Milet.Application/Admin/Dtos.cs`

**Interfaces:**
- Consumes: `BelegDto`, `VerkaufLookups`, `PreisErgebnisDto`, `BelegTyp` (Task 4/1).
- Produces: `IBelegService`, `IVerkaufLookupService`, `IBelegUeberleitungService`, `IRechnungBuchenService`, `IPdfService`, `IFirmenstammService`, `FirmenstammDto` — Interfaces, die Task 8–11 und 14 implementieren und Task 15–19 (App) konsumieren.

- [ ] **Step 1: Verkauf-Service-Interfaces**

`src/Milet.Application/Verkauf/IVerkaufServices.cs`:
```csharp
namespace Milet.Application.Verkauf;

public interface IBelegService
{
    Task<IReadOnlyList<BelegDto>> SucheAsync(Domain.Entities.Verkauf.BelegTyp typ, string? suchtext, CancellationToken ct = default);
    Task<BelegDto> LadeAsync(int id, CancellationToken ct = default);
    Task<BelegDto> SpeichereAsync(BelegDto dto, CancellationToken ct = default);
    Task LoescheAsync(int id, CancellationToken ct = default);
}

public interface IVerkaufLookupService
{
    Task<VerkaufLookups> LadeLookupsAsync(CancellationToken ct = default);
    Task<PreisErgebnisDto> ErmittlePreisAsync(int artikelId, decimal menge, int kundeId, CancellationToken ct = default);
}

public interface IBelegUeberleitungService
{
    /// <summary>Kopiert alle offenen Positionen von <paramref name="quellBelegId"/> in einen neuen Beleg vom Typ <paramref name="zielTyp"/>.</summary>
    Task<BelegDto> UeberleitenAsync(int quellBelegId, Domain.Entities.Verkauf.BelegTyp zielTyp, CancellationToken ct = default);
}

public interface IRechnungBuchenService
{
    /// <summary>Vergibt atomar die Rechnungsnummer, friert den Beleg ein, legt den Offenen Posten an.</summary>
    Task<BelegDto> BuchenAsync(int rechnungId, CancellationToken ct = default);
}
```

- [ ] **Step 2: PDF-Abstraktion**

`src/Milet.Application/Abstractions/IPdfService.cs`:
```csharp
namespace Milet.Application.Abstractions;

public interface IPdfService
{
    /// <summary>Rendert den Beleg (Angebot/Auftrag/Rechnung) als PDF anhand seines Typs.</summary>
    Task<byte[]> GeneriereBelegPdfAsync(int belegId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Firmenstamm-DTO + Interface**

`src/Milet.Application/Admin/Dtos.cs`:
```csharp
using Milet.Application.Stammdaten;

namespace Milet.Application.Admin;

public sealed record FirmenstammDto
{
    public string Firmenname { get; init; } = string.Empty;
    public AdresseDto Adresse { get; init; } = new();
    public string? UStIdNr { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
}
```

`src/Milet.Application/Admin/IFirmenstammService.cs`:
```csharp
namespace Milet.Application.Admin;

public interface IFirmenstammService
{
    Task<FirmenstammDto> LadeAsync(CancellationToken ct = default);
    Task SpeichereAsync(FirmenstammDto dto, CancellationToken ct = default);
}
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Application/Milet.Application.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Application/Verkauf/IVerkaufServices.cs src/Milet.Application/Abstractions/IPdfService.cs src/Milet.Application/Admin/
git commit -m "Application-Interfaces für Verkauf (Beleg/Lookup/Ueberleitung/Buchen), PDF, Firmenstamm"
```

---

### Task 6: Infrastructure — EF-Configurations (Beleg-TPH, BelegPosition, BelegSteuerSumme, OffenerPosten, Firmenstamm) + DbContext + Migration

**Files:**
- Create: `src/Milet.Domain/Entities/Finanzen/OffenerPostenTyp.cs`
- Create: `src/Milet.Domain/Entities/Finanzen/OffenerPosten.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/BelegPositionConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/BelegSteuerSummeConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/OffenerPostenConfiguration.cs`
- Create: `src/Milet.Infrastructure/Persistence/Configurations/FirmenstammConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/MiletDbContext.cs`

**Interfaces:**
- Consumes: `Beleg`/`Angebot`/`Auftrag`/`Rechnung`/`BelegPosition`/`BelegSteuerSumme` (Task 1), `Firmenstamm` (Task 3).
- Produces: `MiletDbContext.Belege`/`.BelegPositionen`/`.BelegSteuerSummen`/`.OffenePosten`/`.Firmenstamm` DbSets, `db.Set<Angebot>()`/`db.Set<Auftrag>()`/`db.Set<Rechnung>()` (TPH-gefiltert) — von allen Infrastructure-Services (Task 8–11) konsumiert.

**Hinweis zu `LieferantId`:** Bewusst **nicht** jetzt schon als ungenutzte Spalte auf `Beleg` angelegt — kommt erst in Phase 4 (Einkauf) per neuer Migration, wenn `Bestellung`/`Wareneingang`/`Eingangsrechnung` als Subklassen hinzukommen. `KundeId` ist deshalb aktuell `int` (nicht `int?`).

- [ ] **Step 1: `OffenerPosten` (minimal für Phase 2 — nur Anlage, kein Zahlungs-/Mahnwesen)**

`src/Milet.Domain/Entities/Finanzen/OffenerPostenTyp.cs`:
```csharp
namespace Milet.Domain.Entities.Finanzen;

public enum OffenerPostenTyp
{
    Debitor = 0,
    Kreditor = 1,
}
```

`src/Milet.Domain/Entities/Finanzen/OffenerPosten.cs`:
```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Finanzen;

public class OffenerPosten : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int BelegId { get; set; }
    public Entities.Verkauf.Beleg? Beleg { get; set; }
    public int KundeId { get; set; }
    public OffenerPostenTyp Typ { get; set; } = OffenerPostenTyp.Debitor;
    public decimal Betrag { get; set; }
    public decimal OffenerBetrag { get; set; }
    public DateOnly Faelligkeit { get; set; }
    public int Mahnstufe { get; set; }
    public bool Mahnsperre { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 2: `BelegConfiguration` — TPH-Discriminator + Owned-Adress-Snapshots + Check-Constraint**

`src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegConfiguration : IEntityTypeConfiguration<Beleg>
{
    public void Configure(EntityTypeBuilder<Beleg> b)
    {
        b.ToTable("Belege", t => t.HasCheckConstraint("CK_Belege_Menge_Positiv", "1 = 1"));
        b.HasKey(x => x.Id);

        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung));

        b.Property(x => x.BelegNummer).HasMaxLength(20).IsRequired();
        // Unique je Typ — leere Rechnungsnummer (Entwurf, erst beim Buchen vergeben) ist erlaubt mehrfach leer,
        // SQL Server behandelt mehrere '' in einem Unique-Index als Duplikate -> daher Filter auf nicht-leer.
        b.HasIndex("BelegTyp", nameof(Beleg.BelegNummer))
            .IsUnique()
            .HasFilter("[BelegNummer] <> ''");

        b.Property(x => x.KundeId).IsRequired();
        b.HasOne(x => x.Kunde).WithMany().HasForeignKey(x => x.KundeId).OnDelete(DeleteBehavior.Restrict);

        b.OwnsOne(x => x.RechnungsadresseSnapshot, a =>
        {
            a.Property(p => p.Name1).HasColumnName("RgAdr_Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("RgAdr_Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("RgAdr_Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("RgAdr_Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("RgAdr_Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("RgAdr_Land").HasMaxLength(2);
        });
        b.Navigation(x => x.RechnungsadresseSnapshot).IsRequired();

        b.OwnsOne(x => x.LieferadresseSnapshot, a =>
        {
            a.Property(p => p.Name1).HasColumnName("LfAdr_Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("LfAdr_Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("LfAdr_Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("LfAdr_Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("LfAdr_Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("LfAdr_Land").HasMaxLength(2);
        });
        b.Navigation(x => x.LieferadresseSnapshot).IsRequired();

        b.Property(x => x.ZahlungsbedingungSkontoProzent).HasPrecision(5, 2);
        b.Property(x => x.SummeNetto).HasPrecision(18, 2);
        b.Property(x => x.SummeMwSt).HasPrecision(18, 2);
        b.Property(x => x.SummeBrutto).HasPrecision(18, 2);

        b.Property(x => x.Kopftext).HasMaxLength(2000);
        b.Property(x => x.Fusstext).HasMaxLength(2000);

        b.HasMany(x => x.Positionen).WithOne(p => p.Beleg).HasForeignKey(p => p.BelegId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Steuersummen).WithOne(s => s.Beleg).HasForeignKey(s => s.BelegId).OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 3: `BelegPositionConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/BelegPositionConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegPositionConfiguration : IEntityTypeConfiguration<BelegPosition>
{
    public void Configure(EntityTypeBuilder<BelegPosition> b)
    {
        b.ToTable("BelegPositionen");
        b.HasKey(x => x.Id);

        b.Property(x => x.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(x => x.EinheitKuerzel).HasMaxLength(10);

        b.Property(x => x.Menge).HasPrecision(18, 3);
        b.Property(x => x.Einzelpreis).HasPrecision(18, 4);
        b.Property(x => x.RabattProzent).HasPrecision(5, 2);
        b.Property(x => x.MwStSatzWert).HasPrecision(5, 2);
        b.Property(x => x.GesamtNetto).HasPrecision(18, 2);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MwStSatz>().WithMany().HasForeignKey(x => x.MwStSatzId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<BelegPosition>().WithMany()
            .HasForeignKey(x => x.UrsprungsPositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 4: `BelegSteuerSummeConfiguration`, `OffenerPostenConfiguration`, `FirmenstammConfiguration`**

`src/Milet.Infrastructure/Persistence/Configurations/BelegSteuerSummeConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegSteuerSummeConfiguration : IEntityTypeConfiguration<BelegSteuerSumme>
{
    public void Configure(EntityTypeBuilder<BelegSteuerSumme> b)
    {
        b.ToTable("BelegSteuerSummen");
        b.HasKey(x => x.Id);
        b.Property(x => x.MwStSatzWert).HasPrecision(5, 2);
        b.Property(x => x.NettoSumme).HasPrecision(18, 2);
        b.Property(x => x.MwStBetrag).HasPrecision(18, 2);
    }
}
```

`src/Milet.Infrastructure/Persistence/Configurations/OffenerPostenConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class OffenerPostenConfiguration : IEntityTypeConfiguration<OffenerPosten>
{
    public void Configure(EntityTypeBuilder<OffenerPosten> b)
    {
        b.ToTable("OffenePosten");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BelegId).IsUnique();
        b.HasOne(x => x.Beleg).WithMany().HasForeignKey(x => x.BelegId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Betrag).HasPrecision(18, 2);
        b.Property(x => x.OffenerBetrag).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

`src/Milet.Infrastructure/Persistence/Configurations/FirmenstammConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class FirmenstammConfiguration : IEntityTypeConfiguration<Firmenstamm>
{
    public void Configure(EntityTypeBuilder<Firmenstamm> b)
    {
        b.ToTable("Firmenstamm");
        b.HasKey(x => x.Id);
        b.Property(x => x.Firmenname).HasMaxLength(100).IsRequired();
        b.OwnsOne(x => x.Adresse, a =>
        {
            a.Property(p => p.Name1).HasColumnName("Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("Land").HasMaxLength(2);
        });
        b.Navigation(x => x.Adresse).IsRequired();
    }
}
```

- [ ] **Step 5: `MiletDbContext` — DbSets ergänzen**

Modify `src/Milet.Infrastructure/Persistence/MiletDbContext.cs` — nach `public DbSet<Nummernkreis> Nummernkreise => Set<Nummernkreis>();` einfügen:
```csharp
    public DbSet<Milet.Domain.Entities.Verkauf.Beleg> Belege => Set<Milet.Domain.Entities.Verkauf.Beleg>();
    public DbSet<Milet.Domain.Entities.Verkauf.Angebot> Angebote => Set<Milet.Domain.Entities.Verkauf.Angebot>();
    public DbSet<Milet.Domain.Entities.Verkauf.Auftrag> Auftraege => Set<Milet.Domain.Entities.Verkauf.Auftrag>();
    public DbSet<Milet.Domain.Entities.Verkauf.Rechnung> Rechnungen => Set<Milet.Domain.Entities.Verkauf.Rechnung>();
    public DbSet<Milet.Domain.Entities.Verkauf.BelegPosition> BelegPositionen => Set<Milet.Domain.Entities.Verkauf.BelegPosition>();
    public DbSet<Milet.Domain.Entities.Verkauf.BelegSteuerSumme> BelegSteuerSummen => Set<Milet.Domain.Entities.Verkauf.BelegSteuerSumme>();
    public DbSet<Milet.Domain.Entities.Finanzen.OffenerPosten> OffenePosten => Set<Milet.Domain.Entities.Finanzen.OffenerPosten>();
    public DbSet<Milet.Domain.Entities.Admin.Firmenstamm> Firmenstamm => Set<Milet.Domain.Entities.Admin.Firmenstamm>();
```
`OnModelCreating` bleibt unverändert (`ApplyConfigurationsFromAssembly` greift die neuen `IEntityTypeConfiguration<T>`-Klassen automatisch ab).

- [ ] **Step 6: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 7: Migration erzeugen**

Run:
```bash
cd src/Milet.Tools.Migrator
"$USERPROFILE/.dotnet/dotnet.exe" tool run dotnet-ef migrations add VerkaufBelegModell --project ../Milet.Infrastructure --startup-project .
```
Expected: neue Migration-Datei unter `src/Milet.Infrastructure/Persistence/Migrations/` mit `CreateTable`-Befehlen für `Belege`, `BelegPositionen`, `BelegSteuerSummen`, `OffenePosten`, `Firmenstamm`.

- [ ] **Step 8: Migration prüfen (Migrator-Tool gegen LocalDB)**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" run --project src/Milet.Tools.Migrator`
Expected: "Migrationen angewendet" o. ä., kein Fehler. Danach per `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Milet -Q "SELECT name FROM sys.tables WHERE name IN ('Belege','BelegPositionen','BelegSteuerSummen','OffenePosten','Firmenstamm')" -C` verifizieren, dass alle 5 Tabellen existieren.

- [ ] **Step 9: Commit**

```bash
git add src/Milet.Domain/Entities/Finanzen/ src/Milet.Infrastructure/Persistence/
git commit -m "EF-Configurations Beleg-TPH/BelegPosition/BelegSteuerSumme/OffenerPosten/Firmenstamm + Migration"
```

---

### Task 7: Infrastructure — BelegImmutabilityInterceptor (GoBD-Sicherheitsnetz)

**Files:**
- Create: `src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Beleg`, `BelegStatus` (Task 1).
- Produces: harte Sperre gegen `SaveChanges` auf bereits gebuchte `Beleg`-Entities — wirkt als Sicherheitsnetz unabhängig vom Service-Guard in Task 8/11.

- [ ] **Step 1: Interceptor implementieren**

`src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Interceptors;

/// <summary>GoBD: ein bereits gebuchter Beleg darf nicht mehr verändert werden. Der Guard in
/// <c>BelegService</c>/<c>RechnungBuchenService</c> greift zuerst mit einer sprechenden Fehlermeldung;
/// dieser Interceptor ist die harte Sperre für jeden Codepfad, der ihn umgeht.</summary>
public sealed class BelegImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Pruefen(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Pruefen(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Pruefen(DbContext? context)
    {
        if (context is null) return;
        foreach (EntityEntry<Beleg> entry in context.ChangeTracker.Entries<Beleg>())
        {
            if (entry.State != EntityState.Modified) continue;
            var urspruenglicherStatus = entry.OriginalValues.GetValue<BelegStatus>(nameof(Beleg.Status));
            if (urspruenglicherStatus is BelegStatus.Gebucht or BelegStatus.Storniert)
            {
                throw new InvalidOperationException(
                    $"Beleg '{entry.Entity.BelegNummer}' ist bereits gebucht und damit unveränderlich (GoBD).");
            }
        }
    }
}
```

- [ ] **Step 2: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddSingleton<AuditSaveChangesInterceptor>();` einfügen:
```csharp
        services.AddSingleton<Persistence.Interceptors.BelegImmutabilityInterceptor>();
```
Und in `AddDbContextFactory<MiletDbContext>` den zweiten Interceptor mit registrieren:
```csharp
        services.AddDbContextFactory<MiletDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<Persistence.Interceptors.BelegImmutabilityInterceptor>()));
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "BelegImmutabilityInterceptor: harte GoBD-Sperre gegen Änderung gebuchter Belege"
```

---

### Task 8: Infrastructure — VerkaufMapping + BelegService

**Hinweis:** Nummernkreise für `AN`/`AU`/`RE` sind bereits in `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs:49-52` angelegt (Format `"AN-{1}-{0:0000}"` → z. B. `AN-2026-0001`) — kein Zusatz-Seed-Task nötig.

**Files:**
- Create: `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs`
- Create: `src/Milet.Infrastructure/Services/BelegService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IBelegService` (Task 5), `Beleg`/`Angebot`/`Auftrag`/`Rechnung`/`BelegPosition`/`SteuerRechner` (Task 1/2), `BelegDto`/`BelegPositionDto`/`BelegValidator` (Task 4), `INumberRangeService` (bestehend), `AdresseDto ToDto(this Adresse)`/`Adresse ToEntity(this AdresseDto)` (bestehend, `StammdatenMapping.cs` — wiederverwendet, gleicher Assembly-`internal`-Scope).
- Produces: `BelegService : IBelegService` — von Task 15 (DI) und App-ViewModels (Task 16–18) konsumiert. Aggregat-Speichern: Beleg + Positionen + Steuersummen in **einem** Aufruf/einer Transaktion (kein separates Positions-CRUD wie bei Kleinstamm/Staffelpreise — Beleg ist ein echtes DDD-Aggregat).

- [ ] **Step 1: Mapping-Extensions**

`src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs`:
```csharp
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Services.Mapping;

internal static class VerkaufMapping
{
    public static BelegPositionDto ToDto(this BelegPosition p) => new()
    {
        Id = p.Id,
        PositionsNr = p.PositionsNr,
        PositionsTyp = p.PositionsTyp,
        ArtikelId = p.ArtikelId,
        Bezeichnung = p.Bezeichnung,
        EinheitKuerzel = p.EinheitKuerzel,
        Menge = p.Menge,
        Einzelpreis = p.Einzelpreis,
        RabattProzent = p.RabattProzent,
        MwStSatzId = p.MwStSatzId,
        MwStSatzWert = p.MwStSatzWert,
        SteuerSchluessel = p.SteuerSchluessel,
        GesamtNetto = p.GesamtNetto,
        UrsprungsPositionId = p.UrsprungsPositionId,
    };

    public static BelegDto ToDto(this Beleg b, bool mitPositionen)
    {
        var typ = b switch
        {
            Angebot => BelegTyp.Angebot,
            Auftrag => BelegTyp.Auftrag,
            Rechnung => BelegTyp.Rechnung,
            _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
        };

        return new BelegDto
        {
            Id = b.Id,
            BelegTyp = typ,
            BelegNummer = b.BelegNummer,
            BelegDatum = b.BelegDatum,
            KundeId = b.KundeId,
            KundeAnzeige = b.Kunde is null ? string.Empty : $"{b.Kunde.Kundennummer} — {b.Kunde.Adresse.Name1}",
            RechnungsadresseSnapshot = b.RechnungsadresseSnapshot.ToDto(),
            LieferadresseSnapshot = b.LieferadresseSnapshot.ToDto(),
            ZahlungsbedingungZielTage = b.ZahlungsbedingungZielTage,
            ZahlungsbedingungSkontoTage = b.ZahlungsbedingungSkontoTage,
            ZahlungsbedingungSkontoProzent = b.ZahlungsbedingungSkontoProzent,
            Status = b.Status,
            SummeNetto = b.SummeNetto,
            SummeMwSt = b.SummeMwSt,
            SummeBrutto = b.SummeBrutto,
            Faelligkeit = b.Faelligkeit,
            Leistungsdatum = b.Leistungsdatum,
            Kopftext = b.Kopftext,
            Fusstext = b.Fusstext,
            Positionen = mitPositionen ? b.Positionen.OrderBy(p => p.PositionsNr).Select(p => p.ToDto()).ToList() : [],
            RowVersion = b.RowVersion,
        };
    }
}
```

- [ ] **Step 2: `BelegService` — Lesepfad (`SucheAsync`/`LadeAsync`)**

`src/Milet.Infrastructure/Services/BelegService.cs`:
```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BelegService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IBelegService
{
    private static readonly BelegValidator Validator = new();

    private static IQueryable<Beleg> SetFuerTyp(MiletDbContext db, BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => db.Angebote,
        BelegTyp.Auftrag => db.Auftraege,
        BelegTyp.Rechnung => db.Rechnungen,
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    public async Task<IReadOnlyList<BelegDto>> SucheAsync(BelegTyp typ, string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = SetFuerTyp(db, typ).AsNoTracking().Include(b => b.Kunde).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.BelegNummer, $"%{s}%") ||
                (b.Kunde != null && EF.Functions.Like(b.Kunde.Adresse.Name1, $"%{s}%")));
        }
        var belege = await query.OrderByDescending(b => b.BelegDatum).ThenByDescending(b => b.Id).Take(500).ToListAsync(ct);
        return belege.Select(b => b.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<BelegDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.AsNoTracking()
            .Include(b => b.Kunde)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        return beleg.ToDto(mitPositionen: true);
    }
```

- [ ] **Step 3: `BelegService` — Schreibpfad (`SpeichereAsync`/`LoescheAsync`), Klasse schließen**

Direkt an `LadeAsync` anschließend, in derselben Datei/Klasse:
```csharp
    public async Task<BelegDto> SpeichereAsync(BelegDto dto, CancellationToken ct = default)
    {
        await Validator.ValidateAndThrowAsync(dto, ct);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        Beleg beleg;
        if (dto.Id == 0)
        {
            var kunde = await db.Kunden.Include(k => k.Zahlungsbedingung).FirstOrDefaultAsync(k => k.Id == dto.KundeId, ct)
                ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Kunde), dto.KundeId);

            beleg = NeueInstanz(dto.BelegTyp);
            beleg.BelegNummer = dto.BelegTyp == BelegTyp.Rechnung
                ? string.Empty
                : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(dto.BelegTyp), ct);
            beleg.KundeId = kunde.Id;
            beleg.RechnungsadresseSnapshot = kunde.Adresse.Kopie();
            beleg.LieferadresseSnapshot = kunde.Adresse.Kopie();
            beleg.ZahlungsbedingungZielTage = kunde.Zahlungsbedingung?.ZielTage ?? 0;
            beleg.ZahlungsbedingungSkontoTage = kunde.Zahlungsbedingung?.SkontoTage;
            beleg.ZahlungsbedingungSkontoProzent = kunde.Zahlungsbedingung?.SkontoProzent;
            db.Add(beleg);
        }
        else
        {
            beleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Steuersummen)
                .FirstOrDefaultAsync(b => b.Id == dto.Id, ct)
                ?? throw new NotFoundException(nameof(Beleg), dto.Id);

            if (beleg.Status != BelegStatus.Entwurf)
                throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht mehr geändert werden.");

            db.Entry(beleg).Property(b => b.RowVersion).OriginalValue = dto.RowVersion;
        }

        beleg.BelegDatum = dto.BelegDatum;
        beleg.Leistungsdatum = dto.Leistungsdatum;
        beleg.Kopftext = dto.Kopftext;
        beleg.Fusstext = dto.Fusstext;

        AktualisierePositionen(db, beleg, dto.Positionen);

        db.RemoveRange(beleg.Steuersummen);
        var neueSteuersummen = SteuerRechner.BerechneSteuersummen(beleg.Positionen);
        beleg.Steuersummen = neueSteuersummen.ToList();
        (beleg.SummeNetto, beleg.SummeMwSt, beleg.SummeBrutto) = SteuerRechner.BerechneKopfsummen(neueSteuersummen);

        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Beleg), beleg.Id, ct);
        return beleg.ToDto(mitPositionen: true);
    }

    private static void AktualisierePositionen(MiletDbContext db, Beleg beleg, IReadOnlyList<BelegPositionDto> positionenDto)
    {
        var vorhandeneIds = positionenDto.Where(p => p.Id != 0).Select(p => p.Id).ToHashSet();
        var zuEntfernen = beleg.Positionen.Where(p => !vorhandeneIds.Contains(p.Id)).ToList();
        foreach (var entfernt in zuEntfernen)
        {
            beleg.Positionen.Remove(entfernt);
            db.Remove(entfernt);
        }

        foreach (var dtoPos in positionenDto)
        {
            var gesamtNetto = SteuerRechner.BerechnePosition(dtoPos.Menge, dtoPos.Einzelpreis, dtoPos.RabattProzent);
            var bestehend = dtoPos.Id != 0 ? beleg.Positionen.FirstOrDefault(p => p.Id == dtoPos.Id) : null;
            if (bestehend is not null)
            {
                bestehend.PositionsNr = dtoPos.PositionsNr;
                bestehend.PositionsTyp = dtoPos.PositionsTyp;
                bestehend.ArtikelId = dtoPos.ArtikelId;
                bestehend.Bezeichnung = dtoPos.Bezeichnung;
                bestehend.EinheitKuerzel = dtoPos.EinheitKuerzel;
                bestehend.Menge = dtoPos.Menge;
                bestehend.Einzelpreis = dtoPos.Einzelpreis;
                bestehend.RabattProzent = dtoPos.RabattProzent;
                bestehend.MwStSatzId = dtoPos.MwStSatzId;
                bestehend.MwStSatzWert = dtoPos.MwStSatzWert;
                bestehend.SteuerSchluessel = dtoPos.SteuerSchluessel;
                bestehend.GesamtNetto = gesamtNetto;
                bestehend.UrsprungsPositionId = dtoPos.UrsprungsPositionId;
            }
            else
            {
                beleg.Positionen.Add(new BelegPosition
                {
                    PositionsNr = dtoPos.PositionsNr,
                    PositionsTyp = dtoPos.PositionsTyp,
                    ArtikelId = dtoPos.ArtikelId,
                    Bezeichnung = dtoPos.Bezeichnung,
                    EinheitKuerzel = dtoPos.EinheitKuerzel,
                    Menge = dtoPos.Menge,
                    Einzelpreis = dtoPos.Einzelpreis,
                    RabattProzent = dtoPos.RabattProzent,
                    MwStSatzId = dtoPos.MwStSatzId,
                    MwStSatzWert = dtoPos.MwStSatzWert,
                    SteuerSchluessel = dtoPos.SteuerSchluessel,
                    GesamtNetto = gesamtNetto,
                    UrsprungsPositionId = dtoPos.UrsprungsPositionId,
                });
            }
        }
    }

    public async Task LoescheAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        if (beleg.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Beleg '{beleg.BelegNummer}' ist bereits gebucht und kann nicht gelöscht werden.");
        db.Remove(beleg);
        await db.SaveChangesTranslatingConcurrencyAsync(nameof(Beleg), id, ct);
    }
}
```

- [ ] **Step 4: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IArtikelPreiseService, ArtikelPreiseService>();` einfügen:
```csharp
        services.AddScoped<IBelegService, BelegService>();
```
(`using Milet.Application.Verkauf;` am Dateikopf ergänzen.)

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs src/Milet.Infrastructure/Services/BelegService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "BelegService: Aggregat-CRUD für Angebot/Auftrag/Rechnung inkl. Steuersummen-Neuberechnung"
```

---

### Task 9: Infrastructure — VerkaufLookupService (inkl. Preisfindung) + FirmenstammService

**Files:**
- Create: `src/Milet.Infrastructure/Services/VerkaufLookupService.cs`
- Create: `src/Milet.Infrastructure/Services/Mapping/AdminMapping.cs`
- Create: `src/Milet.Infrastructure/Services/FirmenstammService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs`

**Interfaces:**
- Consumes: `IVerkaufLookupService`/`IFirmenstammService` (Task 5), `PreisfindungService.ErmittlePreis` (Domain, bereits vorhanden aus Phase 1), `ArtikelPreis`-Entity (bereits vorhanden).
- Produces: `VerkaufLookupService`, `FirmenstammService` — von Task 15 (DI) und BelegEditViewModel (Task 17) konsumiert.

- [ ] **Step 1: `VerkaufLookupService`**

`src/Milet.Infrastructure/Services/VerkaufLookupService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class VerkaufLookupService(IDbContextFactory<MiletDbContext> dbContextFactory) : IVerkaufLookupService
{
    public async Task<VerkaufLookups> LadeLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var kunden = await db.Kunden.AsNoTracking()
            .OrderBy(k => k.Kundennummer)
            .Select(k => new KundeVerkaufLookupDto(
                k.Id, $"{k.Kundennummer} — {k.Adresse.Name1}", k.ZahlungsbedingungId, k.PreislisteId, k.RabattProzent))
            .ToListAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => !a.Gesperrt)
            .OrderBy(a => a.Artikelnummer)
            .Select(a => new ArtikelVerkaufLookupDto(
                a.Id,
                $"{a.Artikelnummer} — {a.Bezeichnung}",
                a.Listenpreis,
                a.MwStSatzId,
                a.MwStSatz!.Satz,
                a.MwStSatz.SteuerSchluessel,
                a.Einheit!.Kuerzel))
            .ToListAsync(ct);

        var zahlungsbedingungen = await db.Zahlungsbedingungen.AsNoTracking()
            .OrderBy(z => z.Bezeichnung)
            .Select(z => new LookupDto(z.Id, z.Bezeichnung))
            .ToListAsync(ct);

        return new VerkaufLookups(kunden, artikel, zahlungsbedingungen);
    }

    public async Task<PreisErgebnisDto> ErmittlePreisAsync(int artikelId, decimal menge, int kundeId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artikelId, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Artikel), artikelId);
        var kunde = await db.Kunden.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kundeId, ct)
            ?? throw new Application.Common.NotFoundException(nameof(Kunde), kundeId);

        var staffelpreise = kunde.PreislisteId is int preislisteId
            ? await db.ArtikelPreise.AsNoTracking()
                .Where(p => p.ArtikelId == artikelId && p.PreislisteId == preislisteId)
                .ToListAsync(ct)
            : [];

        var ergebnis = PreisfindungService.ErmittlePreis(artikel, menge, kunde.PreislisteId, staffelpreise, kunde.RabattProzent);
        return new PreisErgebnisDto(ergebnis.Einzelpreis, ergebnis.RabattProzent);
    }
}
```

- [ ] **Step 2: `FirmenstammService` inkl. Mapping**

`src/Milet.Infrastructure/Services/Mapping/AdminMapping.cs`:
```csharp
using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Services.Mapping;

internal static class AdminMapping
{
    public static FirmenstammDto ToDto(this Firmenstamm f) => new()
    {
        Firmenname = f.Firmenname,
        Adresse = f.Adresse.ToDto(),
        UStIdNr = f.UStIdNr,
        Telefon = f.Telefon,
        Email = f.Email,
        Iban = f.Iban,
        Bic = f.Bic,
    };

    public static void ApplyTo(this FirmenstammDto dto, Firmenstamm entity)
    {
        entity.Firmenname = dto.Firmenname;
        entity.Adresse = dto.Adresse.ToEntity();
        entity.UStIdNr = dto.UStIdNr;
        entity.Telefon = dto.Telefon;
        entity.Email = dto.Email;
        entity.Iban = dto.Iban;
        entity.Bic = dto.Bic;
    }
}
```

`src/Milet.Infrastructure/Services/FirmenstammService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Admin;
using Milet.Domain.Entities.Admin;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class FirmenstammService(IDbContextFactory<MiletDbContext> dbContextFactory) : IFirmenstammService
{
    public async Task<FirmenstammDto> LadeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var firma = await db.Firmenstamm.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);
        return firma?.ToDto() ?? new FirmenstammDto();
    }

    public async Task SpeichereAsync(FirmenstammDto dto, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var firma = await db.Firmenstamm.FirstOrDefaultAsync(f => f.Id == 1, ct);
        if (firma is null)
        {
            firma = new Firmenstamm { Id = 1 };
            db.Add(firma);
        }
        dto.ApplyTo(firma);
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IBelegService, BelegService>();` einfügen:
```csharp
        services.AddScoped<IVerkaufLookupService, VerkaufLookupService>();
        services.AddScoped<IFirmenstammService, FirmenstammService>();
```
(`using Milet.Application.Admin;` am Dateikopf ergänzen.)

- [ ] **Step 4: Firmenstamm-Platzhalterdaten seeden (für PDF-Smoke-Tests in Task 14)**

Modify `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs` — am Ende der bestehenden Methode (vor dem schließenden `}` der Klasse) ergänzen:
```csharp
        if (!await db.Firmenstamm.AnyAsync(ct))
        {
            db.Firmenstamm.Add(new Firmenstamm
            {
                Id = 1,
                Firmenname = "Milet Handels GmbH",
                Adresse = new Adresse { Name1 = "Milet Handels GmbH", Strasse = "Musterstraße 1", Plz = "12345", Ort = "Musterstadt", Land = "DE" },
                UStIdNr = "DE123456789",
            });
            await db.SaveChangesAsync(ct);
        }
```
(`using Milet.Domain.Entities.Admin;` ergänzen — `using Milet.Domain.ValueObjects;` ist für `Adresse` vermutlich schon vorhanden, sonst ergänzen.)

- [ ] **Step 5: Build prüfen, Migrator laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

Run: `"$USERPROFILE/.dotnet/dotnet.exe" run --project src/Milet.Tools.Migrator`
Expected: "Grunddaten ... geprüft/angelegt", danach per `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Milet -Q "SELECT Firmenname FROM Firmenstamm" -C` prüfen, dass die Zeile existiert.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Services/VerkaufLookupService.cs src/Milet.Infrastructure/Services/FirmenstammService.cs src/Milet.Infrastructure/Services/Mapping/AdminMapping.cs src/Milet.Infrastructure/DependencyInjection.cs src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs
git commit -m "VerkaufLookupService (inkl. Preisfindung-Integration) + FirmenstammService + Seed"
```

---

### Task 10: Infrastructure — BelegUeberleitungService

**Files:**
- Create: `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IBelegUeberleitungService` (Task 5), `Beleg`/`BelegPosition.OffeneMenge`/`SteuerRechner` (Task 1/2), `VerkaufMapping.ToDto` (Task 8).
- Produces: `BelegUeberleitungService` — von Task 15 (DI) und Task 19 (Überleitung-UI) konsumiert.

**Geschäftsregel (PLAN.md §Geschäftsprozesse Punkt 1):** Preise aus dem Quellbeleg werden 1:1 übernommen (bindend, keine Neufindung). Nur Positionen mit `PositionsTyp == Artikel` unterliegen der Offene-Mengen-Logik; `Freitext`/`Zwischensumme`-Zeilen werden bei jeder Überleitung vollständig kopiert (Phase-2-Vereinfachung — eine zweite Überleitung desselben Quellbelegs würde solche Zeilen duplizieren; für die Phase-2-Flows Angebot→Auftrag→Rechnung ohne Wiederholung ist das nicht erreichbar, da der Quellbeleg nach voller Übernahme auf `Erledigt` gesetzt wird).

- [ ] **Step 1: Implementierung**

`src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Domain.Services;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class BelegUeberleitungService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IBelegUeberleitungService
{
    private static readonly Dictionary<BelegTyp, BelegTyp> ErlaubteUebergaenge = new()
    {
        [BelegTyp.Angebot] = BelegTyp.Auftrag,
        [BelegTyp.Auftrag] = BelegTyp.Rechnung,
    };

    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    public async Task<BelegDto> UeberleitenAsync(int quellBelegId, BelegTyp zielTyp, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quellBeleg = await db.Belege.Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == quellBelegId, ct)
            ?? throw new NotFoundException(nameof(Beleg), quellBelegId);

        var quellTyp = TypVon(quellBeleg);
        if (!ErlaubteUebergaenge.TryGetValue(quellTyp, out var erwarteterZielTyp) || erwarteterZielTyp != zielTyp)
            throw new InvalidOperationException($"Überleitung von {quellTyp} nach {zielTyp} wird nicht unterstützt.");

        // Offene-Mengen-Prüfung explizit in derselben Transaktion — Schutz gegen Race zweier gleichzeitiger Überleitungen.
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

        if (zielBeleg.Positionen.Count == 0)
            throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");

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
}
```

- [ ] **Step 2: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IFirmenstammService, FirmenstammService>();` einfügen:
```csharp
        services.AddScoped<IBelegUeberleitungService, BelegUeberleitungService>();
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/BelegUeberleitungService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "BelegUeberleitungService: Angebot->Auftrag / Auftrag->Rechnung mit Offene-Mengen-Logik"
```

---

### Task 11: Infrastructure — RechnungBuchenService (atomare RE-Nummer, Freeze, OP-Anlage)

**Files:**
- Create: `src/Milet.Infrastructure/Services/RechnungBuchenService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IRechnungBuchenService` (Task 5), `Rechnung`/`BelegStatus` (Task 1), `OffenerPosten` (Task 6), `INumberRangeService` (bestehend).
- Produces: `RechnungBuchenService` — kritischster Service der Phase (Testbar-Kriterium „Paralleltest: eindeutige RE-Nummern"), von Task 12 (Integrationstest) und Task 18 (RechnungEditViewModel Buchen-Button) konsumiert.

- [ ] **Step 1: Implementierung**

`src/Milet.Infrastructure/Services/RechnungBuchenService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Application.Common;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class RechnungBuchenService(
    IDbContextFactory<MiletDbContext> dbContextFactory,
    INumberRangeService numberRangeService) : IRechnungBuchenService
{
    public async Task<BelegDto> BuchenAsync(int rechnungId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var rechnung = await db.Rechnungen.Include(r => r.Positionen)
            .FirstOrDefaultAsync(r => r.Id == rechnungId, ct)
            ?? throw new NotFoundException(nameof(Rechnung), rechnungId);

        if (rechnung.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Rechnung '{rechnung.BelegNummer}' ist bereits gebucht.");
        if (rechnung.Positionen.Count == 0)
            throw new InvalidOperationException("Rechnung ohne Positionen kann nicht gebucht werden.");

        rechnung.BelegNummer = await numberRangeService.NaechsteNummerAsync("RE", ct);
        rechnung.Faelligkeit = rechnung.BelegDatum.AddDays(rechnung.ZahlungsbedingungZielTage);
        rechnung.Status = BelegStatus.Gebucht;

        db.OffenePosten.Add(new OffenerPosten
        {
            BelegId = rechnung.Id,
            KundeId = rechnung.KundeId,
            Typ = OffenerPostenTyp.Debitor,
            Betrag = rechnung.SummeBrutto,
            OffenerBetrag = rechnung.SummeBrutto,
            Faelligkeit = rechnung.Faelligkeit.Value,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return rechnung.ToDto(mitPositionen: true);
    }
}
```

**Hinweis Immutability:** `BelegImmutabilityInterceptor` (Task 7) blockt nur `EntityState.Modified` bei *bereits* `Gebucht`/`Storniert` — der Übergang Entwurf→Gebucht selbst ist erlaubt (Original-Status war `Entwurf`), genau wie gefordert.

- [ ] **Step 2: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IBelegUeberleitungService, BelegUeberleitungService>();` einfügen:
```csharp
        services.AddScoped<IRechnungBuchenService, RechnungBuchenService>();
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/RechnungBuchenService.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "RechnungBuchenService: atomare RE-Nummer, Freeze, Offener-Posten-Anlage in einer Transaktion"
```

---

### Task 12: Integrationstest — parallele RE-Nummern-Vergabe beim Buchen + Immutability

**Files:**
- Create: `tests/Milet.IntegrationTests/RechnungBuchenServiceTests.cs`

**Interfaces:**
- Consumes: `RechnungBuchenService`, `BelegImmutabilityInterceptor`, `NumberRangeService` (Task 11/7), Testcontainers-Setup-Muster aus `tests/Milet.IntegrationTests/NumberRangeServiceTests.cs` (bestehend — `DockerVerfuegbar()`-Skip-Guard, `TestDbContextFactory`).

- [ ] **Step 1: Test schreiben**

`tests/Milet.IntegrationTests/RechnungBuchenServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;

namespace Milet.IntegrationTests;

public sealed class RechnungBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;

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
        db.Nummernkreise.Add(new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" });
        var kunde = new Kunde { Kundennummer = "KD-TEST", Adresse = new() { Name1 = "Testkunde" } };
        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<int> NeueRechnungAsync()
    {
        await using var db = new MiletDbContext(_options);
        var kunde = await db.Kunden.FirstAsync();
        var rechnung = new Rechnung
        {
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            KundeId = kunde.Id,
            RechnungsadresseSnapshot = kunde.Adresse.Kopie(),
            LieferadresseSnapshot = kunde.Adresse.Kopie(),
            Positionen = [new BelegPosition { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 10m, GesamtNetto = 10m, MwStSatzWert = 19m }],
            SummeNetto = 10m,
            SummeMwSt = 1.90m,
            SummeBrutto = 11.90m,
        };
        db.Add(rechnung);
        await db.SaveChangesAsync();
        return rechnung.Id;
    }

    [Fact]
    public async Task ParallelesBuchen_MehrererRechnungen_LiefertEindeutigeNummern()
    {
        var rechnungIds = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => NeueRechnungAsync()));

        var service = new RechnungBuchenService(_factory, new NumberRangeService(_factory));
        var ergebnisse = await Task.WhenAll(rechnungIds.Select(id => service.BuchenAsync(id)));

        Assert.Equal(15, ergebnisse.Select(r => r.BelegNummer).Distinct().Count());
        Assert.All(ergebnisse, r => Assert.Equal(Domain.Entities.Verkauf.BelegStatus.Gebucht, r.Status));
    }

    [Fact]
    public async Task GebuchteRechnung_AenderungWirftImmutabilityFehler()
    {
        var rechnungId = await NeueRechnungAsync();
        var service = new RechnungBuchenService(_factory, new NumberRangeService(_factory));
        await service.BuchenAsync(rechnungId);

        await using var db = new MiletDbContext(_options);
        var rechnung = await db.Rechnungen.FirstAsync(r => r.Id == rechnungId);
        rechnung.Kopftext = "Nachträgliche Änderung";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
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

- [ ] **Step 2: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: Bei fehlendem Docker: beide Tests `Skipped` (kein Fail, wie bei `NumberRangeServiceTests`). Bei vorhandenem Docker: beide `Passed`.

- [ ] **Step 3: Commit**

```bash
git add tests/Milet.IntegrationTests/RechnungBuchenServiceTests.cs
git commit -m "Integrationstest: parallele RE-Nummern-Vergabe + Immutability gebuchter Rechnungen"
```

---

### Task 13: Infrastructure — QuestPDF: BelegPdfDocument + PdfService + Smoke-Tests

**Designentscheidung:** Ein einziges `BelegPdfDocument` für alle 3 Dokumenttypen (Angebot/Auftragsbestätigung/Rechnung) statt 3 fast identischer Klassen — Layout ist zu 95 % identisch (Briefkopf, Adresse, Positionstabelle, Summenblock), Unterscheidung nur über Titel-Text und optionale Fälligkeits-Zeile (die ohnehin nur bei `Rechnung` befüllt ist, da `BelegDto.Faelligkeit` bei Angebot/Auftrag immer `null` ist). Erfüllt „QuestPDF (Briefkopf + 3 Dokumente)" als 3 unterscheidbare **Ausgaben**, nicht 3 Klassen.

**Files:**
- Create: `src/Milet.Infrastructure/Pdf/BelegPdfDocument.cs`
- Create: `src/Milet.Infrastructure/Pdf/PdfService.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`
- Test: `tests/Milet.IntegrationTests/BelegPdfDocumentTests.cs`

**Interfaces:**
- Consumes: `IPdfService` (Task 5), `BelegDto`/`BelegPositionDto` (Task 4), `FirmenstammDto` (Task 5), `IBelegService`/`IFirmenstammService` (Task 8/9). QuestPDF-API: `IDocument`, `IDocumentContainer`, `QuestPDF.Fluent.*`-Extensions, `QuestPDF.Settings.License`.
- Produces: `IPdfService`-Implementierung — von Task 18 (Belegeditor „PDF"-Button) konsumiert.

- [ ] **Step 1: `BelegPdfDocument`**

`src/Milet.Infrastructure/Pdf/BelegPdfDocument.cs`:
```csharp
using Milet.Application.Admin;
using Milet.Application.Verkauf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

internal sealed class BelegPdfDocument(BelegDto beleg, FirmenstammDto firma, string dokumenttitel) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text(firma.Firmenname).FontSize(14).Bold();
                col.Item().Text($"{firma.Adresse.Strasse}, {firma.Adresse.Plz} {firma.Adresse.Ort}");
                if (!string.IsNullOrWhiteSpace(firma.UStIdNr))
                    col.Item().Text($"USt-IdNr.: {firma.UStIdNr}");
                col.Item().PaddingTop(10).LineHorizontal(1);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text(dokumenttitel).FontSize(16).Bold();
                col.Item().Text($"Nummer: {(string.IsNullOrEmpty(beleg.BelegNummer) ? "(Entwurf)" : beleg.BelegNummer)}");
                col.Item().Text($"Datum: {beleg.BelegDatum:dd.MM.yyyy}");

                col.Item().PaddingTop(4).Text(beleg.RechnungsadresseSnapshot.Name1);
                if (!string.IsNullOrWhiteSpace(beleg.RechnungsadresseSnapshot.Name2))
                    col.Item().Text(beleg.RechnungsadresseSnapshot.Name2!);
                col.Item().Text(beleg.RechnungsadresseSnapshot.Strasse);
                col.Item().Text($"{beleg.RechnungsadresseSnapshot.Plz} {beleg.RechnungsadresseSnapshot.Ort}");

                if (!string.IsNullOrWhiteSpace(beleg.Kopftext))
                    col.Item().PaddingTop(10).Text(beleg.Kopftext);

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(30);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Pos");
                        h.Cell().Text("Bezeichnung");
                        h.Cell().AlignRight().Text("Menge");
                        h.Cell().AlignRight().Text("Preis");
                        h.Cell().AlignRight().Text("Rabatt%");
                        h.Cell().AlignRight().Text("Gesamt");
                    });

                    foreach (var position in beleg.Positionen)
                    {
                        table.Cell().Text(position.PositionsNr.ToString());
                        table.Cell().Text(position.Bezeichnung);
                        table.Cell().AlignRight().Text($"{position.Menge:0.###} {position.EinheitKuerzel}");
                        table.Cell().AlignRight().Text($"{position.Einzelpreis:0.00}");
                        table.Cell().AlignRight().Text($"{position.RabattProzent:0.##}");
                        table.Cell().AlignRight().Text($"{position.GesamtNetto:0.00}");
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Column(sum =>
                {
                    sum.Item().Text($"Netto: {beleg.SummeNetto:0.00} €");
                    sum.Item().Text($"MwSt: {beleg.SummeMwSt:0.00} €");
                    sum.Item().Text($"Brutto: {beleg.SummeBrutto:0.00} €").Bold();
                });

                if (beleg.Faelligkeit is { } faelligkeit)
                    col.Item().PaddingTop(10).Text($"Fällig am: {faelligkeit:dd.MM.yyyy}");

                if (!string.IsNullOrWhiteSpace(beleg.Fusstext))
                    col.Item().PaddingTop(10).Text(beleg.Fusstext);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
```

- [ ] **Step 2: `PdfService`**

`src/Milet.Infrastructure/Pdf/PdfService.cs`:
```csharp
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Milet.Infrastructure.Pdf;

public sealed class PdfService(IBelegService belegService, IFirmenstammService firmenstammService) : IPdfService
{
    static PdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<byte[]> GeneriereBelegPdfAsync(int belegId, CancellationToken ct = default)
    {
        var beleg = await belegService.LadeAsync(belegId, ct);
        var firma = await firmenstammService.LadeAsync(ct);
        var titel = beleg.BelegTyp switch
        {
            BelegTyp.Angebot => "Angebot",
            BelegTyp.Auftrag => "Auftragsbestätigung",
            BelegTyp.Rechnung => "Rechnung",
            _ => throw new ArgumentOutOfRangeException(nameof(belegId)),
        };
        return new BelegPdfDocument(beleg, firma, titel).GeneratePdf();
    }
}
```

- [ ] **Step 3: In DI registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — nach `services.AddScoped<IRechnungBuchenService, RechnungBuchenService>();` einfügen:
```csharp
        services.AddScoped<IPdfService, Pdf.PdfService>();
```

- [ ] **Step 4: Render-Smoke-Tests je Dokumenttyp**

`tests/Milet.IntegrationTests/BelegPdfDocumentTests.cs`:
```csharp
using Milet.Application.Admin;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Milet.IntegrationTests;

public sealed class BelegPdfDocumentTests
{
    static BelegPdfDocumentTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly FirmenstammDto Firma = new()
    {
        Firmenname = "Testfirma GmbH",
        Adresse = new AdresseDto { Name1 = "Testfirma GmbH", Strasse = "Teststr. 1", Plz = "00000", Ort = "Testort" },
        UStIdNr = "DE000000000",
    };

    private static BelegDto Beleg(BelegTyp typ, DateOnly? faelligkeit) => new()
    {
        BelegTyp = typ,
        BelegNummer = typ == BelegTyp.Rechnung && faelligkeit is null ? "" : $"{typ}-0001",
        BelegDatum = DateOnly.FromDateTime(DateTime.Today),
        RechnungsadresseSnapshot = new AdresseDto { Name1 = "Testkunde", Strasse = "Kundenstr. 1", Plz = "11111", Ort = "Kundenstadt" },
        Faelligkeit = faelligkeit,
        SummeNetto = 100m,
        SummeMwSt = 19m,
        SummeBrutto = 119m,
        Positionen = [new BelegPositionDto { PositionsNr = 1, Bezeichnung = "Testartikel", Menge = 1, Einzelpreis = 100m, MwStSatzWert = 19m, GesamtNetto = 100m }],
    };

    [Theory]
    [InlineData(BelegTyp.Angebot)]
    [InlineData(BelegTyp.Auftrag)]
    public void GeneratePdf_AngebotUndAuftrag_LiefertNichtLeeresPdf(BelegTyp typ)
    {
        var titel = typ == BelegTyp.Angebot ? "Angebot" : "Auftragsbestätigung";
        var bytes = new BelegPdfDocument(Beleg(typ, faelligkeit: null), Firma, titel).GeneratePdf();
        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]); // '%' — PDF-Header "%PDF-"
    }

    [Fact]
    public void GeneratePdf_Rechnung_ZeigtFaelligkeitUndLiefertNichtLeeresPdf()
    {
        var beleg = Beleg(BelegTyp.Rechnung, faelligkeit: DateOnly.FromDateTime(DateTime.Today.AddDays(14)));
        var bytes = new BelegPdfDocument(beleg, Firma, "Rechnung").GeneratePdf();
        Assert.NotEmpty(bytes);
    }
}
```

- [ ] **Step 5: Build + Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler.

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`
Expected: die 3 neuen PDF-Tests laufen **ohne** Docker (kein Skip-Guard nötig, reines In-Memory-Rendering) — PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Pdf/ src/Milet.Infrastructure/DependencyInjection.cs tests/Milet.IntegrationTests/BelegPdfDocumentTests.cs
git commit -m "QuestPDF: BelegPdfDocument (Angebot/Auftragsbestätigung/Rechnung) + PdfService + Render-Smoke-Tests"
```

---

### Task 14: App — Navigation-Menü „Verkauf" aktivieren (Scaffolding vor den ViewModels)

**Files:**
- Modify: `src/Milet.App/Shell/ShellPage.xaml`

**Interfaces:**
- Produces: NavigationView-Einträge `angebote`/`auftraege`/`rechnungen` — Tags, auf die Task 15/17 in `ShellPage.xaml.cs` und `NavView_SelectionChanged` registrieren bzw. reagieren.

- [ ] **Step 1: Verkauf-Menüpunkt aktivieren + Untermenü**

Modify `src/Milet.App/Shell/ShellPage.xaml` — ersetze:
```xml
            <NavigationViewItem Content="Verkauf" Tag="verkauf" Icon="Shop" IsEnabled="False" />
```
durch:
```xml
            <NavigationViewItem Content="Verkauf" Tag="verkauf" Icon="Shop">
                <NavigationViewItem.MenuItems>
                    <NavigationViewItem Content="Angebote" Tag="angebote" Icon="Bookmarks" />
                    <NavigationViewItem Content="Aufträge" Tag="auftraege" Icon="Accept" />
                    <NavigationViewItem Content="Rechnungen" Tag="rechnungen" Icon="PostUpdate" />
                </NavigationViewItem.MenuItems>
            </NavigationViewItem>
```

- [ ] **Step 2: Build prüfen (XAML-Compile)**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -c Debug -p:Platform=x64`
Expected: 0 Fehler (Navigation-Handler in `ShellPage.xaml.cs` wird erst in Task 15/17 ergänzt — die neuen Tags lösen bis dahin einfach keinen `case` aus, das ist unschädlich).

- [ ] **Step 3: Commit**

```bash
git add src/Milet.App/Shell/ShellPage.xaml
git commit -m "Verkauf-Menü aktivieren: Untermenü Angebote/Aufträge/Rechnungen"
```

---

### Task 15: App — Angebot/Auftrag/Rechnung ListViewModel + ListPage (3× konkret, Muster wie `KundenListViewModel`)

**Designentscheidung:** Kein gemeinsamer `BelegListViewModelBase` — dieser Codebasis-Stil hält je Entität eine eigene konkrete List-VM (`KundenListViewModel`/`LieferantenListViewModel`/`ArtikelListViewModel` teilen sich ebenfalls keine Basisklasse, trotz identischer Struktur). Konsistenz mit bestehendem Code wiegt hier höher als DRY um den Preis einer neuen, nirgends sonst verwendeten Abstraktion.

**Files:**
- Create: `src/Milet.App/ViewModels/Verkauf/AngebotListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Verkauf/AuftragListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Verkauf/RechnungListViewModel.cs`
- Create: `src/Milet.App/Views/Verkauf/AngebotListPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/AuftragListPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/RechnungListPage.xaml` (+ `.xaml.cs`)
- Modify: `src/Milet.App/App.xaml.cs`
- Modify: `src/Milet.App/Shell/ShellPage.xaml.cs`

**Interfaces:**
- Consumes: `IBelegService` (Task 8), `BelegDto` (Task 4), `INavigationService`/`IDialogService` (bestehend).
- Produces: `AngebotListViewModel`/`AuftragListViewModel`/`RechnungListViewModel` — Navigationsziele aus `ShellPage`, navigieren mit `int`-Parameter (Beleg-Id, `0` = neu) zu den in Task 17 erstellten Edit-ViewModels.

- [ ] **Step 1: `AngebotListViewModel`**

`src/Milet.App/ViewModels/Verkauf/AngebotListViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AngebotListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public AngebotListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService; _navigation = navigation; _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Angebot, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<AngebotEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } beleg) _navigation.Navigate<AngebotEditViewModel>(beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Angebot löschen", $"Angebot '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
```

- [ ] **Step 2: `AuftragListViewModel`, `RechnungListViewModel`**

`src/Milet.App/ViewModels/Verkauf/AuftragListViewModel.cs` — identisch zu Step 1, mit folgenden Ersetzungen: Klassenname `AuftragListViewModel`, `BelegTyp.Angebot` → `BelegTyp.Auftrag`, `AngebotEditViewModel` → `AuftragEditViewModel`, Dialogtexte „Angebot" → „Auftrag":
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class AuftragListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public AuftragListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService; _navigation = navigation; _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Auftrag, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<AuftragEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } beleg) _navigation.Navigate<AuftragEditViewModel>(beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Auftrag löschen", $"Auftrag '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
```

`src/Milet.App/ViewModels/Verkauf/RechnungListViewModel.cs` — dieselbe Struktur, `BelegTyp.Rechnung`, `RechnungEditViewModel`, „Rechnung":
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed partial class RechnungListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public RechnungListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
    {
        _belegService = belegService; _navigation = navigation; _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial string? Suchtext { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegDto> Belege { get; set; } = [];
    [ObservableProperty] public partial BelegDto? Ausgewaehlt { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try { Belege = await _belegService.SucheAsync(BelegTyp.Rechnung, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<RechnungEditViewModel>(0);

    [RelayCommand]
    private void Bearbeiten()
    {
        if (Ausgewaehlt is { } beleg) _navigation.Navigate<RechnungEditViewModel>(beleg.Id);
    }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Rechnung löschen", $"Rechnung '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
```

- [ ] **Step 3: `AngebotListPage.xaml` (+ `.xaml.cs`)**

`src/Milet.App/Views/Verkauf/AngebotListPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Verkauf.AngebotListPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Padding="24" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Angebote" Style="{StaticResource TitleTextBlockStyle}" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8">
            <TextBox Width="300" PlaceholderText="Suche (Nummer oder Kunde)…"
                     Text="{x:Bind ViewModel.Suchtext, Mode=TwoWay}" />
            <Button Content="Suchen" Command="{x:Bind ViewModel.LadenCommand}" />
            <Button Content="Neu" Command="{x:Bind ViewModel.NeuCommand}" />
            <Button Content="Bearbeiten" Command="{x:Bind ViewModel.BearbeitenCommand}" />
            <Button Content="Löschen" Command="{x:Bind ViewModel.LoeschenCommand}" />
            <ProgressRing IsActive="{x:Bind ViewModel.LaedtGerade, Mode=OneWay}" Width="24" Height="24" />
        </StackPanel>

        <ListView Grid.Row="2"
            ItemsSource="{x:Bind ViewModel.Belege, Mode=OneWay}"
            SelectedItem="{x:Bind ViewModel.Ausgewaehlt, Mode=TwoWay}"
            SelectionMode="Single">
            <ListView.HeaderTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="260" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="100" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="Nummer" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="Datum" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="Kunde" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="3" Text="Brutto" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="4" Text="Status" FontWeight="SemiBold" />
                    </Grid>
                </DataTemplate>
            </ListView.HeaderTemplate>
            <ListView.ItemTemplate>
                <DataTemplate>
                    <Grid Padding="8,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="260" /><ColumnDefinition Width="110" />
                            <ColumnDefinition Width="100" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding BelegNummer}" />
                        <TextBlock Grid.Column="1" Text="{Binding BelegDatum}" />
                        <TextBlock Grid.Column="2" Text="{Binding KundeAnzeige}" />
                        <TextBlock Grid.Column="3" Text="{Binding SummeBrutto}" />
                        <TextBlock Grid.Column="4" Text="{Binding Status}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

`src/Milet.App/Views/Verkauf/AngebotListPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AngebotListPage : Page
{
    public AngebotListViewModel ViewModel { get; }
    public AngebotListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AngebotListViewModel>();
        InitializeComponent();
    }
}
```

- [ ] **Step 4: `AuftragListPage`/`RechnungListPage`**

`src/Milet.App/Views/Verkauf/AuftragListPage.xaml` — Kopie von Step 3 mit `x:Class="Milet.App.Views.Verkauf.AuftragListPage"`, Titel „Aufträge", `ViewModel.Suchtext` etc. unverändert (Bindungsnamen sind gleich, nur der ViewModel-Typ ändert sich im Code-Behind).
`src/Milet.App/Views/Verkauf/AuftragListPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AuftragListPage : Page
{
    public AuftragListViewModel ViewModel { get; }
    public AuftragListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AuftragListViewModel>();
        InitializeComponent();
    }
}
```

`src/Milet.App/Views/Verkauf/RechnungListPage.xaml` — Kopie mit `x:Class="Milet.App.Views.Verkauf.RechnungListPage"`, Titel „Rechnungen".
`src/Milet.App/Views/Verkauf/RechnungListPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class RechnungListPage : Page
{
    public RechnungListViewModel ViewModel { get; }
    public RechnungListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<RechnungListViewModel>();
        InitializeComponent();
    }
}
```

- [ ] **Step 5: DI-Registrierung**

Modify `src/Milet.App/App.xaml.cs` — `using Milet.App.ViewModels.Verkauf;` ergänzen, nach `builder.Services.AddTransient<KleinstammViewModel>();` einfügen:
```csharp
        builder.Services.AddTransient<AngebotListViewModel>();
        builder.Services.AddTransient<AuftragListViewModel>();
        builder.Services.AddTransient<RechnungListViewModel>();
```
(Edit-ViewModels werden in Task 17 ergänzt.)

- [ ] **Step 6: Navigation registrieren + Menü verdrahten**

Modify `src/Milet.App/Shell/ShellPage.xaml.cs` — `using Milet.App.ViewModels.Verkauf;` und `using Milet.App.Views.Verkauf;` ergänzen. Nach `_navigation.Register<KleinstammViewModel, KleinstammPage>();` einfügen:
```csharp
        _navigation.Register<AngebotListViewModel, AngebotListPage>();
        _navigation.Register<AuftragListViewModel, AuftragListPage>();
        _navigation.Register<RechnungListViewModel, RechnungListPage>();
```
(Edit-Registrierungen folgen in Task 17.) In `NavView_SelectionChanged` nach `case "einstellungen":` einfügen:
```csharp
            case "angebote":
                _navigation.Navigate<AngebotListViewModel>();
                break;
            case "auftraege":
                _navigation.Navigate<AuftragListViewModel>();
                break;
            case "rechnungen":
                _navigation.Navigate<RechnungListViewModel>();
                break;
```

- [ ] **Step 7: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -c Debug -p:Platform=x64`
Expected: **Fehler erwartet** — `AngebotEditViewModel`/`AuftragEditViewModel`/`RechnungEditViewModel` existieren noch nicht (Task 17). Das ist an dieser Stelle im Plan bewusst; Task 16/17 schließen die Lücke, bevor der nächste vollständige Build erwartet wird.

- [ ] **Step 8: Commit**

```bash
git add src/Milet.App/ViewModels/Verkauf/*ListViewModel.cs src/Milet.App/Views/Verkauf/*ListPage.xaml* src/Milet.App/App.xaml.cs src/Milet.App/Shell/
git commit -m "Verkauf-Listen: Angebot/Auftrag/Rechnung ListViewModel + ListPage + Navigation"
```

---

### Task 16: App — `BelegEditViewModelBase` (Kopf + Positionsgrid + Live-Summen + Preisfindung + Buchen/Überleiten)

**Designentscheidung (Gegensatz zu Task 15):** Hier **wird** eine gemeinsame Basisklasse angelegt. Anders als bei den Listen-VMs ist die Edit-Logik nicht nur strukturell ähnlich, sondern für Angebot/Auftrag/Rechnung exakt identisch (gleiche Felder, gleiche Positions-/Summenlogik) — einziger Unterschied ist ein Datentyp-Tag plus die Buchen-Funktion nur bei Rechnung. Drei Kopien von ~200 Zeilen identischer Geschäftslogik wären eine Wartungsfalle (jede Änderung an der Summenberechnung müsste 3× nachgezogen werden), während bei den Listen-VMs (Task 15) die Methoden kurz genug sind, dass Kopieren dem bestehenden Codebasis-Stil treuer bleibt.

**Live-Summen-Hinweis:** Die Positions-/Steuerformel wird hier **client-seitig dupliziert** (nicht aus `Milet.Domain.Services.SteuerRechner` aufgerufen), weil `Milet.App` architekturbedingt nicht auf `Milet.Domain` referenziert (nur auf `Milet.Application`/`Milet.Infrastructure`, siehe PLAN.md §Solution-Struktur). Das ist reine UI-Vorschau — `BelegService.SpeichereAsync` (Task 8) berechnet `GesamtNetto`/Summen serverseitig autoritativ neu und ignoriert, was der Client sendet.

**Lieferadresse/Rechnungsadresse:** In Phase 2 nicht im Editor editierbar — wird serverseitig beim Erstanlegen immer 1:1 aus dem Kundenstamm übernommen (`BelegService.SpeichereAsync`, Task 8). Manuelles Abweichen (z. B. abweichende Lieferadresse) ist bewusst out of scope, siehe PLAN.md Phase 3 (Lieferschein).

**Files:**
- Create: `src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs`

**Interfaces:**
- Consumes: `IBelegService`/`IVerkaufLookupService`/`IBelegUeberleitungService`/`IRechnungBuchenService`/`IPdfService` (Task 5/8/9/10/11/13), `INavigationService`/`IDialogService`/`INavigationAware` (bestehend), `BelegDto`/`BelegPositionDto`/`KundeVerkaufLookupDto`/`ArtikelVerkaufLookupDto`/`PreisErgebnisDto` (Task 4).
- Produces: `BelegEditViewModelBase` — abstrakte Basis für `AngebotEditViewModel`/`AuftragEditViewModel`/`RechnungEditViewModel` (Task 17). Abstrakte Mitglieder, die Subklassen implementieren müssen: `protected abstract void NavigiereZurListe();`, `protected abstract void NavigiereNachUeberleitung(BelegTyp zielTyp);` (beides **nicht** `[RelayCommand]`-dekoriert, sondern nur intern von den konkreten `[RelayCommand]`-Methoden dieser Basisklasse aufgerufen — vermeidet ungeklärtes Verhalten des CommunityToolkit.Mvvm-Quellgenerators bei `[RelayCommand]` auf abstrakten Methoden).

- [ ] **Step 1: Basisklasse — Felder, Konstruktor, beobachtbare Properties**

`src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs`:
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
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly IRechnungBuchenService? _buchenService;
    private readonly IPdfService _pdfService;
    protected readonly INavigationService Navigation;
    private readonly IDialogService _dialogService;

    private int _id;
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
        _ueberleitungService = ueberleitungService;
        _buchenService = buchenService;
        _pdfService = pdfService;
        Navigation = navigation;
        _dialogService = dialogService;
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
```

- [ ] **Step 2: `OnNavigatedTo`/`InitAsync`**

Direkt anschließend, in derselben Klasse:
```csharp
    public void OnNavigatedTo(NavigationEventArgs args)
    {
        _id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Kunden = lookups.Kunden;
        ArtikelLookups = lookups.Artikel;

        if (_id == 0)
        {
            IstBearbeitbar = true;
            return;
        }

        var beleg = await _belegService.LadeAsync(_id);
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
```

- [ ] **Step 3: Positionsgrid-Commands + Live-Summen**

Direkt anschließend:
```csharp
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
            Bezeichnung = artikel.Anzeige,
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
```

- [ ] **Step 4: `SpeichernAsync`, `BuchenAsync`, `PdfAsync`, `UeberleitenAsync`, `Abbrechen`, abstrakte Mitglieder**

Direkt anschließend, Klasse schließen:
```csharp
    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;
        var dto = new BelegDto
        {
            Id = _id,
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
            _id = gespeichert.Id;
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
            var neuLaden = await _dialogService.BestaetigenAsync(
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
        if (_buchenService is null || _id == 0) return;
        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id);
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
        if (_id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        try
        {
            var pdfBytes = await _pdfService.GeneriereBelegPdfAsync(_id);
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
        if (_id == 0) { Fehlermeldung = "Beleg muss erst gespeichert werden."; return; }
        var zielTyp = _typ switch
        {
            BelegTyp.Angebot => BelegTyp.Auftrag,
            BelegTyp.Auftrag => BelegTyp.Rechnung,
            _ => (BelegTyp?)null,
        };
        if (zielTyp is null) return;

        try
        {
            await _ueberleitungService.UeberleitenAsync(_id, zielTyp.Value);
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

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -c Debug -p:Platform=x64`
Expected: **Fehler erwartet** — `AngebotEditViewModel` & Co. (Task 17) fehlen noch, `BelegEditViewModelBase` selbst muss aber fehlerfrei kompilieren (abstrakte Klasse, kein XAML dafür).

- [ ] **Step 6: Commit**

```bash
git add src/Milet.App/ViewModels/Verkauf/BelegEditViewModelBase.cs
git commit -m "BelegEditViewModelBase: Kopf/Positionsgrid/Live-Summen/Preisfindung/Buchen/Überleiten/PDF"
```

---

### Task 17: App — Angebot/Auftrag/Rechnung EditViewModel + EditPage (3× konkret über `BelegEditViewModelBase`)

**Files:**
- Create: `src/Milet.App/ViewModels/Verkauf/AngebotEditViewModel.cs`
- Create: `src/Milet.App/ViewModels/Verkauf/AuftragEditViewModel.cs`
- Create: `src/Milet.App/ViewModels/Verkauf/RechnungEditViewModel.cs`
- Create: `src/Milet.App/Views/Verkauf/AngebotEditPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/AuftragEditPage.xaml` (+ `.xaml.cs`)
- Create: `src/Milet.App/Views/Verkauf/RechnungEditPage.xaml` (+ `.xaml.cs`)
- Modify: `src/Milet.App/App.xaml.cs`
- Modify: `src/Milet.App/Shell/ShellPage.xaml.cs`

**Interfaces:**
- Consumes: `BelegEditViewModelBase` (Task 16), alle Verkauf-Interfaces (Task 5/8/9/10/11/13), bestehende Converter `DecimalToDoubleConverter`/`StringNotEmptyToBoolConverter`/`DateOnlyToDateTimeOffsetConverter`.
- Produces: `AngebotEditViewModel`/`AuftragEditViewModel`/`RechnungEditViewModel` — Navigationsziele aus Task 15's List-VMs (`Navigate<TEditViewModel>(int id)`).

- [ ] **Step 1: `AngebotEditViewModel`**

`src/Milet.App/ViewModels/Verkauf/AngebotEditViewModel.cs`:
```csharp
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class AngebotEditViewModel : BelegEditViewModelBase
{
    public AngebotEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
        : base(BelegTyp.Angebot, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AngebotListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<AuftragListViewModel>();
}
```

- [ ] **Step 2: `AuftragEditViewModel`, `RechnungEditViewModel`**

`src/Milet.App/ViewModels/Verkauf/AuftragEditViewModel.cs`:
```csharp
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class AuftragEditViewModel : BelegEditViewModelBase
{
    public AuftragEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
        : base(BelegTyp.Auftrag, belegService, lookupService, ueberleitungService, buchenService: null, pdfService, navigation, dialogService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<AuftragListViewModel>();
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
```

`src/Milet.App/ViewModels/Verkauf/RechnungEditViewModel.cs`:
```csharp
using Milet.App.Services;
using Milet.Application.Abstractions;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Verkauf;

public sealed class RechnungEditViewModel : BelegEditViewModelBase
{
    public RechnungEditViewModel(
        IBelegService belegService,
        IVerkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService,
        IRechnungBuchenService buchenService,
        IPdfService pdfService,
        INavigationService navigation,
        IDialogService dialogService)
        : base(BelegTyp.Rechnung, belegService, lookupService, ueberleitungService, buchenService, pdfService, navigation, dialogService)
    {
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<RechnungListViewModel>();

    // Wird nie aufgerufen — ZeigtUeberleitenButton ist bei Rechnung false (kein weiterer Belegtyp in Phase 2).
    protected override void NavigiereNachUeberleitung(BelegTyp zielTyp) => Navigation.Navigate<RechnungListViewModel>();
}
```

- [ ] **Step 3: `AngebotEditPage.xaml` (+ `.xaml.cs`)**

`src/Milet.App/Views/Verkauf/AngebotEditPage.xaml`:
```xml
<Page
    x:Class="Milet.App.Views.Verkauf.AngebotEditPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <ScrollViewer Padding="24">
        <StackPanel MaxWidth="900" Spacing="16" IsEnabled="{x:Bind ViewModel.IstBearbeitbar, Mode=OneWay}">
            <TextBlock Text="Angebot" Style="{StaticResource TitleTextBlockStyle}" />
            <InfoBar IsOpen="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay, Converter={StaticResource StringNotEmptyToBoolConverter}}"
                     Severity="Error" Title="Fehler" Message="{x:Bind ViewModel.Fehlermeldung, Mode=OneWay}" />

            <Grid ColumnSpacing="16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Header="Nummer" IsReadOnly="True" Text="{x:Bind ViewModel.BelegNummer, Mode=OneWay}" />
                <CalendarDatePicker Grid.Column="1" Header="Datum" HorizontalAlignment="Stretch"
                                     Date="{x:Bind ViewModel.BelegDatum, Mode=TwoWay}" />
                <ComboBox Grid.Column="2" Header="Kunde *" HorizontalAlignment="Stretch"
                          ItemsSource="{x:Bind ViewModel.Kunden, Mode=OneWay}"
                          SelectedValue="{x:Bind ViewModel.KundeId, Mode=TwoWay}"
                          SelectedValuePath="Id" DisplayMemberPath="Anzeige" />
            </Grid>

            <TextBlock Text="Positionen" Style="{StaticResource SubtitleTextBlockStyle}" />
            <ListView MinHeight="160" MaxHeight="300" ItemsSource="{x:Bind ViewModel.Positionen, Mode=OneWay}"
                      SelectedItem="{x:Bind ViewModel.PositionAusgewaehlt, Mode=TwoWay}" SelectionMode="Single">
                <ListView.HeaderTemplate>
                    <DataTemplate>
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" /><ColumnDefinition Width="100" />
                                <ColumnDefinition Width="100" /><ColumnDefinition Width="80" />
                                <ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Bezeichnung" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="1" Text="Menge" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="2" Text="Einzelpreis" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="3" Text="Rabatt%" FontWeight="SemiBold" />
                            <TextBlock Grid.Column="4" Text="Gesamt" FontWeight="SemiBold" />
                        </Grid>
                    </DataTemplate>
                </ListView.HeaderTemplate>
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="4" ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" /><ColumnDefinition Width="100" />
                                <ColumnDefinition Width="100" /><ColumnDefinition Width="80" />
                                <ColumnDefinition Width="100" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{Binding Bezeichnung}" />
                            <TextBlock Grid.Column="1" Text="{Binding Menge}" />
                            <TextBlock Grid.Column="2" Text="{Binding Einzelpreis}" />
                            <TextBlock Grid.Column="3" Text="{Binding RabattProzent}" />
                            <TextBlock Grid.Column="4" Text="{Binding GesamtNetto}" />
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

            <Grid ColumnSpacing="12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" /><ColumnDefinition Width="120" />
                    <ColumnDefinition Width="140" /><ColumnDefinition Width="120" />
                    <ColumnDefinition Width="Auto" /><ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <ComboBox Grid.Column="0" Header="Artikel" HorizontalAlignment="Stretch"
                          ItemsSource="{x:Bind ViewModel.ArtikelLookups, Mode=OneWay}"
                          SelectedValue="{x:Bind ViewModel.PositionArtikelId, Mode=TwoWay}"
                          SelectedValuePath="Id" DisplayMemberPath="Anzeige" />
                <NumberBox Grid.Column="1" Header="Menge" Minimum="0" SpinButtonPlacementMode="Compact"
                           Value="{x:Bind ViewModel.PositionMenge, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" />
                <NumberBox Grid.Column="2" Header="Einzelpreis" Minimum="0" SpinButtonPlacementMode="Compact"
                           Value="{x:Bind ViewModel.PositionEinzelpreis, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" />
                <NumberBox Grid.Column="3" Header="Rabatt%" Minimum="0" Maximum="100" SpinButtonPlacementMode="Compact"
                           Value="{x:Bind ViewModel.PositionRabattProzent, Mode=TwoWay, Converter={StaticResource DecimalToDoubleConverter}}" />
                <Button Grid.Column="4" Content="Preisvorschlag" VerticalAlignment="Bottom" Command="{x:Bind ViewModel.PreisVorschlagCommand}" />
                <Button Grid.Column="5" Content="Hinzufügen" VerticalAlignment="Bottom" Command="{x:Bind ViewModel.PositionHinzufuegenCommand}" />
            </Grid>
            <Button Content="Position entfernen" Command="{x:Bind ViewModel.PositionEntfernenCommand}" />

            <Grid ColumnSpacing="16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" /><ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Header="Kopftext" AcceptsReturn="True" Height="60" Text="{x:Bind ViewModel.Kopftext, Mode=TwoWay}" />
                <TextBox Grid.Column="1" Header="Fußtext" AcceptsReturn="True" Height="60" Text="{x:Bind ViewModel.Fusstext, Mode=TwoWay}" />
            </Grid>

            <StackPanel HorizontalAlignment="Right" Spacing="4">
                <TextBlock Text="{x:Bind ViewModel.SummeNetto, Mode=OneWay}" />
                <TextBlock Text="{x:Bind ViewModel.SummeMwSt, Mode=OneWay}" />
                <TextBlock Text="{x:Bind ViewModel.SummeBrutto, Mode=OneWay}" FontWeight="Bold" />
            </StackPanel>

            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="Speichern" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.SpeichernCommand}" />
                <Button Content="{x:Bind ViewModel.UeberleitenButtonText, Mode=OneWay}" Visibility="{x:Bind ViewModel.ZeigtUeberleitenButton}" Command="{x:Bind ViewModel.UeberleitenCommand}" />
                <Button Content="PDF" Command="{x:Bind ViewModel.PdfCommand}" />
                <Button Content="Abbrechen" Command="{x:Bind ViewModel.AbbrechenCommand}" />
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</Page>
```

`src/Milet.App/Views/Verkauf/AngebotEditPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AngebotEditPage : Page
{
    public AngebotEditViewModel ViewModel { get; }
    public AngebotEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AngebotEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
```

- [ ] **Step 4: `AuftragEditPage`**

`src/Milet.App/Views/Verkauf/AuftragEditPage.xaml` — **identisch** zu Step 3 (Angebot/Auftrag teilen exakt dieselben Felder: kein `Fälligkeit`, kein Buchen-Button, beide zeigen `ZeigtUeberleitenButton`), nur `x:Class="Milet.App.Views.Verkauf.AuftragEditPage"` und `Text="Auftrag"` im Titel-`TextBlock` ändern.

`src/Milet.App/Views/Verkauf/AuftragEditPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AuftragEditPage : Page
{
    public AuftragEditViewModel ViewModel { get; }
    public AuftragEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AuftragEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
```

- [ ] **Step 5: `RechnungEditPage`** — zusätzlich Fälligkeit-Anzeige + Buchen-Button, kein Überleiten-Button

`src/Milet.App/Views/Verkauf/RechnungEditPage.xaml` — Kopie von Step 3 mit `x:Class="Milet.App.Views.Verkauf.RechnungEditPage"`, Titel „Rechnung", und zwei Abweichungen:

1. Im Kopf-`Grid` (3 Spalten) eine vierte Spalte für die Fälligkeit ergänzen — Spaltendefinitionen auf 4× `*` erweitern und nach dem Kunde-`ComboBox` einfügen:
```xml
                <TextBox Grid.Column="3" Header="Fälligkeit" IsReadOnly="True"
                         Text="{x:Bind ViewModel.FormatiereDatum(ViewModel.Faelligkeit), Mode=OneWay}" />
```

2. Im Buttons-`StackPanel` den Überleiten-Button durch den Buchen-Button ersetzen:
```xml
                <Button Content="Speichern" Style="{StaticResource AccentButtonStyle}" Command="{x:Bind ViewModel.SpeichernCommand}" />
                <Button Content="Buchen" Visibility="{x:Bind ViewModel.ZeigtBuchenButton}" Command="{x:Bind ViewModel.BuchenCommand}" />
                <Button Content="PDF" Command="{x:Bind ViewModel.PdfCommand}" />
                <Button Content="Abbrechen" Command="{x:Bind ViewModel.AbbrechenCommand}" />
```

`src/Milet.App/Views/Verkauf/RechnungEditPage.xaml.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class RechnungEditPage : Page
{
    public RechnungEditViewModel ViewModel { get; }
    public RechnungEditPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<RechnungEditViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.OnNavigatedTo(e);
}
```

- [ ] **Step 6: DI-Registrierung**

Modify `src/Milet.App/App.xaml.cs` — nach `builder.Services.AddTransient<RechnungListViewModel>();` (Task 15) einfügen:
```csharp
        builder.Services.AddTransient<AngebotEditViewModel>();
        builder.Services.AddTransient<AuftragEditViewModel>();
        builder.Services.AddTransient<RechnungEditViewModel>();
```

- [ ] **Step 7: Navigation registrieren**

Modify `src/Milet.App/Shell/ShellPage.xaml.cs` — nach `_navigation.Register<RechnungListViewModel, RechnungListPage>();` (Task 15) einfügen:
```csharp
        _navigation.Register<AngebotEditViewModel, AngebotEditPage>();
        _navigation.Register<AuftragEditViewModel, AuftragEditPage>();
        _navigation.Register<RechnungEditViewModel, RechnungEditPage>();
```

- [ ] **Step 8: Build prüfen — jetzt vollständig grün erwartet**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -c Debug -p:Platform=x64`
Expected: 0 Fehler. Falls `Windows.Storage.Pickers`/`WinRT.Interop` in `BelegEditViewModelBase.PdfAsync` (Task 16) nicht auflösbar sind (unpackaged-App-Eigenheit), prüfe anhand des Fehlertexts, ob ein zusätzlicher `using Windows.Storage;`/`using Windows.Storage.Pickers;` fehlt, und ergänze ihn direkt in `BelegEditViewModelBase.cs`.

- [ ] **Step 9: Alle Testprojekte + App-Build final verifizieren**

Run (jedes einzeln, MTP-Modus):
```bash
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj
```
Expected: alle grün (Domain 14, Application 14, IntegrationTests: `NumberRangeServiceTests` wie bisher + `RechnungBuchenServiceTests` [Skip ohne Docker] + `BelegPdfDocumentTests` [3× Passed]).

- [ ] **Step 10: Commit**

```bash
git add src/Milet.App/ViewModels/Verkauf/*EditViewModel.cs src/Milet.App/Views/Verkauf/*EditPage.xaml* src/Milet.App/App.xaml.cs src/Milet.App/Shell/
git commit -m "Verkauf-Editoren: Angebot/Auftrag/Rechnung EditViewModel + EditPage, Navigation komplett"
```

---

### Task 18: Live-UI-Abnahme (Angebot→Auftrag→Rechnung End-to-End) + STATUS.md/PLAN.md-Update

**Files:**
- Modify: `STATUS.md`

**Ziel:** Das Phase-2-Testbar-Kriterium aus PLAN.md verifizieren: „Angebot→Rechnung komplett; PDF-Summen stimmen; Paralleltest: eindeutige RE-Nummern" — per Windows-UIAutomation gegen die laufende App + LocalDB, exakt wie die Phase-1-Abnahme (`STATUS.md` „Phase-1-Abnahme — UI live durchgetestet").

- [ ] **Step 1: App starten**

Run: `taskkill //IM Milet.App.exe //F 2>&1` (falls eine alte Instanz noch läuft), dann Migrator laufen lassen (`"$USERPROFILE/.dotnet/dotnet.exe" run --project src/Milet.Tools.Migrator`), dann App starten:
```powershell
Start-Process -FilePath "D:\Projects\Milet\src\Milet.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Milet.App.exe"
```
Expected: Fenstertitel „Milet Warenwirtschaft" erscheint (`Get-Process Milet.App | Select MainWindowTitle`).

- [ ] **Step 2: Firmenstamm + Testkunde/-artikel sicherstellen**

Per `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Milet -Q "SELECT Firmenname FROM Firmenstamm; SELECT TOP 3 Kundennummer FROM Kunden; SELECT TOP 3 Artikelnummer FROM Artikel" -C` prüfen, dass mindestens 1 Firmenstamm-Zeile, 1 Kunde und 1 Artikel existieren (aus Phase-1-Seed/Tests bereits vorhanden — falls nicht, über die UI kurz einen Testkunden „UIA-Verkaufstest" und einen Testartikel anlegen, analog Phase-1-Abnahme).

- [ ] **Step 3: Angebot anlegen (UIAutomation, Muster wie Lieferanten-CRUD-Test)**

Navigiere `Verkauf → Angebote → Neu`, wähle Kunde, füge über die Artikel-ComboBox + Menge/Einzelpreis + „Hinzufügen" mindestens 2 Positionen hinzu (mind. einmal „Preisvorschlag" klicken um die Preisfindung-Integration zu prüfen), klicke „Speichern". Prüfen:
- Nummer wird nach dem Muster `AN-<Jahr>-0001` vergeben (Format aus Seed, `StammdatenSeed.cs:49`).
- Live-Summen (Netto/MwSt/Brutto) stimmen mit `Menge × Einzelpreis × (1 − Rabatt%)` je Position + 19 %/7 % Gruppierung überein.
- Per `sqlcmd -Q "SELECT BelegNummer, SummeNetto, SummeMwSt, SummeBrutto FROM Belege WHERE BelegTyp='Angebot'" -C` gegen die DB verifizieren.

- [ ] **Step 4: Angebot → Auftrag überleiten**

Klicke „→ Auftrag". Prüfen: neuer Auftrag mit Nummer `AU-<Jahr>-0001`, identische Positionen/Preise (1:1 aus Angebot übernommen, keine Neufindung), Angebot-Status wechselt auf `Erledigt` (per `sqlcmd -Q "SELECT BelegNummer, Status FROM Belege WHERE BelegTyp='Angebot'" -C` prüfen — `Status = 2`).

- [ ] **Step 5: Auftrag → Rechnung überleiten, buchen, PDF**

Klicke „→ Rechnung". Öffne die neue Rechnung (Status `Entwurf`, `BelegNummer` leer/„(wird beim Buchen vergeben)"). Klicke „Buchen": Nummer wird jetzt nach `RE-<Jahr>-0001` vergeben, Fälligkeit erscheint, Status → `Gebucht`. Klicke „PDF", speichere die Datei über den `FileSavePicker`, öffne sie und prüfe visuell: Firmenkopf, Rechnungsnummer, Positionen, Summen, Fälligkeit stimmen mit der UI überein.

Verifizieren per `sqlcmd -Q "SELECT r.BelegNummer, r.Status, r.Faelligkeit, o.Betrag, o.OffenerBetrag FROM Belege r JOIN OffenePosten o ON o.BelegId = r.Id WHERE r.BelegTyp='Rechnung'" -C`: ein `OffenerPosten` mit `Betrag = OffenerBetrag = SummeBrutto` muss existieren.

- [ ] **Step 6: Immutability prüfen**

Versuche, die gebuchte Rechnung erneut zu bearbeiten (`IstBearbeitbar` sollte die Eingabefelder bereits deaktivieren — falls die Seite dennoch einen Speichern-Versuch zulässt, z. B. durch direktes Aufrufen, muss die Fehlermeldung „... ist bereits gebucht und kann nicht mehr geändert werden." erscheinen, kein Absturz).

- [ ] **Step 7: Testdaten aufräumen**

Analog Phase-1-Abnahme: alle in diesem Testlauf angelegten Belege/OffenePosten/Testkunde/-artikel wieder aus der DB entfernen (`DELETE FROM OffenePosten; DELETE FROM BelegSteuerSummen; DELETE FROM BelegPositionen; DELETE FROM Belege; ...` in dieser Reihenfolge wegen FK-Constraints, per `sqlcmd`), App schließen (`taskkill //IM Milet.App.exe //F`).

- [ ] **Step 8: `STATUS.md` aktualisieren**

Modify `STATUS.md`:
- Abschnitt „## Erledigt" um einen neuen Unterabschnitt „### Phase 2 — Verkauf+PDF ✅" ergänzen, der die Tasks 1–18 dieses Plans zusammenfasst (Beleg-TPH-Modell, Buchungspipeline, Überleitung, QuestPDF, UI) sowie die konkreten Befunde aus Schritt 3–6 (Nummernformate, Immutability-Verhalten, evtl. gefundene und gefixte Bugs).
- Abschnitt „## Offen" Punkt 1 (Phasen 2–7) auf „Phasen 3–7" reduzieren, Phase 2 dort entfernen.
- Falls in Schritt 1–7 Bugs gefunden+gefixt wurden (wie bei der Phase-1-Abnahme mit dem Staffelpreis-Absturz), diese unter „## Gefixt während UI-Test" dokumentieren.

- [ ] **Step 9: Commit**

```bash
git add STATUS.md
git commit -m "Phase 2 (Verkauf+PDF) live abgenommen: Angebot->Auftrag->Rechnung End-to-End, PDF, Immutability"
```

---

## Self-Review (Spec-Abdeckung gegen PLAN.md §„2 Verkauf+PDF")

| PLAN.md-Anforderung | Task |
|---|---|
| Beleg-TPH-Modell | Task 1 |
| Belegeditor (Kopf+Positionsgrid+Artikel-Lookup+Live-Summen) | Task 16/17 |
| Angebot/Auftrag/Rechnung (direkt, ohne Lager) | Task 1, 8, 15–17 |
| Buchungspipeline (Immutability, atomare RE-Nummer, OP-Anlage) | Task 7, 11 |
| Überleitung | Task 10 (Service), Task 16/17 (UI-Buttons) |
| QuestPDF (Briefkopf + 3 Dokumente) | Task 3, 13 |
| Testbar: „Angebot→Rechnung komplett" | Task 18 Step 3–5 |
| Testbar: „PDF-Summen stimmen" | Task 13 Step 4 (Smoke) + Task 18 Step 5 (visuell) |
| Testbar: „Paralleltest: eindeutige RE-Nummern" | Task 12 |
| Verifikation §Unit: „Offene-Mengen-Berechnung" | Task 2 |
| Verifikation §Integration: „Buchungstransaktionen atomar" | Task 11 (eine Transaktion), Task 12 (Immutability-Test) |
| Verifikation §PDF: „Render-Smoke + Summen-Assertions" | Task 13 |

