# Milet — Projektstatus

Stand: 2026-08-24. Plan: `C:\Users\lulef\.claude\plans\ich-m-chte-ein-warenwirtschaftssystem-immutable-sutton.md`

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
- Kunden: `KundenListViewModel` + `KundenListPage.xaml` (Suche, Liste, Neu/Bearbeiten/Löschen) + `KundeEditViewModel` + `KundeEditPage.xaml` (Formular, Validierungsfehler-Anzeige, Concurrency-Dialog) — **Code steht, UI noch NICHT im laufenden Programm getestet**
- Lieferanten: `LieferantenListViewModel` + `LieferantEditViewModel` geschrieben — **XAML-Views fehlen noch**
- Artikel: `ArtikelListViewModel` + `ArtikelEditViewModel` geschrieben — **XAML-Views fehlen noch**
- DI-Registrierungen in `App.xaml.cs` ergänzt

## Offen

1. **Sofort als Nächstes:**
   - `LieferantenListPage.xaml`, `LieferantEditPage.xaml`, `ArtikelListPage.xaml`, `ArtikelEditPage.xaml` erstellen (Muster von KundenListPage/KundeEditPage übernehmen)
   - Neue Seiten in `ShellPage.xaml.cs` bei `NavigationService` registrieren
   - `ShellPage.xaml`: NavigationView-Menüpunkte für Stammdaten (aktuell `IsEnabled="False"`) aktivieren und verdrahten
   - Kompletten Build erneut prüfen (letzte ViewModel-Änderungen noch ungetestet)

2. **Kleinstamm-Settings-UI** (laut Plan Teil von Phase 1, noch nicht begonnen): einfache CRUD-Masken für Einheiten, MwSt-Sätze, Zahlungsbedingungen, Versandarten, Preislisten

3. **Phase-1-Abnahmekriterien noch zu verifizieren:**
   - CRUD für Kunden/Lieferanten/Artikel im laufenden UI tatsächlich durchklicken
   - Concurrency-Dialog live auslösen (Datensatz in zwei Editoren gleichzeitig öffnen)
   - Kundennummer-Autovergabe im UI sichtbar prüfen

4. **Phasen 2–7** (Verkauf+PDF, Lager, Einkauf, Finanzen+Mail, DATEV+Reporting, Admin) — noch nicht begonnen, siehe Plan-Datei für Details.

## Bekannte Risiken (aus Plan, weiterhin relevant)
- Kein Docker auf dieser Maschine → Integrationstests mit Testcontainers laufen hier nur übersprungen, nicht tatsächlich ausgeführt. Vor Phase mit kritischen Transaktionstests (Lager, Buchungspipeline) sollte Docker verfügbar gemacht werden oder LocalDB-Fallback für Tests ergänzt werden.
- QuestPDF-Lizenz, Graph-Auth, DATEV-Format — noch nicht relevant, erst ab Phase 2/5/6.
