# Phase 4 „Einkauf" Implementation Plan

> **Hinweis für die Umsetzung:** Dieser Plan wird task-für-task abgearbeitet. Jeder Task hat Checkboxen (`- [ ]`) für seine Schritte; nach jedem Task wird gebaut/getestet und einzeln committet (wie in Phase 2/3). Keine superpowers-Skills in dieser Umgebung nötig — reine Abarbeitung von oben nach unten, jeder Task ist in sich abgeschlossen und kompilierbar (mit den explizit dokumentierten Ausnahmen, wo ein Folge-Task denselben Build repariert).

**Goal:** Bestellung→Wareneingang→Eingangsrechnung als drei neue `Beleg`-TPH-Subtypen (Lieferanten-basiert statt Kunden-basiert), Bestellvorschlag anhand Mindestbestand, Wareneingang bucht positiven Lagerzugang inkl. Neuanlage von Seriennummern, Eingangsrechnung legt einen Kreditor-Offenen-Posten an und meldet eine Betragsabweichung zum Wareneingang als Soft-Warnung (kein Blocker) — Bestellvorschlag→Bestellung→Wareneingang(Teilmenge)→Eingangsrechnung End-to-End im UI durchklickbar, EK-Roundtrip erhöht den Bestand nachweisbar, Kreditor-OP entsteht beim Buchen.

