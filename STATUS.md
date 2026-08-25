# Milet — Projektstatus

Stand: 2026-08-25. Plan: `C:\Users\lulef\.claude\plans\ich-m-chte-ein-warenwirtschaftssystem-immutable-sutton.md`

## Erledigt

### Umgebung
- .NET 10 SDK (10.0.400) user-lokal installiert, `dotnet-ef` global installiert
- nuget.org als Paketquelle eingerichtet
- SQL-Zugriff: LocalDB `(localdb)\MSSQLLocalDB`, DB "Milet" (kein Docker/SQL Server auf dieser Maschine — Details in Memory `nexus-dev-umgebung`)

### Phase 0 — Scaffold ✅ vollständig verifiziert
- Solution mit 6 Projekten (Domain/Application/Infrastructure/App/Tools.Migrator) + 3 Testprojekten
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

## Offen

1. **Lieferanten-CRUD** noch nicht live durchgeklickt (Kunden/Artikel/Kleinstamm/Staffelpreise sind es, Lieferanten-Seite ist strukturell identisch zu Kunden, aber nicht separat verifiziert).

2. **Phasen 2–7** (Verkauf+PDF, Lager, Einkauf, Finanzen+Mail, DATEV+Reporting, Admin) — noch nicht begonnen, siehe Plan-Datei für Details.

## Gefixt während UI-Test (2026-08-25)
- LocalDB-Datenbank hieß nach Projekt-Rename noch "Nexus" (Connection String erwartet "Milet") → "Fehler beim Laden" beim Öffnen der Kunden-Liste. Per `ALTER DATABASE ... MODIFY NAME` umbenannt (Seed-Daten erhalten), App neu gestartet — Kunden-Liste lädt jetzt.
- Listenpreis-Präzision 4→2 Nachkommastellen (s. oben, Phase-1-Abnahme).
- Absturz beim Löschen des letzten Staffelpreises einer Preisliste (`NullReferenceException` in WinUI ComboBox-Binding, s. oben, Phase-1-Abnahme) — vom Nutzer live entdeckt und gemeldet.

## Bekannte Risiken (aus Plan, weiterhin relevant)
- Kein Docker auf dieser Maschine → Integrationstests mit Testcontainers laufen hier nur übersprungen, nicht tatsächlich ausgeführt. Vor Phase mit kritischen Transaktionstests (Lager, Buchungspipeline) sollte Docker verfügbar gemacht werden oder LocalDB-Fallback für Tests ergänzt werden.
- QuestPDF-Lizenz, Graph-Auth, DATEV-Format — noch nicht relevant, erst ab Phase 2/5/6.
