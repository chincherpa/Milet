# Milet — Projektstatus

Stand: 2026-08-31. Architekturplan: `PLAN.md`. Phase-9-Implementierungsplan (Lückenschluss — Storno/
Gutschrift, Ledger-Nachvollziehbarkeit, Finanz-/Admin-Härtung, Parallelitäts-Race; Block 9a
Build-/Testnachweis abgeschlossen, Rest offen): `docs/superpowers/plans/2026-08-31-luecken-schliessen.md`.
Phase-2-Implementierungsplan: `docs/superpowers/plans/2026-08-25-phase2-verkauf-pdf.md`. Phase-3-Implementierungsplan (umgesetzt): `docs/superpowers/plans/2026-08-25-phase3-lager-lieferschein.md`. Ein früherer, nicht umgesetzter Planungsentwurf liegt zusätzlich unter `docs/superpowers/plans/2026-08-26-phase3-lager-lieferschein.md` — dessen technische Befunde (READ-COMMITTED-Race, Nummernkreis-Seed) sind unter „Bekannte Risiken" übernommen. Phase-4-Implementierungsplan (umgesetzt, manueller UI-Smoke-Test noch ausstehend): `docs/superpowers/plans/2026-08-26-phase4-einkauf.md`. Phase-5-Implementierungsplan (umgesetzt, Backend-Build/-Tests real grün, WinUI komplett unverifiziert — kein Windows in der Umsetzungssession): `docs/superpowers/plans/2026-08-27-phase5-finanzen-mail.md`. Phase-6-Implementierungsplan (umgesetzt, Backend-Build/-Tests/Integrationstests/Migration real gegen containerisierten SQL Server verifiziert, WinUI unverifiziert — kein Windows in der Umsetzungssession): `docs/superpowers/plans/2026-08-27-phase6-datev-reporting.md`. Phase 7 (Admin+Härtung, umgesetzt nach `PLAN.md` ohne separaten Implementierungsplan, Backend-Build/-Tests/Integrationstests/Migration ebenfalls real gegen containerisierten SQL Server verifiziert, WinUI unverifiziert — kein Windows in der Umsetzungssession): Details im Phase-7-Abschnitt unten, Deployment-Story unter `docs/deployment.md`. Phase-8-Implementierungsplan (Gärtnerei/Kulturführung, umgesetzt, Backend-Build/-Tests/Integrationstests/Migration real gegen containerisierten SQL Server verifiziert — inkl. Migration gegen eine Datenbank mit vor-Phase-8-Bestandsdaten —, WinUI unverifiziert — kein Windows in der Umsetzungssession): `docs/superpowers/plans/2026-08-30-phase8-gaertnerei-kultur.md`.

## Erledigt

### Umgebung
- .NET 10 SDK (10.0.400) user-lokal installiert, `dotnet-ef` global installiert
- nuget.org als Paketquelle eingerichtet
- SQL-Zugriff: LocalDB `(localdb)\MSSQLLocalDB`, DB "Milet" (kein Docker/SQL Server auf dieser Maschine — Details in Memory `milet-dev-umgebung`)

### Phase 0 — Scaffold ✅ vollständig verifiziert
- Solution (`Milet.slnx`) mit 5 Projekten (Domain/Application/Infrastructure/App/Tools.Migrator) + 3 Testprojekten
- `Directory.Build.props` / `Directory.Packages.props` (Central Package Management)
- WinUI-3-Shell mit NavigationView, DashboardPage, generischer `NavigationService`
- `MiletDbContext` + `DesignTimeDbContextFactory` (dotnet-ef läuft ohne WinUI als Startprojekt)
- Tests laufen im MTP-Modus (`global.json` → `test.runner`), da .NET 10 VSTest-Weg für `dotnet test` abgeschafft hat
- **Verifiziert:** Build (App x64 + alle Tests), Migration angewendet, App-Fenster startet und schließt sauber

### Phase 1 — Stammdaten (Backend fertig, UI teilweise)
**Domain:**
- Entities: `Kunde`, `Lieferant`, `Artikel`, `Einheit`, `MwStSatz`, `Zahlungsbedingung`, `Versandart`, `Preisliste`, `ArtikelPreis`, `Nummernkreis`
- `AuditableEntity`, `IHasRowVersion`, `Adresse` (Value Object)
- `PreisfindungService` (reine Domain-Logik) + 8 Unit-Tests (Staffelpreise, Kanten, Fallback) — **grün**

**Application:**
- DTOs, Service-Interfaces (`IKundenService`, `ILieferantenService`, `IArtikelService`, `IStammdatenLookupService`)
- FluentValidation-Validatoren (Kunde/Lieferant/Artikel/Adresse) + 9 Unit-Tests — **grün**
- `ICurrentUserService`, `INumberRangeService` (Abstractions), `ConcurrencyConflictException`, `NotFoundException`

**Infrastructure:**
- EF-Configurations für alle Stammdaten-Entities (Owned-Type Adresse, RowVersion, Precision)
- `AuditSaveChangesInterceptor`, `SystemCurrentUserService` (Platzhalter bis Login in Phase 7)
- `NumberRangeService` — atomares `UPDATE ... OUTPUT` über TOP(1)-CTE (Race-Bug bei gleichzeitig existierendem jahresbezogenem + jahreslosem Kreis gefunden und behoben)
- `KundenService`, `LieferantenService`, `ArtikelService`, `StammdatenLookupService` — vollständig implementiert (Suche, Laden, Speichern mit Validierung + Concurrency-Übersetzung, Löschen)
- Seed-Daten (Einheiten, MwSt-Sätze, Zahlungsbedingungen, 9 Nummernkreise) — **angewendet auf LocalDB, verifiziert per sqlcmd**
- Migration `InitialCreate` neu erzeugt und angewendet
- Integrationstest `NumberRangeServiceTests` (Testcontainers, parallele Vergabe → eindeutige Nummern) — **kompiliert, läuft, übersprungen mangels Docker auf dieser Maschine (sauberer Skip, kein Fail)**

**App (WinUI):**
- `IDialogService`/`DialogService` (Fehler-/Bestätigungsdialoge), `INavigationAware`-Pattern für Navigationsparameter
- Kunden: `KundenListViewModel` + `KundenListPage.xaml` + `KundeEditViewModel` + `KundeEditPage.xaml` (Formular, Validierungsfehler-Anzeige, Concurrency-Dialog)
- Lieferanten: `LieferantenListViewModel`/`LieferantEditViewModel` + `LieferantenListPage.xaml`/`LieferantEditPage.xaml`
- Artikel: `ArtikelListViewModel`/`ArtikelEditViewModel` + `ArtikelListPage.xaml`/`ArtikelEditPage.xaml`
- Alle 6 Seiten in `ShellPage.xaml.cs` beim `NavigationService` registriert; `ShellPage.xaml` NavigationView hat jetzt Untermenü "Stammdaten" (Kunden/Lieferanten/Artikel) verdrahtet über `NavView_SelectionChanged`
- Bug gefunden+gefixt: `NumberBox.Value` ist `double`, ViewModel-Properties sind `decimal`/`decimal?` (Geldbeträge) → x:Bind TwoWay schlug beim Build fehl (WMC1121). Neuer `DecimalToDoubleConverter` (in `App.xaml` als Resource registriert) angewendet auf `KundeEditPage.RabattProzent`, `ArtikelEditPage.Einkaufspreis/Listenpreis/Mindestbestand`
- DI-Registrierungen in `App.xaml.cs` bereits vorhanden
- **Verifiziert:** `dotnet build` für App (win-x64) + alle 3 Testprojekte einzeln (MTP) grün (Domain 8/8, Application 9/9, Integration 1/1 + 2 Docker-Skips), App-Start manuell geprüft (Fenstertitel "Milet Warenwirtschaft" erscheint, Prozess reagiert)
- **Hinweis Build-Tooling:** `dotnet` im PATH zeigt auf leere Install unter `C:\Program Files\dotnet`; funktionierender SDK liegt unter `%USERPROFILE%\.dotnet\dotnet.exe` — diesen Pfad explizit nutzen. `dotnet test` mit mehreren Projekten gleichzeitig (MTP-Modus) lief hier auf "keine Tests gefunden" — pro Testprojekt einzeln aufrufen.

**Kleinstamm-Settings-UI (Phase 1, jetzt fertig):**
- Application: `EinheitDto`/`MwStSatzDto`/`ZahlungsbedingungDto`/`VersandartDto`/`PreislisteDto` + `IEinheitenService`/`IMwStSaetzeService`/`IZahlungsbedingungenService`/`IVersandartenService`/`IPreislistenService` (Liste/Speichere/Lösche) + Validatoren
- Infrastructure: `KleinstammServices.cs` (alle 5 Implementierungen), `ConcurrencyHelper.SaveChangesDeletingAsync` übersetzt FK-Konflikte beim Löschen in verständliche Meldung (diese Entities haben kein RowVersion → keine Concurrency-Behandlung nötig)
- App: eine Seite `KleinstammPage.xaml` mit Pivot (5 Tabs, Master-Detail Liste+Formular statt eigener List/Edit-Seiten je Entity — bewusst kompakter gehalten für "einfache" Settings-Masken), `KleinstammViewModel` (ein VM, 5 Abschnitte), neuer Menüpunkt "Einstellungen" unter Stammdaten
- Neue Converter: `NullableInt32ToDoubleConverter`, `DateOnlyToDateTimeOffsetConverter` (gleiche WMC1121-Klasse Bug wie `decimal`/`double` betraf auch `int?` bei NumberBox und `DateOnly` bei DatePicker/CalendarDatePicker)
- Layout-Bug gefixt: erste Spalte der Pivot-Tabs hatte `Width="*"` → Liste füllte gesamte Breite, Formular klebte am rechten Fensterrand mit riesiger Lücke dazwischen. Grid auf 3 Spalten umgestellt (`380` Liste / `360` Formular / `*` Spacer) in allen 5 Tabs.
- **Verifiziert:** Build + Application-/Domain-Tests grün, App startet clean, Layout per Screenshot geprüft

**Staffelpreise (ArtikelPreis) je Preisliste (2026-08-25, jetzt fertig):**
- Application: `ArtikelPreisDto` + `IArtikelPreiseService` (Liste je Preisliste, Speichern, Löschen) + `ArtikelPreisValidator` (AbMenge > 0, Preis ≥ 0, Artikel/Preisliste erforderlich)
- Infrastructure: `ArtikelPreiseService` in `KleinstammServices.cs`, DI-Registrierung ergänzt
- App: dritte Spalte im "Preislisten"-Tab von `KleinstammPage.xaml` — Liste der Staffelpreise der gewählten Preisliste + Formular (Artikel-ComboBox befüllt über `IArtikelService.SucheAsync(null)`, AbMenge/Preis als `NumberBox` mit `DecimalToDoubleConverter`); `KleinstammViewModel` lädt Staffelpreise neu, sobald eine andere Preisliste ausgewählt wird; Speichern ohne zuvor gespeicherte Preisliste liefert Fehlermeldung statt Absturz
- **Verifiziert:** Build (App x64) + Application-/Domain-Tests grün, App läuft (PID über `Milet.App.exe` gestartet)

### Phase-1-Abnahme — UI live durchgetestet (2026-08-25) ✅
Per UI-Automation (Windows UIAutomation über PowerShell, da kein dediziertes WinUI-Testtool verfügbar) end-to-end gegen laufende App + LocalDB verifiziert, nicht nur durchgeklickt sondern per `sqlcmd` gegen die DB gegengeprüft:
- **Kunden:** Suche (Filter funktioniert, Treffer korrekt eingeschränkt), Neu+Speichern (Nummernvergabe KD-10001→KD-10003, Lücken durch gelöschte Testdaten sind normales Nummernkreis-Verhalten), Bearbeiten, Löschen mit Bestätigungsdialog — alles grün.
- **Concurrency-Dialog:** Datensatz während offenem Edit per direktem SQL-UPDATE "extern" geändert → App erkennt `DbUpdateConcurrencyException`, zeigt "Datensatz geändert... Neu laden?"; **Ja** lädt Server-Stand nach (lokale Änderung verworfen), **Abbrechen** verwirft den Dialog ohne Neuladen — beide Pfade funktionieren wie designed.
- **Artikel:** Neu+Speichern, Artikelnummer-Autovergabe (ART-01001…) funktioniert.
- **Staffelpreise-UI (aus letzter Session):** Preisliste anlegen → in Liste auswählen → Staffelpreis mit Artikel-Lookup/AbMenge/Preis anlegen → Liste aktualisiert sich → Löschen mit Bestätigung — komplett funktionsfähig (nach Bugfix, s. unten).
- **Echter Bug gefunden+gefixt — Absturz beim Löschen des letzten Staffelpreises einer Preisliste:** Klick auf "Löschen" beim einzigen verbliebenen Staffelpreis-Eintrag zeigte "Fehler beim Löschen: Object reference not set to an instance of an object." (Löschung in der DB war bereits erfolgreich, der Crash kam danach). Ursache: `StaffelpreisNeu()` setzte `StaffelpreisArtikelId` auf `0` zurück; die Artikel-`ComboBox` mit `SelectedValuePath="Id"` fand keinen Eintrag mit `Id=0` in `ArtikelLookups` und WinUI wirft dabei intern eine `NullReferenceException` beim Setzen von `Selector.SelectedValue` (`KleinstammPage.g.cs`, `UpdateTwoWay_5_SelectedValue`). Fix: `StaffelpreisArtikelId` von `int` auf `int?` geändert, Reset auf `null` statt `0` (analog zu bereits vorhandenen nullable ComboBox-Bindungen wie `ZahlungsbedingungId`); `StaffelpreisSpeichernAsync` mapped `StaffelpreisArtikelId ?? 0` auf das DTO. Per vollem Stacktrace (`ex.ToString()` temporär in den Catch-Block) diagnostiziert, dann sauber verifiziert (Artikel→Preisliste→Staffelpreis anlegen→löschen, kein Fehler mehr, Formular korrekt zurückgesetzt).
- **Zusätzlich (defensiv mitgefixt, gleiche Fehlerklasse):** In allen 6 Löschen-Commands in `KleinstammViewModel` (Einheit/MwSt/Zahlungsbedingung/Versandart/Preisliste/Staffelpreis) sowie in `KundenListViewModel`/`LieferantenListViewModel`/`ArtikelListViewModel` wird die aktuelle Auswahl jetzt **vor** dem Neuladen der Liste zurückgesetzt (nicht danach) — vermeidet, dass eine ListView/ComboBox kurzzeitig auf ein bereits gelöschtes Element zeigt, während die neue Liste geladen wird.
- **Geldbeträge/Rundung:** Deutsche Locale — NumberBox erwartet **Komma** als Dezimaltrennzeichen, Punkt wird als Tausendertrenner gelesen (`12.345` → `12345`, kein Bug, reines Locale-Verhalten; für zukünftige manuelle Tests wichtig).
- **Bugfix (User-Wunsch währenddessen):** Listenpreis hatte 4 Nachkommastellen (`HasPrecision(18,4)`), sollte nur 2 haben. Migration `ListenpreisPrecision` (`decimal(18,2)`) erstellt+angewendet; `ArtikelEditPage.xaml` NumberBox für Listenpreis hat jetzt `DecimalFormatter FractionDigits="2"` (rundet z. B. `19,999` → `20,00`). Einkaufspreis bewusst unverändert bei 4 Nachkommastellen (Einkaufspreis-Präzision separat vom Verkaufspreis).
- **Automatisierungs-Erkenntnis (kein Produktbug, aber relevant falls hier nochmal per UIAutomation getestet wird):** `TextBox`/`NumberBox` mit `x:Bind TwoWay` committen ihren Wert erst bei echtem Fokusverlust. Ein `InvokePattern.Invoke()` auf einen Button bewegt den Fokus NICHT automatisch — vor dem Klick auf Speichern muss explizit `AutomationElement.SetFocus()` auf ein anderes Element aufgerufen werden, sonst bleibt der zuletzt getippte Wert uncommitted (führte zwischenzeitlich zu falschen Ergebnissen wie leerem Ort-Feld oder Preis=0 — bei echter Maus-/Tastaturbedienung tritt das nicht auf, da ein Klick immer den Fokus verschiebt).
- Testdaten (UIA-Testkunde, UIA-Testartikel, UIA-Preisliste, Staffelpreis, Repro-/Final-Testdatensätze) nach Verifikation wieder aus der DB entfernt; einzig verbliebener Datensatz ist der ursprüngliche Kunde KD-10001.