**Architektur:** Bestellung/Wareneingang/Eingangsrechnung werden dünne `Beleg`-TPH-Subtypen (wie `Angebot`/`Auftrag`/`Rechnung`/`Lieferschein` aus Phase 2/3) — sie nutzen dieselbe `Belege`/`BelegPositionen`-Tabelle, denselben Nummernkreis-Mechanismus, denselben `BelegImmutabilityInterceptor` (GoBD) und dasselbe `UrsprungsPositionId`/`OffeneMenge`-Muster für Teil-Wareneingänge. Der entscheidende Unterschied zu allen bisherigen Belegtypen: Sie hängen an einem **Lieferanten**, nicht an einem Kunden. `Beleg` bekommt dafür eine echte Partei-Erweiterung (`KundeId` wird nullable, `LieferantId` kommt neu dazu, DB-CHECK-Constraint erzwingt „genau eines von beiden"). Dieser Schritt ist keine Design-Neuerfindung, sondern die konsequente Umsetzung dessen, was PLAN.md unter „Beleg (Kopf)" von Anfang an vorsah („KundeId?/LieferantId? (Check-Constraint)") und was in Phase 2 aus Zeitgründen auf den reinen Verkaufsfall verengt implementiert wurde (`KundeId` als `NOT NULL int`, kein `LieferantId`) — siehe „Architektur-Entscheidungen" unten für die genaue Begründung und den Umsetzungsschnitt. Die Bestandsbuchung beim Wareneingang läuft über denselben, unverändert wiederverwendeten `BestandService.BucheBewegungAsync` wie Lieferschein/Inventur — nur mit positivem statt negativem Delta und dem neuen `LagerbewegungTyp.Wareneingang`. Die Überleitungskette Bestellung→Wareneingang→Eingangsrechnung wird **in den bestehenden, generisch gebauten `BelegUeberleitungService` integriert** (Übergänge/Switch-Tabellen erweitert), nicht als Parallelstruktur dupliziert — der Code dort ist bereits Beleg-typ-agnostisch bis auf die Party-Zuweisung. Die WinUI-Editoren bekommen dagegen bewusst **keine** Erweiterung von `BelegEditViewModelBase` (das ist eng an `KundeId`+Preisfindung gekoppelt), sondern eine neue, strukturell parallele `EinkaufBelegEditViewModelBase` — das minimiert das Regressionsrisiko für die bereits abgenommenen Verkaufs-Editoren.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server/LocalDB), FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, xUnit v3, Testcontainers.MsSql. Kein neues NuGet-Paket nötig (alles bereits aus Phase 0–3 vorhanden).

**Spec:** `PLAN.md` (Abschnitte „Datenmodell (Kern)" → Beleg-Pattern, „Geschäftsprozesse" Punkt 6 „Bestellung→Wareneingang→Eingangsrechnung", Phasen-Tabelle Zeile „4 Einkauf"). Konventionen recherchiert aus bestehendem Phase-1/2/3-Code (`BelegService`, `BelegUeberleitungService`, `BestandService`, `LieferscheinBuchenService`, `RechnungBuchenService`, `KleinstammServices`, `BelegEditViewModelBase`, `LieferscheinEditViewModel`) — jede Abweichung davon ist unten explizit begründet.

## Architektur-Entscheidungen (neu in Phase 4) — offene Fragen mit konkreter Lösung

Diese sieben Punkte wurden beim Erkunden des bestehenden Codes als echte Lücken/Weichenstellungen identifiziert (nicht nur theoretisch aus PLAN.md abgeleitet). Jeder Punkt hat eine konkrete, im Plan umgesetzte Lösung — kein „TBD".

1. **`Beleg.KundeId` ist aktuell `NOT NULL int`, kein `LieferantId`-Feld existiert.** PLAN.md sah „KundeId?/LieferantId? (Check-Constraint)" vor; Phase 2 hat das für den reinen Verkaufsfall auf `KundeId int` verengt (nachvollziehbar, damals gab es nur Verkaufsbelege). Für Einkauf ist das ein Blocker. **Lösung:** `Beleg.KundeId` → `int?`, neues `Beleg.LieferantId` (`int?`) + Navigation, DB-CHECK-Constraint `(KundeId IS NOT NULL AND LieferantId IS NULL) OR (KundeId IS NULL AND LieferantId IS NOT NULL)`. Die Migration ist für den Bestand rückwärtskompatibel: alle existierenden Belege haben `KundeId` gesetzt und `LieferantId` NULL, erfüllen den Constraint also automatisch, keine Datenbereinigung nötig. `BelegDto.KundeId` bleibt bewusst `int` (nicht nullable, Default 0) — dadurch bleiben `BelegEditViewModelBase` und alle drei Verkaufs-Edit-ViewModels **unverändert kompilierbar**, nur `VerkaufMapping.ToDto` bekommt ein `?? 0`. Siehe Task 1, 3, 6.
2. **`BelegEditViewModelBase` (WinUI) ist eng an `KundeId` + `IVerkaufLookupService.ErmittlePreisAsync(..., kundeId)` (Preisfindung/Staffelpreise) gekoppelt.** Eine Generalisierung auf Kunde-oder-Lieferant hätte reales Regressionsrisiko für die bereits live abgenommenen Verkaufs-Editoren gebracht, für einen Nutzen, den nur drei neue, fachlich andere Editoren hätten (kein Preisfindungs-Algorithmus im Einkauf, EK-Preis kommt direkt aus `Artikel.Einkaufspreis`). **Lösung:** neue, separate `EinkaufBelegEditViewModelBase` (Task 11) — strukturell parallel, aber ohne Preisfindungs-Aufruf und mit `LieferantId` statt `KundeId`. Kein Code-Pfad der Verkaufs-Editoren wird angefasst.
3. **Kein „Hauptlieferant" am Artikel** — `Artikel` hat `Einkaufspreis`/`Mindestbestand`, aber keine FK zu `Lieferant`. Ein automatisch nach Lieferant gruppierter Bestellvorschlag ist damit in v1 nicht möglich. **Lösung (v1, pragmatisch):** `BestellVorschlagService` liefert eine flache Liste aller Artikel unter Mindestbestand (artikel-, nicht lieferantenbezogen). Die UI (`BestellVorschlagPage`) lässt den Nutzer **einen** Lieferanten auswählen und daraus per Checkbox-Auswahl + editierbarer Menge **eine** Bestellung erzeugen — deckt den in der Praxis häufigsten Fall (Sammelbestellung bei einem Hauptlieferanten) ab, ohne Datenmodell-Änderung. **Folgearbeit (explizit nicht Teil dieses Plans):** `Artikel.HauptlieferantId int?` (FK zu `Lieferant`) in einer späteren Phase ergänzen, dann automatische Gruppierung im Vorschlag.
4. **Nummernkreis-Seed-Lücke bei bereits migrierter DB** (bekanntes Risiko aus STATUS.md „Bekannte Risiken"): `StammdatenSeed.ApplyAsync` legt Nummernkreise bisher nur an, wenn die ganze Tabelle leer ist (`if (!await db.Nummernkreise.AnyAsync(ct))`), nicht je fehlendem Code. Phase 4 braucht zwei neue Codes (`WE`, `ER` — `BE` für Bestellung existiert bereits, vorausschauend in Phase 2 angelegt), die auf einer bereits migrierten Entwicklungs-DB sonst nie automatisch nachgetragen würden. **Lösung:** der Nummernkreis-Seed-Block wird in Task 5 von „nur wenn Tabelle leer" auf „je fehlendem Code einzeln ergänzen" umgestellt (nicht-destruktiv: vorhandene Zeilen werden nie verändert, nur fehlende Codes werden hinzugefügt) — behebt das Risiko dauerhaft, nicht nur für diese Phase.
5. **Adress-Snapshot-Semantik ist bei Einkaufsbelegen invertiert.** `Beleg.RechnungsadresseSnapshot`/`LieferadresseSnapshot` wurden für Verkauf entworfen (wir versenden an den Kunden). Bei einer Bestellung ist es umgekehrt: die Ware soll an **unsere eigene** Adresse geliefert werden, nicht an den Lieferanten. **Lösung (Task 6):** bei Einkaufsbelegen wird `RechnungsadresseSnapshot` = Anschrift des Lieferanten (Partner-Anschrift für den Druck) und `LieferadresseSnapshot` = `Firmenstamm.Adresse` (eigene Firma, „wohin geht die Ware") gesetzt. Beide Owned-Type-Felder bleiben `IsRequired()`, es ändert sich nur, welche Adresse hineingeschrieben wird — kein Schema-Eingriff nötig.
6. **Eingangsrechnung bekommt ihre Belegnummer sofort beim Speichern, nicht erst beim Buchen** (anders als `Rechnung`, wo genau das aus GoBD-Gründen — lückenlose Sequenz ausgehender Rechnungen, §14 UStG — bewusst verzögert wird). **Begründung:** Die GoBD-relevante, lückenlose Nummer ist bei einer Eingangsrechnung die **Rechnungsnummer des Lieferanten**, nicht unsere interne `ER-2026-0001`-Referenz — für die gibt es keine gesetzliche Lückenlosigkeitspflicht. Die Lieferantenrechnungsnummer wird deshalb zusätzlich in einem neuen generischen Feld `Beleg.ExterneReferenz` (string?, nullable) erfasst, das nur auf der Eingangsrechnung-Edit-Seite sichtbar ist (Task 1, 11, 13).
7. **Betrags-Abweichungsvergleich bei Eingangsrechnung geht gegen den Wareneingang, nicht gegen die ursprüngliche Bestellung.** Ein Vergleich gegen die Bestellung würde einen 2-Hop-`UrsprungsPositionId`-Rücklauf (ER-Position → WE-Position → Bestellungs-Position) erfordern. **Lösung (v1, pragmatisch und fachlich sinnvoll — „Drei-Wege-Abgleich" light):** Vergleich der `SummeBrutto` der Eingangsrechnung gegen die `SummeBrutto` des einen Wareneingangs, aus dem sie per Überleitung entstanden ist (1 Hop, `UrsprungsPositionId` → `BelegPosition.BelegId`) — entspricht dem in der Praxis wichtigsten Check „stimmt die Rechnung mit dem überein, was wir zu EK-Preisen eingebucht haben". Sammel-Eingangsrechnungen aus mehreren Wareneingängen sind explizit **nicht** Teil von v1 (wie Sammelrechnung bei Verkauf strukturell möglich wäre, hier aber nicht gefordert/gebaut — Testkriterium in PLAN.md verlangt nur den einfachen Roundtrip).

## Global Constraints

- Neue Aggregate Roots: keine (Bestellung/Wareneingang/Eingangsrechnung sind `Beleg`-Subtypen, `Beleg` ist bereits `AuditableEntity`+`IHasRowVersion`). Modifizierte Aggregate Roots: `Beleg`, `OffenerPosten` (beide bereits `AuditableEntity`+`IHasRowVersion`, bleibt so).
- Jede Service-Methode öffnet einen eigenen `IDbContextFactory<MiletDbContext>`-Context; Reads `AsNoTracking()`; Speichern nutzt `SaveChangesTranslatingConcurrencyAsync`/`SaveChangesDeletingAsync` wo Concurrency/FK-Konflikte auftreten können — exakt wie Phase 1–3.
- DTOs: `sealed record` mit `init`-Properties bzw. positional record für reine Read-DTOs (Muster aus Phase 2/3). Alle neuen Einkauf-DTOs in einer `Dtos.cs`, alle Interfaces in einer `IEinkaufServices.cs` — wie `Lager`-Modul in Phase 3.
- Decimal-Präzisionen (verbindlich, unverändert aus Phase 1–3): `Menge`-Felder `decimal(18,3)`, Geldbeträge `decimal(18,2)` (Ausnahme `Einkaufspreis` bleibt `decimal(18,4)` wie in Phase 1 festgelegt). Rundung: `Math.Round(..., 2, MidpointRounding.ToEven)`, wie in `SteuerRechner`.
- Bestellung-Nummer wird **beim ersten Speichern** vergeben (wie Angebot/Auftrag/Lieferschein). Wareneingang-Nummer ebenfalls beim ersten Speichern. Eingangsrechnung-Nummer **ebenfalls** beim ersten Speichern (anders als `Rechnung` — siehe Architektur-Entscheidung 6). Nummernkreis-Codes: `BE` (existiert bereits), `WE` (neu), `ER` (neu).
- `BelegImmutabilityInterceptor` (GoBD-Sperre gebuchter Belege) greift automatisch für alle drei neuen Subtypen — er arbeitet auf `EntityEntry<Beleg>`, keine Änderung am Interceptor nötig.
- Bestandszugang beim Wareneingang läuft über den unveränderten `BestandService.BucheBewegungAsync` (positives Delta, kein Negativsperre-Risiko) — dieselbe Methode wie bei Lieferschein-Abgang/Inventur-Korrektur, keine Code-Änderung an `BestandService.cs` nötig.
- Betrags-Abweichung bei Eingangsrechnung ist **Soft-Warnung**: das Buchen (inkl. Kreditor-OP-Anlage) läuft immer durch; die Abweichung kommt als DTO-Flag im Rückgabewert zurück, niemals als Exception. Negativbestand-Sperre (Lieferschein) und Betrags-Abweichung (Eingangsrechnung) sind bewusst unterschiedlich streng — nicht verwechseln (bereits so in Phase-3-Plan dokumentiert).
- `dotnet` explizit über `%USERPROFILE%\.dotnet\dotnet.exe` aufrufen. Jedes Testprojekt einzeln ausführen (MTP-Modus). Migrationen ausschließlich über `Milet.Tools.Migrator` anwenden.
- Deutsche Bezeichner für alles Fachliche, englische für rein technische Infrastruktur — wie bisher.
- **Bewusst außerhalb dieses Plans (spätere Phase):** Bestellung/Wareneingang/Eingangsrechnung-PDF (QuestPDF-Erweiterung, analog zur in Phase 3 verschobenen Lieferschein-PDF), Storno/Rückabwicklung einer fehlerhaft gebuchten Eingangsrechnung (nur Neuanlage einer Korrektur/Gutschrift, keine automatisierte Gegenbuchung), Sammel-Eingangsrechnung aus mehreren Wareneingängen, `Artikel.HauptlieferantId` (siehe Architektur-Entscheidung 3), `Lagerbewegung.BenutzerId`-Befüllung (bekannte offene Lücke aus Phase 3, betrifft auch neue Wareneingangs-Buchungen — bleibt NULL bis ein Login/Current-User-Service existiert, Phase 7).

---

### Task 1: Domain — Beleg-Erweiterung um Partei-Typ (Kunde/Lieferant) + OffenerPosten erweitern

**Files:**
- Modify: `src/Milet.Domain/Entities/Verkauf/Beleg.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/BelegTypErweiterung.cs`
- Modify: `src/Milet.Domain/Entities/Finanzen/OffenerPosten.cs`

**Interfaces:**
- Consumes: `Domain.Entities.Stammdaten.Lieferant` (Phase 1), `BelegTyp` (Task 2 erweitert es weiter, hier nur die Helper-Klasse für die bereits existierenden Werte + Vorgriff auf die neuen).
- Produces: `Beleg.KundeId` (`int?`), `Beleg.LieferantId`, `Beleg.ExterneReferenz`, `BelegTypErweiterung.IstEinkaufsBeleg(this BelegTyp)`, `OffenerPosten.KundeId`/`LieferantId` — von Task 3, 5, 6, 7, 10, 16 konsumiert.

- [ ] **Step 1: `Beleg.cs` — Partei-Erweiterung**

Modify `src/Milet.Domain/Entities/Verkauf/Beleg.cs` — vollständiger neuer Inhalt:
```csharp
using Milet.Domain.Common;
using Milet.Domain.ValueObjects;

namespace Milet.Domain.Entities.Verkauf;

public abstract class Beleg : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Leer bei Entwurf einer Rechnung — erst beim Buchen atomar vergeben. Bei allen anderen Belegtypen
    /// (inkl. Eingangsrechnung, siehe Architektur-Plan Phase 4) beim ersten Speichern vergeben.</summary>
    public string BelegNummer { get; set; } = string.Empty;

    public DateOnly BelegDatum { get; set; }

    /// <summary>Genau eines von KundeId/LieferantId ist gesetzt (DB-CHECK-Constraint, siehe BelegConfiguration) —
    /// abhängig vom Belegtyp: Verkaufsbelege (Angebot/Auftrag/Rechnung/Lieferschein) tragen KundeId,
    /// Einkaufsbelege (Bestellung/Wareneingang/Eingangsrechnung) tragen LieferantId.
    /// Siehe BelegTypErweiterung.IstEinkaufsBeleg.</summary>
    public int? KundeId { get; set; }
    public Domain.Entities.Stammdaten.Kunde? Kunde { get; set; }

    public int? LieferantId { get; set; }
    public Domain.Entities.Stammdaten.Lieferant? Lieferant { get; set; }

    /// <summary>Eingefroren bei Erstellung — spätere Adressänderungen wirken nicht rückwirkend.
    /// Bei Einkaufsbelegen invertierte Semantik: RechnungsadresseSnapshot = Anschrift des Lieferanten
    /// (Geschäftspartner-Anschrift für den Druck), LieferadresseSnapshot = eigene Firma (wohin die Ware geht).</summary>
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

    /// <summary>Nur Rechnung: gesetzt beim Buchen (BelegDatum + ZahlungsbedingungZielTage). Bei Eingangsrechnung
    /// ebenfalls beim Buchen gesetzt (für die Fälligkeitsberechnung des Kreditor-OP), s. EingangsrechnungBuchenService.</summary>
    public DateOnly? Faelligkeit { get; set; }

    public DateOnly? Leistungsdatum { get; set; }

    public string? Kopftext { get; set; }
    public string? Fusstext { get; set; }

    /// <summary>Nur Eingangsrechnung: die Rechnungsnummer des Lieferanten. Die eigene BelegNummer (Nummernkreis
    /// "ER-...") ist nur eine interne Referenz ohne GoBD-Lückenlosigkeitspflicht — GoBD-relevant ist das
    /// Originaldokument des Lieferanten, dessen Nummer hier zusätzlich erfasst wird.</summary>
    public string? ExterneReferenz { get; set; }

    public List<BelegPosition> Positionen { get; set; } = [];
    public List<BelegSteuerSumme> Steuersummen { get; set; } = [];

    public byte[] RowVersion { get; set; } = [];
}
```

- [ ] **Step 2: `BelegTypErweiterung` — Helper zur Unterscheidung Verkauf/Einkauf**

`src/Milet.Domain/Entities/Verkauf/BelegTypErweiterung.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

/// <summary>Unterscheidet Verkaufs- von Einkaufsbelegen — bestimmt, ob ein Beleg über Kunde oder Lieferant
/// läuft (siehe Beleg.KundeId/LieferantId) und ob beim Buchen ein Debitor- oder Kreditor-OP entsteht.</summary>
public static class BelegTypErweiterung
{
    private static readonly HashSet<BelegTyp> EinkaufsTypen =
        [BelegTyp.Bestellung, BelegTyp.Wareneingang, BelegTyp.Eingangsrechnung];

    public static bool IstEinkaufsBeleg(this BelegTyp typ) => EinkaufsTypen.Contains(typ);
}
```

- [ ] **Step 3: `OffenerPosten.cs` — Partei-Erweiterung**

Modify `src/Milet.Domain/Entities/Finanzen/OffenerPosten.cs` — vollständiger neuer Inhalt:
```csharp
using Milet.Domain.Common;

namespace Milet.Domain.Entities.Finanzen;

public class OffenerPosten : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public int BelegId { get; set; }
    public Entities.Verkauf.Beleg? Beleg { get; set; }

    /// <summary>Genau eines gesetzt, je Typ — analog Beleg.KundeId/LieferantId.</summary>
    public int? KundeId { get; set; }
    public int? LieferantId { get; set; }

    public OffenerPostenTyp Typ { get; set; } = OffenerPostenTyp.Debitor;
    public decimal Betrag { get; set; }
    public decimal OffenerBetrag { get; set; }
    public DateOnly Faelligkeit { get; set; }
    public int Mahnstufe { get; set; }
    public bool Mahnsperre { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
```

**Hinweis:** `RechnungBuchenService.cs` bleibt unverändert kompilierbar — die Zeile `KundeId = rechnung.KundeId,` weist weiterhin `int?` auf `int?` zu (vorher `int` auf `int`), keine Codeänderung nötig.

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Entities/Verkauf/Beleg.cs src/Milet.Domain/Entities/Verkauf/BelegTypErweiterung.cs src/Milet.Domain/Entities/Finanzen/OffenerPosten.cs
git commit -m "Beleg/OffenerPosten um Lieferant-Partei erweitern (KundeId nullable, LieferantId, ExterneReferenz)"
```

---

### Task 2: Domain — Bestellung/Wareneingang/Eingangsrechnung als Beleg-Subtypen + BelegTyp/LagerbewegungTyp erweitern

**Files:**
- Modify: `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Bestellung.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Wareneingang.cs`
- Create: `src/Milet.Domain/Entities/Verkauf/Eingangsrechnung.cs`
- Modify: `src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs`

**Interfaces:**
- Consumes: `Beleg` (Task 1).
- Produces: `BelegTyp.Bestellung/.Wareneingang/.Eingangsrechnung`, `Bestellung : Beleg`, `Wareneingang : Beleg`, `Eingangsrechnung : Beleg`, `LagerbewegungTyp.Wareneingang` — von Task 5 (TPH-Discriminator, DbSets), Task 6/7 (Switches), Task 9 (`WareneingangBuchenService`), Task 11–13 (UI) konsumiert.

- [ ] **Step 1: `BelegTyp` erweitern**

Modify `src/Milet.Domain/Entities/Verkauf/BelegTyp.cs` — vollständiger neuer Inhalt:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public enum BelegTyp
{
    Angebot = 0,
    Auftrag = 1,
    Rechnung = 2,
    Lieferschein = 3,
    Bestellung = 4,
    Wareneingang = 5,
    Eingangsrechnung = 6,
}
```

- [ ] **Step 2: die drei neuen dünnen Beleg-Subklassen**

`src/Milet.Domain/Entities/Verkauf/Bestellung.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Bestellung : Beleg;
```

`src/Milet.Domain/Entities/Verkauf/Wareneingang.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Wareneingang : Beleg;
```

`src/Milet.Domain/Entities/Verkauf/Eingangsrechnung.cs`:
```csharp
namespace Milet.Domain.Entities.Verkauf;

public sealed class Eingangsrechnung : Beleg;
```

- [ ] **Step 3: `LagerbewegungTyp.Wareneingang` ergänzen**

Modify `src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs` — vollständiger neuer Inhalt:
```csharp
namespace Milet.Domain.Entities.Lager;

public enum LagerbewegungTyp
{
    Korrektur = 0,
    Lieferung = 1,
    InventurKorrektur = 2,

    /// <summary>Positiver Zugang durch Wareneingang aus einer Bestellung (Phase 4).</summary>
    Wareneingang = 3,
}
```

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Domain/Milet.Domain.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Domain/Entities/Verkauf/BelegTyp.cs src/Milet.Domain/Entities/Verkauf/Bestellung.cs src/Milet.Domain/Entities/Verkauf/Wareneingang.cs src/Milet.Domain/Entities/Verkauf/Eingangsrechnung.cs src/Milet.Domain/Entities/Lager/LagerbewegungTyp.cs
git commit -m "Bestellung/Wareneingang/Eingangsrechnung als Beleg-TPH-Subtypen + LagerbewegungTyp.Wareneingang"
```

---

### Task 3: Application — Verkauf/Dtos.cs + Validators.cs um Lieferant-Partei erweitern

**Files:**
- Modify: `src/Milet.Application/Verkauf/Dtos.cs`
- Modify: `src/Milet.Application/Verkauf/Validators.cs`
- Modify: `tests/Milet.Application.Tests/VerkaufValidatorTests.cs`

**Interfaces:**
- Consumes: `BelegTypErweiterung.IstEinkaufsBeleg` (Task 1).
- Produces: `BelegDto.LieferantId`/`.LieferantAnzeige`/`.ExterneReferenz`, `BelegValidator` mit Kunde/Lieferant-Bedingung — von Task 6 (`VerkaufMapping`/`BelegService`), Task 11–13 (UI) konsumiert.

- [ ] **Step 1: `BelegDto` erweitern**

Modify `src/Milet.Application/Verkauf/Dtos.cs` — in `BelegDto`, nach `public string KundeAnzeige { get; init; } = string.Empty;` einfügen:
```csharp
    public int? LieferantId { get; init; }
    public string LieferantAnzeige { get; init; } = string.Empty;
```
und nach `public string? Fusstext { get; init; }` einfügen:
```csharp
    public string? ExterneReferenz { get; init; }
```

- [ ] **Step 2: `BelegValidator` — Kunde/Lieferant bedingt**

Modify `src/Milet.Application/Verkauf/Validators.cs` — `BelegValidator` ersetzen durch:
```csharp
using FluentValidation;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Application.Verkauf;

public sealed class BelegPositionValidator : AbstractValidator<BelegPositionDto>
{
    public BelegPositionValidator()
    {
        RuleFor(p => p.Menge).GreaterThan(0);
        RuleFor(p => p.Einzelpreis).GreaterThanOrEqualTo(0);
        RuleFor(p => p.RabattProzent).InclusiveBetween(0, 100);
        RuleFor(p => p.Bezeichnung).NotEmpty().MaximumLength(200);
        RuleFor(p => p.ArtikelId).NotNull().When(p => p.PositionsTyp == PositionsTyp.Artikel);
    }
}

public sealed class BelegValidator : AbstractValidator<BelegDto>
{
    public BelegValidator()
    {
        RuleFor(b => b.KundeId).GreaterThan(0).WithMessage("Kunde ist erforderlich.")
            .When(b => !b.BelegTyp.IstEinkaufsBeleg());
        RuleFor(b => b.LieferantId).NotNull().GreaterThan(0).WithMessage("Lieferant ist erforderlich.")
            .When(b => b.BelegTyp.IstEinkaufsBeleg());
        RuleFor(b => b.BelegDatum).NotEqual(default(DateOnly));
        RuleFor(b => b.Positionen).NotEmpty().WithMessage("Beleg muss mindestens eine Position enthalten.");
        RuleForEach(b => b.Positionen).SetValidator(new BelegPositionValidator());
    }
}
```
(`using Milet.Domain.Entities.Verkauf;` ist neu — für die Extension-Methode `IstEinkaufsBeleg()`. Der `RuleFor(b => b.LieferantId).NotNull().GreaterThan(0)`-Aufruf funktioniert mit FluentValidation auf `int?`-Properties direkt — `GreaterThan` behandelt `null` bereits als „nicht erfüllt", `NotNull()` davor macht die Absicht nur expliziter/lesbarer.)

- [ ] **Step 3: bestehende Validator-Tests um Einkaufsfall ergänzen**

Modify `tests/Milet.Application.Tests/VerkaufValidatorTests.cs` — am Ende der Klasse (vor der schließenden `}`) einfügen:
```csharp
    [Fact]
    public void Beleg_EinkaufsTyp_OhneLieferant_Fehler()
    {
        var dto = new BelegDto { BelegTyp = BelegTyp.Bestellung, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.False(ergebnis.IsValid);
    }

    [Fact]
    public void Beleg_EinkaufsTyp_MitLieferant_KundeNichtErforderlich()
    {
        var dto = new BelegDto { BelegTyp = BelegTyp.Bestellung, KundeId = 0, LieferantId = 3, Positionen = [GueltigePosition()] };
        var ergebnis = new BelegValidator().Validate(dto);
        Assert.True(ergebnis.IsValid);
    }
```

- [ ] **Step 4: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj`
Expected: alle PASS (bestehende 19 + 2 neue = 21).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Application/Verkauf/Dtos.cs src/Milet.Application/Verkauf/Validators.cs tests/Milet.Application.Tests/VerkaufValidatorTests.cs
git commit -m "BelegDto/BelegValidator um Lieferant-Partei erweitern (Kunde XOR Lieferant je Belegtyp)"
```

---

### Task 4: Application — Einkauf-Modul: DTOs + Service-Interfaces

**Files:**
- Create: `src/Milet.Application/Einkauf/Dtos.cs`
- Create: `src/Milet.Application/Einkauf/IEinkaufServices.cs`

**Interfaces:**
- Consumes: `Verkauf.BelegDto` (Task 3, via impliziter Namespace-Auflösung `Milet.Application.Verkauf` als Geschwister-Namespace — Muster bereits im Code, z. B. `BelegPosition.cs`'s `Domain.Entities.Lager.Lagerort`).
- Produces: `LieferantEinkaufLookupDto`, `ArtikelEinkaufLookupDto`, `EinkaufLookups`, `BestellVorschlagPositionDto`, `EingangsrechnungBuchenErgebnisDto`, `IEinkaufLookupService`, `IBestellVorschlagService`, `IWareneingangBuchenService`, `IEingangsrechnungBuchenService` — von Task 8–10 (Infrastructure) implementiert, von Task 11–14 (App) konsumiert.

- [ ] **Step 1: DTOs**

`src/Milet.Application/Einkauf/Dtos.cs`:
```csharp
namespace Milet.Application.Einkauf;

public sealed record LieferantEinkaufLookupDto(int Id, string Anzeige, int? ZahlungsbedingungId);

public sealed record ArtikelEinkaufLookupDto(
    int Id,
    string Anzeige,
    string Bezeichnung,
    decimal Einkaufspreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel,
    bool HatSeriennummern);

public sealed record EinkaufLookups(
    IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten,
    IReadOnlyList<ArtikelEinkaufLookupDto> Artikel);

/// <summary>Ein lagerfähiger, nicht gesperrter Artikel mit Mindestbestand, dessen Gesamtbestand
/// (über alle Lagerorte) den Mindestbestand unterschreitet.</summary>
public sealed record BestellVorschlagPositionDto(
    int ArtikelId,
    string Artikelnummer,
    string Bezeichnung,
    decimal AktuellerBestand,
    decimal Mindestbestand,
    decimal VorschlagsMenge,
    decimal Einkaufspreis,
    int MwStSatzId,
    decimal MwStSatzWert,
    int? SteuerSchluessel,
    string? EinheitKuerzel);

/// <summary>Ergebnis des Eingangsrechnung-Buchens: der Kreditor-OP wird IMMER angelegt (kein Blocker);
/// BetragWeichtAb ist eine reine Soft-Warnung für die UI (siehe Architektur-Entscheidung 7).</summary>
public sealed record EingangsrechnungBuchenErgebnisDto(
    Verkauf.BelegDto Beleg,
    bool BetragWeichtAb,
    decimal ErwarteterBetrag,
    decimal AbweichungBetrag);
```

- [ ] **Step 2: Service-Interfaces**

`src/Milet.Application/Einkauf/IEinkaufServices.cs`:
```csharp
namespace Milet.Application.Einkauf;

public interface IEinkaufLookupService
{
    Task<EinkaufLookups> LadeLookupsAsync(CancellationToken ct = default);
}

public interface IBestellVorschlagService
{
    /// <summary>Alle lagerfähigen, nicht gesperrten Artikel mit gesetztem Mindestbestand, deren aktueller
    /// Gesamtbestand (über alle Lagerorte) den Mindestbestand unterschreitet.</summary>
    Task<IReadOnlyList<BestellVorschlagPositionDto>> ErmittleVorschlaegeAsync(CancellationToken ct = default);
}

public interface IWareneingangBuchenService
{
    /// <summary>Bucht: positive Lagerbewegung je Artikelposition (BestandService.BucheBewegungAsync), legt bei
    /// serialisierten Artikeln neue Seriennummern an, setzt Status Gebucht — eine Transaktion.</summary>
    Task<Verkauf.BelegDto> BuchenAsync(
        int wareneingangId, IReadOnlyDictionary<int, IReadOnlyList<string>> neueSeriennummernJePosition, CancellationToken ct = default);
}

public interface IEingangsrechnungBuchenService
{
    /// <summary>Legt einen Kreditor-Offenen-Posten an; vergleicht die Rechnungssumme mit der Summe des
    /// ursprünglichen Wareneingangs und meldet eine Abweichung als Soft-Warnung im Ergebnis (kein Blocker,
    /// der OP entsteht in jedem Fall).</summary>
    Task<EingangsrechnungBuchenErgebnisDto> BuchenAsync(int eingangsrechnungId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Application/Milet.Application.csproj`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Application/Einkauf/
git commit -m "Application-Modul Einkauf: DTOs + Service-Interfaces (Lookup, Bestellvorschlag, Wareneingang-/Eingangsrechnung-Buchen)"
```

---

### Task 5: Infrastructure — EF-Configurations + DbContext + Nummernkreis-Seed-Fix

**Files:**
- Modify: `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Configurations/OffenerPostenConfiguration.cs`
- Modify: `src/Milet.Infrastructure/Persistence/MiletDbContext.cs`
- Modify: `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs`

**Interfaces:**
- Consumes: `Bestellung`/`Wareneingang`/`Eingangsrechnung` (Task 2), `Beleg.LieferantId`/`OffenerPosten.LieferantId` (Task 1).
- Produces: `MiletDbContext.Bestellungen`/`.Wareneingaenge`/`.Eingangsrechnungen` DbSets, TPH-Discriminator-Einträge, CHECK-Constraints, Nummernkreis-Codes `WE`/`ER` + idempotente Seed-Logik — von Task 6/7 (Services) und der Migration in Task 7 konsumiert.

- [ ] **Step 1: `BelegConfiguration` — Lieferant-FK, CHECK-Constraint, Discriminator, ExterneReferenz**

Modify `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs` — vollständiger neuer Inhalt:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegConfiguration : IEntityTypeConfiguration<Beleg>
{
    public void Configure(EntityTypeBuilder<Beleg> b)
    {
        b.ToTable("Belege", t => t.HasCheckConstraint(
            "CK_Belege_KundeOderLieferant",
            "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)"));
        b.HasKey(x => x.Id);

        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung))
            .HasValue<Lieferschein>(nameof(BelegTyp.Lieferschein))
            .HasValue<Bestellung>(nameof(BelegTyp.Bestellung))
            .HasValue<Wareneingang>(nameof(BelegTyp.Wareneingang))
            .HasValue<Eingangsrechnung>(nameof(BelegTyp.Eingangsrechnung));

        b.Property(x => x.BelegNummer).HasMaxLength(20).IsRequired();
        b.HasIndex("BelegTyp", nameof(Beleg.BelegNummer))
            .IsUnique()
            .HasFilter("[BelegNummer] <> ''");

        // KundeId/LieferantId bewusst NICHT .IsRequired() — je nach Belegtyp ist genau eines von beiden gesetzt,
        // durchgesetzt vom CHECK-Constraint oben, nicht von EF-Required (das würde beide zwingend machen).
        b.HasOne(x => x.Kunde).WithMany().HasForeignKey(x => x.KundeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lieferant).WithMany().HasForeignKey(x => x.LieferantId).OnDelete(DeleteBehavior.Restrict);

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
        b.Property(x => x.ExterneReferenz).HasMaxLength(50);

        b.HasMany(x => x.Positionen).WithOne(p => p.Beleg).HasForeignKey(p => p.BelegId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Steuersummen).WithOne(s => s.Beleg).HasForeignKey(s => s.BelegId).OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 2: `OffenerPostenConfiguration` — Lieferant-Erweiterung**

Modify `src/Milet.Infrastructure/Persistence/Configurations/OffenerPostenConfiguration.cs` — vollständiger neuer Inhalt:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class OffenerPostenConfiguration : IEntityTypeConfiguration<OffenerPosten>
{
    public void Configure(EntityTypeBuilder<OffenerPosten> b)
    {
        b.ToTable("OffenePosten", t => t.HasCheckConstraint(
            "CK_OffenePosten_KundeOderLieferant",
            "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BelegId).IsUnique();
        b.HasOne(x => x.Beleg).WithMany().HasForeignKey(x => x.BelegId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Betrag).HasPrecision(18, 2);
        b.Property(x => x.OffenerBetrag).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

- [ ] **Step 3: `MiletDbContext` — DbSets ergänzen**

Modify `src/Milet.Infrastructure/Persistence/MiletDbContext.cs` — nach der Zeile `public DbSet<Milet.Domain.Entities.Verkauf.Lieferschein> Lieferscheine => Set<Milet.Domain.Entities.Verkauf.Lieferschein>();` einfügen:
```csharp
    public DbSet<Milet.Domain.Entities.Verkauf.Bestellung> Bestellungen => Set<Milet.Domain.Entities.Verkauf.Bestellung>();
    public DbSet<Milet.Domain.Entities.Verkauf.Wareneingang> Wareneingaenge => Set<Milet.Domain.Entities.Verkauf.Wareneingang>();
    public DbSet<Milet.Domain.Entities.Verkauf.Eingangsrechnung> Eingangsrechnungen => Set<Milet.Domain.Entities.Verkauf.Eingangsrechnung>();
```

- [ ] **Step 4: Nummernkreis-Seed — Fix für die bekannte Lücke (idempotent je Code statt nur bei leerer Tabelle) + neue Codes WE/ER**

Modify `src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs` — den Block
```csharp
        if (!await db.Nummernkreise.AnyAsync(ct))
        {
            db.Nummernkreise.AddRange(
                new Nummernkreis { Code = "KD", NaechsteNummer = 10001, Format = "KD-{0}" },
                new Nummernkreis { Code = "LF", NaechsteNummer = 70001, Format = "LF-{0}" },
                new Nummernkreis { Code = "ART", NaechsteNummer = 1001, Format = "ART-{0:00000}" },
                new Nummernkreis { Code = "AN", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AN-{1}-{0:0000}" },
                new Nummernkreis { Code = "AU", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AU-{1}-{0:0000}" },
                new Nummernkreis { Code = "LS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" },
                new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" },
                new Nummernkreis { Code = "GS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "GS-{1}-{0:0000}" },
                new Nummernkreis { Code = "BE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "BE-{1}-{0:0000}" });
        }
```
komplett ersetzen durch:
```csharp
        // Fix für ein bekanntes Risiko (STATUS.md „Bekannte Risiken"): vorher wurden Nummernkreise nur angelegt,
        // wenn die ganze Tabelle leer war — ein später hinzugefügter Code (hier: WE, ER) wurde auf einer bereits
        // migrierten DB dadurch nie automatisch nachgetragen. Jetzt: je fehlendem Code einzeln ergänzen,
        // vorhandene Zeilen werden nie angefasst (kein Reset von NaechsteNummer bei bereits existierenden Codes).
        var benoetigteNummernkreise = new[]
        {
            new Nummernkreis { Code = "KD", NaechsteNummer = 10001, Format = "KD-{0}" },
            new Nummernkreis { Code = "LF", NaechsteNummer = 70001, Format = "LF-{0}" },
            new Nummernkreis { Code = "ART", NaechsteNummer = 1001, Format = "ART-{0:00000}" },
            new Nummernkreis { Code = "AN", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AN-{1}-{0:0000}" },
            new Nummernkreis { Code = "AU", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "AU-{1}-{0:0000}" },
            new Nummernkreis { Code = "LS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "LS-{1}-{0:0000}" },
            new Nummernkreis { Code = "RE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "RE-{1}-{0:0000}" },
            new Nummernkreis { Code = "GS", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "GS-{1}-{0:0000}" },
            new Nummernkreis { Code = "BE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "BE-{1}-{0:0000}" },
            new Nummernkreis { Code = "WE", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "WE-{1}-{0:0000}" },
            new Nummernkreis { Code = "ER", Jahr = DateTime.UtcNow.Year, NaechsteNummer = 1, Format = "ER-{1}-{0:0000}" },
        };
        var vorhandeneCodes = await db.Nummernkreise.Select(n => n.Code).ToListAsync(ct);
        foreach (var kreis in benoetigteNummernkreise)
        {
            if (!vorhandeneCodes.Contains(kreis.Code))
            {
                db.Nummernkreise.Add(kreis);
            }
        }
```

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: Fehler in `VerkaufMapping.cs`/`BelegService.cs`/`BelegUeberleitungService.cs` (unvollständige Typ-Switches, `KundeId`-Zuweisungen inkompatibel) — normal, wird in Task 6/7 behoben. Für diesen Task genügt: keine Fehler in den hier geänderten Dateien selbst.

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs src/Milet.Infrastructure/Persistence/Configurations/OffenerPostenConfiguration.cs src/Milet.Infrastructure/Persistence/MiletDbContext.cs src/Milet.Infrastructure/Persistence/Seed/StammdatenSeed.cs
git commit -m "EF-Configurations Lieferant-Partei (CHECK-Constraint) + Einkaufsbeleg-Discriminator + Nummernkreis-Seed-Fix (WE/ER)"
```

**Hinweis:** `dotnet ef migrations add` kompiliert das volle Infrastructure-Projekt und schlägt daher erst NACH Task 7 fehlerfrei durch — genau wie in Phase 3 (Task 6/7-Split). Die eigentliche Migration wird als letzter Schritt von Task 7 erzeugt.

---

### Task 6: Infrastructure — BelegService + VerkaufMapping generalisieren

**Files:**
- Modify: `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs`
- Modify: `src/Milet.Infrastructure/Services/BelegService.cs`

**Interfaces:**
- Consumes: `Bestellung`/`Wareneingang`/`Eingangsrechnung`, `BelegTypErweiterung.IstEinkaufsBeleg`, `Beleg.LieferantId`/`.ExterneReferenz`, `db.Firmenstamm` (bereits vorhanden), `db.Lieferanten` (Phase 1).
- Produces: `IBelegService` funktioniert generisch für alle 7 Belegtypen (Suche/Laden/Speichern/Löschen) — von Task 7 (Überleitung), Task 11–14 (UI) konsumiert.

- [ ] **Step 1: `VerkaufMapping.cs` — Typ-Switch + Partei-Mapping**

Modify `src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs` — `ToDto(this Beleg b, bool mitPositionen)` ersetzen durch:
```csharp
    public static BelegDto ToDto(this Beleg b, bool mitPositionen)
    {
        var typ = b switch
        {
            Angebot => BelegTyp.Angebot,
            Auftrag => BelegTyp.Auftrag,
            Rechnung => BelegTyp.Rechnung,
            Lieferschein => BelegTyp.Lieferschein,
            Bestellung => BelegTyp.Bestellung,
            Wareneingang => BelegTyp.Wareneingang,
            Eingangsrechnung => BelegTyp.Eingangsrechnung,
            _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
        };

        return new BelegDto
        {
            Id = b.Id,
            BelegTyp = typ,
            BelegNummer = b.BelegNummer,
            BelegDatum = b.BelegDatum,
            KundeId = b.KundeId ?? 0,
            KundeAnzeige = b.Kunde is null ? string.Empty : $"{b.Kunde.Kundennummer} — {b.Kunde.Adresse.Name1}",
            LieferantId = b.LieferantId,
            LieferantAnzeige = b.Lieferant is null ? string.Empty : $"{b.Lieferant.Lieferantennummer} — {b.Lieferant.Adresse.Name1}",
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
            ExterneReferenz = b.ExterneReferenz,
            Positionen = mitPositionen ? b.Positionen.OrderBy(p => p.PositionsNr).Select(p => p.ToDto()).ToList() : [],
            RowVersion = b.RowVersion,
        };
    }
```

- [ ] **Step 2: `BelegService.cs` — Switches erweitern, Kunde/Lieferant-Zweig, ExterneReferenz**

Modify `src/Milet.Infrastructure/Services/BelegService.cs` — die drei privaten Switch-Helfer ersetzen durch:
```csharp
    private static IQueryable<Beleg> SetFuerTyp(MiletDbContext db, BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => db.Angebote,
        BelegTyp.Auftrag => db.Auftraege,
        BelegTyp.Rechnung => db.Rechnungen,
        BelegTyp.Lieferschein => db.Lieferscheine,
        BelegTyp.Bestellung => db.Bestellungen,
        BelegTyp.Wareneingang => db.Wareneingaenge,
        BelegTyp.Eingangsrechnung => db.Eingangsrechnungen,
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        BelegTyp.Bestellung => new Bestellung(),
        BelegTyp.Wareneingang => new Wareneingang(),
        BelegTyp.Eingangsrechnung => new Eingangsrechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        BelegTyp.Bestellung => "BE",
        BelegTyp.Wareneingang => "WE",
        BelegTyp.Eingangsrechnung => "ER",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };
```

`SucheAsync` — `.Include`/Suchprädikat erweitern:
```csharp
    public async Task<IReadOnlyList<BelegDto>> SucheAsync(BelegTyp typ, string? suchtext, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = SetFuerTyp(db, typ).AsNoTracking().Include(b => b.Kunde).Include(b => b.Lieferant).AsQueryable();
        if (!string.IsNullOrWhiteSpace(suchtext))
        {
            var s = suchtext.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.BelegNummer, $"%{s}%") ||
                (b.Kunde != null && EF.Functions.Like(b.Kunde.Adresse.Name1, $"%{s}%")) ||
                (b.Lieferant != null && EF.Functions.Like(b.Lieferant.Adresse.Name1, $"%{s}%")));
        }
        var belege = await query.OrderByDescending(b => b.BelegDatum).ThenByDescending(b => b.Id).Take(500).ToListAsync(ct);
        return belege.Select(b => b.ToDto(mitPositionen: false)).ToList();
    }

    public async Task<BelegDto> LadeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var beleg = await db.Belege.AsNoTracking()
            .Include(b => b.Kunde)
            .Include(b => b.Lieferant)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Beleg), id);
        return beleg.ToDto(mitPositionen: true);
    }
```

`SpeichereAsync` — den `dto.Id == 0`-Zweig ersetzen durch:
```csharp
        Beleg beleg;
        if (dto.Id == 0)
        {
            beleg = NeueInstanz(dto.BelegTyp);
            beleg.BelegNummer = dto.BelegTyp == BelegTyp.Rechnung
                ? string.Empty
                : await numberRangeService.NaechsteNummerAsync(NummernkreisCode(dto.BelegTyp), ct);

            if (dto.BelegTyp.IstEinkaufsBeleg())
            {
                var lieferant = await db.Lieferanten.Include(l => l.Zahlungsbedingung)
                    .FirstOrDefaultAsync(l => l.Id == dto.LieferantId, ct)
                    ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Lieferant), dto.LieferantId ?? 0);
                var firma = await db.Firmenstamm.AsNoTracking().FirstOrDefaultAsync(f => f.Id == 1, ct);

                beleg.LieferantId = lieferant.Id;
                // Invertierte Semantik ggü. Verkauf (siehe Architektur-Entscheidung 5 im Phase-4-Plan):
                // "Rechnungsadresse" = Anschrift des Geschäftspartners (hier: Lieferant), "Lieferadresse" =
                // wohin die Ware geht (hier: die eigene Firma, nicht der Lieferant).
                beleg.RechnungsadresseSnapshot = lieferant.Adresse.Kopie();
                beleg.LieferadresseSnapshot = firma?.Adresse.Kopie() ?? lieferant.Adresse.Kopie();
                beleg.ZahlungsbedingungZielTage = lieferant.Zahlungsbedingung?.ZielTage ?? 0;
                beleg.ZahlungsbedingungSkontoTage = lieferant.Zahlungsbedingung?.SkontoTage;
                beleg.ZahlungsbedingungSkontoProzent = lieferant.Zahlungsbedingung?.SkontoProzent;
            }
            else
            {
                var kunde = await db.Kunden.Include(k => k.Zahlungsbedingung).FirstOrDefaultAsync(k => k.Id == dto.KundeId, ct)
                    ?? throw new NotFoundException(nameof(Domain.Entities.Stammdaten.Kunde), dto.KundeId);

                beleg.KundeId = kunde.Id;
                beleg.RechnungsadresseSnapshot = kunde.Adresse.Kopie();
                beleg.LieferadresseSnapshot = kunde.Adresse.Kopie();
                beleg.ZahlungsbedingungZielTage = kunde.Zahlungsbedingung?.ZielTage ?? 0;
                beleg.ZahlungsbedingungSkontoTage = kunde.Zahlungsbedingung?.SkontoTage;
                beleg.ZahlungsbedingungSkontoProzent = kunde.Zahlungsbedingung?.SkontoProzent;
            }

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
        beleg.ExterneReferenz = dto.ExterneReferenz;
```
(Der Rest der Methode — `AktualisierePositionen`, Steuersummen-Neuberechnung, `SaveChangesTranslatingConcurrencyAsync` — bleibt unverändert, `SteuerRechner` ist bereits Beleg-typ-agnostisch.)

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: Restfehler nur noch in `BelegUeberleitungService.cs` (Task 7) und ggf. `VerkaufLookupService.cs` (bereits vollständig, keine Änderung nötig — prüfen).

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/Mapping/VerkaufMapping.cs src/Milet.Infrastructure/Services/BelegService.cs
git commit -m "BelegService/VerkaufMapping generalisieren: Einkaufsbelege (Lieferant-Zweig, ExterneReferenz)"
```

---

### Task 7: Infrastructure — BelegUeberleitungService generalisieren + Migration erzeugen

**Files:**
- Modify: `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`
- Create: Migration `EinkaufBestellungWareneingang`

**Interfaces:**
- Consumes: `Bestellung`/`Wareneingang`/`Eingangsrechnung`, `BelegTyp.IstEinkaufsBeleg` — für den Überleitungspfad.
- Produces: `IBelegUeberleitungService.UeberleitenMitAuswahlAsync`/`.UeberleitenAsync` funktionieren für Bestellung→Wareneingang (Teilmenge) und Wareneingang→Eingangsrechnung (voll) — von Task 12/13 (UI) konsumiert. Migration `EinkaufBestellungWareneingang` — Grundlage für alle Folge-Tasks.

- [ ] **Step 1: `ErlaubteUebergaenge`, `TypVon`, `NeueInstanz`, `NummernkreisCode` erweitern**

Modify `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs`:
```csharp
    private static readonly Dictionary<BelegTyp, BelegTyp[]> ErlaubteUebergaenge = new()
    {
        [BelegTyp.Angebot] = [BelegTyp.Auftrag],
        [BelegTyp.Auftrag] = [BelegTyp.Rechnung, BelegTyp.Lieferschein],
        [BelegTyp.Lieferschein] = [BelegTyp.Rechnung],
        [BelegTyp.Bestellung] = [BelegTyp.Wareneingang],
        [BelegTyp.Wareneingang] = [BelegTyp.Eingangsrechnung],
    };

    private static BelegTyp TypVon(Beleg b) => b switch
    {
        Angebot => BelegTyp.Angebot,
        Auftrag => BelegTyp.Auftrag,
        Rechnung => BelegTyp.Rechnung,
        Lieferschein => BelegTyp.Lieferschein,
        Bestellung => BelegTyp.Bestellung,
        Wareneingang => BelegTyp.Wareneingang,
        Eingangsrechnung => BelegTyp.Eingangsrechnung,
        _ => throw new InvalidOperationException($"Unbekannter Beleg-Subtyp {b.GetType().Name}."),
    };

    private static Beleg NeueInstanz(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => new Angebot(),
        BelegTyp.Auftrag => new Auftrag(),
        BelegTyp.Rechnung => new Rechnung(),
        BelegTyp.Lieferschein => new Lieferschein(),
        BelegTyp.Bestellung => new Bestellung(),
        BelegTyp.Wareneingang => new Wareneingang(),
        BelegTyp.Eingangsrechnung => new Eingangsrechnung(),
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };

    private static string NummernkreisCode(BelegTyp typ) => typ switch
    {
        BelegTyp.Angebot => "AN",
        BelegTyp.Auftrag => "AU",
        BelegTyp.Rechnung => "RE",
        BelegTyp.Lieferschein => "LS",
        BelegTyp.Bestellung => "BE",
        BelegTyp.Wareneingang => "WE",
        BelegTyp.Eingangsrechnung => "ER",
        _ => throw new ArgumentOutOfRangeException(nameof(typ)),
    };
```

- [ ] **Step 2: `UeberleitenAsync` — Guards + Partei-Kopie erweitern**

In `UeberleitenAsync`:
- Zeile `if (zielTyp == BelegTyp.Lieferschein) throw ...` ersetzen durch:
```csharp
        if (zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang)
            throw new InvalidOperationException($"{zielTyp}-Erstellung erfordert Mengenauswahl und Lagerort — verwenden Sie UeberleitenMitAuswahlAsync.");
```
- Zeile `if (quellTyp == BelegTyp.Lieferschein && quellBeleg.Status != BelegStatus.Gebucht) throw ...` ersetzen durch:
```csharp
        if (quellTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang && quellBeleg.Status != BelegStatus.Gebucht)
            throw new InvalidOperationException($"{quellTyp} '{quellBeleg.BelegNummer}' muss erst gebucht werden, bevor er überführt werden kann.");
```
- Zeile `zielBeleg.KundeId = quellBeleg.KundeId;` ergänzen um direkt danach:
```csharp
        zielBeleg.LieferantId = quellBeleg.LieferantId;
```

- [ ] **Step 3: `UeberleitenMitAuswahlAsync` — Guards + Partei-Kopie + LagerortId-Zuweisung erweitern**

Analog in `UeberleitenMitAuswahlAsync`:
- `if (quellTyp == BelegTyp.Lieferschein && quellBeleg.Status != BelegStatus.Gebucht) throw ...` → gleiche Erweiterung wie Step 2.
- `if (zielTyp == BelegTyp.Lieferschein) { if (lagerortId is null) throw ...; if (quellBeleg.Kunde?.Liefersperre == true) throw ...; }` ersetzen durch:
```csharp
        if (zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang)
        {
            if (lagerortId is null)
                throw new InvalidOperationException($"Lagerort ist für die {zielTyp}-Erstellung erforderlich.");
            if (quellBeleg.Kunde?.Liefersperre == true)
                throw new InvalidOperationException($"Kunde '{quellBeleg.Kunde.Kundennummer}' hat Liefersperre.");
        }
```
- `zielBeleg.KundeId = quellBeleg.KundeId;` ergänzen um `zielBeleg.LieferantId = quellBeleg.LieferantId;` (wie Step 2).
- `LagerortId = zielTyp == BelegTyp.Lieferschein ? lagerortId : null,` ersetzen durch:
```csharp
                LagerortId = zielTyp is BelegTyp.Lieferschein or BelegTyp.Wareneingang ? lagerortId : null,
```

(`UeberleitenMehrereAsync` bleibt unverändert — Sammelrechnung ist ein reiner Verkaufsfall, Einkauf ruft diese Methode nie auf.)

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj`
Expected: 0 Fehler (letzter offener Compile-Fehler aus Task 5 jetzt behoben).

- [ ] **Step 5: Migration erzeugen**

Run:
```bash
cd src/Milet.Tools.Migrator
"$USERPROFILE/.dotnet/dotnet.exe" tool run dotnet-ef migrations add EinkaufBestellungWareneingang --project ../Milet.Infrastructure --startup-project .
```
Expected: neue Migration `EinkaufBestellungWareneingang` in `src/Milet.Infrastructure/Persistence/Migrations/` — `ALTER TABLE Belege ALTER COLUMN KundeId int NULL`, neue Spalten `LieferantId`/`ExterneReferenz` auf `Belege`, `KundeId`/`LieferantId` auf `OffenePosten`, beide CHECK-Constraints, keine Datenverlust-Warnung (bestehende Zeilen erfüllen den Constraint bereits, siehe Architektur-Entscheidung 1).
Anschließend anwenden: `"$USERPROFILE/.dotnet/dotnet.exe" run --project ../Milet.Tools.Migrator` (bzw. das übliche Migrator-Kommando dieses Repos).

- [ ] **Step 6: Commit**

```bash
git add src/Milet.Infrastructure/Services/BelegUeberleitungService.cs src/Milet.Infrastructure/Persistence/Migrations/
git commit -m "BelegUeberleitungService generalisieren (Bestellung→Wareneingang→Eingangsrechnung) + Migration EinkaufBestellungWareneingang"
```

---

### Task 8: Infrastructure — EinkaufLookupService + BestellVorschlagService + Integrationstest

**Files:**
- Create: `src/Milet.Infrastructure/Services/EinkaufLookupService.cs`
- Create: `src/Milet.Infrastructure/Services/BestellVorschlagService.cs`
- Test: `tests/Milet.IntegrationTests/BestellVorschlagServiceTests.cs`

**Interfaces:**
- Consumes: `IEinkaufLookupService`, `IBestellVorschlagService` (Task 4), `db.ArtikelBestaende` (Phase 3).
- Produces: Implementierungen — von Task 11 (Bestellung-Editor), Task 14 (Bestellvorschlag-UI), DI (Task 15) konsumiert.

- [ ] **Step 1: `EinkaufLookupService`**

`src/Milet.Infrastructure/Services/EinkaufLookupService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Einkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class EinkaufLookupService(IDbContextFactory<MiletDbContext> dbContextFactory) : IEinkaufLookupService
{
    public async Task<EinkaufLookups> LadeLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var lieferanten = await db.Lieferanten.AsNoTracking()
            .OrderBy(l => l.Lieferantennummer)
            .Select(l => new LieferantEinkaufLookupDto(l.Id, $"{l.Lieferantennummer} — {l.Adresse.Name1}", l.ZahlungsbedingungId))
            .ToListAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => !a.Gesperrt)
            .OrderBy(a => a.Artikelnummer)
            .Select(a => new ArtikelEinkaufLookupDto(
                a.Id,
                $"{a.Artikelnummer} — {a.Bezeichnung}",
                a.Bezeichnung,
                a.Einkaufspreis,
                a.MwStSatzId,
                a.MwStSatz!.Satz,
                a.MwStSatz.SteuerSchluessel,
                a.Einheit!.Kuerzel,
                a.HatSeriennummern))
            .ToListAsync(ct);

        return new EinkaufLookups(lieferanten, artikel);
    }
}
```

- [ ] **Step 2: `BestellVorschlagService`**

`src/Milet.Infrastructure/Services/BestellVorschlagService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Einkauf;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class BestellVorschlagService(IDbContextFactory<MiletDbContext> dbContextFactory) : IBestellVorschlagService
{
    public async Task<IReadOnlyList<BestellVorschlagPositionDto>> ErmittleVorschlaegeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.IstLagerartikel && !a.Gesperrt && a.Mindestbestand != null)
            .Include(a => a.Einheit)
            .Include(a => a.MwStSatz)
            .ToListAsync(ct);
        if (artikel.Count == 0) return [];

        var artikelIds = artikel.Select(a => a.Id).ToList();
        var bestaendeJeArtikel = await db.ArtikelBestaende.AsNoTracking()
            .Where(b => artikelIds.Contains(b.ArtikelId))
            .GroupBy(b => b.ArtikelId)
            .Select(g => new { ArtikelId = g.Key, Summe = g.Sum(b => b.Menge) })
            .ToDictionaryAsync(x => x.ArtikelId, x => x.Summe, ct);

        var ergebnis = new List<BestellVorschlagPositionDto>();
        foreach (var a in artikel)
        {
            var bestand = bestaendeJeArtikel.GetValueOrDefault(a.Id, 0m);
            var mindestbestand = a.Mindestbestand!.Value;
            if (bestand >= mindestbestand) continue;

            ergebnis.Add(new BestellVorschlagPositionDto(
                a.Id, a.Artikelnummer, a.Bezeichnung, bestand, mindestbestand,
                VorschlagsMenge: mindestbestand - bestand,
                a.Einkaufspreis, a.MwStSatzId, a.MwStSatz!.Satz, a.MwStSatz.SteuerSchluessel, a.Einheit?.Kuerzel));
        }

        return ergebnis.OrderBy(v => v.Artikelnummer).ToList();
    }
}
```

- [ ] **Step 3: Integrationstest**

`tests/Milet.IntegrationTests/BestellVorschlagServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services;
using Testcontainers.MsSql;
using Xunit;

