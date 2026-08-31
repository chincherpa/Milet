# Plan: Was Milet noch nicht kann — Lückenschluss (Phase 9)

Stand der Analyse: 2026-08-31. Grundlage: `PLAN.md` (Soll), `STATUS.md` (Ist je Phase),
`REVIEW_2026-08-29.md` (30 Befunde, davon 3 offen/teiloffen) — **plus eine Gegenprüfung im Quellcode**,
weil mehrere dieser Dokumente in Sessions ohne Compiler geschrieben wurden. Jede Lücke unten trägt
deshalb eine konkrete Belegstelle, nicht nur einen Doku-Verweis.

Teil A listet die Lücken, Teil B grenzt ab, was bewusst offen bleibt, Teil C ist der Umsetzungsplan.

---

## Teil A — Die Lückenliste

### A1 — Fachliche Funktionslücken (Milet kann es schlicht nicht)

#### A1.1 Storno und Gutschrift existieren nicht ⚠️ **kritisch**

Die größte Lücke, und die einzige mit direkter Rechtsfolge.

- **Symptom:** Eine versehentlich gebuchte Rechnung ist in der Anwendung **nicht korrigierbar**. Kein
  Storno, keine Gutschrift, kein Weg zurück — nur ein Eingriff direkt in der Datenbank, der genau das
  ist, was die GoBD-Sperre verhindern soll.
- **Befund im Code (verifiziert):**
  - `grep -rl Gutschrift src/` findet **keine einzige Datei** — es gibt keine Klasse `Gutschrift : Beleg`,
    keinen Discriminator-Wert, keinen Nummernkreis-Verbraucher.
  - `BelegStatus.Storniert` (`src/Milet.Domain/Entities/Verkauf/BelegStatus.cs:8`) wird **nirgends
    zugewiesen** — die einzigen Fundstellen sind die Enum-Definition und der Immutability-Interceptor,
    der den Status *liest*.
  - `BelegImmutabilityInterceptor.PruefeBelege` erlaubt auf einem gebuchten Beleg **ausschließlich**
    die Statusfortschreibung `Gebucht → Erledigt`
    (`src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs:64-73`). Ein
    Storno würde heute an genau dieser Stelle abgewiesen — die Whitelist ist die Stelle, die aufgehen muss.
  - Der Nummernkreis `GS` wird geseedet (`StammdatenSeed.cs:70`), aber nie gezogen.
- **Mitbetroffen:** `PLAN.md` Geschäftsprozess 5 (Gutschrift aus Rechnung, negativer OP, optionale
  Warenrücknahme) ist unumgesetzt; der DATEV-Export kennt nur Rechnung/Eingangsrechnung/Zahlung;
  `BelegPosition.OffeneMenge` zählt Positionen stornierter Folgebelege mit (Review-Befund 16 — heute
  latent, weil es keine Stornos gibt, ab dem Moment des Storno-Baus aber ein echter Mengenfehler).

#### A1.2 Der Lagerledger sagt nicht, warum und von wem gebucht wurde

- **Symptom:** Eine Bestandskorrektur verlangt im UI einen Grund ("Bruch", "Schwund"), eine
  Ausfallbuchung eine Bemerkung ("Frostschaden") — beides ist danach **nirgends mehr auffindbar**. Wer
  gebucht hat, steht auch nicht in der Zeile.
- **Befund im Code (verifiziert):** `Lagerbewegung`
  (`src/Milet.Domain/Entities/Lager/Lagerbewegung.cs`) hat kein Feld für Grund/Bemerkung. Der
  vorhandene `BenutzerId`-Spalte (Zeile 36) wird in **keinem** Service gesetzt — `grep -rn "BenutzerId ="
  src/Milet.Infrastructure/` trifft nur `AuthService`, `CurrentSessionService` und den
  AuditLog-Interceptor. `BestandskorrekturDto.Grund` (`src/Milet.Application/Lager/Dtos.cs:34`) wird
  validiert (`Validators.cs:21`) und dann verworfen; dasselbe gilt für die vier `Bemerkung`-Felder der
  Gärtnerei-DTOs (`src/Milet.Application/Gaertnerei/Dtos.cs:124,138,152,163`).