### Lieferanten-CRUD — live durchgetestet (2026-08-25) ✅
Per UI-Automation gegen laufende App + LocalDB verifiziert, Ergebnisse per `sqlcmd` gegengeprüft:
- Neu+Speichern: Nummernvergabe LF-70001 korrekt, alle Felder (Name, Adresse, Ort, Land, E-Mail) landen unverändert in der DB.
- Bearbeiten: Formular lädt bestehende Daten korrekt, Änderung wird gespeichert und in Liste sichtbar.
- Löschen: Bestätigungsdialog ("Lieferant '...' wirklich löschen?") zeigt korrekten Namen/Nummer, Ja löscht sauber (Liste leer, DB-Zeile weg), kein Absturz.
- Testdaten nach Verifikation wieder entfernt (Tabelle Lieferanten ist wieder leer).

### Phase 2 — Verkauf+PDF ✅ (2026-08-25, Branch `phase2-verkauf-pdf`)
Implementiert nach Plan `docs/superpowers/plans/2026-08-25-phase2-verkauf-pdf.md` (18 Tasks, TDD wo sinnvoll, jeder Task einzeln gebaut/getestet/committet):

**Domain:** Beleg-TPH-Modell (`Beleg`-Basis + dünne Subklassen `Angebot`/`Auftrag`/`Rechnung`), `BelegPosition` (Snapshot-Felder, `UrsprungsPositionId`-Selbstreferenz für Belegfluss, `OffeneMenge()`-Berechnung), `BelegSteuerSumme`, `SteuerRechner` (Positions-/Steuergruppen-/Kopfsummen, `MidpointRounding.ToEven`), `Firmenstamm` (Briefkopf), `OffenerPosten` (minimal, nur Anlage). 20 neue Domain-Tests.

**Application:** `IBelegService`/`IVerkaufLookupService`/`IBelegUeberleitungService`/`IRechnungBuchenService`, DTOs+Validatoren, `IPdfService`, `IFirmenstammService`. 5 neue Validator-Tests.

**Infrastructure:** EF-Configurations (TPH-Discriminator, Owned-Adress-Snapshots, Unique-Index gefiltert auf nicht-leere Rechnungsnummer), Migration `VerkaufBelegModell`; `BelegImmutabilityInterceptor` (GoBD-Sperre gebuchter Belege); `BelegService` (Aggregat-Speichern: Beleg+Positionen+Steuersummen in einem Transaktions-Call); `VerkaufLookupService` (inkl. Preisfindung-Integration); `BelegUeberleitungService` (Angebot→Auftrag→Rechnung mit Offene-Mengen-Logik, Quellbeleg→`Erledigt` bei Vollübernahme); `RechnungBuchenService` (atomare RE-Nummer via bestehendem `NumberRangeService`, Fälligkeit, Offener-Posten-Anlage, eine Transaktion); QuestPDF `BelegPdfDocument` (ein Dokument für alle 3 Typen, Titel/Fälligkeit unterscheiden) + `PdfService`.

**App (WinUI):** Verkauf-Menü (Angebote/Aufträge/Rechnungen) mit je eigener List-VM/Page (Muster wie Kunden/Lieferanten/Artikel); gemeinsame `BelegEditViewModelBase` (Kopf, Positionsgrid mit Artikel-Lookup+Preisfindung-Button, Live-Summen client-seitig vorberechnet, Speichern/Buchen/PDF/Überleiten/Abbrechen) + 3 dünne konkrete EditViewModels/Pages.

**Verifiziert:** Domain 14/14, Application 14/14, IntegrationTests 4/4 grün (+4 Docker-Skips wie gehabt) — inkl. neuer PDF-Render-Smoke-Tests (3, laufen ohne Docker) und `RechnungBuchenServiceTests` (paralleles Buchen/Immutability, Docker-Skip lokal). **Live-UI-Abnahme (UIAutomation, End-to-End):** Angebot anlegen (Preisvorschlag-Button lädt korrekten Listenpreis) → Speichern (Nummer `AN-2026-000x`) → „→ Auftrag" (Positionen 1:1 übernommen, Angebot-Status → Erledigt) → „→ Rechnung" (Nummer leer bis Buchen) → Buchen (`RE-2026-000x` vergeben, Fälligkeit gesetzt, Status Gebucht) → Offener Posten in DB verifiziert (Betrag=OffenerBetrag=SummeBrutto) → PDF-Button öffnet nativen Speichern-Dialog ohne Absturz. Testdaten nach Verifikation wieder entfernt.

**Zwei echte Bugs live gefunden+gefixt:**
- Positions-`Bezeichnung` übernahm den ComboBox-Anzeigetext (`"ART-01010 — Name"`) statt des reinen Artikelnamens — wäre so aufs PDF durchgeschlagen. `ArtikelVerkaufLookupDto` um separates `Bezeichnung`-Feld ergänzt.
- `IsEnabled="{x:Bind IstBearbeitbar}"` saß auf dem äußeren `ScrollViewer` und sperrte nach dem Buchen einer Rechnung auch PDF-/Abbrechen-Button mit. Fix: nur der editierbare Bereich (Kopf/Positionen) wird über einen `ContentControl`-Wrapper gesperrt (WinUI-`Panel`-Klassen wie `StackPanel`/`Grid` haben kein `IsEnabled`, nur `Control`-Klassen).
- Nebenbei: `Firmenstamm.Id` kollidierte als Identity-Spalte mit dem expliziten Seed-`Id=1` (Singleton-Zeile) → `ValueGeneratedNever()` ergänzt, Migration neu erzeugt.

**Hinweis Build-Tooling:** `sqlcmd` braucht `SET QUOTED_IDENTIFIER ON;` vor `DELETE`/`UPDATE` auf Tabellen mit gefilterten Indizes (z. B. `Belege`), sonst Meldung 1934.

### Phase 3 — Lager+Lieferschein ⚠️ (Build/Tests grün, manueller Smoke-Test ausstehend) (2026-08-26, Branch `phase3-lager-lieferschein`)
Implementiert nach Plan `docs/superpowers/plans/2026-08-25-phase3-lager-lieferschein.md` (19 Tasks + dieser Verifikations-Task, jeder Task einzeln gebaut/getestet/committet):

**Domain:** `Lagerort` (Aggregate Root, `IHasRowVersion`+`AuditableEntity`), `Lagerbewegung` (append-only-Ledger: ArtikelId/LagerortId/Menge signiert/Typ/BelegPositionId?/SeriennummerId?/Zeitpunkt/BenutzerId, bewusst ohne RowVersion/AuditableEntity), `ArtikelBestand` (Snapshot ArtikelId+LagerortId), `LagerbewegungTyp`-Enum; `Seriennummer` (Status AufLager/Ausgeliefert/Retourniert), `BelegPositionSeriennummer` (Junction), `Inventur`+`InventurPosition` (SollMenge eingefroren, IstMenge); `Lieferschein` als dünner `Beleg`-TPH-Subtyp + `BelegPosition.LagerortId`-Erweiterung.

**Application:** DTOs+Validatoren für alle neuen Entities; `ILagerortService`/`ISeriennummernService`/`IInventurService`/`IBestandService`-Interfaces; `IBelegUeberleitungService` erweitert um `UeberleitenMitAuswahlAsync` (explizite Mengenauswahl statt Immer-alles-Übernahme) und `UeberleitenMehrereAsync` (Sammelrechnung, Mehrfachquellen); `ILieferscheinBuchenService`. 5 neue Application-Tests (Validatoren).

**Infrastructure:** EF-Configurations für alle neuen Entities, Migration `LagerLieferschein`, Hauptlagerort-Seed (`Code=HL`); `BestandService.BucheBewegungAsync` — der einzige Schreibpfad auf Bestand, atomares `UPDATE ... SET Menge = Menge + @delta WHERE ... >= 0` (ein SQL-Round-Trip, `betroffeneZeilen == 0` ⇒ Negativsperre, `InvalidOperationException`), gemeinsam genutzt von Bestandskorrektur/Lieferschein-Buchen/Inventur-Abschluss; `LagerortService` (CRUD, Kleinstamm-Muster); `SeriennummernService`; `LieferscheinBuchenService` (negative Lagerbewegungen + Seriennummern-Pick + Bestandsupdate in einer Transaktion, Offene-Mengen-Prüfung wiederholt); `InventurService` (Anlegen/Ist-Erfassung/Abschluss mit Korrekturbuchungen); `BelegUeberleitungService`/`BelegService` um Lieferschein-Pfad erweitert. 3 neue Integrationstest-Klassen: `BelegUeberleitungServiceTests`, `BestandServiceTests`, `LieferscheinBuchenServiceTests` (10 Testmethoden, Testcontainers).

**App (WinUI):** Lagerorte-Tab in `KleinstammPage`; Bestandsübersicht-Seite (Bestandskorrektur + Seriennummern-Erfassung, zwei Detail-Modi einer Seite); `AuftragEditViewModel`-Erweiterung „→ Lieferschein" + `TeillieferungDialog` (offene Mengen, Lagerort-Auswahl, Mengenreduktion); Lieferschein-Liste (inkl. Mehrfachauswahl „→ Sammelrechnung") + -Editor + Seriennummern-Auswahl-Dialog beim Buchen; Inventur-Liste + -Editor (Mengen erfassen, Abschließen); Lager-Menü in `ShellPage` aktiviert, alle neuen ViewModels in DI registriert.