namespace Milet.IntegrationTests;

public sealed class BestellVorschlagServiceTests : IAsyncLifetime
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
        _options = new DbContextOptionsBuilder<MiletDbContext>().UseSqlServer(_container.GetConnectionString()).Options;
        _factory = new TestDbContextFactory(_options);

        await using var db = new MiletDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var einheit = new Einheit { Kuerzel = "Stk", Bezeichnung = "Stück" };
        var mwst = new MwStSatz { Bezeichnung = "Voll", Satz = 19m, GueltigAb = new DateOnly(2007, 1, 1) };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, lagerort);
        await db.SaveChangesAsync();

        var unterschritten = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Knapp", EinheitId = einheit.Id, MwStSatzId = mwst.Id, Mindestbestand = 10m, Einkaufspreis = 5m };
        var ausreichend = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Ausreichend", EinheitId = einheit.Id, MwStSatzId = mwst.Id, Mindestbestand = 5m, Einkaufspreis = 5m };
        var ohneMindestbestand = new Artikel { Artikelnummer = "ART-3", Bezeichnung = "Kein Minimum", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        db.AddRange(unterschritten, ausreichend, ohneMindestbestand);
        await db.SaveChangesAsync();

        await BestandService.BucheBewegungAsync(db, unterschritten.Id, lagerort.Id, 3m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
        await BestandService.BucheBewegungAsync(db, ausreichend.Id, lagerort.Id, 8m, LagerbewegungTyp.Korrektur, null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task ErmittleVorschlaegeAsync_NurArtikelUnterMindestbestand_MitKorrekterVorschlagsmenge()
    {
        var service = new BestellVorschlagService(_factory);
        var vorschlaege = await service.ErmittleVorschlaegeAsync(TestContext.Current.CancellationToken);

        var vorschlag = Assert.Single(vorschlaege);
        Assert.Equal("ART-1", vorschlag.Artikelnummer);
        Assert.Equal(3m, vorschlag.AktuellerBestand);
        Assert.Equal(7m, vorschlag.VorschlagsMenge);
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

- [ ] **Step 4: Build + Tests**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Infrastructure/Milet.Infrastructure.csproj` (0 Fehler), dann `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj` (neuer Test läuft grün oder sauber übersprungen, kein Fail).

- [ ] **Step 5: Commit**

```bash
git add src/Milet.Infrastructure/Services/EinkaufLookupService.cs src/Milet.Infrastructure/Services/BestellVorschlagService.cs tests/Milet.IntegrationTests/BestellVorschlagServiceTests.cs
git commit -m "EinkaufLookupService + BestellVorschlagService (Mindestbestand-Unterschreitung)"
```

---

### Task 9: Infrastructure — WareneingangBuchenService + Integrationstest

**Files:**
- Create: `src/Milet.Infrastructure/Services/WareneingangBuchenService.cs`
- Test: `tests/Milet.IntegrationTests/WareneingangBuchenServiceTests.cs`

**Interfaces:**
- Consumes: `BestandService.BucheBewegungAsync` (Phase 3, unverändert), `LagerbewegungTyp.Wareneingang` (Task 2), `IWareneingangBuchenService` (Task 4).
- Produces: Implementierung — von Task 12 (Wareneingang-Editor), DI (Task 15) konsumiert.

- [ ] **Step 1: `WareneingangBuchenService`**

`src/Milet.Infrastructure/Services/WareneingangBuchenService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class WareneingangBuchenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IWareneingangBuchenService
{
    public async Task<Verkauf.BelegDto> BuchenAsync(
        int wareneingangId, IReadOnlyDictionary<int, IReadOnlyList<string>> neueSeriennummernJePosition, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var wareneingang = await db.Wareneingaenge.Include(w => w.Positionen)
            .FirstOrDefaultAsync(w => w.Id == wareneingangId, ct)
            ?? throw new NotFoundException(nameof(Wareneingang), wareneingangId);

        if (wareneingang.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Wareneingang '{wareneingang.BelegNummer}' ist bereits gebucht.");
        if (wareneingang.Positionen.Count == 0)
            throw new InvalidOperationException("Wareneingang ohne Positionen kann nicht gebucht werden.");

        foreach (var position in wareneingang.Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            if (position.ArtikelId is not { } artikelId || position.LagerortId is not { } lagerortId)
                throw new InvalidOperationException($"Position {position.PositionsNr}: Artikel oder Lagerort fehlt.");

            var artikel = await db.Artikel.AsNoTracking().FirstAsync(a => a.Id == artikelId, ct);

            // Positives Delta — BestandService.BucheBewegungAsync ist unverändert wiederverwendbar (siehe
            // Phase-3-Kommentar dort): die atomare UPDATE-Bedingung "Menge + delta >= 0" ist bei einem Zugang
            // immer erfüllt, und legt bei erstem Bestand am Lagerort die ArtikelBestand-Zeile automatisch an.
            await BestandService.BucheBewegungAsync(db, artikelId, lagerortId, position.Menge, LagerbewegungTyp.Wareneingang, position.Id, ct);

            if (artikel.HatSeriennummern)
            {
                if (!neueSeriennummernJePosition.TryGetValue(position.Id, out var nummern) || nummern.Count != position.Menge)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: es müssen genau {position.Menge} Seriennummer(n) erfasst werden.");

                var doppelte = await db.Seriennummern.AsNoTracking()
                    .Where(s => s.ArtikelId == artikelId && nummern.Contains(s.Nummer))
                    .Select(s => s.Nummer)
                    .ToListAsync(ct);
                if (doppelte.Count > 0)
                    throw new InvalidOperationException($"Position {position.PositionsNr}: Seriennummer(n) bereits vorhanden: {string.Join(", ", doppelte)}.");

                // Neue Seriennummern (anders als LieferscheinBuchenService, das bestehende auswählt): Id ist vor
                // SaveChangesAsync noch 0, daher Verknüpfung über Navigationseigenschaften statt SeriennummerId.
                foreach (var nummer in nummern)
                {
                    var seriennummer = new Seriennummer
                    {
                        ArtikelId = artikelId,
                        Nummer = nummer,
                        Status = SeriennummerStatus.AufLager,
                        LagerortId = lagerortId,
                    };
                    db.Seriennummern.Add(seriennummer);
                    db.BelegPositionSeriennummern.Add(new BelegPositionSeriennummer { BelegPosition = position, Seriennummer = seriennummer });
                }
            }
        }

        wareneingang.Status = BelegStatus.Gebucht;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return wareneingang.ToDto(mitPositionen: true);
    }
}
```

- [ ] **Step 2: Integrationstest**

`tests/Milet.IntegrationTests/WareneingangBuchenServiceTests.cs`:
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

public sealed class WareneingangBuchenServiceTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private DbContextOptions<MiletDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private int _lieferantId;
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
        var lieferant = new Lieferant { Lieferantennummer = "LF-TEST", Adresse = new() { Name1 = "Testlieferant" } };
        var lagerort = new Lagerort { Code = "HL", Bezeichnung = "Hauptlager" };
        db.AddRange(einheit, mwst, lieferant, lagerort);
        await db.SaveChangesAsync();

        var artikel = new Artikel { Artikelnummer = "ART-1", Bezeichnung = "Normalartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id };
        var artikelSerial = new Artikel { Artikelnummer = "ART-2", Bezeichnung = "Serienartikel", EinheitId = einheit.Id, MwStSatzId = mwst.Id, HatSeriennummern = true };
        db.AddRange(artikel, artikelSerial);
        await db.SaveChangesAsync();

        _lieferantId = lieferant.Id;
        _artikelId = artikel.Id;
        _artikelSerialisiertId = artikelSerial.Id;
        _lagerortId = lagerort.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task<Wareneingang> NeuerWareneingangAsync(int artikelId, decimal menge, CancellationToken ct)
    {
        await using var db = new MiletDbContext(_options);
        var lieferant = await db.Lieferanten.FirstAsync(l => l.Id == _lieferantId, ct);
        var wareneingang = new Wareneingang
        {
            BelegNummer = $"WE-{Guid.NewGuid():N}"[..12],
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = lieferant.Id,
            RechnungsadresseSnapshot = lieferant.Adresse.Kopie(),
            LieferadresseSnapshot = lieferant.Adresse.Kopie(),
            Positionen = [new BelegPosition
            {
                PositionsNr = 1, Bezeichnung = "Test", Menge = menge, Einzelpreis = 5m, GesamtNetto = menge * 5m,
                MwStSatzWert = 19m, ArtikelId = artikelId, LagerortId = _lagerortId,
            }],
        };
        db.Add(wareneingang);
        await db.SaveChangesAsync(ct);
        return wareneingang;
    }

    [Fact]
    public async Task BuchenAsync_NormalArtikel_ErhoehtBestandUndSetztGebucht()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelId, 20, ct);
        var service = new WareneingangBuchenService(_factory);

        var gebucht = await service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>>(), ct);

        Assert.Equal(BelegStatus.Gebucht, gebucht.Status);
        await using var db = new MiletDbContext(_options);
        var bestand = await db.ArtikelBestaende.FirstAsync(b => b.ArtikelId == _artikelId && b.LagerortId == _lagerortId, ct);
        Assert.Equal(20m, bestand.Menge);
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelMitNeuenNummern_LegtSeriennummernAn()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelSerialisiertId, 2, ct);
        var positionId = wareneingang.Positionen[0].Id;
        var service = new WareneingangBuchenService(_factory);

        await service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>> { [positionId] = ["SN-A", "SN-B"] }, ct);

        await using var db = new MiletDbContext(_options);
        var seriennummern = await db.Seriennummern.Where(s => s.ArtikelId == _artikelSerialisiertId).ToListAsync(ct);
        Assert.Equal(2, seriennummern.Count);
        Assert.All(seriennummern, s => Assert.Equal(SeriennummerStatus.AufLager, s.Status));
        Assert.Equal(2, await db.BelegPositionSeriennummern.CountAsync(b => b.BelegPositionId == positionId, ct));
    }

    [Fact]
    public async Task BuchenAsync_SerialisierterArtikelFalscheAnzahl_Wirft()
    {
        var ct = TestContext.Current.CancellationToken;
        var wareneingang = await NeuerWareneingangAsync(_artikelSerialisiertId, 2, ct);
        var positionId = wareneingang.Positionen[0].Id;
        var service = new WareneingangBuchenService(_factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuchenAsync(wareneingang.Id, new Dictionary<int, IReadOnlyList<string>> { [positionId] = ["SN-A"] }, ct));
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

- [ ] **Step 3: Build + Tests**

Run: Build Infrastructure (0 Fehler), dann `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj`.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/WareneingangBuchenService.cs tests/Milet.IntegrationTests/WareneingangBuchenServiceTests.cs
git commit -m "WareneingangBuchenService: positive Lagerbewegung + Neuanlage von Seriennummern"
```

---

### Task 10: Infrastructure — EingangsrechnungBuchenService + Integrationstest

**Files:**
- Create: `src/Milet.Infrastructure/Services/EingangsrechnungBuchenService.cs`
- Test: `tests/Milet.IntegrationTests/EingangsrechnungBuchenServiceTests.cs`

**Interfaces:**
- Consumes: `IEingangsrechnungBuchenService` (Task 4), `OffenerPosten.LieferantId` (Task 1).
- Produces: Implementierung — von Task 13 (Eingangsrechnung-Editor), DI (Task 15) konsumiert.

- [ ] **Step 1: `EingangsrechnungBuchenService`**

`src/Milet.Infrastructure/Services/EingangsrechnungBuchenService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Domain.Entities.Finanzen;
using Milet.Domain.Entities.Verkauf;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Services.Mapping;

namespace Milet.Infrastructure.Services;

public sealed class EingangsrechnungBuchenService(IDbContextFactory<MiletDbContext> dbContextFactory) : IEingangsrechnungBuchenService
{
    public async Task<EingangsrechnungBuchenErgebnisDto> BuchenAsync(int eingangsrechnungId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var eingangsrechnung = await db.Eingangsrechnungen.Include(e => e.Positionen)
            .FirstOrDefaultAsync(e => e.Id == eingangsrechnungId, ct)
            ?? throw new NotFoundException(nameof(Eingangsrechnung), eingangsrechnungId);

        if (eingangsrechnung.Status != BelegStatus.Entwurf)
            throw new InvalidOperationException($"Eingangsrechnung '{eingangsrechnung.BelegNummer}' ist bereits gebucht.");
        if (eingangsrechnung.Positionen.Count == 0)
            throw new InvalidOperationException("Eingangsrechnung ohne Positionen kann nicht gebucht werden.");
        if (eingangsrechnung.LieferantId is not { } lieferantId)
            throw new InvalidOperationException("Eingangsrechnung ohne Lieferant kann nicht gebucht werden.");

        // Abweichungs-Soft-Warnung (siehe Architektur-Entscheidung 7 im Phase-4-Plan): Rechnungssumme vs. Summe
        // des ursprünglichen Wareneingangs. Die Positionen einer Eingangsrechnung entstehen per Überleitung aus
        // genau einem Wareneingang (UeberleitenAsync, v1 keine Sammel-Eingangsrechnung) — daher genügt ein Hop
        // über UrsprungsPositionId der ersten Position, um den Quell-Beleg zu finden.
        var erwarteterBetrag = eingangsrechnung.SummeBrutto;
        var ersteQuellPositionId = eingangsrechnung.Positionen.Select(p => p.UrsprungsPositionId).FirstOrDefault(id => id != null);
        if (ersteQuellPositionId is int quellPositionId)
        {
            var quellBelegId = await db.BelegPositionen.AsNoTracking()
                .Where(p => p.Id == quellPositionId)
                .Select(p => (int?)p.BelegId)
                .FirstOrDefaultAsync(ct);
            if (quellBelegId is int belegId)
            {
                erwarteterBetrag = await db.Belege.AsNoTracking()
                    .Where(b => b.Id == belegId)
                    .Select(b => b.SummeBrutto)
                    .FirstOrDefaultAsync(ct);
            }
        }

        var abweichung = eingangsrechnung.SummeBrutto - erwarteterBetrag;
        var weichtAb = Math.Abs(abweichung) > 0.01m;

        eingangsrechnung.Status = BelegStatus.Gebucht;
        eingangsrechnung.Faelligkeit = eingangsrechnung.BelegDatum.AddDays(eingangsrechnung.ZahlungsbedingungZielTage);

        db.OffenePosten.Add(new OffenerPosten
        {
            BelegId = eingangsrechnung.Id,
            LieferantId = lieferantId,
            Typ = OffenerPostenTyp.Kreditor,
            Betrag = eingangsrechnung.SummeBrutto,
            OffenerBetrag = eingangsrechnung.SummeBrutto,
            Faelligkeit = eingangsrechnung.Faelligkeit.Value,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new EingangsrechnungBuchenErgebnisDto(eingangsrechnung.ToDto(mitPositionen: true), weichtAb, erwarteterBetrag, abweichung);
    }
}
```

- [ ] **Step 2: Integrationstest**

`tests/Milet.IntegrationTests/EingangsrechnungBuchenServiceTests.cs` — Grundgerüst analog zu `WareneingangBuchenServiceTests.cs` (gleiche Docker-Verfügbarkeits-/TestDbContextFactory-Helfer), Kernfälle:
```csharp
    [Fact]
    public async Task BuchenAsync_BetragStimmtMitWareneingangUeberein_KeineAbweichung_LegtKreditorOpAn()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: Wareneingang (gebucht) mit SummeBrutto = 119,00 (100 netto + 19% MwSt), dann Eingangsrechnung
        // per UeberleitenAsync daraus erzeugt (Positionen 1:1 übernommen, UrsprungsPositionId gesetzt) und
        // unverändert gebucht.
        var ergebnis = await service.BuchenAsync(eingangsrechnungId, ct);

        Assert.False(ergebnis.BetragWeichtAb);
        Assert.Equal(0m, ergebnis.AbweichungBetrag);
        await using var db = new MiletDbContext(_options);
        var op = await db.OffenePosten.SingleAsync(o => o.BelegId == eingangsrechnungId, ct);
        Assert.Equal(OffenerPostenTyp.Kreditor, op.Typ);
        Assert.Equal(lieferantId, op.LieferantId);
        Assert.Null(op.KundeId);
    }

    [Fact]
    public async Task BuchenAsync_BetragWeichtAb_MeldetSoftWarnungLegtOpTrotzdemAn()
    {
        // Arrange wie oben, aber Einzelpreis der Eingangsrechnung-Position vor dem Buchen manuell auf einen
        // höheren Wert geändert (simuliert eine reale Rechnung mit abweichendem Preis).
        var ergebnis = await service.BuchenAsync(eingangsrechnungId, ct);

        Assert.True(ergebnis.BetragWeichtAb);
        Assert.True(ergebnis.AbweichungBetrag > 0);
        await using var db = new MiletDbContext(_options);
        Assert.Equal(1, await db.OffenePosten.CountAsync(o => o.BelegId == eingangsrechnungId, ct));
    }
```
(Vollständiger Testcode analog zum Muster aus Task 9 — Arrange baut Lieferant/Artikel/Lagerort/Wareneingang auf, bucht ihn über `WareneingangBuchenService`, leitet per `BelegUeberleitungService.UeberleitenAsync` in eine Eingangsrechnung über, optional wird vor dem zweiten Testfall `db.BelegPositionen` direkt manipuliert um eine Abweichung zu erzeugen.)

- [ ] **Step 3: Build + Tests**

Run: Build Infrastructure (0 Fehler), Integrationstests laufen/übersprungen ohne Fail.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.Infrastructure/Services/EingangsrechnungBuchenService.cs tests/Milet.IntegrationTests/EingangsrechnungBuchenServiceTests.cs
git commit -m "EingangsrechnungBuchenService: Kreditor-OP + Betrags-Abweichungs-Soft-Warnung gegen Wareneingang"
```

---

### Task 11: App — EinkaufBelegEditViewModelBase + Bestellung List/Edit

**Files:**
- Create: `src/Milet.App/ViewModels/Einkauf/EinkaufBelegEditViewModelBase.cs`
- Create: `src/Milet.App/ViewModels/Einkauf/BestellungListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Einkauf/BestellungEditViewModel.cs`
- Create: `src/Milet.App/Views/Einkauf/BestellungListPage.xaml(.cs)`
- Create: `src/Milet.App/Views/Einkauf/BestellungEditPage.xaml(.cs)`

**Interfaces:**
- Consumes: `IBelegService`, `IEinkaufLookupService`, `IBelegUeberleitungService`, `ILagerortService` (Phase 3), `INavigationService`, `IDialogService`.
- Produces: `EinkaufBelegEditViewModelBase` (Basis für Task 12/13), `BestellungListViewModel`/`.EditViewModel` — von Task 15 (Navigation/DI) konsumiert.

- [ ] **Step 1: `EinkaufBelegEditViewModelBase`**

`src/Milet.App/ViewModels/Einkauf/EinkaufBelegEditViewModelBase.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Common;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

/// <summary>Analog zu Verkauf.BelegEditViewModelBase, aber Lieferant- statt Kunde-basiert und ohne
/// Preisfindung (EK-Preis kommt direkt aus Artikel.Einkaufspreis). Bewusst eine eigene Basisklasse statt
/// Erweiterung von BelegEditViewModelBase — siehe Architektur-Entscheidung 2 im Phase-4-Plan.</summary>
public abstract partial class EinkaufBelegEditViewModelBase : ObservableObject, INavigationAware
{
    private readonly BelegTyp _typ;
    private readonly IBelegService _belegService;
    private readonly IEinkaufLookupService _lookupService;
    protected readonly INavigationService Navigation;
    protected readonly IDialogService DialogService;

    protected int Id;
    private byte[] _rowVersion = [];
    private int _naechstePositionsNr = 1;

    protected EinkaufBelegEditViewModelBase(
        BelegTyp typ, IBelegService belegService, IEinkaufLookupService lookupService,
        INavigationService navigation, IDialogService dialogService)
    {
        _typ = typ;
        _belegService = belegService;
        _lookupService = lookupService;
        Navigation = navigation;
        DialogService = dialogService;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = "(automatisch)";
    [ObservableProperty] public partial DateTimeOffset? BelegDatum { get; set; } = DateTimeOffset.Now;
    [ObservableProperty] public partial IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten { get; set; } = [];
    [ObservableProperty] public partial int LieferantId { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ArtikelEinkaufLookupDto> ArtikelLookups { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial BelegPositionDto? PositionAusgewaehlt { get; set; }
    [ObservableProperty] public partial int? PositionArtikelId { get; set; }
    [ObservableProperty] public partial decimal PositionMenge { get; set; } = 1;
    [ObservableProperty] public partial decimal PositionEinzelpreis { get; set; }

    [ObservableProperty] public partial decimal SummeNetto { get; set; }
    [ObservableProperty] public partial decimal SummeMwSt { get; set; }
    [ObservableProperty] public partial decimal SummeBrutto { get; set; }

    [ObservableProperty] public partial BelegStatus Status { get; set; } = BelegStatus.Entwurf;
    [ObservableProperty] public partial string? Kopftext { get; set; }
    [ObservableProperty] public partial string? Fusstext { get; set; }
    /// <summary>Nur auf der Eingangsrechnung-Seite als Eingabefeld sichtbar ("Rechnungsnummer des Lieferanten") —
    /// siehe Architektur-Entscheidung 6. Für Bestellung/Wareneingang bleibt das Feld leer/ungenutzt.</summary>
    [ObservableProperty] public partial string? ExterneReferenz { get; set; }
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; } = true;

    public void OnNavigatedTo(NavigationEventArgs args)
    {
        Id = args.Parameter is int id ? id : 0;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var lookups = await _lookupService.LadeLookupsAsync();
        Lieferanten = lookups.Lieferanten;
        ArtikelLookups = lookups.Artikel;

        if (Id == 0) { IstBearbeitbar = true; return; }

        var beleg = await _belegService.LadeAsync(Id);
        _rowVersion = beleg.RowVersion;
        BelegNummer = beleg.BelegNummer;
        BelegDatum = beleg.BelegDatum.ToDateTime(TimeOnly.MinValue);
        LieferantId = beleg.LieferantId ?? 0;
        Positionen = new ObservableCollection<BelegPositionDto>(beleg.Positionen);
        _naechstePositionsNr = Positionen.Count == 0 ? 1 : Positionen.Max(p => p.PositionsNr) + 1;
        SummeNetto = beleg.SummeNetto;
        SummeMwSt = beleg.SummeMwSt;
        SummeBrutto = beleg.SummeBrutto;
        Status = beleg.Status;
        Kopftext = beleg.Kopftext;
        Fusstext = beleg.Fusstext;
        ExterneReferenz = beleg.ExterneReferenz;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
    }

    [RelayCommand]
    private void EkPreisUebernehmen()
    {
        if (PositionArtikelId is not { } artikelId) return;
        var artikel = ArtikelLookups.FirstOrDefault(a => a.Id == artikelId);
        if (artikel is not null) PositionEinzelpreis = artikel.Einkaufspreis;
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
            MwStSatzId = artikel.MwStSatzId,
            MwStSatzWert = artikel.MwStSatzWert,
            SteuerSchluessel = artikel.SteuerSchluessel,
            GesamtNetto = Math.Round(PositionMenge * PositionEinzelpreis, 2, MidpointRounding.ToEven),
        });

        PositionArtikelId = null;
        PositionMenge = 1;
        PositionEinzelpreis = 0;
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

    [RelayCommand]
    private async Task SpeichernAsync()
    {
        Fehlermeldung = null;
        var dto = new BelegDto
        {
            Id = Id,
            BelegTyp = _typ,
            BelegDatum = DateOnly.FromDateTime((BelegDatum ?? DateTimeOffset.Now).DateTime),
            LieferantId = LieferantId,
            Kopftext = Kopftext,
            Fusstext = Fusstext,
            ExterneReferenz = ExterneReferenz,
            Positionen = Positionen.ToList(),
            RowVersion = _rowVersion,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            Id = gespeichert.Id;
            _rowVersion = gespeichert.RowVersion;
            BelegNummer = gespeichert.BelegNummer;
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
    private void Abbrechen() => NavigiereZurListe();

    protected abstract void NavigiereZurListe();
}
```

- [ ] **Step 2: `BestellungListViewModel`**

`src/Milet.App/ViewModels/Einkauf/BestellungListViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class BestellungListViewModel : ObservableObject
{
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;

    public BestellungListViewModel(IBelegService belegService, INavigationService navigation, IDialogService dialogService)
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
        try { Belege = await _belegService.SucheAsync(BelegTyp.Bestellung, Suchtext); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message); }
        finally { LaedtGerade = false; }
    }

    [RelayCommand] private void Neu() => _navigation.Navigate<BestellungEditViewModel>(0);
    [RelayCommand] private void Bearbeiten() { if (Ausgewaehlt is { } beleg) _navigation.Navigate<BestellungEditViewModel>(beleg.Id); }

    [RelayCommand]
    private async Task LoeschenAsync()
    {
        if (Ausgewaehlt is not { } beleg) return;
        var bestaetigt = await _dialogService.BestaetigenAsync("Bestellung löschen", $"Bestellung '{beleg.BelegNummer}' wirklich löschen?");
        if (!bestaetigt) return;
        try { await _belegService.LoescheAsync(beleg.Id); Ausgewaehlt = null; await LadenAsync(); }
        catch (Exception ex) { await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message); }
    }
}
```

- [ ] **Step 3: `BestellungEditViewModel`**

`src/Milet.App/ViewModels/Einkauf/BestellungEditViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Lager;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class BestellungEditViewModel : EinkaufBelegEditViewModelBase
{
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly ILagerortService _lagerortService;

    public BestellungEditViewModel(
        IBelegService belegService, IEinkaufLookupService lookupService, IBelegUeberleitungService ueberleitungService,
        ILagerortService lagerortService, INavigationService navigation, IDialogService dialogService)
        : base(BelegTyp.Bestellung, belegService, lookupService, navigation, dialogService)
    {
        _ueberleitungService = ueberleitungService;
        _lagerortService = lagerortService;
    }

    [RelayCommand]
    private async Task UeberleitenZuWareneingangAsync()
    {
        if (Id == 0) { Fehlermeldung = "Bestellung muss erst gespeichert werden."; return; }

        var lagerorte = (await _lagerortService.SucheAsync(null)).Where(l => l.Aktiv).ToList();
        if (lagerorte.Count == 0) { Fehlermeldung = "Kein aktiver Lagerort angelegt."; return; }

        var offenePositionen = await _ueberleitungService.LadeOffenePositionenAsync(Id);
        if (offenePositionen.Count == 0) { Fehlermeldung = "Keine offenen Positionen für einen Wareneingang vorhanden."; return; }

        var dialog = new Milet.App.Views.Einkauf.WareneingangMengenDialog(offenePositionen, lagerorte) { XamlRoot = App.MainWindow.Content.XamlRoot };
        var ergebnis = await dialog.ShowAsync();
        if (ergebnis != ContentDialogResult.Primary) return;

        try
        {
            await _ueberleitungService.UeberleitenMitAuswahlAsync(Id, BelegTyp.Wareneingang, dialog.GewaehlteMengen(), dialog.AusgewaehlterLagerortId);
            Navigation.Navigate<WareneingangListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<BestellungListViewModel>();
}
```

- [ ] **Step 4: Views**

`BestellungListPage.xaml(.cs)`: analog zu `src/Milet.App/Views/Lager/LieferscheinListPage.xaml`, Unterschiede: keine Mehrfachauswahl/Sammelrechnung-Button, Titel „Bestellungen", Spalte „Lieferant" (`LieferantAnzeige`) statt „Kunde".

`BestellungEditPage.xaml(.cs)`: analog zu `src/Milet.App/Views/Verkauf/AuftragEditPage.xaml`, Unterschiede: ComboBox „Lieferant" (bindet `Lieferanten`/`LieferantId`) statt „Kunde"; Positionszeile ohne Rabatt-Feld, mit Button „EK-Preis übernehmen" (`EkPreisUebernehmenCommand`) statt „Preisvorschlag"; kein PDF-/Buchen-Button (Bestellung wird nicht „gebucht", sie geht direkt per Überleitung in den Wareneingang); Button „→ Wareneingang" (`UeberleitenZuWareneingangCommand`) statt „→ Rechnung"/„→ Lieferschein".

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: Fehler wegen fehlender `WareneingangListViewModel`/`WareneingangMengenDialog` — normal, wird in Task 12 ergänzt. (Alternativ: Task 11 und 12 in einem Durchgang bauen, falls der Zwischenstand nicht separat kompilieren soll — dann Steps 5/6 dieses Tasks ans Ende von Task 12 verschieben.)

- [ ] **Step 6: Commit**

```bash
git add src/Milet.App/ViewModels/Einkauf/EinkaufBelegEditViewModelBase.cs src/Milet.App/ViewModels/Einkauf/BestellungListViewModel.cs src/Milet.App/ViewModels/Einkauf/BestellungEditViewModel.cs src/Milet.App/Views/Einkauf/BestellungListPage.xaml src/Milet.App/Views/Einkauf/BestellungListPage.xaml.cs src/Milet.App/Views/Einkauf/BestellungEditPage.xaml src/Milet.App/Views/Einkauf/BestellungEditPage.xaml.cs
git commit -m "EinkaufBelegEditViewModelBase + Bestellung-Liste/-Editor"
```

---

### Task 12: App — Bestellung→Wareneingang (WareneingangMengenDialog) + Wareneingang-Liste/-Editor + SeriennummernErfassungDialog + Buchen

**Files:**
- Create: `src/Milet.App/Views/Einkauf/WareneingangMengenDialog.xaml(.cs)`
- Create: `src/Milet.App/Views/Einkauf/SeriennummernErfassungDialog.xaml(.cs)`
- Create: `src/Milet.App/ViewModels/Einkauf/WareneingangListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Einkauf/WareneingangEditViewModel.cs`
- Create: `src/Milet.App/Views/Einkauf/WareneingangListPage.xaml(.cs)`
- Create: `src/Milet.App/Views/Einkauf/WareneingangEditPage.xaml(.cs)`

**Interfaces:**
- Consumes: `OffenePositionDto` (Phase 3), `IWareneingangBuchenService` (Task 9), `IEinkaufLookupService` (Task 8), `IBelegUeberleitungService` (Task 7).
- Produces: kompletter Bestellung→Wareneingang-Flow inkl. Buchen — von Task 15 (Navigation/DI) konsumiert.

- [ ] **Step 1: `WareneingangMengenDialog`** (analog zu `src/Milet.App/Views/Lager/TeillieferungDialog.xaml(.cs)`, mit anderem Titel)

`src/Milet.App/Views/Einkauf/WareneingangMengenDialog.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Lager;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class WareneingangMengenZeile : ObservableObject
{
    public int PositionId { get; }
    public string Bezeichnung { get; }
    public decimal OffeneMenge { get; }

    [ObservableProperty]
    public partial decimal GewaehlteMenge { get; set; }

    public WareneingangMengenZeile(OffenePositionDto dto)
    {
        PositionId = dto.PositionId;
        Bezeichnung = dto.EinheitKuerzel is { } einheit ? $"{dto.Bezeichnung} ({einheit})" : dto.Bezeichnung;
        OffeneMenge = dto.OffeneMenge;
        GewaehlteMenge = dto.OffeneMenge;
    }
}

public sealed partial class WareneingangMengenDialog : ContentDialog
{
    public ObservableCollection<WareneingangMengenZeile> Zeilen { get; }
    public IReadOnlyList<LagerortDto> Lagerorte { get; }
    public int AusgewaehlterLagerortId { get; set; }

    public WareneingangMengenDialog(IReadOnlyList<OffenePositionDto> offenePositionen, IReadOnlyList<LagerortDto> lagerorte)
    {
        Zeilen = new ObservableCollection<WareneingangMengenZeile>(offenePositionen.Select(p => new WareneingangMengenZeile(p)));
        Lagerorte = lagerorte;
        AusgewaehlterLagerortId = lagerorte[0].Id;
        InitializeComponent();
    }

    public IReadOnlyDictionary<int, decimal> GewaehlteMengen() =>
        Zeilen.Where(z => z.GewaehlteMenge > 0).ToDictionary(z => z.PositionId, z => z.GewaehlteMenge);
}
```

`src/Milet.App/Views/Einkauf/WareneingangMengenDialog.xaml` — 1:1 wie `TeillieferungDialog.xaml`, nur `x:Class="Milet.App.Views.Einkauf.WareneingangMengenDialog"`, `Title="Wareneingang erzeugen"`, `PrimaryButtonText="Wareneingang erzeugen"`, `local:WareneingangMengenZeile` als `x:DataType`.

- [ ] **Step 2: `SeriennummernErfassungDialog`** (Erfassung NEUER Nummern, im Unterschied zur Auswahl bestehender in `SeriennummernAuswahlDialog`)

`src/Milet.App/Views/Einkauf/SeriennummernErfassungDialog.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Milet.Application.Verkauf;

namespace Milet.App.Views.Einkauf;

public sealed partial class SeriennummerErfassungZeile : ObservableObject
{
    [ObservableProperty]
    public partial string Nummer { get; set; } = string.Empty;
}

public sealed partial class SeriennummernErfassungDialog : ContentDialog
{
    public string PositionsBezeichnung { get; }
    public string BenoetigteMengeText { get; }
    public ObservableCollection<SeriennummerErfassungZeile> Zeilen { get; }

    public SeriennummernErfassungDialog(BelegPositionDto position)
    {
        // Gleiche Reihenfolge-Regel wie SeriennummernAuswahlDialog (Phase 3): Properties VOR
        // InitializeComponent() setzen, da x:Bind ohne Mode synchron innerhalb InitializeComponent() ausgewertet wird.
        PositionsBezeichnung = position.Bezeichnung;
        BenoetigteMengeText = $"Benötigt: {position.Menge} Stück";
        Zeilen = new ObservableCollection<SeriennummerErfassungZeile>(
            Enumerable.Range(0, (int)position.Menge).Select(_ => new SeriennummerErfassungZeile()));
        InitializeComponent();
    }

    /// <summary>Keine clientseitige Duplikat-/Leerprüfung — WareneingangBuchenService prüft serverseitig
    /// (exakte Anzahl, Duplikate im Artikelbestand) und liefert bei Verstoß eine verständliche Fehlermeldung.</summary>
    public IReadOnlyList<string> ErfassteNummern() => Zeilen.Select(z => z.Nummer.Trim()).Where(n => n.Length > 0).ToList();
}
```

`src/Milet.App/Views/Einkauf/SeriennummernErfassungDialog.xaml`:
```xml
<ContentDialog
    x:Class="Milet.App.Views.Einkauf.SeriennummernErfassungDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Milet.App.Views.Einkauf"
    Title="Seriennummern erfassen"
    PrimaryButtonText="Übernehmen"
    CloseButtonText="Abbrechen"
    DefaultButton="Primary">
    <StackPanel Spacing="8" MinWidth="400">
        <TextBlock Text="{x:Bind PositionsBezeichnung}" FontWeight="SemiBold" />
        <TextBlock Text="{x:Bind BenoetigteMengeText}" />
        <ItemsControl ItemsSource="{x:Bind Zeilen}">
            <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="local:SeriennummerErfassungZeile">
                    <TextBox Text="{x:Bind Nummer, Mode=TwoWay}" PlaceholderText="Seriennummer" Margin="0,0,0,4" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </StackPanel>
</ContentDialog>
```

- [ ] **Step 3: `WareneingangListViewModel`** (analog zu Task 11 Step 2, `BelegTyp.Wareneingang`, `Navigate<WareneingangEditViewModel>`, Meldungstexte „Wareneingang")

- [ ] **Step 4: `WareneingangEditViewModel`**

`src/Milet.App/ViewModels/Einkauf/WareneingangEditViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class WareneingangEditViewModel : ObservableObject, INavigationAware
{
    private readonly IBelegService _belegService;
    private readonly IWareneingangBuchenService _buchenService;
    private readonly IEinkaufLookupService _lookupService;
    private readonly IBelegUeberleitungService _ueberleitungService;
    private readonly INavigationService _navigation;

    private int _id;
    private IReadOnlyList<ArtikelEinkaufLookupDto> _artikelLookups = [];

    public WareneingangEditViewModel(
        IBelegService belegService, IWareneingangBuchenService buchenService, IEinkaufLookupService lookupService,
        IBelegUeberleitungService ueberleitungService, INavigationService navigation)
    {
        _belegService = belegService;
        _buchenService = buchenService;
        _lookupService = lookupService;
        _ueberleitungService = ueberleitungService;
        _navigation = navigation;
    }

    [ObservableProperty] public partial string BelegNummer { get; set; } = string.Empty;
    [ObservableProperty] public partial DateOnly BelegDatum { get; set; }
    [ObservableProperty] public partial string LieferantAnzeige { get; set; } = string.Empty;
    [ObservableProperty] public partial BelegStatus Status { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BelegPositionDto> Positionen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool IstBearbeitbar { get; set; }
    [ObservableProperty] public partial bool KannUeberleiten { get; set; }

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
        LieferantAnzeige = beleg.LieferantAnzeige;
        Status = beleg.Status;
        Positionen = beleg.Positionen;
        IstBearbeitbar = beleg.Status == BelegStatus.Entwurf;
        KannUeberleiten = beleg.Status == BelegStatus.Gebucht;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (_id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        var neueSeriennummernJePosition = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var position in Positionen.Where(p => p.PositionsTyp == PositionsTyp.Artikel))
        {
            var artikel = _artikelLookups.FirstOrDefault(a => a.Id == position.ArtikelId);
            if (artikel is not { HatSeriennummern: true }) continue;

            var dialog = new Milet.App.Views.Einkauf.SeriennummernErfassungDialog(position) { XamlRoot = App.MainWindow.Content.XamlRoot };
            var ergebnis = await dialog.ShowAsync();
            if (ergebnis != ContentDialogResult.Primary) return;
            neueSeriennummernJePosition[position.Id] = dialog.ErfassteNummern();
        }

        try
        {
            var gebucht = await _buchenService.BuchenAsync(_id, neueSeriennummernJePosition);
            Status = gebucht.Status;
            IstBearbeitbar = false;
            KannUeberleiten = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UeberleitenZuEingangsrechnungAsync()
    {
        if (_id == 0 || Status != BelegStatus.Gebucht) return;
        try
        {
            await _ueberleitungService.UeberleitenAsync(_id, BelegTyp.Eingangsrechnung);
            _navigation.Navigate<EingangsrechnungListViewModel>();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void Abbrechen() => _navigation.Navigate<WareneingangListViewModel>();
}
```

- [ ] **Step 5: Views**

`WareneingangListPage.xaml(.cs)`, `WareneingangEditPage.xaml(.cs)`: analog zu `src/Milet.App/Views/Lager/LieferscheinListPage.xaml`/`LieferscheinEditPage.xaml`, Unterschiede: Titel „Wareneingänge", Spalte „Lieferant" statt „Kunde", zusätzlicher Button „→ Eingangsrechnung" (`UeberleitenZuEingangsrechnungCommand`, nur sichtbar/aktiv wenn `KannUeberleiten`).

- [ ] **Step 6: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: Fehler wegen fehlender `EingangsrechnungListViewModel` — normal, wird in Task 13 ergänzt.

- [ ] **Step 7: Commit**

```bash
git add src/Milet.App/Views/Einkauf/WareneingangMengenDialog.xaml src/Milet.App/Views/Einkauf/WareneingangMengenDialog.xaml.cs src/Milet.App/Views/Einkauf/SeriennummernErfassungDialog.xaml src/Milet.App/Views/Einkauf/SeriennummernErfassungDialog.xaml.cs src/Milet.App/ViewModels/Einkauf/WareneingangListViewModel.cs src/Milet.App/ViewModels/Einkauf/WareneingangEditViewModel.cs src/Milet.App/Views/Einkauf/WareneingangListPage.xaml src/Milet.App/Views/Einkauf/WareneingangListPage.xaml.cs src/Milet.App/Views/Einkauf/WareneingangEditPage.xaml src/Milet.App/Views/Einkauf/WareneingangEditPage.xaml.cs
git commit -m "Bestellung→Wareneingang: Mengen-/Seriennummern-Erfassungsdialoge + Wareneingang-Liste/-Editor + Buchen"
```

---

### Task 13: App — Wareneingang→Eingangsrechnung + Eingangsrechnung-Liste/-Editor + Buchen mit Abweichungswarnung

**Files:**
- Create: `src/Milet.App/ViewModels/Einkauf/EingangsrechnungListViewModel.cs`
- Create: `src/Milet.App/ViewModels/Einkauf/EingangsrechnungEditViewModel.cs`
- Create: `src/Milet.App/Views/Einkauf/EingangsrechnungListPage.xaml(.cs)`
- Create: `src/Milet.App/Views/Einkauf/EingangsrechnungEditPage.xaml(.cs)`

**Interfaces:**
- Consumes: `EinkaufBelegEditViewModelBase` (Task 11), `IEingangsrechnungBuchenService` (Task 10).
- Produces: kompletten Wareneingang→Eingangsrechnung→Buchen-Flow inkl. sichtbarer Abweichungswarnung — von Task 15 (Navigation/DI) konsumiert.

- [ ] **Step 1: `EingangsrechnungListViewModel`** (analog zu Task 11 Step 2, `BelegTyp.Eingangsrechnung`, `Navigate<EingangsrechnungEditViewModel>`)

- [ ] **Step 2: `EingangsrechnungEditViewModel`**

`src/Milet.App/ViewModels/Einkauf/EingangsrechnungEditViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class EingangsrechnungEditViewModel : EinkaufBelegEditViewModelBase
{
    private readonly IEingangsrechnungBuchenService _buchenService;

    public EingangsrechnungEditViewModel(
        IBelegService belegService, IEinkaufLookupService lookupService,
        IEingangsrechnungBuchenService buchenService, INavigationService navigation, IDialogService dialogService)
        : base(BelegTyp.Eingangsrechnung, belegService, lookupService, navigation, dialogService)
    {
        _buchenService = buchenService;
    }

    [RelayCommand]
    private async Task BuchenAsync()
    {
        if (Id == 0 || Status != BelegStatus.Entwurf) return;
        Fehlermeldung = null;

        try
        {
            var ergebnis = await _buchenService.BuchenAsync(Id);
            Status = ergebnis.Beleg.Status;
            IstBearbeitbar = false;

            if (ergebnis.BetragWeichtAb)
            {
                // Der Kreditor-OP ist zu diesem Zeitpunkt bereits angelegt (Soft-Warnung, siehe
                // Architektur-Entscheidung 7) — der Dialog informiert nur, blockiert nichts mehr.
                await DialogService.ZeigeFehlerAsync(
                    "Betragsabweichung zum Wareneingang",
                    $"Rechnungsbetrag ({ergebnis.Beleg.SummeBrutto:C}) weicht vom Wareneingang ({ergebnis.ErwarteterBetrag:C}) um {ergebnis.AbweichungBetrag:C} ab. Der Offene Posten wurde trotzdem angelegt.");
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void NavigiereZurListe() => Navigation.Navigate<EingangsrechnungListViewModel>();
}
```

- [ ] **Step 3: Views**

`EingangsrechnungListPage.xaml(.cs)`: analog zu Task 11 Step 4, Titel „Eingangsrechnungen".

`EingangsrechnungEditPage.xaml(.cs)`: analog zu `BestellungEditPage.xaml` (Task 11), Unterschiede: zusätzliches Textfeld „Rechnungsnummer des Lieferanten" (bindet `ExterneReferenz`, TwoWay), Button „Buchen" (`BuchenCommand`) statt „→ Wareneingang", `IsEnabled` des Editierbereichs an `IstBearbeitbar` wie bei `RechnungEditPage`/`LieferscheinEditPage` (nur der editierbare Bereich in einen `ContentControl`-Wrapper legen — siehe der in Phase 2 dokumentierte Fund zu `IsEnabled` auf `Panel`-Klassen).

- [ ] **Step 4: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: Fehler wegen fehlender `BestellVorschlagViewModel` (im Bestellung-Menü noch nicht referenziert an dieser Stelle, sollte eigentlich sauber sein) — falls Task 11/12/13 vollständig sind, sollte hier **0 Fehler** stehen, außer noch fehlende Navigation-Registrierung/DI aus Task 15 wird bereits vom Compiler verlangt (nicht der Fall — Navigation/DI sind Laufzeit-Konfiguration, kein Compile-Fehler). Bei 0 Fehlern: weiter zu Task 14.

- [ ] **Step 5: Commit**

```bash
git add src/Milet.App/ViewModels/Einkauf/EingangsrechnungListViewModel.cs src/Milet.App/ViewModels/Einkauf/EingangsrechnungEditViewModel.cs src/Milet.App/Views/Einkauf/EingangsrechnungListPage.xaml src/Milet.App/Views/Einkauf/EingangsrechnungListPage.xaml.cs src/Milet.App/Views/Einkauf/EingangsrechnungEditPage.xaml src/Milet.App/Views/Einkauf/EingangsrechnungEditPage.xaml.cs
git commit -m "Wareneingang→Eingangsrechnung: Eingangsrechnung-Liste/-Editor + Buchen mit Abweichungs-Soft-Warnung"
```

---

### Task 14: App — Bestellvorschlag-Seite

**Files:**
- Create: `src/Milet.App/ViewModels/Einkauf/BestellVorschlagViewModel.cs`
- Create: `src/Milet.App/Views/Einkauf/BestellVorschlagPage.xaml(.cs)`

**Interfaces:**
- Consumes: `IBestellVorschlagService` (Task 8), `IEinkaufLookupService` (Task 8), `IBelegService` (Task 6).
- Produces: „Bestellvorschlag erzeugt Bestellung" — von Task 15 (Navigation/DI) konsumiert.

- [ ] **Step 1: `BestellVorschlagViewModel`**

`src/Milet.App/ViewModels/Einkauf/BestellVorschlagViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Einkauf;
using Milet.Application.Verkauf;
using Milet.Domain.Entities.Verkauf;

namespace Milet.App.ViewModels.Einkauf;

public sealed partial class BestellVorschlagZeile : ObservableObject
{
    public int ArtikelId { get; }
    public string Artikelnummer { get; }
    public string Bezeichnung { get; }
    public decimal AktuellerBestand { get; }
    public decimal Mindestbestand { get; }
    public decimal Einkaufspreis { get; }
    public int MwStSatzId { get; }
    public decimal MwStSatzWert { get; }
    public int? SteuerSchluessel { get; }
    public string? EinheitKuerzel { get; }

    [ObservableProperty] public partial bool Ausgewaehlt { get; set; } = true;
    [ObservableProperty] public partial decimal Menge { get; set; }

    public BestellVorschlagZeile(BestellVorschlagPositionDto dto)
    {
        ArtikelId = dto.ArtikelId;
        Artikelnummer = dto.Artikelnummer;
        Bezeichnung = dto.Bezeichnung;
        AktuellerBestand = dto.AktuellerBestand;
        Mindestbestand = dto.Mindestbestand;
        Einkaufspreis = dto.Einkaufspreis;
        MwStSatzId = dto.MwStSatzId;
        MwStSatzWert = dto.MwStSatzWert;
        SteuerSchluessel = dto.SteuerSchluessel;
        EinheitKuerzel = dto.EinheitKuerzel;
        Menge = dto.VorschlagsMenge;
    }
}

public sealed partial class BestellVorschlagViewModel : ObservableObject
{
    private readonly IBestellVorschlagService _vorschlagService;
    private readonly IEinkaufLookupService _lookupService;
    private readonly IBelegService _belegService;
    private readonly INavigationService _navigation;

    public BestellVorschlagViewModel(
        IBestellVorschlagService vorschlagService, IEinkaufLookupService lookupService, IBelegService belegService,
        INavigationService navigation)
    {
        _vorschlagService = vorschlagService;
        _lookupService = lookupService;
        _belegService = belegService;
        _navigation = navigation;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<LieferantEinkaufLookupDto> Lieferanten { get; set; } = [];
    [ObservableProperty] public partial int LieferantId { get; set; }
    [ObservableProperty] public partial ObservableCollection<BestellVorschlagZeile> Zeilen { get; set; } = [];
    [ObservableProperty] public partial string? Fehlermeldung { get; set; }
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            var lookups = await _lookupService.LadeLookupsAsync();
            Lieferanten = lookups.Lieferanten;
            var vorschlaege = await _vorschlagService.ErmittleVorschlaegeAsync();
            Zeilen = new ObservableCollection<BestellVorschlagZeile>(vorschlaege.Select(v => new BestellVorschlagZeile(v)));
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            LaedtGerade = false;
        }
    }

    [RelayCommand]
    private async Task BestellungErzeugenAsync()
    {
        Fehlermeldung = null;
        if (LieferantId == 0)
        {
            // Kein Hauptlieferant am Artikel hinterlegt (siehe Phase-4-Plan, Architektur-Entscheidung 3) —
            // manuelle Auswahl je Bestellvorschlag-Lauf ist die bewusste v1-Vereinfachung.
            Fehlermeldung = "Lieferant auswählen.";
            return;
        }

        var ausgewaehlt = Zeilen.Where(z => z.Ausgewaehlt && z.Menge > 0).ToList();
        if (ausgewaehlt.Count == 0) { Fehlermeldung = "Mindestens eine Position auswählen."; return; }

        var positionen = ausgewaehlt.Select((z, i) => new BelegPositionDto
        {
            PositionsNr = i + 1,
            PositionsTyp = PositionsTyp.Artikel,
            ArtikelId = z.ArtikelId,
            Bezeichnung = z.Bezeichnung,
            EinheitKuerzel = z.EinheitKuerzel,
            Menge = z.Menge,
            Einzelpreis = z.Einkaufspreis,
            MwStSatzId = z.MwStSatzId,
            MwStSatzWert = z.MwStSatzWert,
            SteuerSchluessel = z.SteuerSchluessel,
            GesamtNetto = Math.Round(z.Menge * z.Einkaufspreis, 2, MidpointRounding.ToEven),
        }).ToList();

        var dto = new BelegDto
        {
            BelegTyp = BelegTyp.Bestellung,
            BelegDatum = DateOnly.FromDateTime(DateTime.Today),
            LieferantId = LieferantId,
            Positionen = positionen,
        };

        try
        {
            var gespeichert = await _belegService.SpeichereAsync(dto);
            _navigation.Navigate<BestellungEditViewModel>(gespeichert.Id);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
}
```

- [ ] **Step 2: `BestellVorschlagPage.xaml(.cs)`**

Struktur: ComboBox „Lieferant" (`Lieferanten`/`LieferantId`) oben, darunter eine `ListView`/`ItemsControl` über `Zeilen` mit `CheckBox` (`Ausgewaehlt`), Artikelnummer/Bezeichnung/aktueller Bestand/Mindestbestand als reine Anzeige, `NumberBox` für `Menge` (editierbar, `DecimalToDoubleConverter` wie an anderen Stellen), Button „Bestellung erzeugen" (`BestellungErzeugenCommand`) unten. Layout analog zum bereits bestehenden `KleinstammPage`-Muster (Liste + Formularbereich in einem Grid), kein neuer Converter nötig.

- [ ] **Step 3: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/Milet.App/ViewModels/Einkauf/BestellVorschlagViewModel.cs src/Milet.App/Views/Einkauf/BestellVorschlagPage.xaml src/Milet.App/Views/Einkauf/BestellVorschlagPage.xaml.cs
git commit -m "Bestellvorschlag-Seite (Mindestbestand-Unterschreitung, manuelle Lieferantenauswahl)"
```

---

### Task 15: App — Navigation aktivieren (Einkauf-Menü) + DI-Registrierungen

**Files:**
- Modify: `src/Milet.App/Shell/ShellPage.xaml`
- Modify: `src/Milet.App/Shell/ShellPage.xaml.cs`
- Modify: `src/Milet.App/App.xaml.cs`
- Modify: `src/Milet.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: alle Einkauf-ViewModels/-Services aus Task 6–14.
- Produces: vollständig navigierbares „Einkauf"-Menü + funktionierende DI — Voraussetzung für Task 17 (Verifikation).

- [ ] **Step 1: `ShellPage.xaml` — Einkauf-Menü aktivieren**

Modify `src/Milet.App/Shell/ShellPage.xaml` — die Zeile
```xml
            <NavigationViewItem Content="Einkauf" Tag="einkauf" Icon="Import" IsEnabled="False" />
```
ersetzen durch:
```xml
            <NavigationViewItem Content="Einkauf" Tag="einkauf" Icon="Import">
                <NavigationViewItem.MenuItems>
                    <NavigationViewItem Content="Bestellvorschlag" Tag="bestellvorschlag" Icon="Add" />
                    <NavigationViewItem Content="Bestellungen" Tag="bestellungen" Icon="Bookmarks" />
                    <NavigationViewItem Content="Wareneingänge" Tag="wareneingaenge" Icon="Package" />
                    <NavigationViewItem Content="Eingangsrechnungen" Tag="eingangsrechnungen" Icon="PostUpdate" />
                </NavigationViewItem.MenuItems>
            </NavigationViewItem>
```

- [ ] **Step 2: `ShellPage.xaml.cs` — Registrierung + Navigation-Switch**

Modify `src/Milet.App/Shell/ShellPage.xaml.cs` — `using Milet.App.ViewModels.Einkauf;` und `using Milet.App.Views.Einkauf;` ergänzen; nach den Lager-Registrierungen einfügen:
```csharp
        _navigation.Register<BestellVorschlagViewModel, BestellVorschlagPage>();
        _navigation.Register<BestellungListViewModel, BestellungListPage>();
        _navigation.Register<BestellungEditViewModel, BestellungEditPage>();
        _navigation.Register<WareneingangListViewModel, WareneingangListPage>();
        _navigation.Register<WareneingangEditViewModel, WareneingangEditPage>();
        _navigation.Register<EingangsrechnungListViewModel, EingangsrechnungListPage>();
        _navigation.Register<EingangsrechnungEditViewModel, EingangsrechnungEditPage>();
```
und im `switch` von `NavView_SelectionChanged` ergänzen:
```csharp
            case "bestellvorschlag":
                _navigation.Navigate<BestellVorschlagViewModel>();
                break;
            case "bestellungen":
                _navigation.Navigate<BestellungListViewModel>();
                break;
            case "wareneingaenge":
                _navigation.Navigate<WareneingangListViewModel>();
                break;
            case "eingangsrechnungen":
                _navigation.Navigate<EingangsrechnungListViewModel>();
                break;
```

- [ ] **Step 3: `App.xaml.cs` — ViewModel-DI**

Modify `src/Milet.App/App.xaml.cs` — nach den bestehenden `AddTransient<InventurEditViewModel>();` einfügen (und `using Milet.App.ViewModels.Einkauf;` am Dateikopf ergänzen):
```csharp
        builder.Services.AddTransient<BestellVorschlagViewModel>();
        builder.Services.AddTransient<BestellungListViewModel>();
        builder.Services.AddTransient<BestellungEditViewModel>();
        builder.Services.AddTransient<WareneingangListViewModel>();
        builder.Services.AddTransient<WareneingangEditViewModel>();
        builder.Services.AddTransient<EingangsrechnungListViewModel>();
        builder.Services.AddTransient<EingangsrechnungEditViewModel>();
```

- [ ] **Step 4: `DependencyInjection.cs` — Infrastructure-Services registrieren**

Modify `src/Milet.Infrastructure/DependencyInjection.cs` — `using Milet.Application.Einkauf;` ergänzen; nach `services.AddScoped<IInventurService, InventurService>();` einfügen:
```csharp
        services.AddScoped<IEinkaufLookupService, EinkaufLookupService>();
        services.AddScoped<IBestellVorschlagService, BestellVorschlagService>();
        services.AddScoped<IWareneingangBuchenService, WareneingangBuchenService>();
        services.AddScoped<IEingangsrechnungBuchenService, EingangsrechnungBuchenService>();
```

- [ ] **Step 5: Build prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64`
Expected: 0 Fehler, 0 neue Warnungen außer ggf. bekannten XAML-Binding-Hinweisen (wie `WMC1506` aus Phase 3).

- [ ] **Step 6: Commit**

```bash
git add src/Milet.App/Shell/ShellPage.xaml src/Milet.App/Shell/ShellPage.xaml.cs src/Milet.App/App.xaml.cs src/Milet.Infrastructure/DependencyInjection.cs
git commit -m "Einkauf-Menü aktivieren + Navigation-/DI-Registrierungen aller neuen ViewModels/Services"
```

---

### Task 16: Tests — Domain-Ergänzung (BelegTypErweiterung; BelegValidator bereits in Task 3 erledigt)

**Files:**
- Create: `tests/Milet.Domain.Tests/BelegTypErweiterungTests.cs`

**Interfaces:**
- Consumes: `BelegTypErweiterung.IstEinkaufsBeleg` (Task 1).

- [ ] **Step 1: Test**

`tests/Milet.Domain.Tests/BelegTypErweiterungTests.cs`:
```csharp
using Milet.Domain.Entities.Verkauf;
using Xunit;

namespace Milet.Domain.Tests;

public class BelegTypErweiterungTests
{
    [Theory]
    [InlineData(BelegTyp.Angebot, false)]
    [InlineData(BelegTyp.Auftrag, false)]
    [InlineData(BelegTyp.Rechnung, false)]
    [InlineData(BelegTyp.Lieferschein, false)]
    [InlineData(BelegTyp.Bestellung, true)]
    [InlineData(BelegTyp.Wareneingang, true)]
    [InlineData(BelegTyp.Eingangsrechnung, true)]
    public void IstEinkaufsBeleg_KorrekteKlassifizierung(BelegTyp typ, bool erwartet) =>
        Assert.Equal(erwartet, typ.IstEinkaufsBeleg());
}
```

- [ ] **Step 2: Tests laufen lassen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj`
Expected: alle PASS (bestehende 14 + 7 neue = 21).

- [ ] **Step 3: Commit**

```bash
git add tests/Milet.Domain.Tests/BelegTypErweiterungTests.cs
git commit -m "Test: BelegTypErweiterung.IstEinkaufsBeleg klassifiziert alle 7 Belegtypen korrekt"
```

---

### Task 17: Verifikation — vollständiger Build/Test-Durchlauf, manueller Smoke-Test-Katalog, STATUS.md-Update

**Files:**
- Modify: `STATUS.md`

**Interfaces:**
- Consumes: nichts Neues — reiner Verifikationsschritt über alle Tasks 1–16.

- [ ] **Step 1: Build — alle Projekte**

Run:
```bash
"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.App/Milet.App.csproj -p:Platform=x64
"$USERPROFILE/.dotnet/dotnet.exe" build src/Milet.Tools.Migrator/Milet.Tools.Migrator.csproj
```
Expected: 0 Fehler in beiden.

- [ ] **Step 2: Tests — jedes Projekt einzeln (MTP-Modus)**

Run:
```bash
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.Application.Tests/Milet.Application.Tests.csproj
"$USERPROFILE/.dotnet/dotnet.exe" test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj
```
Expected: Domain 21/21, Application 21/21, IntegrationTests: alle neuen Tests (BestellVorschlagServiceTests, WareneingangBuchenServiceTests, EingangsrechnungBuchenServiceTests) laufen grün oder sauber übersprungen (kein Docker), 0 Fails.

- [ ] **Step 3: Migration anwenden + Seed prüfen**

Run: `"$USERPROFILE/.dotnet/dotnet.exe" run --project src/Milet.Tools.Migrator` (bzw. übliches Migrator-Kommando).
Erwartet: Migration `EinkaufBestellungWareneingang` wird angewendet, Seed ergänzt Nummernkreise `WE`/`ER` (Codes `BE`/übrige bleiben unangetastet). Per `sqlcmd` gegenprüfen: `SELECT Code, NaechsteNummer FROM Nummernkreise WHERE Code IN ('BE','WE','ER');` liefert 3 Zeilen mit `NaechsteNummer = 1`.

- [ ] **Step 4: Manueller UI-Smoke-Test (End-to-End, analog zum Muster aus Phase 1–3-Abnahmen)**

Folgende Teilschritte im laufenden UI durchklicken und jeweils per `sqlcmd` gegen die DB verifizieren (falls diese Verifikation von einem headless Agenten ohne Display-Zugriff durchgeführt wird, ist dieser Schritt — wie bereits bei Phase 3 dokumentiert — als offen zu markieren, nicht als erledigt zu verbuchen):

1. Lieferant anlegen (falls noch keiner existiert) mit Zahlungsbedingung.
2. Artikel mit `Mindestbestand` gesetzt und aktuellem Bestand darunter (z. B. per Bestandskorrektur aus Phase 3 auf 3 Stück setzen, `Mindestbestand = 10`).
3. „Einkauf → Bestellvorschlag" öffnen: Artikel erscheint in der Liste mit `VorschlagsMenge = 7`. Lieferant auswählen, Position auswählen, „Bestellung erzeugen" — landet auf `BestellungEditPage` mit Nummer `BE-2026-000x`.
4. Bestellung „→ Wareneingang": Dialog zeigt offene Menge 7, Lagerort wählen, „Wareneingang erzeugen" — landet auf `WareneingangListPage`, neuer Wareneingang `WE-2026-000x` mit Status Entwurf.
5. Wareneingang öffnen, „Buchen": Bestand des Artikels erhöht sich um 7 (per `sqlcmd` gegen `ArtikelBestaende` prüfen), `Lagerbewegungen` bekommt eine neue Zeile mit `Typ = 3` (Wareneingang). Status wird Gebucht.
6. Falls ein serialisierter Artikel Teil der Bestellung war: beim Buchen erscheint der `SeriennummernErfassungDialog`, N Nummern eintragen, Übernehmen — `Seriennummern`-Tabelle bekommt N neue Zeilen mit `Status = AufLager`.
7. Wareneingang „→ Eingangsrechnung": neue Eingangsrechnung `ER-2026-000x` mit denselben Positionen/Preisen, Status Entwurf.
8. Eingangsrechnung öffnen, optional „Rechnungsnummer des Lieferanten" eintragen, „Buchen": Kreditor-OP entsteht (`sqlcmd` gegen `OffenePosten` prüfen: `Typ = 1`, `LieferantId` gesetzt, `KundeId = NULL`, `Betrag = OffenerBetrag = SummeBrutto`), keine Abweichungswarnung (Beträge identisch zum Wareneingang).
9. **Abweichungsfall gezielt provozieren:** zweite Bestellung→Wareneingang→Eingangsrechnung durchlaufen, aber vor dem Buchen der Eingangsrechnung den Einzelpreis einer Position im Editor ändern und speichern — beim Buchen erscheint der Abweichungs-Dialog mit korrektem Differenzbetrag, der OP wird trotzdem angelegt (Soft-Warnung, kein Blocker).
10. Negativ-Check: Bestellvorschlag ohne ausgewählten Lieferanten „Bestellung erzeugen" klicken → Fehlermeldung „Lieferant auswählen.", kein Absturz, keine leere Bestellung in der DB.

- [ ] **Step 5: `STATUS.md` aktualisieren**

Modify `STATUS.md`: neuen Abschnitt „### Phase 4 — Einkauf" nach dem Phase-3-Abschnitt einfügen, analog zum bestehenden Berichtsformat (implementierte Domain-/Application-/Infrastructure-/App-Bausteine auflisten, Testergebnisse, gefundene Bugs falls welche auftreten, offene Punkte — insbesondere falls Schritt 4 mangels Display-Zugriff nicht durchführbar war, das explizit als offen markieren wie bei Phase 3). Abschnitt „Offen" um „Phasen 5–7 (Finanzen+Mail, DATEV+Reporting, Admin) — noch nicht begonnen" aktualisieren (Phase 4 aus der Liste entfernen, sofern Schritt 4 erledigt wurde).

- [ ] **Step 6: Commit**

```bash
git add STATUS.md
git commit -m "Phase 4 (Einkauf) abgeschlossen: Build/Tests grün, STATUS.md aktualisiert"
```

---

### Critical Files for Implementation

- `src/Milet.Domain/Entities/Verkauf/Beleg.cs` — Partei-Erweiterung (KundeId nullable, LieferantId), Herzstück der Einkaufsintegration ins bestehende TPH-Modell
- `src/Milet.Infrastructure/Services/BelegService.cs` — generalisierter Kunde/Lieferant-Zweig beim Speichern, trägt die gesamte Einkaufs-CRUD-Logik
- `src/Milet.Infrastructure/Services/BelegUeberleitungService.cs` — Bestellung→Wareneingang→Eingangsrechnung-Kette, wiederverwendet die bestehende Teilmengen-/Offene-Mengen-Logik
- `src/Milet.Infrastructure/Services/WareneingangBuchenService.cs` — Bestandszugang + Seriennummern-Neuanlage in einer Transaktion
- `src/Milet.Infrastructure/Services/EingangsrechnungBuchenService.cs` — Kreditor-OP-Anlage + Betrags-Abweichungs-Soft-Warnung
- `src/Milet.Infrastructure/Persistence/Configurations/BelegConfiguration.cs` — CHECK-Constraint und Discriminator-Erweiterung, Basis für die Migration
- `src/Milet.App/ViewModels/Einkauf/EinkaufBelegEditViewModelBase.cs` — UI-Basis für alle drei neuen Editoren