- **Auswirkung:** Der Ledger ist zwar append-only, aber für eine Betriebsprüfung oder eine interne
  Klärung ("wer hat 300 Stück ausgebucht?") wertlos. Das untergräbt den Zweck des Ledgers.

#### A1.3 Skontokonten sind nicht konfigurierbar

- **Symptom:** Der DATEV-Export bucht Skonto immer auf die Standard-Sammelkonten des Kontenrahmens.
  Ein Mandant mit abweichendem Kontenplan muss jeden Stapel beim Steuerberater umschlüsseln lassen.
- **Befund:** `DatevExportService.SkontoKonto(...)` ist ein fest verdrahteter `switch`
  (`src/Milet.Infrastructure/Services/DatevExportService.cs:313`), SKR03 8736/3736, SKR04 4736/5736.
  `FibuKonfiguration` hat für alles andere (Bankkonto, Berater, Mandant) bereits Felder — die zwei
  Skontokonten fehlen dort nur, weil die Review-Session ohne SDK keine Migration schreiben konnte.

#### A1.4 Kein Schutz gegen Passwort-Durchprobieren

- **Symptom:** Ein Angreifer mit Netzzugang zur Datenbank-App kann beliebig viele Anmeldeversuche
  fahren. Es gibt keine Sperre, keinen Zähler, keine Verzögerung.
- **Befund:** `AuthService` (Review-Befund 13) hat das Timing-Leck geschlossen (Dummy-Hash), aber ein
  Lockout braucht Spalten auf `Benutzer` (`FehlversucheSeitLetztemErfolg`, `GesperrtBis`) und wurde
  deshalb in der SDK-losen Session zurückgestellt.

#### A1.5 Das Initialpasswort muss nicht gewechselt werden

- **Symptom:** `admin` / `Milet!Admin1` steht öffentlich in `docs/deployment.md` und in diesem
  Repository. Wechselt es niemand von Hand, bleibt es gültig — die App verlangt nichts.
- **Befund:** Review-Befund 30; als Zwischenschritt warnt der Migrator bei jedem Lauf. Ein erzwungener
  Wechsel braucht ein Flag auf `Benutzer` (`PasswortWechselErforderlich`) und einen Dialog im Login-Flow.

#### A1.6 Lieferadresse ist im Belegeditor nicht änderbar

- **Symptom:** Eine abweichende Lieferadresse (Baustelle, Filiale, Geschenksendung) ist nicht erfassbar
  — die Adresse wird beim Anlegen 1:1 aus dem Kundenstamm eingefroren.