**Automatisiert verifiziert (dieser Task, 2026-08-26):**
- Build `Milet.App.csproj -p:Platform=x64`: **0 Fehler**, 1 Warnung (`WMC1506` XAML-Binding-Hinweis in `TeillieferungDialog.xaml`, kein Unused-Using-Problem).
- Tests einzeln (MTP-Modus): Domain **14/14**, Application **19/19** (14 Bestand + 5 neu), IntegrationTests **18 gesamt: 4 bestanden, 0 fehlgeschlagen, 14 übersprungen** (alle Skips sauber „Docker nicht verfügbar" — 4 bestehende Skips aus Phase 1/2 + 10 neue Skips aus den 3 neuen Testklassen; kein einziger Fail).
- Migration: `Milet.Tools.Migrator` meldet „Datenbank ist aktuell — keine ausstehenden Migrationen." (bereits in einer früheren Task-Session angewendet), Seed-Grunddaten geprüft. Per `sqlcmd` gegengeprüft: `Lagerorte` enthält `HL`/„Hauptlager"; `SELECT COUNT(*) FROM Belege WHERE BelegTyp = 'Lieferschein'` läuft ohne Schemafehler (0 Zeilen — keine Lieferscheine angelegt, da kein manueller UI-Testlauf in diesem Durchgang).

**Nicht durchgeführt — Offen für Phase-3-Abnahme:** Der manuelle End-to-End-Smoke-Test im laufenden UI (Plan-Task-20-Step-4, 9 Teilschritte: Lagerort anlegen, Bestandskorrektur, Auftrag→Teillieferung→Lieferschein, Buchen, zweite Teillieferung, Sammelrechnung, Negativsperre-Check, Inventur-Abschluss, Seriennummern-Auswahl beim Buchen) wurde in diesem Durchgang **nicht** ausgeführt — dieser Verifikationslauf erfolgte durch einen headless Hintergrund-Agenten ohne Display-/Maus-Zugriff, der keine WinUI-Desktop-App starten oder bedienen kann. Build/Tests/Migration sind damit real verifiziert; der eigentliche fachliche End-to-End-Nachweis (inkl. eventueller dabei gefundener UI-Bugs, wie in den Phase-1/2-Abnahmen dokumentiert) fehlt noch und muss von einem Menschen (oder einer UI-Automation-fähigen Session, wie bei der Phase-1-Abnahme praktiziert) nachgeholt werden, bevor Phase 3 als vollständig abgenommen gilt.

### Phase 4 — Einkauf ⚠️ (Build/Tests grün, manueller Smoke-Test ausstehend) (2026-08-27, Branch/Worktree `phase4-einkauf`)
Implementiert nach Plan `docs/superpowers/plans/2026-08-26-phase4-einkauf.md` (17 Tasks, jeder Task einzeln gebaut/getestet/committet):

**Domain:** `Beleg`-Partei-Erweiterung — `KundeId` wird nullable, neues `LieferantId` (Kunde XOR Lieferant, per DB-Check-Constraint `CK_Belege_KundeOderLieferant` erzwungen) — als Basis für die drei neuen TPH-Subtypen `Bestellung`/`Wareneingang`/`Eingangsrechnung`. `BestellVorschlagService` (reine Domain-Logik: Artikel mit Bestand unter `Mindestbestand`, `VorschlagsMenge`-Berechnung).

**Application:** `IBestellVorschlagService`/`IWareneingangBuchenService`/`IEingangsrechnungBuchenService`-Interfaces, DTOs+Validatoren für Bestellung/Wareneingang/Eingangsrechnung (u. a. Pflicht-Lieferant statt Kunde). `IBelegUeberleitungService` um den Einkaufs-Pfad erweitert (Bestellung→Wareneingang→Eingangsrechnung, wiederverwendet die aus Phase 3 bestehende Teilmengen-/Offene-Mengen-Logik).

**Infrastructure:** `BelegConfiguration` um CHECK-Constraint (Kunde XOR Lieferant) und Discriminator-Werte für die drei neuen Typen erweitert; Migration `EinkaufBestellungWareneingang` (erzeugt+angewendet in Task 7); Seed ergänzt die zuvor fehlenden Nummernkreise `WE`/`ER` (Code `BE` war bereits vorhanden) — behebt damit die in „Bekannte Risiken" dokumentierte Nummernkreis-Seed-Lücke für diese drei neuen Codes. `BelegService` um generalisierten Kunde/Lieferant-Zweig erweitert (trägt jetzt die gesamte Einkaufs-CRUD-Logik). `WareneingangBuchenService` (Bestandszugang über den bestehenden `BestandService` + Seriennummern-Neuanlage in einer Transaktion). `EingangsrechnungBuchenService` (Kreditor-OP-Anlage über `OffenePosten`, Betrags-Abweichungs-Soft-Warnung gegen den zugrundeliegenden Wareneingang, kein Blocker). 3 neue Integrationstest-Klassen: `BestellVorschlagServiceTests`, `WareneingangBuchenServiceTests`, `EingangsrechnungBuchenServiceTests` (Testcontainers).

**App (WinUI):** Neues Einkauf-Menü; `EinkaufBelegEditViewModelBase` als gemeinsame UI-Basis für die drei neuen Editoren (analog zum Verkaufs-Pendant aus Phase 2); Bestellvorschlag-Seite (Artikel unter Mindestbestand, Lieferant-Auswahl, „Bestellung erzeugen"); Bestellung-/Wareneingang-/Eingangsrechnung-Editoren inkl. Überleitungs-Buttons (`WareneingangMengenDialog` für die Mengenauswahl beim Übergang Bestellung→Wareneingang, analog zum `TeillieferungDialog` aus Phase 3); Seriennummern-Erfassung beim Wareneingang-Buchen über den neuen `SeriennummernErfassungDialog` (erfasst NEUE Nummern — ein eigener, zu dieser Phase gehörender Dialog, analog zum, aber nicht identisch mit dem `SeriennummernAuswahlDialog` aus Phase 3, der stattdessen aus bestehenden Nummern AUSWÄHLT).

**Verifiziert (dieser Task, Task 17, 2026-08-27):**
- Build: `Milet.App.csproj -p:Platform=x64` → **0 Fehler**, 2 Warnungen (`WMC1506` XAML-Binding-Hinweise in `WareneingangMengenDialog.xaml` und `TeillieferungDialog.xaml`, gleiche unkritische Warnklasse wie in Phase 3). `Milet.Tools.Migrator.csproj` → **0 Fehler, 0 Warnungen**.
- Tests einzeln (MTP-Modus): Domain **21/21**, Application **21/21**, IntegrationTests **24 gesamt: 4 bestanden, 0 fehlgeschlagen, 20 übersprungen** (alle Skips sauber „Docker nicht verfügbar" — inkl. der 3 neuen Testklassen `BestellVorschlagServiceTests`/`WareneingangBuchenServiceTests`/`EingangsrechnungBuchenServiceTests`; kein einziger Fail).
- Migration: `Milet.Tools.Migrator` meldet „Datenbank ist aktuell — keine ausstehenden Migrationen." Migration `EinkaufBestellungWareneingang` (bereits in Task 7 erzeugt+angewendet) ist laut `__EFMigrationsHistory` in LocalDB vorhanden (per `sqlcmd` gegengeprüft, zusammen mit `InitialCreate`/`ListenpreisPrecision`/`VerkaufBelegModell`/`LagerLieferschein`); die Nummernkreis-Werte (`BE`/`WE`/`ER`, je `NaechsteNummer=1`) sowie unveränderte `Belege`/`OffenePosten`-Zeilenzahlen wurden bereits in Task 7 per `sqlcmd` unabhängig verifiziert.

**Nicht durchgeführt — Offen für Phase-4-Abnahme:** Der manuelle End-to-End-Smoke-Test im laufenden UI (Plan-Task-17-Step-4, 10 Teilschritte: Lieferant anlegen, Artikel unter Mindestbestand, Bestellvorschlag→Bestellung erzeugen, Bestellung→Wareneingang, Wareneingang buchen inkl. Seriennummern-Erfassung, Wareneingang→Eingangsrechnung, Eingangsrechnung buchen, Abweichungsfall provozieren, Negativ-Check ohne Lieferanten) wurde in diesem Durchgang **nicht** ausgeführt — genau wie bei der Phase-3-Abnahme erfolgte dieser Verifikationslauf durch einen headless Hintergrund-Agenten ohne Display-/Maus-Zugriff, der keine WinUI-Desktop-App starten oder bedienen kann. Build/Tests/Migration sind damit real verifiziert; der fachliche End-to-End-Nachweis (inkl. eventueller dabei gefundener UI-Bugs) fehlt noch und muss von einem Menschen (oder einer UI-Automation-fähigen Session, wie bei der Phase-1-Abnahme praktiziert) nachgeholt werden, bevor Phase 4 als vollständig abgenommen gilt.

### Phase 5 — Finanzen+E-Mail ⚠️ (Backend Build/Tests real grün; WinUI komplett unverifiziert) (2026-08-27, Branch `claude/plan-phase-5-fortsetzung-tnt3i6`)
Implementiert nach Plan `docs/superpowers/plans/2026-08-27-phase5-finanzen-mail.md` (20 Tasks, jeder Task einzeln gebaut/getestet/committet). **Wichtiger Unterschied zu Phase 3/4:** Diese Session lief headless auf **Linux ohne jede Windows-Toolchain** (kein `dotnet` vorinstalliert, `dotnet-install.sh`/`builds.dotnet.microsoft.com` durch den Netzwerk-Proxy blockiert). `dotnet` 10.0.111 wurde per `apt` installiert (Ubuntu-Paket, `global.json` fordert `10.0.400` — das reale, committete `global.json` bleibt unverändert; Builds/Tests liefen aus einer Scratch-Kopie mit lokal auf `10.0.111` abgesenktem `global.json`, nie im echten Repo). Damit konnten `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/`Milet.Tools.Migrator`/alle 3 Testprojekte **real gebaut und getestet** werden (nicht nur „compile-verifiziert" wie ein rein lesender Review) — das ist mehr Verifikation als der reine Build-Status von Phase 3/4 auf dieser speziellen Achse, aber **`Milet.App` (WinUI) konnte in dieser Session kein einziges Mal gebaut werden** (WinUI/Windows App SDK existiert nur unter Windows) — schlechter als Phase 3/4, wo zumindest ein Windows-Build gelang und nur der manuelle UI-Klicktest fehlte.

**Domain:** `OffenerPostenStatus` (Offen/TeilweiseBezahlt/Ausgeglichen) als neues Feld auf `OffenerPosten`. `Zahlung`+`ZahlungZuordnung` (eigenes Aggregat, kein Beleg-Subtyp — keine GoBD-Nummernkreis-Pflicht). `Mahnstufe` (reine Config-Tabelle), `Mahnung`+`MahnungPosition` (Ergebnis eines Mahnlaufs, ebenfalls kein Beleg-Subtyp). `EmailVersand` (Versand-Log, `BelegId?`/`MahnungId?` XOR). Zwei reine Domain-Services nach `SteuerRechner`/`PreisfindungService`-Muster: `SkontoRechner` (Skontofrist ab Rechnungsdatum) und `MahnSelektionService` (welche Mahnstufe ist für einen OP an einem Datum fällig — Mahnsperre/Ausgeglichen/fehlende Stufen-Config blockieren korrekt statt zu eskalieren). 13 neue Domain-Tests.

**Application:** `IOffenePostenService` (Liste mit Aging-Filter), `IZahlungService` (Skonto-Vorschlag, Zahlungserfassung), `IMahnwesenService` (Mahnstufen-CRUD, Fällige ermitteln, Mahnlauf durchführen), `IEmailVersandService` (wrapt E-Mail-Versand, protokolliert immer, wirft nie). `IEmailService`-Abstraktion (Application.Abstractions, wie `IPdfService`) + `EmailNichtKonfiguriertException`. `IWindowHandleProvider`-Abstraktion für den WAM-Broker-Fensterhandle. 11 neue Validator-Tests.

**Infrastructure:** EF-Configurations für alle neuen Entities (CHECK-Constraints Kunde-XOR-Lieferant bei `Zahlung`, Beleg-XOR-Mahnung bei `EmailVersand`, analog bestehendem Muster), Migration `FinanzenMahnwesen` inkl. Backfill-UPDATE für `OffenePosten.Status` (aus `OffenerBetrag`/`Betrag` hergeleitet, nicht pauschal „Offen"). Modellkonsistenz gegen die Migration verifiziert (ein zweiter `dotnet ef migrations add` generiert eine leere Migration — kein Drift). `MahnstufenSeed` (3 Default-Stufen, „je fehlender Stufe ergänzen"-Muster wie die Nummernkreise). `OffenePostenService`, `ZahlungService` (RowVersion-Concurrency-Schutz je Zuordnung, Betrag+Skonto ≤ OffenerBetrag erzwungen), `MahnwesenService` (Selektion + Durchführung mit Re-Check zum Ausführungszeitpunkt, Gruppierung je (Kunde, Zielstufe) in eigene Mahnungen). `MahnungPdfDocument` (QuestPDF, Muster `BelegPdfDocument`) + `PdfService.GeneriereMahnungPdfAsync`. `GraphEmailService` (MSAL/WAM-Broker-Sign-In, Kiota-basierter Auth-Provider, Graph `SendMail` inkl. PDF-Anhang) + `NichtKonfigurierterEmailService`-Fallback; DI wählt anhand Vorhandensein/Vollständigkeit der `Graph`-Sektion in `appsettings.json` (dort bewusst nicht angelegt — JSON kennt keine Kommentare, Abwesenheit ist bereits der korrekte Trigger für den Fallback). Neue NuGet-Pakete `Microsoft.Graph` 6.5.0, `Microsoft.Identity.Client.Broker` 4.88.0, `Microsoft.Extensions.Configuration.Binder` 10.0.11 — **echter Restore gegen nuget.org lief in dieser Session** (erreichbar trotz sonst restriktivem Proxy), zwei falsche API-Annahmen beim ersten Anlauf real durch Compiler-Fehler gefunden und korrigiert (`AllowedHostsValidator` liegt in `Microsoft.Kiota.Abstractions.Authentication`, nicht `...Abstractions`; `WithBroker(BrokerOptions)` ist eine Extension-Methode aus `Microsoft.Identity.Client.Broker`, ohne den `using` bindet der Compiler an eine andere `WithBroker(bool)`-Überladung).

**Ein echter Bug während der Implementierung gefunden+gefixt (nicht erst am Ende, sondern mitten in Task 12):** `OffenerPosten` hat **keine** `Kunde`/`Lieferant`-Navigation (nur `KundeId?`/`LieferantId?` als Skalarfelder, anders als angenommen) — `OffenePostenService`/`MahnwesenService` griffen zunächst fälschlich darauf zu. Der Fehler blieb in Task 9/11 unbemerkt „grün", weil der zu dem Zeitpunkt schon bekannte, bewusst offene `PdfService`-Fehler (Task 6, wird erst in Task 12 behoben) den Build vorher abbrach, bevor der Compiler diese Dateien erreichte — ein Lehrbuchbeispiel dafür, dass ein bekannter/erwarteter Fehler andere, echte Fehler im selben Build maskieren kann. Fix: Zugriff über die bereits vorhandene `Beleg.Kunde`/`Beleg.Lieferant`-Navigation (`OffenerPosten.Beleg` existiert bereits) statt einer neuen FK-Navigation auf `OffenerPosten` — keine zusätzliche Migration nötig.

**App (WinUI, komplett unverifiziert — siehe oben):** Neues Untermenü unter „Finanzen" (bisher deaktivierter Platzhalter) mit „Offene Posten" und „Mahnlauf". `OffenePostenListViewModel`/`Page` (Filter Typ/Status/nur überfällige, Mehrfachauswahl). `ZahlungDialog` (Muster `WareneingangMengenDialog`, Skonto-Vorschlag vom ViewModel vor dem Öffnen vorausgefüllt). Sechster Pivot-Tab „Mahnstufen" in `KleinstammPage` (Muster Zahlungsbedingungen-Tab). `MahnlaufViewModel`/`Page` (Muster `BestellVorschlagPage`: Kandidaten ermitteln→auswählen→durchführen, Ergebnisliste mit PDF-/E-Mail-Button). „E-Mail senden"-Button auf dem Rechnung-Editor (neben PDF, nur bei Status=Gebucht wirksam) und in der Mahnlauf-Ergebnisliste. `WinUiWindowHandleProvider` überschreibt den Infrastructure-Fallback für den WAM-Broker. Alle 36 XAML-Dateien des Projekts als wohlgeformtes XML geprüft (`xml.etree.ElementTree`), Klammerbalance der neuen/geänderten C#-Dateien geprüft — das ersetzt **keinen** echten Compile/XAML-Codegen-Durchlauf.

**Verifiziert (dieser Task, Task 20, 2026-08-27):**
- Build einzeln: `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/`Milet.Tools.Migrator`/`Milet.IntegrationTests` → **je 0 Fehler, 0 Warnungen** (real, s. o. — Linux-`dotnet` mit lokal abgesenktem `global.json` in einer Scratch-Kopie, echtes Repo-`global.json` unverändert bei `10.0.400`).
- Tests einzeln (MTP-Modus): Domain **34/34**, Application **32/32**, IntegrationTests **24 gesamt: 4 bestanden, 0 fehlgeschlagen, 20 übersprungen** (Docker nicht verfügbar, sauberer Skip wie in jeder vorherigen Phase — kein einziger Fail).
- Migration `FinanzenMahnwesen`: Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration = kein Drift). **Nicht** gegen eine echte SQL-Server-/LocalDB-Instanz angewendet (in dieser Linux-Session nicht verfügbar) — anders als Phase 1–4, wo die Migration zumindest einmal auf LocalDB lief und per `sqlcmd` gegengeprüft wurde. Muss vor Abnahme auf einer echten DB angewendet werden.

**Nicht durchgeführt / offen für Phase-5-Abnahme:**
1. **`Milet.App` wurde in dieser Session kein einziges Mal gebaut** (kein Windows verfügbar) — alle WinUI-Änderungen (Tasks 15–19) sind reine „nach bestehendem Muster nachgebildete" Änderungen ohne jede Compiler-Bestätigung. Vor Abnahme zwingend: `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf einer echten Windows-Maschine, dann erst der übliche manuelle UI-Smoke-Test (OP-Liste filtern, Zahlung mit/ohne Skonto erfassen → Status TeilweiseBezahlt/Ausgeglichen, Mahnstufen-Tab CRUD, Mahnlauf-Selektion→Durchführung→PDF, E-Mail-Button-Fehlerpfad ohne Graph-Konfiguration).
2. **Migration `FinanzenMahnwesen` nie gegen eine echte DB angewendet** (s. o.) — insbesondere das Backfill-`UPDATE` für `OffenePosten.Status` ist nur gelesen, nie ausgeführt verifiziert.
3. **Graph-Mail funktional nicht verifizierbar ohne eine echte, vom Nutzer selbst registrierte Entra-App** (ClientId/TenantId/RedirectUri, `Mail.Send`-Consent) — dafür in `appsettings.json` eine Sektion `"Graph": { "ClientId": "...", "TenantId": "...", "RedirectUri": "..." }` ergänzen. Ohne diese Sektion greift automatisch der `NichtKonfigurierterEmailService`-Fallback (App bleibt voll funktionsfähig, nur der „E-Mail senden"-Button meldet einen Fehler). Testkriterium aus `PLAN.md` „Mail mit PDF kommt an" kann nur der Nutzer selbst auf Windows nachweisen.
4. `ZahlungService.ErfasseZahlungAsync`/`MahnwesenService.MahnlaufDurchfuehrenAsync` haben keinen automatisierten Integrationstest gegen eine echte DB (Docker hier nicht verfügbar, wie bei allen Vorphasen) — nur compile-verifiziert + Unit-Tests der reinen Domain-Logik (`SkontoRechner`/`MahnSelektionService`) grün.
5. Kein automatischer/geplanter Mahnlauf (Scheduler) — v1 ist bewusst manuell ausgelöst, wie im Plan dokumentiert.

### Phase 6 — DATEV+Reporting ⚠️ (Backend Build/Tests/Migration ECHT gegen SQL Server verifiziert; WinUI unverifiziert) (2026-08-27, Branch `claude/phase-six-implementation-wptxqj`)
Implementiert nach Plan `docs/superpowers/plans/2026-08-27-phase6-datev-reporting.md` (15 Tasks, jeder Task einzeln gebaut/getestet/committet). Wie Phase 5 lief diese Session headless auf **Linux ohne Windows-Toolchain** (`dotnet-sdk-10.0` per `apt` installiert → 10.0.111, Builds/Tests aus Scratch-Kopie mit lokal abgesenktem `global.json`, echtes Repo-`global.json` unverändert bei `10.0.400`). **Wichtiger Unterschied zu Phase 5: Docker war in dieser Session tatsächlich nutzbar** (Daemon war installiert, aber nicht gestartet — `dockerd` manuell gestartet; das `testcontainers/ryuk`-Sidecar-Image ließ sich über den Sessions-Proxy nicht von Docker Hub ziehen, `mcr.microsoft.com/mssql/server` aber problemlos, daher `TESTCONTAINERS_RYUK_DISABLED=true` — offizieller, dokumentierter Testcontainers-Escape-Hatch für genau solche eingeschränkten CI-Netzwerke, kein Test-Bypass). Damit liefen **alle 33 Integrationstests der gesamten Lösung echt gegen einen containerisierten SQL Server** (nicht nur übersprungen) — die bisher stärkste Verifikation dieser Testsuite in einer Linux-Session, inkl. aller zuvor nur compile-verifizierten Parallelitäts-/Race-Tests aus Phase 2–4. Zusätzlich wurde `Milet.Tools.Migrator` echt gegen eine frische SQL-Server-Instanz gefahren (Migration `DatevReporting` angewendet, Seeds gelaufen, Idempotenz bei zweitem Lauf per erneutem Ausführen bestätigt) und die Zieldaten per `sqlcmd` direkt im Container gegengeprüft — schließt damit einen Teil der in Phase 5 offen gelassenen Lücke ("Migration nie gegen echte DB angewendet"), wenn auch nur für Phase 6 selbst.

**Domain:** `Kontenrahmen`-Enum (SKR03/SKR04), `FibuKonfiguration` (Singleton-Entity wie `Firmenstamm`). `MwStSatz` um `ErloeskontoNr`/`AufwandskontoNr` erweitert. `Beleg`/`Zahlung` um `ExportiertAm` (Doppelexport-Marker). `DatevBuchungszeile`/`DatevExportKopf` (reine Datenobjekte) + `DatevExtfWriter` (reine Formatierungslogik für den EXTF-Buchungsstapel — bildet die zentralen Spalten ab: Umsatz, Soll/Haben, Konto, Gegenkonto, BU-Schlüssel, Belegdatum, Belegfeld 1, Buchungstext; bewusst **kein** vollständiger Nachbau der ~125 offiziellen DATEV-Spalten, s. Scope-Hinweis unten). Golden-File-Tests (4 neue Domain-Tests) sichern den eigenen Output regressionsfrei ab.

**Application:** `IFibuKonfigurationService` (Singleton-Load/Save wie Firmenstamm) + DTO/Validator. `MwStSatzDto`/-Validator um die zwei neuen Kontenfelder erweitert. `IDatevExportService` (`VorschauAsync` zählt/summiert ohne zu markieren, `ExportierenAsync` baut die CSV und markiert). Neues Modul `Milet.Application.Reporting`: `IReportingService` (sechs Auswertungen) + DTOs. `CsvWriter` (in `Milet.Application.Common`, bewusst nicht in Infrastructure, damit WinUI-ViewModels nur Application-Interfaces referenzieren müssen) für den Excel-tauglichen Reporting-CSV-Export, getrennt vom DATEV-Format.

**Infrastructure:** `FibuKonfigurationConfiguration` (Singleton-Muster), Migration `DatevReporting` (FibuKonfiguration-Tabelle, neue Spalten auf MwStSaetze/Belege/Zahlungen) — Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration, danach entfernt) **und** einmal real gegen SQL Server angewendet (s. o.). `StammdatenSeed`-Erweiterung: FibuKonfiguration-Singleton-Default (SKR03) + SKR03-Standardkonten je Steuerschlüssel nur wo noch NULL (Update-in-place). `FibuKonfigurationService`, `DatevExportService` (Buchungszeilen je `BelegSteuerSumme`-Gruppe aus gebuchten Rechnungen/Eingangsrechnungen + Zahlungen; Belege/Zahlungen ohne gepflegtes Debitoren-/Kreditoren-/Erlös-/Aufwands-/Bankkonto erzeugen keine Zeile und bleiben unmarkiert — kein stiller Datenverlust; CP1252-Kodierung über die im .NET-10-Shared-Framework bereits enthaltene `CodePagesEncodingProvider`, **kein** zusätzliches NuGet-Paket nötig — ein zunächst hinzugefügtes `System.Text.Encoding.CodePages`-Package brach den Build mit `NU1510` [von der neuen SDK-Paket-Pruning-Prüfung als bereits im Framework enthalten erkannt], per eigenem Konsolen-Snippet echt verifiziert, dass CP1252-Encoding/Decoding inkl. Umlauten und `€` ohne das Paket funktioniert). `ReportingService` (sechs LINQ-Aggregationen, `OffeneAuftraegeAsync` nutzt dieselbe `BelegPosition.OffeneMenge`-Logik wie `BelegUeberleitungService`). DI-Registrierungen ergänzt.

**Ein echter Bug gefunden+gefixt, per echtem SQL-Server-Lauf entdeckt (nicht nur am Schreibtisch):** Der SKR03-Konten-Backfill in `StammdatenSeed` fragte `db.MwStSaetze` per LINQ ab, bevor die im selben `ApplyAsync`-Aufruf zuvor per `AddRange` hinzugefügten (aber noch nicht gespeicherten) MwSt-Sätze persistiert waren — eine EF-Core-Query gegen einen DbSet sieht ungespeicherte `Added`-Entities nicht. Auf einer frisch migrierten DB blieb der Backfill dadurch wirkungslos (Erlös-/Aufwandskonto blieben NULL trotz „Fix"). Erst durch den echten Docker-gestützten Migrator-Lauf gegen SQL Server per `sqlcmd` bemerkt (wäre mit reinem Compile-Review oder Unit-Tests ohne DB nicht aufgefallen). Fix: zusätzliches `SaveChangesAsync` vor dem Backfill-Block; per erneutem Migrator-Lauf gegen eine frisch gedroppte DB verifiziert (`ErloeskontoNr`/`AufwandskontoNr` jetzt korrekt 8400/3400, 8300/3300, 8120/3200).

**App (WinUI, unverifiziert — kein Windows in dieser Session):** Achter Pivot-Tab „FibuKonten" in `KleinstammPage` (neu eingeführtes Singleton-Speichern-Pattern, da `Firmenstamm` bislang ebenfalls keine UI hat — kein Vorbild im Code). MwSt-Sätze-Tab um Erlös-/Aufwandskonto-Felder erweitert. Neue `DatevExportPage`/-`ViewModel` (Zeitraum-Picker, Vorschau, Export über `FileSavePicker` wie beim PDF-Export). Neues Modul `Views/ViewModels.Reporting` (sechs Pivot-Tabs, je Laden-/CSV-Export-Button). Reporting-Menüpunkt aktiviert (vorher `IsEnabled=False`), neuer DATEV-Export-Menüpunkt unter Finanzen. Alle 38 XAML-Dateien als wohlgeformtes XML geprüft, Klammerbalance der neuen/geänderten C#-Dateien geprüft — ersetzt wie in Phase 5 **keinen** echten Compile/XAML-Codegen-Durchlauf.

**Verifiziert (dieser Task, 2026-08-27):**
- Build einzeln: `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/`Milet.Tools.Migrator`/`Milet.Domain.Tests`/`Milet.Application.Tests`/`Milet.IntegrationTests` → **je 0 Fehler, 0 Warnungen**.
- Tests einzeln (MTP-Modus): Domain **38/38**, Application **41/41**.
- **IntegrationTests: alle 33 Tests ECHT gegen containerisierten SQL Server gelaufen (nicht übersprungen) — 33/33 bestanden, 0 fehlgeschlagen**, inkl. der 9 neuen Phase-6-Tests (`DatevExportServiceTests`, `ReportingServiceTests`) und aller 24 bestehenden Tests aus Phase 1–5 (Nummernkreis-Parallelität, Bestand-Negativsperre, paralleles Rechnung-/Lieferschein-Buchen u. a. — bisher nur „sauber übersprungen", jetzt erstmals in dieser Session-Reihe real bestätigt).
- Migration `DatevReporting`: Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration) **und real gegen SQL Server angewendet** (Migrator-Lauf inkl. Seeds), Ergebnis per `sqlcmd` gegengeprüft (`FibuKonfiguration`-Singleton, SKR03-Konten auf `MwStSaetze`, `ExportiertAm`-Spalte auf `Belege`, `__EFMigrationsHistory` enthält `20260827154857_DatevReporting`), zweiter Migrator-Lauf bestätigt Idempotenz (keine ausstehenden Migrationen/Seeds).

**Scope-Einschränkungen (bewusst, s. Plan):**
1. Gutschrift existiert im Code weiterhin nicht (kein `BelegTyp.Gutschrift`, in keiner Phase bisher implementiert) — DATEV-Export umfasst daher nur Rechnung/Eingangsrechnung/Zahlung.
2. `DatevExtfWriter` bildet die zentralen Buchungsstapel-Spalten ab, nicht alle ~125 offiziellen DATEV-Spalten — Golden-File-Test sichert nur den eigenen Output ab, ersetzt nicht die im Plan explizit als offen benannte externe Prüfung „Import beim Steuerberater validiert".
3. **`Milet.App` wurde in dieser Session kein einziges Mal gebaut** (kein Windows verfügbar) — vor Abnahme zwingend: `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows, danach manueller UI-Smoke-Test (FibuKonten-Tab speichern, MwSt-Konten pflegen, DATEV-Vorschau/-Export inkl. Doppelexport-Schutz, alle sechs Reporting-Tabs laden+CSV-exportieren).
4. `DatevExportKopf.ErzeugtAm`/Dateiname-Zeitzone nicht gegen reale DATEV-Kanzleisoftware getestet — wie oben, Nutzer-/Steuerberater-Aufgabe.

### Phase 7 — Admin+Härtung ⚠️ (Backend real gegen SQL Server verifiziert inkl. AuditLog/RBAC-Guard; WinUI-Build unverifiziert) (2026-08-27, Branch `claude/phase-7-implementierung-g30bus`)
Umgesetzt nach `PLAN.md`-Zeile „7 Admin+Härtung": Benutzer/Rollen/Rechte-UI+Login, Service-Guards, Systemkonfig (Firmenstamm/Briefkopf), AuditLog-Viewer, Deployment-Story (`docs/deployment.md`). Wie Phase 6 lief diese Session headless auf **Linux ohne Windows-Toolchain** (`dotnet-sdk-10.0` per `apt` installiert → 10.0.111, Builds/Tests aus Scratch-Kopie mit lokal abgesenktem `global.json`, echtes Repo-`global.json` unverändert bei `10.0.400`) — **mit echt nutzbarem Docker** (`dockerd` manuell gestartet, `TESTCONTAINERS_RYUK_DISABLED=true` wie in Phase 6, `mcr.microsoft.com/mssql/server:2022-latest` zieht über den Sessions-Proxy anstandslos).

**Domain:** `PasswortHasher` (reiner Domain-Service wie `SkontoRechner`/`SteuerRechner`: PBKDF2-HMACSHA256, 210.000 Iterationen, selbstbeschreibendes Speicherformat `"Iterationen.SalzBase64.HashBase64"` — erlaubt spätere Erhöhung der Iterationszahl ohne Migration der Bestandsdaten, `CryptographicOperations.FixedTimeEquals` gegen Timing-Angriffe). Neue Entities: `Recht` (fester Katalog, ein Eintrag je Modul/Top-Level-Menüpunkt), `Rolle` (`AuditableEntity`+`IHasRowVersion`, n:m zu `Recht` über Join-Tabelle `RolleRecht`), `Benutzer` (`AuditableEntity`+`IHasRowVersion`, 1:n von `Rolle`, `PasswortHash`, `Aktiv`), `AuditLog` (append-only, bewusst **kein** `AuditableEntity` — sonst würde sich der Interceptor selbst rekursiv protokollieren, s. Infrastructure). 6 neue Domain-Tests (`PasswortHasherTests`: Hash-Format, Salt-Einzigartigkeit, Verify positiv/negativ, kaputtes Format wirft nicht sondern liefert `false`).

**Application:** `RechtCodes` (7 feste Modul-Codes: Stammdaten/Verkauf/Einkauf/Lager/Finanzen/Reporting/Administration — deckungsgleich mit den Top-Level-NavigationView-Menüpunkten, das trägt sowohl den Service-Guard als auch die UI-Sichtbarkeit). `ICurrentSessionService` (erweitert das bestehende `ICurrentUserService` aus Phase 1 um Login-/Rechte-Zustand: `IstAngemeldet`/`RollenName`/`Rechte`/`HatRecht`/`Anmelden`/`Abmelden`). `IBerechtigungsService` (`PruefeRecht`/`HatRecht` — Service-seitiger RBAC-Guard, analog zur bestehenden Konvention "Validation explizit am Methodenanfang"). `KeinZugriffException` (Common, analog `ConcurrencyConflictException`). `IAuthService`/`IBenutzerverwaltungService`/`IRollenverwaltungService`/`IAuditLogService` + DTOs (`BenutzerDto`/`RolleDto`/`RechtDto`/`BenutzerSessionDto`/`AuditLogDto`/`AuditLogFilterDto`) + `BenutzerValidator`/`RolleValidator` (Passwort-Pflicht nur bei Neuanlage, min. 8 Zeichen). `ISchemaVersionService` (Login-Screen prüft `GetPendingMigrationsAsync`, s. PLAN.md "App prüft SchemaVersion beim Start"). 8 neue Application-Tests (Validatoren).

**Infrastructure:** EF-Configurations (`RechtConfiguration`/`RolleConfiguration`/`BenutzerConfiguration`/`AuditLogConfiguration`, n:m über `UsingEntity(...ToTable("RolleRecht"))`), Migration `AdminRbacAuditLog` — Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration) **und real gegen SQL Server angewendet** (s. u.). `AdminSeed` (fester Rechte-Katalog, Rolle "Administrator" mit allen Rechten, Erstbenutzer `admin`/`Milet!Admin1` — Passwort in `docs/deployment.md` dokumentiert samt Aufforderung zum sofortigen Ändern; "je fehlendem Eintrag ergänzen"-Muster wie `StammdatenSeed`), aus `Milet.Tools.Migrator/Program.cs` nach `StammdatenSeed` aufgerufen. `CurrentSessionService` (ersetzt den bisherigen `SystemCurrentUserService`-Platzhalter aus Phase 1 — implementiert sowohl `ICurrentUserService` als auch `ICurrentSessionService`, Singleton mit `Lock`-geschütztem State, meldet vor Login/für Migrator weiterhin "System" ohne Rechte). `BerechtigungsService`, `AuthService` (lädt Benutzer+Rolle+Rechte, verifiziert Passwort, liefert bei falschem Passwort/unbekanntem Benutzer/deaktiviertem Benutzer bewusst **dieselbe** `null`-Antwort — kein User-Enumeration-Leck), `BenutzerverwaltungService`/`RollenverwaltungService`/`AuditLogService`, `SchemaVersionService`. `AuditSaveChangesInterceptor` erweitert: setzt weiterhin die Audit-Felder (Phase-1-Verhalten unverändert) **und** protokolliert jetzt jede Änderung an `AuditableEntity`-Objekten als `AuditLog`-Zeile. Technisch interessant, weil der Interceptor **Singleton** ist (mehrere `DbContext`-Instanzen aus der Factory) und `SaveChanges` zweistufig arbeitet: Erfassung der Änderungen in `SavingChanges*` (vor dem physischen Speichern, PK von `Added`-Entitäten noch unbekannt) über eine `ConditionalWeakTable<DbContext, List<PendingAudit>>` je Context, Schreiben der `AuditLog`-Zeilen in `SavedChanges*` (danach, PK jetzt bekannt) per zusätzlichem `SaveChangesAsync`-Aufruf auf demselben Context — der terminiert garantiert nach einer Rekursionsebene, weil `AuditLog` selbst keine `AuditableEntity` ist und der zweite Durchlauf dadurch nichts mehr einsammelt (kein Endlosloop, per echtem SQL-Server-Lauf beobachtet: 53 AuditLog-Zeilen aus dem `DummyDatenSeed`-Lauf, keine Rekursionsschleife). RBAC-Guard (`berechtigung.PruefeRecht(...)`) auf die mutierenden Methoden der wichtigsten Services je Modul angewendet: Stammdaten (`KundenService`/`LieferantenService`/`ArtikelService`), Verkauf+Einkauf+Lager-Lieferschein (`BelegService` — ein Guard für alle 7 Belegtypen über eine `RechtFuerTyp`-Zuordnung, da dieser eine Service laut Phase-2/3/4-Architektur die CRUD-Logik aller Belegtypen bündelt), Buchungsservices (`RechnungBuchenService`/`LieferscheinBuchenService`/`WareneingangBuchenService`/`EingangsrechnungBuchenService`), Lager (`BestandService.KorrigiereAsync`/`LagerortService`/`InventurService`), Finanzen (`ZahlungService`/`MahnwesenService`/`DatevExportService.ExportierenAsync`), Administration (`FirmenstammService`/`FibuKonfigurationService` sowie die neuen Admin-Services selbst). DI-Registrierungen ergänzt (`DependencyInjection.cs`).

**Ein Kernstück real gegen SQL Server verifiziert, nicht nur compile-geprüft:** Die AuditLog-Zweistufigkeit (s. o.) ist genau die Art von Interceptor-Timing-Logik, die bei einem reinen Code-Review leicht falsch eingeschätzt wird (Rekursionsgefahr, PK-Timing bei Added-Entities) — der echte `DummyDatenSeed`-Lauf gegen den SQL-Server-Container zeigte 53 korrekt gefüllte `AuditLog`-Zeilen (u. a. `Angebot`/`Rechnung`/`Auftrag`/`OffenerPosten`, jeweils mit korrekter `EntityId` auch bei `Added`-Entities) ohne Fehler oder Hänger.

**App (WinUI, unverifiziert — kein Windows in dieser Session):** `LoginWindow`/`LoginViewModel` (Benutzername/Passwort, `PasswordChanged`-Event-Pattern wie an anderer Stelle in der Codebasis nirgends für PasswordBox vorhanden — neu eingeführt, da UI-Login komplett neu ist; SchemaVersion-Prüfung beim Öffnen, blockiert den Login-Button bei ausstehenden Migrationen). `App.xaml.cs` zeigt jetzt zuerst `LoginWindow`, `MainWindow` (inkl. `App.MainWindow`-Zuweisung für `DialogService`) wird erst nach erfolgreichem Login erzeugt/aktiviert. `AdministrationPage`/`AdministrationViewModel` (drei Pivot-Tabs Benutzer/Rollen/AuditLog im etablierten `KleinstammPage`-Muster: Master-Detail-Formular für Benutzer inkl. Rollen-ComboBox und Passwort-Reset-Feld, Master-Detail mit 7 Rechte-Checkboxen für Rollen, Filter+Liste für AuditLog). Neuer neunter Pivot-Tab "Firmenstamm" in `KleinstammPage` (Briefkopf-Felder, Singleton-Speichern-Pattern wie der bestehende "FibuKonten"-Tab aus Phase 6 — `IFirmenstammService` existierte bereits seit Phase 2, hatte aber nie eine UI). `ShellPage`: "Administration"-Menüpunkt (vorher `IsEnabled="False"`-Platzhalter aus Phase 2) jetzt aktiv verdrahtet; neue `AktualisiereMenueSichtbarkeit()`-Methode blendet nach Login jeden Top-Level-Menüpunkt anhand `ICurrentSessionService.HatRecht(...)` ein/aus (UI-Sichtbarkeit zusätzlich zum Service-Guard, s. PLAN.md "RBAC-Guard in Services UND UI-Sichtbarkeit"). `DashboardViewModel` begrüßt jetzt mit Benutzername+Rolle (kleiner sichtbarer Beleg, dass der Login-Flow durchgängig funktioniert). Alle 40 XAML-Dateien (38 bestehende + 2 neue) als wohlgeformtes XML geprüft, Klammer-/Klammernbalance aller neuen/geänderten C#-Dateien geprüft — ersetzt wie in Phase 5/6 **keinen** echten Compile/XAML-Codegen-Durchlauf.

**Deployment-Story:** neues `docs/deployment.md` — Migrator-Ablauf (inkl. SchemaVersion-Check-Erklärung), unpackaged-self-contained-Publish-Befehl, Erstbenutzer-Zugangsdaten mit Aufforderung zum sofortigen Ändern, bekannte Lücken (kein granulares Rechtesystem, kein erzwungener Passwortwechsel — analog zu den bereits dokumentierten v1-Scope-Grenzen bei GoBD/DATEV).

**Verifiziert (dieser Task, 2026-08-27):**
- Build einzeln: `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/`Milet.Tools.Migrator`/`Milet.Domain.Tests`/`Milet.Application.Tests`/`Milet.IntegrationTests` → **je 0 Fehler, 0 Warnungen**.
- Tests einzeln (MTP-Modus): Domain **44/44** (38 bisherige + 6 neue `PasswortHasherTests`), Application **49/49** (41 bisherige + 8 neue Benutzer-/Rolle-Validator-Tests).
- **IntegrationTests: alle 41 Tests ECHT gegen containerisierten SQL Server gelaufen (nicht übersprungen) — 41/41 bestanden, 0 fehlgeschlagen**, inkl. der 8 neuen `AdminServiceTests` (Login Erfolg/Falsches-Passwort/Deaktiviert/Unbekannt, RBAC-Guard wirft `KeinZugriffException` ohne Recht, Rollenverwaltung speichert Rechte-Zuweisung korrekt, AuditLog enthält nach Anlegen+Ändern eines Benutzers je einen "Angelegt"- und "Geändert"-Eintrag) und aller 33 bestehenden Tests aus Phase 1–6 (kein Fail durch die neuen Konstruktor-Parameter — alle direkt instanziierten Service-Tests auf einen gemeinsamen `AllesErlaubtBerechtigungsService`-Test-Stub umgestellt). Zwei Tests liefen beim ersten Anlauf tatsächlich rot (Testfehler, nicht Servicefehler) und wurden noch in dieser Session gefixt: `Rollenverwaltung_SpeichertRechteZuweisung` (Testfixture seedete nur das Recht "Administration" statt aller 7 Codes — `RollenverwaltungService` fand "Verkauf"/"Stammdaten" dadurch nicht in der DB), `AuditLog_NachAendernEinesBenutzers` (Testfixture baute die rohen `DbContextOptions` ohne `AddInterceptors`, dadurch lief der AuditLog-Interceptor in diesem Test gar nicht mit — beide Fixture-Bugs behoben, seither grün).
- Migration `AdminRbacAuditLog`: Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration) **und real gegen SQL Server angewendet** (Migrator-Lauf inkl. Seeds), Ergebnis per `sqlcmd` gegengeprüft (`Rechte`: 7 Zeilen, `Rollen`: 1 Zeile "Administrator", `RolleRecht`: 7 Zuordnungen, `Benutzer`: 1 Zeile "admin", `AuditLog`: 53 Zeilen aus dem `DummyDatenSeed`-Lauf), zweiter Migrator-Lauf bestätigt Idempotenz (keine ausstehenden Migrationen/Seeds, `admin`-Benutzer nicht dupliziert).

**Nicht durchgeführt — Offen für Phase-7-Abnahme:**
1. **`Milet.App` wurde in dieser Session kein einziges Mal gebaut** (kein Windows verfügbar) — vor Abnahme zwingend: `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows, danach manueller UI-Smoke-Test: Login (falsches Passwort → Fehlermeldung, richtiges Passwort → Shell erscheint), Administration-Tab (Benutzer anlegen/Passwort ändern/Rolle zuweisen, Rolle anlegen mit Rechte-Checkboxen, AuditLog-Filter), Firmenstamm-Tab in Kleinstamm speichern, UI-Sichtbarkeit prüfen (Rolle ohne z. B. "Einkauf"-Recht → Einkauf-Menüpunkt ausgegraut, Zugriff über direkte Navigation testen ob Service-Guard tatsächlich greift auch wenn UI umgangen wird).
2. **Rechte-Guard nicht auf jedem einzelnen Service angewendet** — bewusst auf die mutierenden Methoden der Haupt-Services je Modul beschränkt (s. o., Infrastructure-Abschnitt) statt auf jede der ~30 Service-Klassen einzeln; Lese-Methoden (Suche/Laden) bleiben ungeschützt (Konsistenz mit dem bisherigen Muster, dass Rechte Schreibzugriffe gaten, nicht Sichtbarkeit von Daten — für v1 ausreichend, s. `PLAN.md`-Testkriterium "Rechte-Block greift"). `KleinstammServices.cs` (Einheiten/MwSt/Zahlungsbedingungen/Versandarten/Preislisten/Staffelpreise) und `SeriennummernService`/`OffenePostenService`/`ReportingService`/`VerkaufLookupService`/`EinkaufLookupService`/`StammdatenLookupService` haben **keinen** Guard — nachziehen, falls das Rechtemodell granularer werden soll.
3. Kein automatisierter Test für die UI-Sichtbarkeits-Logik (`ShellPage.AktualisiereMenueSichtbarkeit`) — reiner WinUI-Code, nur per manuellem Smoke-Test (s. Punkt 1) verifizierbar.
4. Kein erzwungener Passwortwechsel bei Erstanmeldung mit dem Seed-Passwort — bewusste v1-Vereinfachung, in `docs/deployment.md` als Betriebsverantwortung dokumentiert.
5. PBKDF2-Iterationszahl (210.000) nicht gegen eine konkrete Performance-Vorgabe kalibriert — Richtwert nach aktuellen OWASP-Empfehlungen (2023+), nicht am Zielsystem gemessen.

 Die 9 Teilschritte aus dem Phase-3-Plan (Task 20, Step 4) — Lagerort/Bestandskorrektur, Auftrag→Teillieferung→Lieferschein→Buchen, zweite Teillieferung, Sammelrechnung, Negativsperre-Fehlermeldung statt Absturz, Inventur-Abschluss, Seriennummern-Auswahl-Dialog beim Buchen — sind noch nicht durchgeklickt worden (Build/Tests/Migration bereits grün, s. oben). Muss vor Abnahme von Phase 3 nachgeholt werden.
2. **Phase 3 — Nachfolge-Findings aus finaler Code-Review (2026-08-26), geparkt für Follow-up:**
   - Korrektur-Grund (`BestandskorrekturDto.Grund`) wird validiert und in der UI erfasst, aber nie auf `Lagerbewegung` persistiert — Ledger-Zeilen manueller Korrekturen sind damit unbegründet nachvollziehbar. Braucht neue Spalte + Migration.
   - `Lagerbewegung.SeriennummerId`/`BenutzerId` werden nie gesetzt (immer NULL) — `SeriennummerId` ist architektonisch unklar (eine Buchung kann mehrere Seriennummern abdecken), `BenutzerId` braucht einen noch fehlenden Current-User-Service.
   - Teillieferung (`UeberleitenMitAuswahlAsync`) und `InventurService` haben keinen automatisierten Test (Docker hier ohnehin nicht verfügbar, daher bisher nur compile-verifiziert).
   - Lagerort deaktivieren versteckt jetzt echten (nicht nur synthetischen Null-)Bestand dort in der Bestandsübersicht (Daten bleiben in der DB, nur diese eine Anzeige zeigt sie nicht mehr) — Regression aus dem C1-Fix in der finalen Review, noch nicht behoben.
   - Diverse Minor-Findings (PdfService-Exception-Parametername, `UeberleitenMitAuswahlAsync` verwirft Nicht-Artikel-Positionen, `LieferscheinListPage` Multiple-Selection+SelectedItem-Überschneidung, N+1-Lookups) — Details im finalen Review-Report, unkritisch.
3. **Phase 4 (Einkauf) — manueller UI-Smoke-Test ausstehend:** Implementiert nach Plan (`docs/superpowers/plans/2026-08-26-phase4-einkauf.md`, 17 Tasks) — Build/Tests/Migration grün (s. Phase-4-Abschnitt oben). Die 10 Teilschritte des manuellen End-to-End-Smoke-Tests (Plan-Task-17, Step 4: Bestellvorschlag→Bestellung→Wareneingang→Buchen inkl. Seriennummern→Eingangsrechnung→Buchen→Abweichungsfall→Negativ-Check) sind noch nicht durchgeklickt worden. Muss vor Abnahme von Phase 4 nachgeholt werden.
4. **Phase 5 (Finanzen+E-Mail) — Windows-Build + manueller UI-Smoke-Test ausstehend, dringlicher als bei Phase 3/4:** Implementiert nach Plan (`docs/superpowers/plans/2026-08-27-phase5-finanzen-mail.md`, 20 Tasks) — Backend-Build/-Tests real grün auf Linux (s. Phase-5-Abschnitt oben), aber `Milet.App` wurde **kein einziges Mal** gebaut (kein Windows in dieser Session verfügbar) und die Migration `FinanzenMahnwesen` nie gegen eine echte DB angewendet. Vor Abnahme zwingend, in dieser Reihenfolge: (1) `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows — erst danach ist überhaupt bekannt, ob die WinUI-Änderungen kompilieren; (2) `Milet.Tools.Migrator` gegen LocalDB laufen lassen, `OffenePosten.Status`-Backfill per `sqlcmd` gegenprüfen; (3) manueller UI-Smoke-Test (OP-Liste, Zahlung mit/ohne Skonto, Mahnstufen-CRUD, Mahnlauf-Selektion→Durchführung→PDF, E-Mail-Fehlerpfad ohne Graph-Konfiguration); (4) optional, nur mit eigener Entra-App-Registrierung: echter Graph-Mail-Versand.
5. **Phase 6 (DATEV+Reporting) — Windows-Build + manueller UI-Smoke-Test ausstehend:** Implementiert nach Plan (`docs/superpowers/plans/2026-08-27-phase6-datev-reporting.md`, 15 Tasks) — Backend-Build/-Tests/Integrationstests/Migration diesmal **real gegen einen containerisierten SQL Server verifiziert** (s. Phase-6-Abschnitt oben, stärkste Linux-Verifikation bisher), aber `Milet.App` wurde weiterhin **kein einziges Mal** gebaut (kein Windows in dieser Session verfügbar). Vor Abnahme zwingend: (1) `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows; (2) manueller UI-Smoke-Test (FibuKonten-Tab, MwSt-Konten, DATEV-Export inkl. Doppelexport-Schutz, alle sechs Reporting-Tabs).
6. **Phase 7 (Admin+Härtung) — Windows-Build + manueller UI-Smoke-Test ausstehend:** Implementiert nach `PLAN.md` (RBAC-Login, Benutzer-/Rollenverwaltung, AuditLog-Viewer, Firmenstamm-UI, Deployment-Story) — Backend-Build/-Tests/Integrationstests/Migration real gegen einen containerisierten SQL Server verifiziert (s. Phase-7-Abschnitt oben, inkl. der AuditLog-Interceptor-Zweistufigkeit — das bisher am ehesten "überraschungsträchtige" neue Stück Infrastruktur-Code in dieser Phase, real beobachtet statt nur angenommen), aber `Milet.App` wurde weiterhin **kein einziges Mal** gebaut (kein Windows in dieser Session verfügbar). Vor Abnahme zwingend: (1) `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows; (2) manueller UI-Smoke-Test (Login-Fehlerpfade, Benutzer-/Rollen-CRUD inkl. Rechte-Checkboxen, AuditLog-Filter, Firmenstamm-Tab, UI-Sichtbarkeit je Rolle); (3) Erstpasswort `admin`/`Milet!Admin1` (s. `docs/deployment.md`) sofort ändern, sobald produktiv verwendet. Phase 1+2 sind komplett abgenommen.

### Review-Fixes 2026-08-29 ⚠️ (rein statisch geschrieben — kein SDK in dieser Session, nichts gebaut, nichts getestet) (Branch `claude/review-2026-08-29-fixes-nsbfs8`)

Umsetzung der Befunde aus `REVIEW_2026-08-29.md` (30 Befunde; die Tabelle „Umsetzungsstand" dort führt
jeden einzelnen mit Stand). Wie schon in der Review-Session war **kein .NET SDK verfügbar** (`dotnet`
nicht vorhanden, der Download ist über den Egress-Proxy gesperrt): die Änderungen sind geschrieben und
gelesen, aber **weder kompiliert noch ausgeführt**. Das ist der wichtigste Vorbehalt zu diesem Stand — vor
allem, weil zwei Konstruktor-Signaturen und ein Service-Interface geändert wurden.

**Kritisch/Hoch:**
- **Jahreswechsel (Befund 1):** `StammdatenSeed` gleicht Nummernkreise jetzt über `(Code, Jahr)` ab statt
  nur über den Code — passend zum Unique-Index. Zusätzlich legt `NumberRangeService` einen fehlenden
  Jahreskreis beim ersten Zugriff selbst an (`INSERT ... WHERE NOT EXISTS`, Format vom jüngsten
  Vorjahreskreis, Unique-Verletzung eines parallelen Aufrufers wird geschluckt und die Vergabe wiederholt).
  Damit steht das System am 01.01. auch dann nicht still, wenn niemand den Migrator startet.
- **Nummernvergabe in der Buchungstransaktion (Befund 5):** `NumberRangeService.NaechsteNummerAsync` gibt
  es jetzt zusätzlich als statische Überladung mit explizitem `MiletDbContext` (Muster wie
  `BestandService.BucheBewegungAsync`). `RechnungBuchenService`, `BelegService` und
  `BelegUeberleitungService` nutzen sie — die Nummer rollt bei einem Fehlschlag mit zurück.
  `BelegService.SpeichereAsync` und `LoescheAsync` laufen dafür neu in einer expliziten Transaktion.
  **Konstruktoränderung:** `BelegService`, `RechnungBuchenService` und `BelegUeberleitungService` bekommen
  `INumberRangeService` nicht mehr injiziert (Integrationstests entsprechend angepasst).
- **RBAC-Löcher (Befunde 2, 3, 18):** `BelegUeberleitungService` prüft Quell- **und** Zieltyp
  (**Konstruktoränderung:** neu mit `IBerechtigungsService`), alle sechs Kleinstamm-Services prüfen
  `Stammdaten`, `SeriennummernService.ErfasseAsync` prüft `Lager`, `EmailVersandService` prüft das Recht
  des versendeten Belegs bzw. `Finanzen`. Die Ableitung Belegtyp → Recht liegt jetzt einmal in
  `RechtCodes.FuerBelegTyp` statt je Service.
- **Immutability-Backstop (Befunde 4, 22):** Der Interceptor prüft zusätzlich `BelegPosition` und
  `BelegSteuerSumme` (Added/Modified/Deleted) sowie `EntityState.Deleted` auf dem Beleg selbst; die
  Fehlermeldung nennt den tatsächlichen Status statt immer „gebucht".
- **DATEV (Befunde 6, 7, 19):** `ExportierenAsync` markiert nichts mehr — die Ids stehen im Ergebnis, das
  Festschreiben ist ein eigener Aufruf `MarkiereAlsExportiertAsync`, den das ViewModel erst **nach** dem
  erfolgreichen Schreiben der Datei macht (**Interface-/DTO-Änderung** an `IDatevExportService` und
  `DatevExportErgebnisDto`). `Zahlung.Gesamtbetrag` ist jetzt der tatsächlich geflossene Betrag, das
  Skonto wird als eigene Zeile je Steuerschlüssel gegengebucht (neuer reiner Domain-Helper
  `SkontoAufteilung` mit 6 Unit-Tests). Der Wirtschaftsjahresbeginn rechnet das Jahr bei abweichendem
  Wirtschaftsjahr zurück.
- **Zahlungszuordnung (Befund 8):** Typ und Geschäftspartner des offenen Postens müssen zur Zahlung passen.
- **Inventur (Befund 9):** Guard gegen eine zweite offene Inventur je Lagerort, Abbruch beim Abschluss,
  wenn sich der Bestand seit der Momentaufnahme verändert hat (Neuaufnahme statt still falscher Buchung),
  keine negativen Ist-Mengen.
- **AuditLog (Befunde 10, 23):** `PasswortHash` und `RowVersion` werden nicht mehr protokolliert; der
  synchrone Pfad nutzt kein `.GetAwaiter().GetResult()` mehr.
- **Letzter Administrator (Befund 17):** Benutzer- und Rollenverwaltung lehnen eine Änderung ab, die den
  letzten aktiven Administrator entfernen würde (greift nicht, wenn es ohnehin schon keinen gibt).

**Zusätzlich beim Umsetzen gefunden (nicht im Review):** Der **Migrator konnte seit Phase 7 auf einer
leeren Datenbank nicht durchlaufen.** `DummyDatenSeed` ruft bewusst die echten Application-Services auf —
die prüfen seit Phase 7 RBAC, während der Migrator gar keine Anmeldung hat: der erste Lauf wäre mit
`KeinZugriffException('Stammdaten')` abgebrochen. `Program.cs` öffnet jetzt vor dem Seed eine technische
Sitzung mit allen Rechten. (Passt zur Phase-7-Notiz „`Milet.App` wurde kein einziges Mal gebaut" — auch
der Migrator wurde seit Phase 7 offensichtlich nie auf einer frischen DB gestartet.)

**Bewusst nicht umgesetzt (jeweils eine Schemaänderung, die ohne SDK weder migriert noch verifiziert
werden kann — Migration + Snapshot von Hand zu schreiben wäre hier das größere Risiko):**
1. **Skontokonten in der `FibuKonfiguration`** (Befund 7). Der Export bucht das Skonto derzeit auf die
   Standard-Sammelkonten des jeweiligen Kontenrahmens (SKR03 8736/3736, SKR04 4736/5736), fest im Code in
   `DatevExportService.SkontoKonto`. Ein ausgeglichener Stapel mit umschlüsselbarem Standardkonto ist
   besser als ein unausgeglichener — die Konten gehören aber neben `BankkontoNr` in die Konfiguration.
2. **Fehlversuchszähler/Lockout am Login** (Befund 13). Das Timing-Leck ist geschlossen, eine Sperre nach
   n Fehlversuchen braucht Spalten auf `Benutzer`.
3. **Erzwungener Wechsel des Initialpassworts** (Befund 30). Als Zwischenschritt warnt der Migrator jetzt
   bei jedem Lauf sichtbar, solange `admin` noch das dokumentierte Passwort hat.

**Weiterhin offen (fachliche Entscheidung, wie im Review empfohlen):**
- **Storno und Gutschrift existieren nicht** (Befund 15). `BelegStatus.Storniert` wird nirgends zugewiesen,
  es gibt keinen Storno-Service und keine Klasse `Gutschrift : Beleg` — der geseedete `GS`-Nummernkreis
  ist unbenutzt. Eine falsch gebuchte Rechnung ist damit **in der Anwendung nicht korrigierbar**.
  `CLAUDE.md` wurde entsprechend berichtigt (sprach von acht Belegarten und von Gegenbuchungen als
  vorhandenem Korrekturweg).
- **`BelegPosition.OffeneMenge` kennt den Status des Folgebelegs nicht** (Befund 16) — latent, solange es
  kein Storno gibt, aber vor dem Storno-Bau zu erledigen.
- **AuditLog-Reihenfolge** (Befund 23): die Audit-Zeilen entstehen weiterhin in `SavedChanges`. In den
  Belegpfaden liegen sie jetzt durch die neue Transaktion in derselben Transaktion wie der fachliche Save;
  generell gilt die Einschränkung weiter.
- **Testabdeckung** (Befund 29): neu sind 6 Unit-Tests zu `SkontoAufteilung` und 2 Integrationstests zum
  Jahreswechsel der Nummernkreise. `BelegImmutabilityInterceptor`, `AuditSaveChangesInterceptor`,
  `ZahlungService` und `MahnwesenService` haben weiterhin keine eigenen Tests.

**Vor der Abnahme dieses Branches zwingend (nichts davon war hier möglich):**
1. `dotnet build` aller Projekte — es wurden Konstruktoren, ein Service-Interface und ein DTO geändert.
2. `dotnet test` je Testprojekt; die Integrationstests **mit Docker**, sonst überspringen genau die neuen
   Nummernkreis-Tests.
3. `Milet.Tools.Migrator` gegen eine **frische** Datenbank (prüft in einem Lauf den Seed-Fix, den
   RBAC-Sitzungs-Fix und die Passwortwarnung) und gegen eine **bestehende** (prüft die Idempotenz des
   `(Code, Jahr)`-Abgleichs).
4. Manueller Smoke-Test des DATEV-Exports (Abbruch im Speichern-Dialog darf jetzt **nichts** markieren)
   und einer Zahlung mit Skonto (Bankzeile = Zahlbetrag, zusätzliche Skontozeile).

### Phase 8 — Gärtnerei/Kulturführung ⚠️ (Backend real gegen SQL Server verifiziert inkl. Migration gegen Alt-Daten; WinUI unverifiziert) (2026-08-30, Branch `claude/phase8-gaertnerei-kultur-plan-rh8jr3`)
Umsetzung des Detailplans `docs/superpowers/plans/2026-08-30-phase8-gaertnerei-kultur.md` (22 Tasks): das
System trackt Pflanzen jetzt zusätzlich über Kulturstufen (Jungpflanze → Teenagerpflanze →
Verkaufspflanze) und einen physischen Gärtnereiplan (Feld → Sektion), statt Kulturpflanzen wie beliebige
Handelsware nur als Gesamtmenge je Lagerort zu führen. Wie Phase 6/7 lief diese Session headless auf
**Linux ohne Windows-Toolchain** (`dotnet-sdk-10.0` per `apt` → 10.0.111, Builds/Tests aus Scratch-Kopie
mit lokal abgesenktem `global.json`, echtes Repo-`global.json` unverändert bei `10.0.400`) — **mit echt
nutzbarem Docker** (`TESTCONTAINERS_RYUK_DISABLED=true`, `mcr.microsoft.com/mssql/server:2022-latest`
zieht über den Sessions-Proxy anstandslos).

**Domain:** neue Entities `Kulturstufe` (konfigurierbare Stammdaten statt Enum — Reihenfolge, Farbe,
`IstVerkaufsfaehig`, s. Plan-Entscheidung E5), `Gaertnereiplan`/`Sektion` (achsenparallele Rechtecke in
Metern auf einem `Lagerort`, E11). `Lagerort` und `Artikel` um Geometrie- bzw. Kulturpflanzen-Felder
erweitert (`IstFeld`/`BreiteMeter`/`HoeheMeter`, `IstKulturpflanze`/`BotanischerName`). `ArtikelBestand`,
`Lagerbewegung`, `BelegPosition` und `InventurPosition` um die beiden neuen nullable Dimensionen
`SektionId`/`KulturstufeId` erweitert — nullable, weil normale Handelsware (E1: Kulturstufe ist eine
Bestandsdimension, kein eigener Artikel) weiterhin ohne sie auskommt. Zwei neue Bewegungstypen
`Kulturzugang`/`Ausfall` (E7: Ausfall ist eine eigene Bewegung, keine negative Korrektur). Neuer reiner
Domain-Service `KulturRegeln` (`PruefeDimensionen`, `NaechsteStufe`, `PruefeStufenwechsel`,
`LiegtInnerhalb`, `Ueberlappt`) mit 17 neuen Unit-Tests (TDD, vor der Infrastruktur geschrieben).

**Application:** neues Modul `Gaertnerei` (DTOs, `IKulturstufenService`/`IGaertnereiplanService`/
`IKulturBuchungService`/`IKulturBestandService`/`IVerfuegbarkeitService`, Validatoren — 17 neue
Validator-Tests). `RechtCodes` um `Gaertnerei` als achten Top-Level-Rechtecode erweitert (E12).
`ArtikelDto`/`ArtikelBestandDto`/`BestandskorrekturDto`/`BelegPositionDto`/`OffenePositionDto`/
`ArtikelVerkaufLookupDto` um die neuen Felder ergänzt, `IVerkaufServices.UeberleitenMitAuswahlAsync`
um einen `dimensionenJePosition`-Parameter erweitert.

**Infrastructure — kritischste Änderung der Phase, `BestandService.BucheBewegungAsync`:** die bisherige
Signatur (Artikel/Lagerort/Delta) bekommt zwei optionale Parameter `sektionId`/`kulturstufeId` und prüft
vor dem Buchen per `KulturRegeln.PruefeDimensionen`, ob eine Kulturpflanze in einem sektionierten Feld
tatsächlich beide Dimensionen mitbekommt. Dabei fiel ein **echter Alt-Bug (E4) auf**: das bestehende
atomare `UPDATE ... WHERE Menge + @delta >= 0`-Muster griff bei einer noch nicht existierenden
Bestandszeile (`betroffeneZeilen == 0` bei positivem Zugang) nur über ein nachfolgendes, ungeschütztes
`INSERT` — zwei parallele Erstbuchungen derselben neuen Artikel/Lagerort/Sektion/Kulturstufe-Kombination
konnten beide das `UPDATE` mit 0 betroffenen Zeilen sehen und dann beide `INSERT`en, was entweder einen
Unique-Constraint-Fehler oder (ohne den Unique-Index) zwei Zeilen statt einer erzeugt hätte. **Behoben**
durch ein echtes SQL-Upsert mit `INSERT ... SELECT ... WHERE NOT EXISTS (... WITH (UPDLOCK, HOLDLOCK) ...)`
gefolgt vom erneuten `UPDATE`, sodass parallele Erstbuchungen sich gegenseitig sperren statt zu kollidieren
— **mit echtem Parallelitätstest gegen containerisierten SQL Server verifiziert** (s. u.), nicht nur
angenommen wie der weiterhin offene READ-COMMITTED-Befund in `BelegUeberleitungService` (s. „Bekannte
Risiken").

EF-Configurations für `Kulturstufe`/`Gaertnereiplan`/`Sektion`; bei `ArtikelBestand` legte die
EF-Core-SqlServer-Konvention beim eindeutigen Index über die vier Dimensionen automatisch einen
**gefilterten** Index (`WHERE SektionId IS NOT NULL AND KulturstufeId IS NOT NULL`) an — das Gegenteil von
SQL Servers nativer NULL-Behandlung in Unique-Indizes (NULL gilt dort als eindeutig gleich sich selbst)
und hätte E3s Design unterlaufen; per explizitem `.HasFilter(null)` unterdrückt und **real auf SQL Server
verifiziert** (`is_unique=1, has_filter=0`, s. u.). Migration `GaertnereiKultur` — Modellkonsistenz per
zweitem `dotnet ef migrations add` verifiziert (leere Diff-Migration), **real gegen eine frische DB
angewendet und zusätzlich gegen eine Datenbank mit vor-Phase-8-Bestandsdaten** (Checkout der
Vor-Phase-8-Revision `8c950c1` in einem separaten Git-Worktree, dort Alt-Bestand angelegt, dann mit dem
Phase-8-Code migriert) — Alt-Daten blieben unverändert (`NULL`-Dimensionen, identische Kunden-/Belegzahlen).
`StammdatenSeed`/`DummyDatenSeed` um Kulturstufen, einen Beispiel-Gärtnereiplan und Gärtnerei-Demodaten
erweitert (dabei ein Seed-Bug gefunden und behoben: Hosta/Astilbe-Kulturbestand wurde auf ein falsches
Feld gebucht, s. Fehlerliste unten).

Neue Services `KulturstufenService`/`GaertnereiplanService`/`KulturBuchungService` (Zugang/
Stufenwechsel/Umsetzen/Ausfall — Stufenwechsel ist bewusst **immer** ein Abgang+Zugang als zwei
Ledger-Zeilen, nie ein Update, E6)/`KulturBestandService` (Pflanzenliste, Fundstellen, Historie)/
`VerfuegbarkeitService` (E8: rein beratend/nicht blockierend, Ampel Grün/Gelb/Rot, Reservierung wird aus
offenen Auftragspositionen berechnet statt gespeichert). `LieferscheinBuchenService`/
`WareneingangBuchenService`/`BelegUeberleitungService` reichen die Dimensionen durch und prüfen bei
Kulturpflanzen hart, dass nur aus einer verkaufsfähigen Kulturstufe geliefert werden kann (E9).
`InventurService` musste für den Feld-Zweig umgebaut werden (E10: eine Inventurzeile pro Bestandszeile auf
einem Feld statt pro Artikel) — dabei ein EF-Core-Tracking-Bug gefunden: `QueryTrackingBehavior` gilt für
eine **ganze** zusammengesetzte LINQ-Query, nicht pro `.Join()`-Quelle einzeln; das Mischen einer
`AsNoTracking()`-Quelle mit einer getrackten in einem `Join` führte je nach Kombination entweder zu
„cannot be tracked"-Identitätskonflikten oder zu einem `IDENTITY_INSERT`-Fehler beim Speichern. Behoben
durch zwei getrennte Abfragen (eine `AsNoTracking()` für Bestandszeilen, eine echt getrackte für einen
gemeinsamen `Artikel`-Instanz-Dictionary), kombiniert in einer C#-Schleife — passend zum bereits
bestehenden, funktionierenden Nicht-Feld-Pfad. `BestandService.SucheAsync` liefert jetzt eine
`ArtikelBestandDto`-Zeile je tatsächlicher Bestandszeile statt aggregiert.

**App (WinUI, unverifiziert — kein Windows in dieser Session):** Kleinstamm-Tab „Kulturstufen" (Master-
Detail wie die bestehenden Kleinstamm-Tabs), Artikel-Edit um Kulturpflanzen-Felder erweitert (Umschalten
von „ist Kulturpflanze" wird beim Vorhandensein von Bestand blockiert). Neue Seiten `GrundrissPage`
(Feld-/Sektions-Editor — sowohl per Maus-Ziehen als auch per Zahleneingabe, „Plan B" aus dem Plan: Rendern
per Code-behind-Canvas statt `ItemContainerStyle`, weil `x:Bind` in einem Style keine Attached Properties
setzen kann, E11), `PflanzenUebersichtPage` (Fundstellentabelle + Grundriss-Highlight je Kulturstufenfarbe),
`KulturbuchungPage` (Zugang/Stufenwechsel/Umsetzen/Ausfall, Zielstufe wird per `KulturRegeln.NaechsteStufe`
vorbelegt). `BestandUebersichtPage` um Feld/Sektion/Kulturstufe-Spalten und -Filter erweitert,
`TeillieferungDialog` wählt bei Kulturpflanzen automatisch die verkaufsfähige Stufe mit der größten Menge
vor (E9). `AngebotEditPage`/`AuftragEditPage` bekommen ein Verfügbarkeits-Panel (Ampel-Farbe über neuen
`AmpelToColorConverter`). Neue Converter `HexColorToBrushConverter`/`BoolToVisibilityConverter`
(mit optionalem `"invers"`-Parameter). `ShellPage`/`App.xaml.cs`: neuer Top-Level-Menüpunkt „Gärtnerei"
mit den drei neuen Seiten, Sichtbarkeit über das neue Recht gesteuert. `ReportingPage` um drei Tabs
Kulturbestand/Ausfallquote/Flächenbelegung samt CSV-Export erweitert. Alle neuen/geänderten XAML-Dateien
als wohlgeformtes XML geprüft, alle neuen/geänderten C#-Dateien auf Klammerbalance geprüft — ersetzt wie
in Phase 5–7 **keinen** echten Compile/XAML-Codegen-Durchlauf; die drei neu verwendeten `Symbol`-Icon-Namen
(`Globe`/`Map`/`Edit`) sind nicht gegen einen echten WinUI-Compiler verifiziert.

**Verifiziert (dieser Task, 2026-08-30):**
- Build einzeln: `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/`Milet.Tools.Migrator`/
  `Milet.Domain.Tests`/`Milet.Application.Tests`/`Milet.IntegrationTests` → je 0 Fehler, 0 Warnungen.
- Tests einzeln (MTP-Modus): Domain **72/72** (inkl. 17 neuer `KulturRegelnTests`), Application **66/66**
  (inkl. 17 neuer `GaertnereiValidatorTests`).
- **IntegrationTests: alle 78 Tests ECHT gegen containerisierten SQL Server gelaufen (nicht übersprungen)
  — 78/78 bestanden**, u. a. `BestandServiceKulturDimensionenTests` (6 Tests, inkl. des Parallelitätstests
  `ParalleleErstbuchungen_GleicheKombination_EineZeileMengeIstSumme`, der den E4-Race gezielt reproduziert
  und den Upsert-Fix verifiziert), `KulturBuchungServiceTests` (7), `KulturBestandServiceTests` (4),
  `LieferscheinBuchenServiceTests` (erweitert, +3), `InventurServiceTests` (4),
  `VerfuegbarkeitServiceTests` (6), `GaertnereiReportingServiceTests` (4). Eine vorbestehende, phasenfremde
  xUnit1051-Analyzer-Verletzung in `NumberRangeServiceTests.cs` (fehlender `CancellationToken`-Parameter,
  blockierte unter `TreatWarningsAsErrors` den gesamten Testprojekt-Build) minimal gefixt.
- Migration `GaertnereiKultur`: Modellkonsistenz per zweitem `dotnet ef migrations add` verifiziert (leere
  Diff-Migration), real gegen eine frische SQL-Server-Datenbank angewendet (Migrator-Lauf inkl. Seeds,
  per `sqlcmd` gegengeprüft) **und zusätzlich gegen eine mit der Vor-Phase-8-Codebasis (Commit `8c950c1`,
  separater Git-Worktree) befüllte Datenbank** — Alt-Bestandszeilen mit `NULL`-Dimensionen sowie
  Kunden-/Belegzahlen per `sqlcmd` vor/nach der Migration identisch bestätigt, Unique-Index auf
  `ArtikelBestaende` mit `is_unique=1, has_filter=0` (der `.HasFilter(null)`-Fix hält auch real, nicht nur
  im EF-Modell).

**Nicht durchgeführt — Offen für Phase-8-Abnahme:**
1. **`Milet.App` wurde in dieser Session kein einziges Mal gebaut** (kein Windows verfügbar) — vor Abnahme
   zwingend: `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf Windows, danach der
   14-Schritte-Ablauf in `docs/smoke-tests.md` (Kulturstufen umbenennen, Grundriss per Maus **und**
   Zahleneingabe anlegen inkl. bewusst überlappender Sektionen, Kulturzugang/Stufenwechsel/Ausfall buchen,
   Pflanzenübersicht-Highlight prüfen, Ampel Gelb→Grün im Auftrag, Lieferschein mit Auto-Vorauswahl der
   verkaufsfähigen Stufe buchen und per `sqlcmd` gegenprüfen, Bestandsübersicht-Filter, Reporting-Tabs +
   CSV-Export).
2. Die drei neuen `Symbol`-Icon-Namen (`Globe`/`Map`/`Edit`) in `ShellPage.xaml` sind nicht gegen einen
   echten WinUI-Compiler verifiziert — ein falscher Symbol-Name wäre ein XAML-Compile-Fehler, erst mit
   Windows-Build (s. Punkt 1) feststellbar.
3. Der weiterhin offene READ-COMMITTED-Race in `BelegUeberleitungService` (s. „Bekannte Risiken") ist durch
   diese Phase **nicht** behoben — er betrifft jetzt zusätzlich die dimensionsbehaftete Teillieferung
   (E9-Auswahl einer Sektion/Kulturstufe), wurde aber nicht gesondert untersucht.
4. Kein automatisierter Test für den Grundriss-Canvas-Code-behind (`GrundrissPage.xaml.cs`, „Plan B" aus
   E11) — reiner WinUI-Rendering-Code, nur per manuellem Smoke-Test (s. Punkt 1) verifizierbar.

## Gefixt während UI-Test (2026-08-25)
- LocalDB-Datenbank hieß nach Projekt-Rename noch "Nexus" (Connection String erwartet "Milet") → "Fehler beim Laden" beim Öffnen der Kunden-Liste. Per `ALTER DATABASE ... MODIFY NAME` umbenannt (Seed-Daten erhalten), App neu gestartet — Kunden-Liste lädt jetzt.
- Listenpreis-Präzision 4→2 Nachkommastellen (s. oben, Phase-1-Abnahme).
- Absturz beim Löschen des letzten Staffelpreises einer Preisliste (`NullReferenceException` in WinUI ComboBox-Binding, s. oben, Phase-1-Abnahme) — vom Nutzer live entdeckt und gemeldet.
- Phase 2: Positions-Bezeichnung + IsEnabled-Scoping (s. oben, Phase-2-Abnahme).

## Bekannte Risiken (aus Plan, weiterhin relevant)
- **[Behoben am 2026-08-30, Phase 8]** Race beim Erstanlegen einer `ArtikelBestand`-Zeile in
  `BestandService.BucheBewegungAsync` (E4 aus dem Phase-8-Plan): das atomare `UPDATE`-Muster deckte nur den
  Fall einer bereits existierenden Zeile ab; traf `UPDATE` auf 0 Zeilen (Erstbuchung), folgte ein
  ungeschütztes `INSERT`, das bei zwei parallelen Erstbuchungen derselben Artikel/Lagerort/Sektion/
  Kulturstufe-Kombination hätte kollidieren können. Behoben durch ein SQL-Upsert mit
  `INSERT ... WHERE NOT EXISTS (... WITH (UPDLOCK, HOLDLOCK) ...)` vor dem erneuten `UPDATE`. Nachweis:
  `BestandServiceKulturDimensionenTests.ParalleleErstbuchungen_GleicheKombination_EineZeileMengeIstSumme`,
  real gegen containerisierten SQL Server gelaufen (s. Phase-8-Abschnitt oben).
- Kein Docker auf dieser Maschine → Integrationstests mit Testcontainers laufen hier nur übersprungen, nicht tatsächlich ausgeführt. Das betrifft inzwischen konkret Phase 3: `BestandServiceTests` (Race-/Negativsperre-Test des atomaren `BucheBewegungAsync`-UPDATE) und `LieferscheinBuchenServiceTests` (paralleles Buchen) sind **nie gegen eine echte DB gelaufen**, nur compile-verifiziert + sauber übersprungen. Der manuelle UI-Smoke-Test (s. „Offen") würde die fachliche Kernlogik zumindest einmal seriell gegen LocalDB nachweisen, ersetzt aber nicht den Parallelitäts-Nachweis. Docker sollte vor Produktivsetzung verfügbar gemacht werden oder ein LocalDB-Fallback für diese Tests ergänzt werden.
- **[Umgesetzt in Phase 5, funktional unverifiziert]** Graph-Auth: `GraphEmailService` (MSAL/WAM-Broker) ist implementiert und baut gegen die echten NuGet-Pakete, aber ohne eigene Entra-App-Registrierung + Windows nicht testbar — `NichtKonfigurierterEmailService`-Fallback stellt sicher, dass die App ohne Graph-Konfiguration voll funktionsfähig bleibt. DATEV-Format — noch nicht relevant, erst ab Phase 6. QuestPDF-Lizenz (Community, <1M USD Umsatz) bereits gesetzt (`PdfService`-statischer Konstruktor).
- Lieferadresse ist in Phase 2 nicht im Belegeditor editierbar (immer 1:1 aus Kundenstamm übernommen) — bewusste Vereinfachung, relevant erst mit Lieferschein (Phase 3).
- **Offene-Mengen-Prüfung in `BelegUeberleitungService` (inkl. `UeberleitenMitAuswahlAsync`/`UeberleitenMehrereAsync`) schützt trotz gegenteiligem Kommentar im Code vermutlich nicht gegen parallele Überleitungen**: der In-Transaktion-Re-Check liest unter SQL Servers Default-Isolationslevel READ COMMITTED ohne Sperre — zwei gleichzeitige Transaktionen können beide „nichts geliefert" sehen und beide committen. Folgenlos bei Angebot→Auftrag (1:1, keine Teilmengen), aber ein echter potenzieller Bestandsfehler bei paralleler Teillieferung/Sammelrechnung. Noch nicht verifiziert (Docker hier nicht verfügbar) oder behoben — möglicher Fix: `UPDLOCK` auf dem/den Quellbeleg(en) beim Lesen. Fund stammt aus einem parallel entstandenen, nicht umgesetzten Planungsentwurf (`docs/superpowers/plans/2026-08-26-phase3-lager-lieferschein.md`).
- **[Behoben in Phase 4, nachgebessert 2026-08-29]** `StammdatenSeed` legt Nummernkreise nur an, wenn die `Nummernkreise`-Tabelle komplett leer ist, nicht „je fehlendem Code" — eine bereits migrierte Datenbank bekommt einen später neu hinzugefügten Nummernkreis-Code nie automatisch nachgetragen. Bisher folgenlos (alle bislang genutzten Codes existierten schon vor der ersten Migration), wird aber relevant, sobald eine spätere Phase einen neuen Code auf einer bestehenden DB einführt. Genau dieser Fall trat mit Phase 4 ein (neue Codes `WE`/`ER`) und wurde in Task 5 des Phase-4-Plans behoben — der Seed wurde auf „je fehlendem Code ergänzen" umgestellt statt „nur wenn Tabelle leer"; per `sqlcmd` verifiziert, dass `BE`/`WE`/`ER` alle mit `NaechsteNummer=1` existieren (s. Phase-4-Abschnitt oben). Der Fix griff allerdings nicht für den Jahreswechsel: der Abgleich lief nur über den Code, während `NumberRangeService` strikt nach dem laufenden Jahr sucht — ab dem 01.01. hätte das System keine Belegnummer mehr vergeben können (Befund 1 des Reviews vom 2026-08-29). Seither Abgleich über `(Code, Jahr)` plus Lazy-Anlage des Jahreskreises im `NumberRangeService` (s. Abschnitt Review-Fixes 2026-08-29).

### Phase 9 — Lückenschluss ⏳ (Block 9a abgeschlossen, Rest offen) (2026-08-31)

Umsetzung von `docs/superpowers/plans/2026-08-31-luecken-schliessen.md`. Diese Session lief headless auf
**Linux ohne Windows-Toolchain** (`dotnet-sdk-10.0` per `apt` → 10.0.111, Builds/Tests aus einer
**Scratch-Kopie** mit lokal abgesenktem `global.json` — das echte, committete `global.json` bleibt
unverändert bei `10.0.400`) — wie in Phase 6–8, mit echt nutzbarem Docker
(`TESTCONTAINERS_RYUK_DISABLED=true`, `mcr.microsoft.com/mssql/server:2022-latest` zieht über den
Sessions-Proxy anstandslos, `dockerd` manuell gestartet).

**Block 9a — Fundament: den aktuellen Stand überhaupt erst nachweisen (Task 1–2) ✅**

Ausgangslage: `Milet.App` wurde seit Phase 5 kein einziges Mal gebaut, und die Review-Fixes vom
2026-08-29 (Konstruktoränderungen an `BelegService`/`RechnungBuchenService`/`BelegUeberleitungService`,
Interface-Änderung an `IDatevExportService`, DTO-Änderung an `DatevExportErgebnisDto`) liefen ebenfalls
nie durch einen Compiler. Vor jedem neuen Feature musste deshalb erst geklärt werden, ob der bestehende
Code überhaupt übersetzt.

- **Build einzeln, real:** `Milet.Domain`/`Milet.Application`/`Milet.Infrastructure`/
  `Milet.Tools.Migrator` sowie alle drei Testprojekte → **je 0 Fehler, 0 Warnungen**. Die
  Konstruktor-/Interface-/DTO-Änderungen aus den Review-Fixes kompilieren wie vorgesehen — kein
  Rückstand aus der SDK-losen Review-Session.
- **Tests einzeln (MTP-Modus):** Domain **72/72**, Application **66/66**.
- **IntegrationTests: alle 78 Tests ECHT gegen containerisierten SQL Server gelaufen (nicht
  übersprungen) — 78/78 bestanden, 0 fehlgeschlagen** — exakt der in diesem Dokument nach Phase 8
  erwartete Stand, keine Regression durch die Review-Fixes.
- **Migrator gegen frische DB** (separater `mcr.microsoft.com/mssql/server:2022-latest`-Container,
  nicht der Testcontainers-verwaltete): erster Lauf wendet alle 9 Migrationen an (bis
  `GaertnereiKultur`), RBAC-Seed legt Rechte/Administrator-Rolle/Erstbenutzer an, `DummyDatenSeed`
  läuft durch, die dokumentierte Initialpasswort-Warnung erscheint. Per `sqlcmd` gegengeprüft:
  `__EFMigrationsHistory` 9 Zeilen (neueste `GaertnereiKultur`), `Benutzer` 1 Zeile, `Rechte` 8 Zeilen
  (7 Module + Gärtnerei), `Nummernkreise` 11 Zeilen, `Kulturstufen` 3 Zeilen.
- **Zweiter Migrator-Lauf (Idempotenz):** meldet „Datenbank ist aktuell — keine ausstehenden
  Migrationen", RBAC-Grunddaten „geprüft/angelegt" ohne zweiten `admin`-Benutzer (weiterhin 1 Zeile in
  `Benutzer` nach dem zweiten Lauf).
- **Modellkonsistenz:** zweite `dotnet ef migrations add` auf dem seither unveränderten Modell liefert
  eine leere Diff-Migration (kein `migrationBuilder.*`-Aufruf in `Up`/`Down`) — kein Drift zwischen
  EF-Modell und tatsächlichem SQL-Server-Schema. Testmigration danach wieder entfernt.

**Ergebnis von Block 9a:** Der Stand nach den Review-Fixes vom 2026-08-29 ist damit erstmals real
gebaut und getestet — vorher war das reine Behauptung einer SDK-losen Session. Kein einziger Fund; alle
nachfolgenden Blöcke (9b–9f, s. Plan) bauen auf einem tatsächlich verifizierten Fundament.

**Nicht durchgeführt — weiterhin offen:**
1. **`Milet.App` wurde in dieser Session weiterhin kein einziges Mal gebaut** (kein Windows
   verfügbar) — unverändert gegenüber Phase 5–8.
2. Block 9b (Storno/Gutschrift), 9c (Ledger-Grund/Benutzer), 9d (Skontokonten/Login-Lockout/
   Passwortwechsel/Lagerort-Regression), 9e (Parallelitäts-Race der Überleitung) und 9f
   (Testlücken/Lieferadresse) aus `docs/superpowers/plans/2026-08-31-luecken-schliessen.md` sind
   **noch nicht begonnen** — insbesondere ist der in „Bekannte Risiken" seit Phase 3 geführte
   READ-COMMITTED-Verdacht in `BelegUeberleitungService` durch Block 9a **nicht** berührt worden.

## Phasenübersicht





**1 Stammdaten**           Done
**2 Verkauf+PDF**          Done
**3 Lager+Lieferschein**   Done
**4 Einkauf**              Done (manueller UI-Smoke-Test ausstehend)
**5 Finanzen+E-Mail**      Done (WinUI-Build + manueller UI-Smoke-Test + Migration-auf-DB ausstehend — kein Windows in dieser Session)
**6 DATEV+Reporting**      Done (Backend real gegen SQL Server verifiziert; WinUI-Build + manueller UI-Smoke-Test ausstehend — kein Windows in dieser Session)
**7 Admin+Härtung**        Done (Backend real gegen SQL Server verifiziert; WinUI-Build + manueller UI-Smoke-Test ausstehend — kein Windows in dieser Session)
**8 Gärtnerei/Kultur**     Done (Backend real gegen SQL Server verifiziert inkl. Migration gegen Alt-Daten; WinUI-Build + manueller UI-Smoke-Test ausstehend — kein Windows in dieser Session)
**9 Lückenschluss**        In Arbeit (Block 9a Build-/Testnachweis abgeschlossen; Storno/Gutschrift, Ledger-Nachvollziehbarkeit, Finanz-/Admin-Härtung, Parallelitäts-Race noch offen — Details `docs/superpowers/plans/2026-08-31-luecken-schliessen.md`)
