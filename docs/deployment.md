# Deployment — Milet Warenwirtschaft

Zielbild (s. `PLAN.md` § Architektur-Details "Deployment"): unpackaged, self-contained
Desktop-Deployment ohne MSIX-Zertifikat-Friktion, zentraler SQL Server, Migrationen
ausschließlich über das Migrator-Tool.

## 1. Datenbank bereitstellen

Ein zentraler SQL Server (oder für Einzelplatz-Tests LocalDB) ist Voraussetzung — Mehrplatzbetrieb
ist ein Kernziel, ein lokales SQLite-/embedded-Setup ist nicht vorgesehen.

Connection String in `src/Milet.Tools.Migrator/appsettings.json` (`ConnectionStrings:Milet`)
setzen, oder per Umgebungsvariable `MILET_CONNECTIONSTRING` / CLI-Argument `--connection=...`
überschreiben (praktisch für CI/Server ohne eingecheckte Zugangsdaten).

## 2. Migrator ausführen (bei jedem Deployment/Update)

```
dotnet run --project src/Milet.Tools.Migrator
```

Der Migrator:
1. wendet ausstehende EF-Core-Migrationen an,
2. führt `StammdatenSeed` aus (Einheiten, MwSt-Sätze, Zahlungsbedingungen, Nummernkreise,
   Lagerort, Mahnstufen, Firmenstamm, FibuKonfiguration — jeweils "je fehlendem Eintrag
   ergänzen", nie destruktiv),
3. führt `AdminSeed` aus (RBAC: fester Rechte-Katalog, Rolle "Administrator" mit allen Rechten,
   Erstbenutzer — s. § 4 unten),
4. führt `DummyDatenSeed` aus (nur wenn die DB noch leer ist — Testdaten, nicht für Produktion
   gedacht, aber harmlos idempotent).

**Nur der Migrator darf das Schema ändern.** Die WinUI-App selbst migriert nie (kann auch
nicht — kein EF-Core-Design-Time-Startprojekt, s. `PLAN.md` Risiko 1) und **prüft beim Start
nur, ob das Schema aktuell ist** (`ISchemaVersionService`/`SchemaVersionService`,
`Database.GetPendingMigrationsAsync()`): Fehlt der Migrator-Lauf, meldet das der Login-Screen
als Fehler ("Datenbankschema ist nicht aktuell...") und blockiert die Anmeldung, statt mit
einem veralteten Schema weiterzulaufen.

Bei Mehrplatzbetrieb: Migrator einmal zentral gegen den Server laufen lassen, **bevor** eine
neue App-Version an die Clients verteilt wird — nie pro Client.

## 3. App bauen und verteilen (unpackaged, self-contained)

```
dotnet publish src/Milet.App/Milet.App.csproj -c Release -p:Platform=x64 ^
  -p:WindowsAppSDKSelfContained=true --self-contained true -r win-x64 ^
  -o publish/
```

Ergebnis ist ein eigenständiger Ordner (`publish/`), der ohne Installation/MSIX auf jeder
Windows-10/11-x64-Maschine mit den nötigen Runtime-Voraussetzungen läuft — per XCOPY/Netzlaufwerk/
Softwareverteilung ausrollbar. `appsettings.json` neben der `.exe` trägt den Connection String
(oder `MILET_CONNECTIONSTRING` clientseitig setzen).

## 4. Erste Anmeldung (RBAC)

`AdminSeed` legt bei leerem `Benutzer`-Table einen Erstbenutzer an:

- Benutzername: `admin`
- Passwort: `Milet!Admin1`

**Nach dem ersten Login umgehend über Administration → Benutzer das Passwort ändern**
(Feld "Neues Passwort" ausfüllen und speichern) — der Seed-Wert ist öffentlich in diesem
Repository dokumentiert und darf nie produktiv stehen bleiben. Es gibt bewusst keinen
erzwungenen Passwortwechsel-Flow in v1 (wie bei der noch offenen GoBD-Vollzertifizierung,
s. `PLAN.md` Risiko 7 — Basics abgedeckt, kein Anspruch auf vollständige Härtung).

Rechte sind modulweise vergeben (ein `Recht` je Top-Level-Menüpunkt: Stammdaten, Verkauf,
Einkauf, Lager, Finanzen, Reporting, Administration). Neue Rollen unter Administration → Rollen
anlegen, Rechte per Checkbox zuweisen, Benutzer der Rolle zuordnen.

## 5. Bekannte Lücken für Produktivbetrieb

- Kein automatischer Passwort-Reset/E-Mail-Verifizierung (out of scope v1).
- Rechte sind modulweit (kein granulares Lesen/Schreiben je Aktion) — ausreichend für "Rechte-
  Block greift", aber kein feingranulares RBAC.
- Graph-Mail (Phase 5) und DATEV-Export (Phase 6) brauchen eigene, vom Kunden bereitgestellte
  Konfiguration (`Graph`-Sektion in `appsettings.json` bzw. FibuKonten-Tab) — ohne sie bleibt
  die App voll funktionsfähig (Fallback-Services), nur die jeweilige Funktion meldet einen
  sprechenden Fehler.
- Kein Docker/Container-Deployment vorgesehen (WinUI 3 ist ein natives Windows-Desktop-
  Framework, kein Web-/Container-Kandidat).
