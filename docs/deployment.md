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

# Nur für Entwicklung/Demo — legt Testdaten inkl. gebuchter Rechnungen an, s. Warnung unten:
dotnet run --project src/Milet.Tools.Migrator -- --mit-testdaten
```

Der Migrator:
1. wendet ausstehende EF-Core-Migrationen an,
2. führt `StammdatenSeed` aus (Einheiten, MwSt-Sätze, Zahlungsbedingungen, Nummernkreise,
   Lagerort, Mahnstufen, Firmenstamm, FibuKonfiguration — jeweils "je fehlendem Eintrag
   ergänzen", nie destruktiv),
3. führt `AdminSeed` aus (RBAC: fester Rechte-Katalog, Rolle "Administrator" mit allen Rechten,
   Erstbenutzer — s. § 4 unten),
4. führt `DummyDatenSeed` **nur auf ausdrückliche Anforderung** aus — mit `--mit-testdaten` oder
   `MILET_SEED_TESTDATEN=1`. Ohne das passiert nichts, und der Migrator sagt es auch.

> **Testdaten gehören nicht in eine Produktivdatenbank.** Der Seed legt nicht nur erfundene Stammdaten
> an, er durchläuft die echte Buchungspipeline und **bucht drei Rechnungen**. Die verbrauchen
> `RE-{Jahr}-0001` bis `-0003` aus dem lückenlosen Rechnungsnummernkreis (§14 UStG), sind als gebuchte
> Belege durch den `BelegImmutabilityInterceptor` unveränderlich und mangels Storno-Funktion über die
> Anwendung **nie wieder zu entfernen**. Sein eigenes Gate („noch keine Artikel vorhanden") trifft genau
> den Zustand einer frischen Produktivdatenbank — deshalb ist er seit dem 2026-09-06 Opt-in und nicht
> mehr Opt-out.

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

**Der Wechsel wird beim ersten Login erzwungen.** Der Anmeldedialog zeigt nach korrekter Eingabe
des Initialpassworts eine zweite Stufe („Neues Passwort" + Wiederholung); die Anwendung öffnet sich
erst danach. Der Seed-Wert ist öffentlich in diesem Repository dokumentiert und darf nie produktiv
stehen bleiben. Mindestlänge des neuen Passworts: 10 Zeichen (`PasswortRegeln`).

Dasselbe gilt nach einem administrativen Zurücksetzen (Administration → Benutzer → „Neues
Passwort"): der Administrator kennt den gesetzten Wert, der Benutzer muss ihn beim nächsten Login
wechseln.

**Anmeldesperre:** Nach 5 aufeinanderfolgenden Fehlversuchen ist der Zugang 15 Minuten gesperrt.
Ein Zurücksetzen des Passworts hebt die Sperre auf. Die Sperre wird dem Anmeldenden nur genannt,
wenn sein Passwort stimmt — andernfalls verriete die Meldung, dass es den Benutzernamen gibt.

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