- **Befund:** Bewusste Vereinfachung aus Phase 2, dokumentiert in `STATUS.md` („Bekannte Risiken"), seit
  Phase 3 (Lieferschein) fachlich fällig. Die Snapshot-Felder existieren bereits am Beleg — es fehlt nur
  die Bearbeitbarkeit im Editor.

#### A1.7 Bestand an einem deaktivierten Lagerort verschwindet aus der Übersicht

- **Symptom:** Wird ein Lagerort deaktiviert, zeigt die Bestandsübersicht den dort tatsächlich noch
  liegenden Bestand nicht mehr an. Die Daten bleiben in der Datenbank, aber niemand sieht sie.
- **Befund:** Regression aus dem C1-Fix der Phase-3-Abschlussreview, in `STATUS.md` als offener
  Follow-up geführt. Richtig wäre: Bestand ≠ 0 wird immer angezeigt (ggf. markiert), nur synthetische
  Null-Zeilen werden ausgeblendet.

#### A1.8 Rechte gelten nur modulweit, Lesezugriffe gar nicht

- **Symptom:** Wer „Verkauf" darf, darf alles im Verkauf. Und wer ein Modul nicht darf, kann dessen
  Daten trotzdem lesen, sobald er auf einem anderen Weg dorthin gelangt.
- **Befund:** `RechtCodes` kennt acht Modulcodes, die Guards sitzen auf den mutierenden Methoden der
  Hauptservices; Lese-Methoden sind bewusst ungeschützt (`STATUS.md`, Phase-7-Abschnitt, Punkt 2).
  Für v1 abgesprochen — hier nur der Vollständigkeit halber gelistet, **kein** Umsetzungspunkt (s. Teil B).

### A2 — Korrektheitsrisiken (Milet kann es, aber unter Umständen falsch)

#### A2.1 Parallele Teillieferung/Sammelrechnung kann doppelt liefern ⚠️

- **Symptom:** Zwei Anwender leiten gleichzeitig denselben Auftrag in Lieferscheine über — beide sehen
  „noch nichts geliefert", beide committen. Ergebnis: mehr geliefert als bestellt, ohne dass irgendetwas
  Alarm schlägt (die Negativsperre des Bestands greift erst, wenn der Bestand tatsächlich nicht reicht).
- **Befund (verifiziert):** Der In-Transaktion-Re-Check in `BelegUeberleitungService` liest die
  Folgepositionen mit `AsNoTracking()` und **ohne jeden Sperrhinweis**
  (`src/Milet.Infrastructure/Services/BelegUeberleitungService.cs:82, 162, 203, 335`). Unter SQL Servers
  Default-Isolationslevel READ COMMITTED gibt eine solche Leseabfrage keine Sperre, die bis zum Commit
  hielte. `grep -rn UPDLOCK src/` findet den Hint **ausschließlich** in `BestandService` (Zeile 118) —
  die Überleitung hat ihn nicht. Der Kommentar im Code behauptet den Schutz trotzdem.
- **Status:** In `STATUS.md` seit Phase 3 als Verdacht geführt, nie verifiziert, nie behoben. Durch
  Phase 8 zusätzlich auf die dimensionsbehaftete Teillieferung ausgeweitet.

#### A2.2 `OffeneMenge` kennt den Status des Folgebelegs nicht

- Review-Befund 16. Heute folgenlos (es gibt keine Stornos), ab A1.1 aber ein direkter Mengenfehler:
  ein stornierter Lieferschein würde die Menge weiterhin als „geliefert" blockieren. **Muss zusammen mit
  A1.1 erledigt werden**, nicht danach.

#### A2.3 AuditLog-Zeilen entstehen nach dem fachlichen Commit

- Review-Befund 23, teilweise behoben: die Belegpfade laufen inzwischen in einer Transaktion, generell
  schreibt der Interceptor aber weiterhin in `SavedChanges` — bricht der Prozess zwischen fachlichem
  Save und Audit-Save ab, fehlt der Protokolleintrag zu einer real erfolgten Änderung.

### A3 — Verifikationslücken (unbekannt, ob es funktioniert)

#### A3.1 Der WinUI-Client wurde seit Phase 5 nie kompiliert ⚠️ **höchstes Risiko**

- Die Phasen 5, 6, 7 und 8 haben jeweils substanzielle WinUI-Änderungen eingebracht (neue Seiten,
  ViewModels, Dialoge, Converter, Menüpunkte), die in **keiner** Session je durch den XAML-Codegen oder
  den C#-Compiler gelaufen sind — geprüft wurde nur „wohlgeformtes XML" und Klammerbalance.
- Verschärfend: die **Review-Fixes vom 2026-08-29 wurden ebenfalls nie gebaut**, haben aber
  **Konstruktorsignaturen** (`BelegService`, `RechnungBuchenService`, `BelegUeberleitungService`), ein
  **Service-Interface** (`IDatevExportService`) und ein **DTO** (`DatevExportErgebnisDto`) geändert.
  Ob die Lösung im aktuellen Stand überhaupt übersetzt, ist damit offen — für den Backend-Teil ebenso
  wie für den Client.
- **Konsequenz für diesen Plan:** Bevor irgendein neues Feature entsteht, muss der aktuelle Stand
  gebaut und getestet werden. Alles andere baut auf Sand (Task 1).

#### A3.2 Testlücken an den heikelsten Stellen

Ohne eigene Tests: `BelegImmutabilityInterceptor`, `AuditSaveChangesInterceptor`, `ZahlungService`,
`MahnwesenService`, `UeberleitenMitAuswahlAsync` (Teillieferung), Teile des `InventurService`
(Review-Befund 29). Genau die Stellen, an denen dieser Plan ansetzt.

---

## Teil B — Was bewusst offen bleibt (kein Umsetzungspunkt)

Damit die Lückenliste nicht als Aufgabenliste missverstanden wird — diese Punkte sind entschieden, nicht
vergessen:

| Thema | Begründung |
|---|---|
| Granulares RBAC (Lesen/Schreiben je Aktion) | v1-Entscheidung, `PLAN.md` Risiko 7; modulweite Rechte erfüllen das Testkriterium |
| Automatischer/geplanter Mahnlauf | v1 bewusst manuell (`STATUS.md`, Phase 5, Punkt 5) |
| Alle ~125 DATEV-Spalten | eng gescopet nach `PLAN.md` Risiko 5; externe Validierung beim Steuerberater bleibt Nutzeraufgabe |
| Volle GoBD-Zertifizierung | `PLAN.md` Risiko 7 — Basics abgedeckt, Zertifizierung out of scope |
| Kulturplanung/Prognose, Etikettendruck, mobile Felderfassung, Topfgrößen als Dimension | `PLAN.md` § Gärtnerei, „Bewusst außerhalb von v1" |
| Web-/Mobil-Client | WinUI-Desktop ist die bestätigte Zielplattform |
| Merge-UI bei Concurrency-Konflikten | `PLAN.md`: „kein Merge-UI in v1", Neu-laden-Dialog genügt |

---

## Teil C — Umsetzungsplan

Reihenfolge ist bindend: Task 1 zuerst (sonst ist jede spätere Verifikation wertlos), danach die
Storno-Strecke als Block (9b), dann die kleineren Lücken.

### Umgebungs- und Verifikationsstrategie

Diese Session läuft headless auf Linux ohne Windows-Toolchain — dieselbe Lage wie in den Phasen 5–8:

- **.NET SDK:** `dotnet-sdk-10.0` ist per `apt` installierbar (Kandidat 10.0.104; `global.json` fordert
  10.0.400). Wie in Phase 6–8: Builds/Tests laufen aus einer **Scratch-Kopie** mit lokal abgesenktem
  `global.json` — das committete `global.json` bleibt unverändert bei 10.0.400.
- **Docker:** Client ist vorhanden, der Daemon läuft nicht (`/var/run/docker.sock` fehlt) — `dockerd`
  wie in Phase 6–8 manuell starten, `TESTCONTAINERS_RYUK_DISABLED=true` setzen (Ryuk-Image zieht über
  den Proxy nicht, `mcr.microsoft.com/mssql/server` schon). Ziel: Integrationstests **echt** laufen
  lassen, nicht überspringen.
- **`Milet.App` (WinUI) kann hier nicht gebaut werden.** Alle Client-Änderungen dieses Plans folgen
  strikt bestehenden Mustern und bleiben bis zu einem Windows-Build unverifiziert — jede davon wandert
  in `docs/smoke-tests.md`. Das ist die bekannte, in `STATUS.md` seit Phase 5 geführte Einschränkung;
  dieser Plan verkleinert sie nicht, er verschweigt sie nur nicht.
- **Migrationen:** jede neue Migration real gegen einen containerisierten SQL Server anwenden — einmal
  auf eine frische DB und einmal auf eine mit Alt-Daten befüllte (Muster Phase 8), plus
  Modellkonsistenz-Check über eine zweite, leere `dotnet ef migrations add`.

### Phase 9a — Fundament: den aktuellen Stand überhaupt erst nachweisen

**Task 1 — Build- und Testnachweis des Ist-Standes.**
SDK per apt installieren, Scratch-Kopie anlegen, alle Nicht-WinUI-Projekte einzeln bauen
(`Milet.Domain`/`Application`/`Infrastructure`/`Tools.Migrator` + 3 Testprojekte), alle drei
Testprojekte einzeln fahren (MTP-Modus), `dockerd` starten und die Integrationstests **echt** laufen
lassen. Erwartung laut `STATUS.md`: Domain 72+, Application 66+, Integration 78+. **Jede Abweichung ist
ein Fund** — insbesondere Kompilierfehler aus den nie gebauten Review-Fixes (Konstruktoren, Interface,
DTO). Gefundene Fehler hier sofort beheben und einzeln committen, bevor Task 2 beginnt.
*Nachweis:* Buildausgaben + Testzahlen in `STATUS.md`.

**Task 2 — Migrator gegen frische und gegen bestehende DB.**
`Milet.Tools.Migrator` gegen eine frische Container-DB laufen lassen (prüft Seed-Fix, RBAC-Sitzung,
Passwortwarnung aus den Review-Fixes — laut Review nie ausgeführt) und ein zweites Mal (Idempotenz).
*Nachweis:* `sqlcmd`-Gegenprüfung der Seed-Tabellen.

### Phase 9b — Storno und Gutschrift (A1.1 + A2.2)

Der Kern. Reihenfolge innerhalb des Blocks ist fachlich zwingend.

**Task 3 — Domain: `Gutschrift : Beleg`.**
Dünner TPH-Subtyp nach dem Muster von `Rechnung : Beleg`, Discriminator-Wert `Gutschrift`,
Nummernkreis `GS` (bereits geseedet, Format `GS-{1}-{0:0000}`). Feld `StorniertenBelegId?` am Beleg
(Selbstreferenz) als Verweis der Storno-Gutschrift auf ihre Ursprungsrechnung — trennt die
Storno-Gutschrift (automatische Gegenbuchung) von der fachlichen Gutschrift (Retoure/Gutschein), die
denselben Belegtyp nutzt, aber ohne Bezug entsteht.

**Task 4 — Domain: `OffeneMenge` respektiert Belegstatus (A2.2).**
`BelegPosition.OffeneMenge` zählt Positionen stornierter Folgebelege nicht mehr mit. Domain-Tests
zuerst (TDD): stornierter Lieferschein gibt die Menge wieder frei, gebuchter nicht.
**Muss vor Task 6 fertig sein** — sonst blockiert der erste Storno Mengen dauerhaft.

**Task 5 — Immutability-Interceptor: Storno-Pfad öffnen.**
Die Whitelist in `PruefeBelege` (heute nur `Gebucht → Erledigt`) um `Gebucht → Storniert` und
`Erledigt → Storniert` erweitern — weiterhin **ausschließlich** als alleinige Statusänderung, jede
inhaltliche Modifikation bleibt gesperrt. Zusätzlich: das Setzen von `StorniertAm`/`StorniertVon`
zulassen. Erste eigene Tests für diesen Interceptor überhaupt (deckt zugleich A3.2 teilweise ab).

**Task 6 — Application/Infrastructure: `IStornoService`/`StornoService`.**
Eine Transaktion je Storno, Gegenbuchung statt Löschung:
- *Rechnung stornieren:* Storno-Gutschrift mit gespiegelten Positionen (negative Mengen bzw.
  Vorzeichenumkehr — Entscheidung im Task festhalten und im Code begründen), eigene `GS`-Nummer,
  Gegen-OP (negativer offener Posten) bzw. Ausgleich des Ursprungs-OP; Ursprungsrechnung → `Storniert`.
- *Lieferschein stornieren:* Gegenbuchungen über `BestandService.BucheBewegungAsync` (der einzige
  Schreibpfad — nicht umgehen), inkl. Rückgabe gepickter Seriennummern auf `AufLager`;
  Beleg → `Storniert`.
- *Wareneingang stornieren:* negative Gegenbuchung, dabei greift die bestehende Negativsperre — ist die
  Ware bereits weiterverkauft, muss der Storno mit einer verständlichen Meldung scheitern, nicht mit
  einem SQL-Fehler.
- Rechteprüfung über `RechtCodes.FuerBelegTyp` (Muster aus den Review-Fixes), Pflichtfeld „Grund".

**Task 7 — Application/Infrastructure: fachliche Gutschrift aus Rechnung.**
Überleitung `Rechnung → Gutschrift` im bestehenden `BelegUeberleitungService` (Teilmengen möglich,
`UrsprungsPositionId` wie überall), negativer OP beim Buchen, optionale Warenrücknahme als positive
Lagerbewegung (`PLAN.md` Geschäftsprozess 5). Buchen läuft über den vorhandenen
`RechnungBuchenService`-Pfad bzw. eine Schwesterklasse — die atomare Nummernvergabe in der
Buchungstransaktion (Review-Befund 5) ist zwingend wiederzuverwenden, nicht nachzubauen.

**Task 8 — DATEV: Gutschrift und Storno im Export.**
`DatevExportService` um den Belegtyp `Gutschrift` erweitern (Soll/Haben gespiegelt), stornierte Belege
korrekt behandeln: ein bereits exportierter, danach stornierter Beleg wird **nicht** rückwirkend
verändert — die Storno-Gutschrift ist eine eigene, zusätzlich zu exportierende Buchung. Golden-File-Test
erweitern.

**Task 9 — PDF: Gutschriftdokument.**
`BelegPdfDocument` um den Typ erweitern (Titel „Gutschrift", Hinweis auf die stornierte Rechnung bei
Storno-Gutschriften). Render-Smoke-Test wie bei den bestehenden drei Typen.

**Task 10 — Integrationstests Storno (Docker, echt).**
Rechnung buchen → stornieren → OP ausgeglichen, Ursprung unveränderlich, zweiter Storno wird
abgewiesen. Lieferschein stornieren → Bestand zurück, Seriennummern wieder `AufLager`, offene Menge im
Auftrag wieder frei (verzahnt mit Task 4). Wareneingang stornieren ohne ausreichenden Bestand → saubere
Fehlermeldung. Paralleler Doppel-Storno desselben Belegs → nur einer gewinnt.

**Task 11 — WinUI (unverifiziert): Storno- und Gutschrift-UI.**
„Stornieren"-Schaltfläche mit Grund-Dialog auf gebuchten Rechnungen/Lieferscheinen/Wareneingängen;
Gutschrift-Liste + -Editor nach dem Muster der Rechnungsseiten; Menüpunkt unter Verkauf; Statusanzeige
„Storniert" in allen Belegliste. Ablauf für den späteren Windows-Smoke-Test in `docs/smoke-tests.md`
ergänzen.

**Task 12 — Doku-Korrektur.**
`CLAUDE.md` (spricht von sieben Belegarten und nennt Storno explizit als nicht existent), `PLAN.md`
(§ Stand), `STATUS.md`, `REVIEW_2026-08-29.md` (Befunde 15/16 → behoben) und `docs/anleitung.md`
(Abschnitt „Was Milet noch nicht kann") auf den neuen Stand bringen.

### Phase 9c — Nachvollziehbarkeit im Ledger (A1.2)

**Task 13 — Schema: `Lagerbewegung` um `Grund`/`Bemerkung` und gefüllte `BenutzerId`.**
Ein Textfeld (`Bemerkung`, max. 200) plus konsequentes Setzen von `BenutzerId` aus
`ICurrentUserService`. Migration + Anwendung gegen frische **und** befüllte DB.

**Task 14 — Alle Schreibpfade durchreichen.**
`BestandService.BucheBewegungAsync` (Signatur um den Grund erweitern), Bestandskorrektur (nutzt endlich
`BestandskorrekturDto.Grund`), Kulturbuchungen (die vier `Bemerkung`-Felder), Lieferschein-/
Wareneingang-Buchen (automatischer Text mit Belegnummer), Inventurabschluss, Storno-Gegenbuchungen aus
Task 6.

**Task 15 — Anzeige + Test.** Spalte in der Bestandsübersicht/Bewegungshistorie; Integrationstest, dass
Grund und Benutzer je Bewegungstyp tatsächlich in der Zeile landen.

### Phase 9d — Härtung Finanzen und Administration

**Task 16 — Skontokonten konfigurierbar (A1.3).** Zwei Felder auf `FibuKonfiguration`
(Debitor/Kreditor), Migration, Seed-Default aus dem heutigen Hardcode je Kontenrahmen,
`DatevExportService.SkontoKonto` liest aus der Konfiguration, FibuKonten-Tab um die Felder erweitert.

**Task 17 — Login-Lockout (A1.4).** Spalten `Fehlversuche`/`GesperrtBis` auf `Benutzer`, Zähler im
`AuthService` (Reset bei Erfolg), Sperre nach n Versuchen für m Minuten — die Fehlermeldung bleibt
bewusst unspezifisch (kein User-Enumeration-Leck, bestehende Entscheidung), Entsperren über die
Benutzerverwaltung. Integrationstests für beide Pfade.

**Task 18 — Erzwungener Wechsel des Initialpassworts (A1.5).** Flag
`PasswortWechselErforderlich` auf `Benutzer`, gesetzt vom `AdminSeed` und bei jedem Passwort-Reset durch
einen Administrator; Login-Flow erzwingt den Wechsel vor dem Öffnen der Shell (WinUI, unverifiziert).
Migrator-Warnung aus den Review-Fixes kann bleiben.

**Task 19 — Lagerort-Regression (A1.7).** Bestandsübersicht zeigt Bestand ≠ 0 auch an deaktivierten
Lagerorten (markiert), blendet nur synthetische Nullzeilen aus. Test gegen echte DB.

### Phase 9e — Korrektheit unter Parallelität (A2.1)

**Task 20 — Race in der Überleitung reproduzieren.** Zuerst ein Integrationstest, der zwei parallele
Teillieferungen desselben Auftrags fährt und heute **fehlschlagen muss** (Überlieferung nachweisen).
Ohne diesen roten Test ist der Fix nicht belegbar — der Verdacht steht seit Phase 3 unverifiziert im
`STATUS.md`.

**Task 21 — Fix per Sperre auf dem Quellbeleg.** `UPDLOCK`(+`HOLDLOCK`) beim Lesen der Quellbelege und
Folgepositionen in allen vier Lesestellen von `BelegUeberleitungService` (Zeilen 82/162/203/335), Muster
wie im `BestandService`-Upsert. Danach muss der Test aus Task 20 grün sein, ohne dass die bestehenden
Überleitungstests brechen. Den irreführenden Kommentar im Code korrigieren.

### Phase 9f — Abschluss

**Task 22 — Testlücken schließen (A3.2).** Eigene Tests für `AuditSaveChangesInterceptor`,
`ZahlungService`, `MahnwesenService` und den Teillieferungspfad (der Immutability-Interceptor ist mit
Task 5 abgedeckt).

**Task 23 — Lieferadresse im Belegeditor (A1.6).** Snapshot-Felder im Editor freigeben (nur im
Entwurfsstatus), Default weiterhin aus dem Kundenstamm. Rein WinUI + DTO-Durchreichung, keine
Schemaänderung.

**Task 24 — Verifikation und Dokumentation.** Voller Durchlauf: alle Projekte bauen, alle drei
Testprojekte einzeln, Integrationstests echt gegen Container-SQL-Server, Migrator gegen frische und
befüllte DB. `STATUS.md` um einen Phase-9-Abschnitt ergänzen (mit denselben ehrlichen Vorbehalten:
WinUI nie gebaut), `docs/smoke-tests.md` um die neuen Abläufe, `docs/anleitung.md` und `PLAN.md`
nachziehen.

---

## Umfang und Abbruchpunkte

Der Plan ist bewusst in Blöcke geschnitten, die einzeln einen sinnvollen Stand hinterlassen:

| Block | Inhalt | Nutzen bei Abbruch danach |
|---|---|---|
| 9a (Task 1–2) | Build-/Testnachweis | **Unverzichtbar.** Klärt, ob der aktuelle Stand überhaupt übersetzt |
| 9b (Task 3–12) | Storno + Gutschrift | Die einzige Lücke mit Rechtsfolge ist geschlossen — guter Stopp |
| 9c (Task 13–15) | Ledger-Nachvollziehbarkeit | Betriebsprüfungstauglicher Ledger |
| 9d (Task 16–19) | Finanz-/Admin-Härtung | Vier kleine, unabhängige Lücken |
| 9e (Task 20–21) | Parallelitäts-Race | Der letzte bekannte Korrektheitsverdacht, endlich belegt oder entkräftet |
| 9f (Task 22–24) | Tests, Lieferadresse, Doku | Aufräumen |

**Was dieser Plan nicht leisten kann:** den Windows-Build und den manuellen UI-Smoke-Test. Beides bleibt
nach wie vor offen — für die Phasen 5–8 ebenso wie für alles, was hier hinzukommt. Das ist keine
Nachlässigkeit dieses Plans, sondern die Grenze der Ausführungsumgebung; sie gehört bei jeder Abnahme
mitgesagt.
