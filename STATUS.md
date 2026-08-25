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
- Preisliste-UI hat nur Name/Gültig-von/-bis (Header); Staffelpreise (`ArtikelPreis`) haben noch keine UI — s. offen unten
- Layout-Bug gefixt: erste Spalte der Pivot-Tabs hatte `Width="*"` → Liste füllte gesamte Breite, Formular klebte am rechten Fensterrand mit riesiger Lücke dazwischen. Grid auf 3 Spalten umgestellt (`380` Liste / `360` Formular / `*` Spacer) in allen 5 Tabs.
- **Verifiziert:** Build + Application-/Domain-Tests grün, App startet clean, Layout per Screenshot geprüft

## Offen

1. **Sofort als Nächstes — Phase-1-Abnahmekriterien noch manuell zu verifizieren** (kein UI-Automation-Tool für WinUI-Desktop-Apps verfügbar, App läuft aber und wartet):
   - CRUD für Kunden/Lieferanten/Artikel/Kleinstamm im laufenden UI tatsächlich durchklicken (inkl. Suche/Neu/Bearbeiten/Löschen)
   - Concurrency-Dialog live auslösen (Datensatz in zwei Editoren gleichzeitig öffnen)
   - Kundennummer-/Lieferantennummer-/Artikelnummer-Autovergabe im UI sichtbar prüfen
   - Geldbeträge (NumberBox mit Convertern) auf korrekte Anzeige/Rundung prüfen

2. **Staffelpreise (ArtikelPreis) je Preisliste**: keine UI zum Pflegen der Preisliste-Zeilen (AbMenge/Preis pro Artikel) — für vollständige Preisfindung im UI nötig, aktuell nur über Seed-Daten/Code vorhanden.

3. **Phasen 2–7** (Verkauf+PDF, Lager, Einkauf, Finanzen+Mail, DATEV+Reporting, Admin) — noch nicht begonnen, siehe Plan-Datei für Details.

## Gefixt während UI-Test (2026-08-25)
- LocalDB-Datenbank hieß nach Projekt-Rename noch "Nexus" (Connection String erwartet "Milet") → "Fehler beim Laden" beim Öffnen der Kunden-Liste. Per `ALTER DATABASE ... MODIFY NAME` umbenannt (Seed-Daten erhalten), App neu gestartet — Kunden-Liste lädt jetzt.

## Bekannte Risiken (aus Plan, weiterhin relevant)
- Kein Docker auf dieser Maschine → Integrationstests mit Testcontainers laufen hier nur übersprungen, nicht tatsächlich ausgeführt. Vor Phase mit kritischen Transaktionstests (Lager, Buchungspipeline) sollte Docker verfügbar gemacht werden oder LocalDB-Fallback für Tests ergänzt werden.
- QuestPDF-Lizenz, Graph-Auth, DATEV-Format — noch nicht relevant, erst ab Phase 2/5/6.
