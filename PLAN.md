# Plan: „Nexus" — Warenwirtschaftssystem (.NET 10 / WinUI 3 / SQL Server)

## Kontext

Greenfield-Projekt in leerem Verzeichnis `d:\Projects\Nexus`. Ziel: deutsches Warenwirtschaftssystem (Orientierung „Rita Bosse") mit den Modulen Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Administration.

**Bestätigte Entscheidungen (Nutzer):**
- .NET 10 LTS (statt 8/9 — beide nahe/über EOL)
- WinUI 3 (Windows App SDK), MVVM via CommunityToolkit.Mvvm
- EF Core + SQL Server, Mehrplatzbetrieb (zentraler Server, optimistische Concurrency via RowVersion)
- QuestPDF für Belege (Community License beachten)
- E-Mail via Microsoft Graph API (MSAL/WAM), abstrahiert hinter `IEmailService`
- Schichtenarchitektur: Domain / Application / Infrastructure / Presentation

**Bewusst NICHT verwendet:** MediatR (kommerziell, kein Mehrwert in Desktop-App), AutoMapper (dito) — stattdessen Plain Services + explizite Mapping-Extensions. Kein Repository-Layer über EF (DbContext ist UoW).

## Solution-Struktur

```
Nexus.sln
Directory.Build.props / Directory.Packages.props (Central Package Management)
src/
  Nexus.Domain/           # keine Dependencies; Entities, Enums, ValueObjects,
                          # PreisfindungService, SteuerRechner, AuditableEntity
  Nexus.Application/      # → Domain; Abstractions (IEmailService, IPdfService,
                          # ICurrentUserService, INumberRangeService), Services+DTOs+Validators
                          # je Modul (Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Admin)
  Nexus.Infrastructure/   # → Application; NexusDbContext, Configurations, Migrations,
                          # Interceptors, QuestPDF-Dokumente, GraphEmailService, DatevCsvWriter
  Nexus.App/              # WinUI 3; Host-Builder-DI, ShellPage+NavigationView,
                          # Views/ViewModels je Modul, NavigationService, DialogService
  Nexus.Tools.Migrator/   # Konsole: Migrationen anwenden + Seeds; Startup-Projekt für dotnet-ef
tests/
  Nexus.Domain.Tests / Nexus.Application.Tests / Nexus.IntegrationTests (Testcontainers.MsSql)
```

Pakete: Microsoft.WindowsAppSDK 1.8.x, CommunityToolkit.Mvvm 8.4.x, CommunityToolkit.WinUI DataGrid, Microsoft.EntityFrameworkCore.SqlServer 10.x, FluentValidation 12.x, QuestPDF 2026.x, Microsoft.Graph 5.x + Microsoft.Identity.Client.Broker, Serilog, xunit.v3, NSubstitute, Testcontainers.MsSql.

## Datenmodell (Kern)

### Beleg-Pattern: EINE Tabelle, TPH-Discriminator
Alle 8 Belegtypen (Angebot, Auftrag, Lieferschein, Rechnung, Gutschrift, Bestellung, Wareneingang, Eingangsrechnung) in einer `Beleg`-Tabelle + einer `BelegPosition`-Tabelle, EF-TPH mit Basisklasse `Beleg` und dünnen Subklassen (`Rechnung : Beleg`). Grund: Überleitung, Nummerierung, Druck, Referenzgraph identisch — separate Tabellen = 8× dupliziertes Plumbing.

**Beleg (Kopf):** BelegTyp, BelegNummer (unique je Typ), BelegDatum, KundeId?/LieferantId? (Check-Constraint), Adress-**Snapshots** als Owned Types (Rechnungs-/Lieferadresse — eingefroren bei Erstellung), Zahlungsbedingung-Snapshot (ZielTage, SkontoTage, SkontoProzent), Status, Summen (Netto/MwSt/Brutto, persistiert), Fälligkeitsdatum?, Leistungsdatum? (§14 UStG), Kopf-/Fußtext, Audit + RowVersion.

**BelegPosition:** PositionsNr, PositionsTyp (Artikel|Freitext|Zwischensumme), ArtikelId?, Bezeichnung (Snapshot!), Menge dec(18,3), Einzelpreis dec(18,4) netto, RabattProzent, **MwStSatz als Snapshot je Zeile** + SteuerSchluessel (DATEV), GesamtNetto, **`UrsprungsPositionId?` = zeilenbasierter Belegfluss** — offene Menge = Menge − Σ referenzierender Folgepositionen. Diese eine Spalte trägt Teillieferung, Teilfakturierung und Sammelrechnung.

**Steuerberechnung:** MwSt je Steuersatz-Gruppe auf Summe der Nettozeilen (nicht je Zeile summiert), in Kindtabelle `BelegSteuerSumme` — vermeidet 1-Cent-Differenzen, DATEV-konform. Sätze 19/7/0 % in `MwStSatz`-Tabelle mit GueltigAb.

### Stammdaten
- **Kunde**: Kundennummer (Nummernkreis), Adresse (owned), USt-IdNr, Zahlungsbedingung, Preisliste?, Rabatt%, Kreditlimit?, DebitorenkontoNr (DATEV, 10000+). **Lieferant** analog (KreditorenkontoNr 70000+).
- **Artikel**: Artikelnummer, Einheit, MwStSatz (Default), EK-/Listenpreis, EAN?, IstLagerartikel, HatSeriennummern, Mindestbestand?, Gesperrt.
- **Preise**: `Preisliste` + `ArtikelPreis` (PreislisteId, ArtikelId, AbMenge=Staffel, Preis). **Preisfindung** (Domain-Service, exhaustiv unit-getestet): kundenspezifischer Staffelpreis → Listenpreis → Positionsrabatt → Kundenrabatt.
- Einheit, Zahlungsbedingung (ZielTage, Skonto), Versandart.

### Status-Workflow
`Entwurf → Gebucht → (Erledigt | Storniert)`.
- Nummer bei erstem Speichern — **außer Rechnungsnummer: erst beim Buchen** (lückenlose Sequenz, §14 UStG).
- **Gebucht = unveränderlich** (GoBD): SaveChanges-Interceptor wirft bei Änderung gebuchter Rechnung. Buchen der Rechnung → OffenerPosten; Buchen Lieferschein/Wareneingang → Lagerbewegungen. Storno = Gegenbuchung, nie löschen.

### Lager: Append-only-Ledger + Snapshot
- `Lagerbewegung` (append-only): ArtikelId, LagerortId, Menge signiert, Typ, BelegPositionId?, SeriennummerId?, Zeitpunkt, BenutzerId.
- `ArtikelBestand`-Snapshot (ArtikelId+LagerortId, Menge, RowVersion): Update in **derselben Transaktion** via atomarem `UPDATE ... SET Menge = Menge + @delta` — kein Read-Modify-Write-Race. Konsistenzjob leitet Snapshot bei Bedarf aus Ledger neu ab.
- `Seriennummer` (Status AufLager/Ausgeliefert/Retourniert); Junction `BelegPositionSeriennummer` beim Lieferschein.
- `Inventur` + `InventurPosition` (SollMenge eingefroren, IstMenge); Abschluss bucht Differenzen als Inventurkorrektur.

### Finanzen
- `OffenerPosten`: 1:1 zu Rechnung/Eingangsrechnung (Gutschrift = negativer OP), Typ Debitor/Kreditor, Mahnstufe 0–3, Mahnsperre; OffenerBetrag = Betrag − Σ Zuordnungen.
- `Zahlung` + `ZahlungZuordnung` (ZahlungId, OPId, Betrag, SkontoBetrag) — eine Zahlung kann mehrere OPs ausgleichen.
- `Mahnung` (kein Beleg-Subtyp) + `MahnungPosition`; Mahnstufen-Config (Karenztage, Gebühr).
- DATEV: `DatevExportService` erzeugt EXTF-Buchungsstapel-CSV aus gebuchten Belegen/Zahlungen; `FibuKonten`-Config (SKR03/04, MwStSatz→Erlöskonto); ExportiertAm-Marker gegen Doppelexport.

### Nummernkreise — concurrency-sicher
```sql
UPDATE Nummernkreis SET NaechsteNummer = NaechsteNummer + 1
OUTPUT deleted.NaechsteNummer
WHERE Code = @code AND (Jahr = @jahr OR Jahr IS NULL);
```
Atomar in Buchungs-Transaktion, kein Retry-Loop.

### Audit & Concurrency
- `AuditableEntity` (ErstelltAm/Von, GeaendertAm/Von) via SaveChangesInterceptor + ICurrentUserService.
- RowVersion auf jedem Aggregate Root; `DbUpdateConcurrencyException` → Standard-Dialog „neu laden?" (kein Merge-UI in v1).
- Optional `AuditLog` (JSON-Diff) für Belege + Stammdaten (GoBD-Nachweis).

## Geschäftsprozesse

Ein generischer `BelegUeberleitungService.Ueberleiten(sourceBelegId, targetTyp, selection)`: kopiert Kopf-Snapshots + gewählte Zeilen (offene Mengen), setzt UrsprungsPositionId. Eine Transaktion je Nutzeraktion.

1. **Angebot→Auftrag**: Voll-/Teilkopie; Preise aus Angebot übernommen (bindend, keine Neufindung).
2. **Auftrag→Lieferschein**: Teillieferungs-Dialog (offene Mengen); Buchen = negative Lagerbewegungen + Seriennummern-Pick + Bestandsupdate in einer Transaktion. Offene-Mengen-Prüfung **in der Transaktion wiederholen** (Race zweier Nutzer).
3. **Lieferschein→Rechnung**: inkl. **Sammelrechnung** (mehrere Lieferscheine gleicher Kunde/Zahlungsbedingung); auch direkt Auftrag→Rechnung (Dienstleistung). Buchen: Rechnungsnummer atomar, einfrieren, Fälligkeit, OP — eine Transaktion.
4. **Rechnung→OP→Mahnlauf**: Zahlungsdialog mit Skonto-Vorschlag (innerhalb SkontoTage), Multi-OP-Zuordnung. Mahnlauf-Batch: OPs mit Fälligkeit+Karenz überschritten, je Kunde gruppiert, PDF + optional E-Mail, Mahnstufe++.
5. **Gutschrift**: Überleitung aus Rechnung; negativer OP; optional Warenrücknahme (positive Lagerbewegung).
6. **Bestellung→Wareneingang→Eingangsrechnung**: EK-Preise aus Artikel; Wareneingang bucht Zugang + legt Seriennummern an; Eingangsrechnung → Kreditor-OP. Abweichung = Soft-Warnung.

## Architektur-Details

- **Application**: Plain Services, Interface je Service, DTOs (records). FluentValidation explizit am Methodenanfang; zentrale ValidationException→ContentDialog.
- **DbContext**: `IDbContextFactory<NexusDbContext>` (Singleton-Factory); **jede Service-Methode eigener kurzlebiger Context**; Reads `AsNoTracking`; Save re-attacht DTO mit Original-RowVersion.
- **DI**: `Host.CreateApplicationBuilder` in App.xaml.cs, appsettings.json für Connection String; ViewModels transient, konstruktorinjiziert.
- **Navigation**: NavigationView + NavigationService (Dictionary VM→Page, `Navigate<TViewModel>()`); je Modul Master-Detail (Liste mit DataGrid+Suche → Detailseite).
- **RBAC**: eigene Benutzer/Rolle/Recht-Tabellen (PBKDF2), Login vor Shell, Rechte-Guard in Services UND UI-Sichtbarkeit.
- **Deployment**: unpackaged self-contained (kein MSIX-Zertifikat-Friktion); Migrationen NUR via Migrator-Tool (Mehrplatz!); App prüft SchemaVersion beim Start.
- **Rundung**: decimal, MidpointRounding.ToEven, invariant speichern, de-DE nur an UI/PDF-Grenze.

## Phasen

| Phase | Inhalt | Testbar am Ende |
|---|---|---|
| **0 Scaffold** | Solution, 6 Projekte + Tests, Host-DI, Shell+Navigation, DbContext + DesignTimeFactory + Erstmigration, Migrator | App startet, navigiert; `dotnet ef` läuft; Migrator erzeugt DB |
| **1 Stammdaten** | Alle Stammdaten-Entities + Migrationen + Seeds, Audit-Interceptor, Services+Validators, CRUD-UI (Kunden/Lieferanten/Artikel + Kleinstamm-Settings), Nummernkreise, PreisfindungService | CRUD im UI; Auto-Kundennummer; Concurrency-Dialog; Preisfindung-Tests grün |
| **2 Verkauf+PDF** ⭐ | Beleg-TPH-Modell, Belegeditor (Kopf+Positionsgrid+Artikel-Lookup+Live-Summen), Angebot/Auftrag/Rechnung (direkt, ohne Lager), Buchungspipeline (Immutability, atomare RE-Nummer, OP-Anlage), Überleitung, QuestPDF (Briefkopf + 3 Dokumente) | Angebot→Rechnung komplett; PDF-Summen stimmen; Paralleltest: eindeutige RE-Nummern |
| **3 Lager+Lieferschein** | Ledger+Snapshot, Teillieferung, Bestandsabbuchung, Sammelrechnung, Bestandsübersicht, Seriennummern, Inventur | Teillieferung korrekt; Ledger=Snapshot (Integrationstest); Negativsperre |
| **4 Einkauf** | Bestellung→Wareneingang→Eingangsrechnung, Bestellvorschlag (Mindestbestand) | EK-Roundtrip erhöht Bestand; Kreditor-OP entsteht |
| **5 Finanzen+E-Mail** | OP-Liste (Aging), Zahlungsdialog+Skonto, Mahnwesen (Config, Lauf, PDF), Graph-Mail (MSAL/WAM, Entra-App, Versand-Log je Beleg) | Teilzahlung→TeilBezahlt; Mahnlauf-Selektion getestet; Mail mit PDF kommt an |
| **6 DATEV+Reporting** | FibuKonten-UI, EXTF-CSV-Export (Golden-File-Tests), Auswertungen (Umsatz je Kunde/Artikel/Monat, Artikelbewegungen, Top-Artikel, offene Aufträge) + CSV-Export | DATEV-CSV byte-exakt gegen Referenz; Import beim Steuerberater validiert |
| **7 Admin+Härtung** | Benutzer/Rollen/Rechte-UI+Login, Service-Guards, Systemkonfig (Firmenstamm/Briefkopf), AuditLog-Viewer, Deployment-Story | Rechte-Block greift (Test+UI); Regressionspass Phase 1–6 |

## Verifikation

- **Unit (xUnit v3)**: Preisfindung (Staffelkanten), Steuerrundung (1-Cent-Fälle), Skonto-Datumslogik, Mahnselektion, Offene-Mengen-Berechnung.
- **Integration (Testcontainers.MsSql, Fallback LocalDB)**: Nummernkreis unter `Parallel.For` ohne Duplikate; RowVersion-Konflikt; Buchungstransaktionen atomar (Rollback = kein Teilzustand); Ledger-Invariante; Immutability-Interceptor; DATEV-Golden-Files; Migrationen from-zero.
- **PDF**: Render-Smoke je Dokumenttyp + Assertions auf Summen im Dokumentmodell (kein Pixel-Diff).
- **UI**: manueller Smoke-Check je Phase (`docs/smoke-tests.md`), keine automatisierten WinUI-UI-Tests in v1.

## Risiken

1. **`dotnet ef` × WinUI**: WinUI-App taugt nicht als Startup-Projekt → Migrator + DesignTimeDbContextFactory (im Design gelöst).
2. **WinUI 3 DataGrid**: CommunityToolkit-Grid semi-maintained; Plan B für Positionsgrid: ListView-Templates.
3. **QuestPDF-Lizenz**: Community nur <1 M USD Umsatz; `Settings.License = LicenseType.Community` explizit setzen.
4. **Graph Desktop-Auth**: WAM-Broker braucht Window-Handle + Redirect `ms-appx-web://microsoft.aad.brokerplugin/{clientId}`; Mail.Send erfordert Admin-Consent. NullEmailService als Fallback — E-Mail blockiert nie anderes.
5. **DATEV EXTF**: ~125 Spalten, CP1252, CRLF, Komma-Dezimal, Steuerschlüssel — eng scopen, früh mit Steuerberater validieren.
6. **Business-Races**: atomare UPDATEs (Nummernkreis, Bestand) sind tragend — nie durch Read-Modify-Write ersetzen; Mengenprüfung in der Transaktion.
7. **GoBD**: Basics abgedeckt (Immutability, lückenlose RE-Nummern, Audit); volle Zertifizierung out of scope v1.

## Kritische Dateien (Implementierung)

- `src/Nexus.Domain/Entities/Belege/Beleg.cs` — TPH-Basis, Herzstück des Modells
- `src/Nexus.Infrastructure/Persistence/NexusDbContext.cs` — Mappings, Interceptors, TPH/RowVersion
- `src/Nexus.Application/Verkauf/BelegUeberleitungService.cs` — generische Überleitung mit Zeilenreferenzen
- `src/Nexus.Application/Verkauf/RechnungBuchenService.cs` — Buchungstransaktion (Nummer, Freeze, OP)
- `src/Nexus.App/App.xaml.cs` — Host-Builder, DI-Root, Navigations-Registry

**Startpunkt: Phase 0.**
