# Phase 8 „Gärtnerei / Kulturführung" Implementation Plan

> **Hinweis für die Umsetzung:** Dieser Plan wird task-für-task abgearbeitet. Jeder Task hat Checkboxen (`- [ ]`); nach jedem Task wird gebaut/getestet und einzeln committet (wie Phase 2–7). Jeder Task ist in sich abgeschlossen und kompilierbar (mit den explizit dokumentierten Ausnahmen, wo ein Folge-Task denselben Build repariert).

**Goal:** Milet wird vom reinen Handels-WWS zum Warenwirtschaftssystem einer **Staudengärtnerei**: Pflanzen werden von der Jungpflanze über Zwischenstufen zur Verkaufspflanze hochgezogen. Der Nutzer soll jederzeit sehen können, **welche Pflanze** in **welcher Kulturstufe** auf **welchem Feld** in **welcher Sektion** in **welcher Menge** steht; er soll den **Grundriss seiner Gärtnerei** einpflegen, Felder darin einzeichnen und in Sektionen unterteilen können; beim Erfassen eines **Auftrags** soll ihm angezeigt werden, ob und wo die bestellte Pflanze verkaufsfähig vorrätig ist; und eine **Pflanzenübersicht** soll links alle Pflanzen listen und rechts im Grundriss die Sektionen hervorheben, in denen die gewählte Pflanze steht — eingefärbt nach Kulturstufe.

**Architektur (in einem Satz):** Die Kulturführung wird **keine Parallelwelt neben dem Lager**, sondern zwei zusätzliche Dimensionen auf dem bestehenden, bereits abgenommenen Lagermodell — `ArtikelBestand`/`Lagerbewegung` bekommen `SektionId` und `KulturstufeId`, das Feld ist ein `Lagerort` mit Geometrie, die Sektion ist die klassische Lagerplatz-Ebene darunter, und `BestandService.BucheBewegungAsync` bleibt der einzige Schreibpfad auf Bestand — jetzt mit vier statt zwei Dimensionen.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server/LocalDB), FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, xUnit v3, Testcontainers.MsSql. **Kein neues NuGet-Paket nötig** — der Grundriss wird mit Bordmitteln (`Canvas` + `ItemsControl`) gezeichnet, keine Grafik-/Diagramm-Bibliothek.

**Spec:** dieser Plan (die Gärtnerei-Domäne ist in `PLAN.md` bisher nicht abgebildet — `PLAN.md` beschreibt ein branchenneutrales Handels-WWS). Konventionen recherchiert aus `BestandService`, `LieferscheinBuchenService`, `InventurService`, `KleinstammServices`, `BelegEditViewModelBase`, `BestandUebersichtViewModel`, `StammdatenSeed`/`AdminSeed` — jede Abweichung davon ist unten begründet.

---

## Fachliches Modell (Domänenverständnis)

Eine Staudengärtnerei kauft oder zieht **Jungpflanzen** an, topft sie im Lauf von Monaten bis Jahren mehrfach um und verkauft sie am Ende als **Verkaufspflanzen**. Zwischen diesen Enden liegen ein oder mehrere Zwischenstadien (vom Nutzer „Teenagerpflanze" genannt). Fachlich heißt das:

1. **Dieselbe Pflanze durchläuft mehrere Zustände.** Eine *Salvia nemorosa 'Caradonna'* als Jungpflanze und dieselbe Sorte als Verkaufspflanze sind **derselbe Artikel** — nur in unterschiedlicher **Kulturstufe**. Sie sind keine getrennten Artikel: der Preis, die Einheit, der Steuersatz, die Artikelnummer gehören zur Sorte, nicht zum Reifegrad. Deshalb ist die Kulturstufe eine **Bestandsdimension** und kein Artikelmerkmal (siehe Entscheidung E1).
2. **Nur eine (oder mehrere) Stufe(n) sind verkaufsfähig.** Ein Auftrag über 200 Stauden kann nicht aus Jungpflanzen bedient werden, auch wenn 5.000 davon dastehen. Die Verkaufsfähigkeit hängt an der Stufe, nicht am Artikel (E5).
3. **Der Ort ist zweistufig.** Ein Feld ist mehrere hundert Quadratmeter groß, eine Kultur belegt davon zwei bis drei. Ohne Unterteilung ist die Ortsangabe „Feld B" wertlos, um die Pflanze physisch zu finden. Deshalb: Feld → Sektion (E2).
4. **Stufenwechsel ist eine Bewegung, keine Feldänderung.** Wenn 500 Jungpflanzen zu Teenagerpflanzen umgetopft werden, gehen 500 Stück aus (Stufe JP, Sektion A) und 500 Stück ein (Stufe TP, oft in einer *anderen* Sektion, weil größere Töpfe mehr Fläche brauchen). Das ist buchhalterisch eine Umbuchung mit zwei Ledger-Zeilen, kein Update einer Bestandszeile (E6).
5. **Ausfall ist normal und muss messbar sein.** Frost, Trockenheit, Pilz: in einer Gärtnerei überlebt ein Teil einer Kultur nicht. Ausfall braucht einen eigenen Bewegungstyp, sonst verschwindet er in „Korrektur" und die Ausfallquote je Sorte/Stufe ist nicht auswertbar (E7).

---

## Architektur-Entscheidungen — offene Fragen mit konkreter Lösung

Diese Punkte wurden beim Erkunden des bestehenden Codes als echte Weichenstellungen identifiziert. Jeder hat eine im Plan umgesetzte Lösung — kein „TBD".

### E1 — Kulturstufe ist eine Bestandsdimension, kein eigener Artikel und kein Parallelmodell

**Frage:** Wie hängen „Pflanze in Stufe X" und `Artikel`/`ArtikelBestand` zusammen? Drei Optionen standen zur Wahl: (a) je Stufe ein eigener Artikel (`ART-1001-JP`, `ART-1001-TP`, …), (b) ein eigenes Kulturbestandsmodell neben `ArtikelBestand`, (c) die Stufe als zusätzliche Dimension des bestehenden Bestands.

**Lösung: (c).** Begründung:
- (a) würde Preisfindung, Staffelpreise, Mindestbestand, Umsatzauswertung und den Artikelstamm je Sorte verdreifachen, und ein Stufenwechsel wäre eine Umbuchung zwischen zwei Artikeln — fachlich falsch (es ist dieselbe Pflanze) und in jeder Auswertung eine Fehlerquelle.
- (b) erzeugt **zwei Wahrheiten über denselben physischen Bestand**. Der Lieferschein bucht gegen `ArtikelBestand`, die Kulturführung gegen `KulturBestand` — jeder Verkauf müsste beide fortschreiben, jede Inventur beide abgleichen. Genau die Klasse von Drift, die `PLAN.md` mit „Ledger + abgeleiteter Snapshot, ein Schreibpfad" bewusst ausschließt.
- (c) hält **einen** Bestand, **einen** Ledger, **einen** Schreibpfad. Der Verkauf bucht weiterhin gegen `ArtikelBestand` — nur eben gegen die Zeile mit der verkaufsfähigen Stufe.

**Konsequenz:** `ArtikelBestand` bekommt den Schlüssel `(ArtikelId, LagerortId, SektionId, KulturstufeId)` statt `(ArtikelId, LagerortId)`.

### E2 — Feld = `Lagerort` mit Geometrie; Sektion = neue Ebene darunter (klassischer Lagerplatz)

**Frage:** Ist ein Feld ein neuer Entitätstyp neben `Lagerort`, oder ein `Lagerort`?

**Lösung:** Ein Feld **ist** ein `Lagerort` — es bekommt lediglich optionale Geometrie (`IstFeld`, `GaertnereiplanId`, `PosXMeter`, `PosYMeter`, `BreiteMeter`, `HoeheMeter`) und darunter `Sektion`-Zeilen. Das ist die klassische ERP-Hierarchie *Lagerort → Lagerplatz*, nur mit Gärtnerei-Vokabular.

Begründung: Ein neuer paralleler Ortstyp hätte bedeutet, dass jede bestandsführende Stelle (Lieferschein-Buchung, Wareneingang, Inventur, Bestandskorrektur, `Lagerbewegung.LagerortId`) eine Fallunterscheidung „Lagerort oder Feld?" bekommt. Als `Lagerort` erbt das Feld all das unverändert. Das „Hauptlager" (`Code = HL`, aus Phase 3 geseedet) bleibt ein Lagerort **ohne** Geometrie und ohne Sektionen — dort liegen Töpfe, Substrat, Etiketten, also normale Handelsware.

**Verworfene Alternative:** Geometrie in eine eigene 1:1-Tabelle `FeldLayout` auslegen. Sauberer im Sinne „Lagerort bleibt schlank", kostet aber bei jedem Rendern des Grundrisses einen Join und bei jeder Feldbearbeitung eine zweite Entität — für sechs nullable Spalten kein guter Tausch.

### E3 — Nullable Dimensionen halten den Bestandscode rückwärtskompatibel

`SektionId` und `KulturstufeId` sind **nullable**. Damit gilt:
- Handelsware im Hauptlager: beide `NULL` → exakt das heutige Verhalten, keine Datenmigration, kein geänderter Aufrufpfad.
- Kulturpflanze auf einem Feld: beide gesetzt.

Der Unique-Index wird auf `(ArtikelId, LagerortId, SektionId, KulturstufeId)` erweitert. **Wichtig und bewusst genutzt:** SQL Server behandelt in einem Unique-Index `NULL` als *einen* Wert — es kann also weiterhin nur **eine** Zeile `(Artikel, Lagerort, NULL, NULL)` geben. Die heutige Eindeutigkeitsgarantie für Handelsware bleibt damit unverändert bestehen, ohne gefilterten Zusatzindex. Das ist der Grund, warum nullable Spalten hier funktionieren und nicht (wie sonst oft) ein Sentinel-Wert nötig wäre — wird in Task 4 mit einem Integrationstest festgenagelt, weil es ein leicht zu brechendes Detail ist.

### E4 — Der atomare Bestands-UPDATE bekommt die Dimensionen NULL-sicher, und wird beim Erstanlegen rennfest gemacht

`BestandService.BucheBewegungAsync` ist laut `CLAUDE.md`/`PLAN.md` der **einzige** Schreibpfad auf Bestand und die sicherheitskritischste Methode im System. Sie wird an genau zwei Stellen angefasst:

1. **Dimensionen in die `WHERE`-Klausel**, NULL-sicher — sonst würde `SektionId = NULL` nie matchen und jede Handelsware-Buchung stillschweigend eine neue Zeile anlegen statt die bestehende fortzuschreiben:
```sql
UPDATE ArtikelBestaende SET Menge = Menge + @delta
WHERE ArtikelId = @artikelId AND LagerortId = @lagerortId
  AND ((@sektionId IS NULL AND SektionId IS NULL) OR SektionId = @sektionId)
  AND ((@kulturstufeId IS NULL AND KulturstufeId IS NULL) OR KulturstufeId = @kulturstufeId)
  AND Menge + @delta >= 0;
```

2. **Der Erstanlage-Pfad wird zum echten Upsert.** Heute gilt: `betroffeneZeilen == 0` **und** `delta > 0` ⇒ `db.ArtikelBestaende.Add(...)`. Das ist ein latenter Fehler **schon im aktuellen Code**: zwei parallele Erstbuchungen auf dieselbe Kombination sehen beide 0 betroffene Zeilen, fügen beide ein, und die zweite Transaktion stirbt am Unique-Index (SQL-Fehler 2601/2627, für den Nutzer eine unverständliche `DbUpdateException`). Bisher praktisch folgenlos, weil Erstbestückung selten parallel passiert; in einer Gärtnerei mit vier Dimensionen entstehen dagegen **laufend** neue Bestandszeilen (jeder Stufenwechsel in eine neue Sektion legt eine an), oft im selben Arbeitsgang von mehreren Mitarbeitern. Ersetzt durch das Standard-Upsert-Muster:
```sql
INSERT INTO ArtikelBestaende (ArtikelId, LagerortId, SektionId, KulturstufeId, Menge)
SELECT @artikelId, @lagerortId, @sektionId, @kulturstufeId, 0
WHERE NOT EXISTS (
    SELECT 1 FROM ArtikelBestaende WITH (UPDLOCK, HOLDLOCK)
    WHERE ArtikelId = @artikelId AND LagerortId = @lagerortId
      AND ((@sektionId IS NULL AND SektionId IS NULL) OR SektionId = @sektionId)
      AND ((@kulturstufeId IS NULL AND KulturstufeId IS NULL) OR KulturstufeId = @kulturstufeId));
```
… danach wird derselbe `UPDATE` wie oben ein zweites Mal ausgeführt. `UPDLOCK, HOLDLOCK` erzeugt eine Key-Range-Sperre auf dem Unique-Index und verhindert damit das Phantom-Insert der Konkurrenztransaktion (der Unique-Index aus E3 ist Voraussetzung dafür, dass die Range-Sperre greift). Kosten: **ein** zusätzlicher Round-Trip, und zwar nur im seltenen Erstanlage-Fall — der Normalfall bleibt bei einem einzigen Statement. Die Negativsperre bleibt unverändert scharf: schlägt der zweite `UPDATE` fehl, wird geworfen.

### E5 — Kulturstufen sind Stammdaten mit Reihenfolge, Verkaufsfähigkeits-Flag und Farbe

Der Nutzer hat ausdrücklich verlangt, dass die Stufennamen änderbar sind. Deshalb keine `enum`, sondern eine Stammdatentabelle `Kulturstufe` mit:
- `Bezeichnung` — frei änderbar; Referenzen laufen über `Id`, ein Umbenennen wirkt sofort überall und rückwirkend (gewollt: es ist dieselbe Stufe, nur anders benannt).
- `Reihenfolge` (int, unique) — definiert die Kette. Der Stufenwechsel-Dialog schlägt die *nächsthöhere* Stufe vor, erlaubt aber jede (Rückstufung kommt vor: eine Kultur wird zurückgeschnitten und nochmal ein Jahr weitergezogen).
- `IstVerkaufsfaehig` (bool) — **die** Regel für den Verkauf. Mehrere Stufen dürfen verkaufsfähig sein (z. B. „Verkaufspflanze" und „Solitär"). Ist keine Stufe verkaufsfähig, ist keine Kulturpflanze lieferbar — das wird bei der Konfiguration als Warnung angezeigt, aber nicht verboten.
- `FarbeHex` — Highlight-Farbe im Grundriss. Ohne Farbe je Stufe wäre die vom Nutzer gewünschte Anzeige „dieselbe Pflanze liegt in unterschiedlichen Sektionen, weil sie unterschiedliche Stufen hat" nicht unterscheidbar.
- `Aktiv` — Stufen werden **stillgelegt, nicht gelöscht**. Löschen wird per `DeleteBehavior.Restrict` blockiert, sobald Bestand oder Bewegungen referenzieren (`Lagerbewegung` ist append-only, also faktisch für immer). Der Löschen-Button meldet das als verständlichen Text, wie bei den anderen Kleinstämmen über `ConcurrencyHelper.SaveChangesDeletingAsync`.

Seed: `JP` „Jungpflanze" (1), `TP` „Teenagerpflanze" (2), `VP` „Verkaufspflanze" (3, `IstVerkaufsfaehig = true`) — die Namen des Nutzers als Startpunkt, jederzeit in den Einstellungen änderbar.

### E6 — Stufenwechsel ist eine Umbuchung mit zwei Ledger-Zeilen, nie ein Update

Ein Stufenwechsel bucht in **einer** Transaktion:
`BucheBewegungAsync(artikel, feld, sektionVon, stufeVon, −menge, Stufenwechsel)` und
`BucheBewegungAsync(artikel, feldNach, sektionNach, stufeNach, +menge, Stufenwechsel)`.

Damit gilt weiterhin: der Ledger ist append-only und die Summe aller Bewegungen je Dimension entspricht dem Snapshot. Ein „UPDATE ArtikelBestand SET KulturstufeId = …" wäre der naheliegende, aber falsche Weg — er würde die Historie vernichten (wann wurde diese Kultur umgetopft?) und die Ledger-Invariante brechen. Neue Bewegungstypen: `Kulturzugang = 5`, `Stufenwechsel = 6`, `Umsetzen = 7` (nur Ortswechsel, Stufe bleibt), `Ausfall = 8`.

Die Abgangsbuchung läuft durch dieselbe Negativsperre wie ein Lieferschein: man kann nicht 600 Pflanzen umtopfen, wenn 500 dastehen.

### E7 — Ausfall ist ein eigener Bewegungstyp, keine „Korrektur"

Fachlich zwingend für die Auswertung („Ausfallquote je Sorte und Stufe"), und operativ ein Unterschied: eine Korrektur heilt einen Erfassungsfehler, ein Ausfall dokumentiert einen realen Verlust. Beide sind negative Bewegungen, aber nur der Ausfall gehört in die Kultur-Auswertung.

### E8 — Verfügbarkeit im Verkauf ist beratend, nicht sperrend; Reservierung wird berechnet, nicht gespeichert

**Frage:** Soll ein Auftrag über nicht vorhandene Pflanzen blockiert werden, und braucht es eine Reservierungs-Entität?

**Lösung:** Nein und nein.
- **Nicht sperrend:** Eine Gärtnerei nimmt Aufträge selbstverständlich auf Pflanzen an, die erst in drei Monaten verkaufsfähig sind — das ist das Geschäftsmodell, nicht ein Fehler. Der Auftragseditor zeigt deshalb eine **Ampel** (grün = verkaufsfähig frei verfügbar, gelb = nur in nicht-verkaufsfähigen Stufen vorhanden, rot = gar nicht vorhanden) plus die konkrete Fundstellenliste. Gebucht wird trotzdem. Die harte Sperre bleibt dort, wo sie hingehört: beim **Lieferschein-Buchen**, über die bestehende Negativsperre.
- **Reservierung berechnet:** „Reserviert" = Summe der offenen Mengen aller Auftragspositionen dieses Artikels (Belegtyp `Auftrag`, Status `Entwurf`/`Gebucht`, offene Menge > 0) — genau die bereits vorhandene `BelegPosition.OffeneMenge`-Logik über `UrsprungsPositionId`. „Frei" = verkaufsfähiger Bestand − reserviert. Eine eigene Reservierungstabelle wäre ein zweiter Zustand, der mit den Belegen synchron gehalten werden müsste; die Berechnung ist ableitbar und damit driftfrei.

**Bekannte Grenze (dokumentiert, nicht behoben):** Die berechnete Reservierung ist eine Anzeige ohne Sperre. Zwei Sachbearbeiter können denselben freien Bestand gleichzeitig als „frei" sehen. Fachlich unkritisch (der Konflikt fällt spätestens beim Lieferschein-Buchen auf, dort atomar) — das ist dieselbe Klasse wie das bereits in `STATUS.md` dokumentierte READ-COMMITTED-Thema bei der Überleitung.

### E9 — Der Lieferschein bucht aus einer konkreten Sektion und Stufe ab

`BelegPosition` trägt heute `LagerortId` (Phase 3). Sie bekommt zusätzlich `SektionId?` und `KulturstufeId?`. Ohne das wäre nach E1/E3 nicht entscheidbar, welche der (potenziell vielen) Bestandszeilen eines Artikels abgebucht wird.

**Vorbelegung im Teillieferungs-Dialog:** verkaufsfähige Stufe mit der größten verfügbaren Menge, darin die Sektion mit der größten Menge. Der Nutzer kann umstellen (auch auf mehrere Sektionen, indem er die Position teilt — das kann der Dialog bereits über Mengenreduktion + zweite Überleitung). Automatisches Splitten über mehrere Sektionen ist **nicht** Teil von v1 (siehe „Bewusst außerhalb").

Für Nicht-Kulturartikel bleiben beide Felder `NULL` und der gesamte Pfad verhält sich unverändert.

### E10 — Die Inventur wird je Sektion und Stufe gezählt, sonst zählt sie falsch

`InventurService.NeueInventurAsync` legt heute je lagerfähigem Artikel **eine** Position an und friert `ArtikelBestand.Menge` je `(Artikel, Lagerort)` ein. Nach E1/E3 gibt es je Artikel und Lagerort mehrere Zeilen — die heutige `FirstOrDefault(b => b.ArtikelId == …)`-Abfrage würde stillschweigend **eine beliebige davon** einfrieren und der Abschluss würde die Differenz auf eine willkürliche Dimension buchen. Das ist ein echter Datenzerstörungspfad, kein Schönheitsfehler; er ist deshalb Pflichtbestandteil dieser Phase und nicht verschiebbar.

**Lösung:** `InventurPosition` bekommt `SektionId?`/`KulturstufeId?`. Die Positionsbildung wird unterschieden:
- **Lagerort ohne Felder-Geometrie** (Hauptlager): unverändert — je lagerfähigem Artikel eine Position (Dimensionen `NULL`). Das ist der Regressionspfad, er muss identisch bleiben.
- **Feld:** eine Position je **existierender Bestandszeile** dieses Lagerorts (also je vorhandener Kombination Artikel × Sektion × Stufe). Nicht je Kreuzprodukt aller Artikel × Sektionen × Stufen — das wären bei 300 Sorten, 40 Sektionen und 3 Stufen 36.000 leere Zeilen.
- Die Drift-Prüfung beim Abschluss („Bestand hat sich seit Beginn geändert") schlüsselt entsprechend auf `(ArtikelId, SektionId, KulturstufeId)` statt nur `ArtikelId` um.

### E11 — Grundriss: achsenparallele Rechtecke in Metern, kein Polygon-Editor in v1

**Frage:** Wie wird der Grundriss modelliert — freie Polygone, Rechtecke, Hintergrundbild zum Nachzeichnen?

**Lösung v1: achsenparallele Rechtecke, Koordinaten in Metern** (`decimal(9,2)`), auf einem `Gaertnereiplan` (Breite × Höhe in Metern) als Zeichenfläche. Sektionen tragen Koordinaten **relativ zum Feld**.

Begründung: Beete und Stellflächen einer Gärtnerei sind fast immer rechteckig; ein Rechteck-Editor mit Ziehen/Größenändern ist in WinUI an einem Tag baubar, ein Polygon-Editor (Punkte setzen, verschieben, löschen, Selbstüberschneidung prüfen, Punkt-in-Polygon-Test fürs Highlighting) ist eine Woche Arbeit für einen Zugewinn, der erst bei unregelmäßigen Grundstücken zählt. Metrische Koordinaten statt Pixel, damit der Plan zoombar und flächenauswertbar bleibt (`FlaecheQm` ergibt sich aus Breite × Höhe und beantwortet die vom Nutzer genannte Frage „das Feld ist mehrere Quadratmeter groß, die Kultur belegt zwei bis drei").

**Explizit später (nicht v1):** Polygone, Drehung, ein hinterlegtes Luftbild/Katasterbild als Zeichenvorlage, mehrere Standorte. Der `Gaertnereiplan` ist bereits als Tabelle (nicht als Singleton-Zeile) angelegt, damit „mehrere Standorte" später ohne Schemabruch nachrüstbar ist; die UI zeigt in v1 genau einen Plan.

### E12 — Neues Top-Level-Recht `Gaertnerei`

`RechtCodes.Alle` ist laut Kommentar deckungsgleich mit den Top-Level-Menüpunkten. Ein neues Menü „Gärtnerei" bekommt deshalb ein neues Recht `Gaertnerei`. Der `AdminSeed` trägt fehlende Rechte bereits „je fehlendem Code" nach und hängt sie an die Administrator-Rolle — auf einer bestehenden DB entsteht das Recht also automatisch beim nächsten Migrator-Lauf. Kein Sonderweg nötig.

**Abgrenzung zu `Lager`:** Bestandskorrektur, Inventur und Lieferschein bleiben unter `Lager`. Kulturbuchungen (Zugang, Stufenwechsel, Umsetzen, Ausfall) und die Grundriss-Pflege laufen unter `Gaertnerei` — eine Aushilfe darf umtopfen, ohne Lieferscheine buchen zu dürfen.

---

## Datenmodell — Übersicht

**Neue Entitäten**

| Entität | Felder | Anmerkung |
|---|---|---|
| `Kulturstufe` | Id, Code (unique, ≤10), Bezeichnung (≤50), Reihenfolge (int, unique), IstVerkaufsfaehig, FarbeHex (≤7), Aktiv, RowVersion, Audit | Aggregate Root, Kleinstamm-Muster |
| `Gaertnereiplan` | Id, Bezeichnung (≤100), BreiteMeter dec(9,2), HoeheMeter dec(9,2), Aktiv, RowVersion, Audit | v1: eine Zeile, als Tabelle vorbereitet |
| `Sektion` | Id, LagerortId (FK, Restrict), Code (≤10), Bezeichnung (≤100), PosXMeter/PosYMeter/BreiteMeter/HoeheMeter dec(9,2) **relativ zum Feld**, Aktiv, RowVersion, Audit; unique (LagerortId, Code) | „Lagerplatz"-Ebene |

**Erweiterte Entitäten**

| Entität | Neu | Wirkung |
|---|---|---|
| `Lagerort` | IstFeld (bool, default false), GaertnereiplanId?, PosXMeter?, PosYMeter?, BreiteMeter?, HoeheMeter? (alle dec(9,2)) | Feld = Lagerort mit Geometrie (E2) |
| `Artikel` | IstKulturpflanze (bool, default false), BotanischerName? (≤150) | steuert Dimensionspflicht + Pflanzenliste |
| `ArtikelBestand` | SektionId?, KulturstufeId? — Unique-Index auf (ArtikelId, LagerortId, SektionId, KulturstufeId) | E1/E3 |
| `Lagerbewegung` | SektionId?, KulturstufeId? — Index (ArtikelId, LagerortId, SektionId, KulturstufeId) | E1 |
| `BelegPosition` | SektionId?, KulturstufeId? | E9 |
| `InventurPosition` | SektionId?, KulturstufeId? | E10 |
| `LagerbewegungTyp` | Kulturzugang=5, Stufenwechsel=6, Umsetzen=7, Ausfall=8 | E6/E7 |

**Zentrale Dimensionsregeln** (geprüft in `BucheBewegungAsync`, also unumgehbar — siehe Task 6):
1. `Artikel.IstKulturpflanze` ⇒ `KulturstufeId` **muss** gesetzt sein.
2. `!Artikel.IstKulturpflanze` ⇒ `KulturstufeId` **muss** `NULL` sein.
3. Lagerort mit mindestens einer aktiven Sektion ⇒ `SektionId` muss gesetzt sein **und** zu diesem Lagerort gehören.
4. Lagerort ohne Sektionen ⇒ `SektionId` muss `NULL` sein.

Regel 1 und 3 sind der Grund, warum ein Artikel nicht „halb" auf Kultur umgestellt werden kann: das Setzen von `IstKulturpflanze` auf einem Artikel mit vorhandenem dimensionslosem Bestand wird im `ArtikelService` blockiert (Task 7) mit dem Hinweis, den Bestand zuerst über eine Kulturzugangsbuchung umzustellen.

---

## Global Constraints

- **Kein neuer Schreibpfad auf Bestand.** Alles (Kulturzugang, Stufenwechsel, Umsetzen, Ausfall, Inventurkorrektur, Lieferschein, Wareneingang) geht durch `BestandService.BucheBewegungAsync`. Wer eine zweite Stelle mit `UPDATE ArtikelBestaende` schreibt, hat den Plan verletzt.
- **Ledger bleibt append-only.** Keine `UPDATE`/`DELETE` auf `Lagerbewegungen`, auch nicht beim Stufenwechsel.
- Jede Service-Methode öffnet einen eigenen Context aus `IDbContextFactory<MiletDbContext>`; Reads `AsNoTracking()`; Speichern über `SaveChangesTranslatingConcurrencyAsync`/`SaveChangesDeletingAsync` — wie Phase 1–7.
- DTOs als `sealed record`; alle Gärtnerei-DTOs in `src/Milet.Application/Gaertnerei/Dtos.cs`, alle Interfaces in `IGaertnereiServices.cs`, Validatoren in `Validators.cs` — Modulschnitt wie bei `Lager`/`Einkauf`.
- Präzisionen: Mengen `decimal(18,3)` (unverändert), Geometrie/Fläche `decimal(9,2)` in **Metern**, Geld unverändert.
- Rechte-Guard: `berechtigung.PruefeRecht(RechtCodes.Gaertnerei)` in **jeder** schreibenden Kultur-Methode, `RechtCodes.Lager` bleibt für Bestandskorrektur/Inventur/Lieferschein.
- Deutsche Fachbezeichner, englische nur für rein technische Infrastruktur.
- Migrationen ausschließlich über `Milet.Tools.Migrator`. `dotnet` explizit über `%USERPROFILE%\.dotnet\dotnet.exe`, App-Build mit `-p:Platform=x64`, Testprojekte **einzeln** (MTP).
- WinUI: `NumberBox`+`decimal` braucht `DecimalToDoubleConverter`, `int?` den `NullableInt32ToDoubleConverter`, `DateOnly` den `DateOnlyToDateTimeOffsetConverter` — bekannte WMC1121-Falle aus Phase 1. ComboBox mit `SelectedValuePath="Id"` immer an `int?` binden und auf `null` (nie `0`) zurücksetzen — bekannter Absturz aus der Phase-1-Abnahme.

---

## Tasks

### Block A — Domain & Persistenz

---

### Task 1: Domain — neue Entitäten `Kulturstufe`, `Gaertnereiplan`, `Sektion` + Erweiterung `Lagerort`/`Artikel`

**Files:**
- Create: `src/Milet.Domain/Entities/Gaertnerei/Kulturstufe.cs`, `Gaertnereiplan.cs`, `Sektion.cs`
- Modify: `src/Milet.Domain/Entities/Lager/Lagerort.cs`, `src/Milet.Domain/Entities/Stammdaten/Artikel.cs`

**Produces:** die drei neuen Aggregate Roots + Geometriefelder — konsumiert von Task 2, 4, 5, 6, 8, 10.

- [ ] **Step 1:** `Kulturstufe` als `AuditableEntity, IHasRowVersion` mit `Code`, `Bezeichnung`, `Reihenfolge`, `IstVerkaufsfaehig`, `FarbeHex` (Default `"#4CAF50"`), `Aktiv`.
```csharp
/// <summary>Konfigurierbare Stufe der Pflanzenanzucht (z. B. Jungpflanze → Teenagerpflanze → Verkaufspflanze).
/// Bewusst Stammdaten statt Enum: der Nutzer benennt und erweitert die Stufen selbst (Einstellungen).
/// Referenzen laufen über Id — ein Umbenennen wirkt rückwirkend, weil es dieselbe Stufe bleibt.</summary>
public class Kulturstufe : AuditableEntity, IHasRowVersion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;

    /// <summary>Bestimmt die Kette; der Stufenwechsel schlägt die nächsthöhere Stufe vor. Rückstufung bleibt erlaubt.</summary>
    public int Reihenfolge { get; set; }

    /// <summary>Nur Bestand in einer verkaufsfähigen Stufe zählt als lieferbar (Verfügbarkeitsprüfung, Lieferschein-Vorbelegung).</summary>
    public bool IstVerkaufsfaehig { get; set; }

    /// <summary>Highlight-Farbe im Grundriss (#RRGGBB) — trennt die Stufen derselben Pflanze optisch.</summary>
    public string FarbeHex { get; set; } = "#4CAF50";

    public bool Aktiv { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
```
- [ ] **Step 2:** `Gaertnereiplan` mit `Bezeichnung`, `BreiteMeter`, `HoeheMeter`, `Aktiv`.
- [ ] **Step 3:** `Sektion` mit `LagerortId`, `Code`, `Bezeichnung`, `PosXMeter`/`PosYMeter`/`BreiteMeter`/`HoeheMeter` (relativ zum Feld), `Aktiv`, plus berechnete Eigenschaft `FlaecheQm => BreiteMeter * HoeheMeter` (`[NotMapped]`-frei, weil Domain keine EF-Attribute kennt — als reine `get`-Property, in der Configuration `Ignore()`).
- [ ] **Step 4:** `Lagerort` um `IstFeld`, `GaertnereiplanId?`, `PosXMeter?`, `PosYMeter?`, `BreiteMeter?`, `HoeheMeter?` erweitern; Kommentar: „Geometrie nur bei `IstFeld`; ein reines Warenlager (Hauptlager) lässt sie NULL."
- [ ] **Step 5:** `Artikel` um `IstKulturpflanze` (default `false`) und `BotanischerName?` erweitern.
- [ ] Build `Milet.Domain` grün.

---

### Task 2: Domain — Bestandsdimensionen + neue Bewegungstypen

**Files:** Modify `Lagerbewegung.cs`, `ArtikelBestand.cs`, `LagerbewegungTyp.cs`, `InventurPosition.cs`, `Verkauf/BelegPosition.cs`

- [ ] **Step 1:** `ArtikelBestand` + `Lagerbewegung` je um `SektionId?`/`Sektion?` und `KulturstufeId?`/`Kulturstufe?` erweitern, mit XML-Doc: „NULL bei Handelsware ohne Kulturführung — dann verhält sich die Zeile exakt wie vor Phase 8."
- [ ] **Step 2:** `LagerbewegungTyp` um `Kulturzugang = 5`, `Stufenwechsel = 6`, `Umsetzen = 7`, `Ausfall = 8` erweitern (bestehende Werte **nicht** umnummerieren — sie stehen bereits in der DB).
- [ ] **Step 3:** `BelegPosition` um `SektionId?`/`KulturstufeId?` erweitern (Doc: „nur Lieferschein/Wareneingang — bestimmt, gegen welche Bestandszeile gebucht wird").
- [ ] **Step 4:** `InventurPosition` um `SektionId?`/`KulturstufeId?` erweitern.
- [ ] Build `Milet.Domain` grün.

---

### Task 3: Domain — `KulturRegeln` (reine Regeln) + Unit-Tests (TDD)

**Files:**
- Create: `src/Milet.Domain/Services/KulturRegeln.cs`
- Create: `tests/Milet.Domain.Tests/KulturRegelnTests.cs`

**Warum als Domain-Service:** Diese Regeln werden an drei Stellen gebraucht (Bestandsbuchung, Validatoren, UI-Vorbelegung). Als reine Funktionen sind sie ohne DB testbar — dieselbe Rolle wie `PreisfindungService`/`SteuerRechner`.

- [ ] **Step 1 (Test zuerst):** Tests für
  - `PruefeDimensionen(bool istKulturpflanze, bool lagerortHatSektionen, int? sektionId, int? kulturstufeId)` → wirft `InvalidOperationException` mit sprechendem Text bei jeder Verletzung der vier Regeln aus „Datenmodell → Zentrale Dimensionsregeln"; die vier gültigen Kombinationen laufen durch.
  - `NaechsteStufe(IReadOnlyList<Kulturstufe> stufen, int aktuelleStufeId)` → nächsthöhere aktive Stufe nach `Reihenfolge`, `null` wenn es keine gibt (höchste Stufe erreicht).
  - `PruefeStufenwechsel(vonStufeId, nachStufeId, menge)` → wirft bei gleicher Von/Nach-Stufe **und** gleicher Sektion (Nulloperation), bei `menge <= 0`.
  - `LiegtInnerhalb(Sektion sektion, Lagerort feld)` → Sektion muss vollständig im Feld liegen (0 ≤ x, x+breite ≤ feld.Breite, analog y).
  - `Ueberlappt(Sektion a, Sektion b)` → Rechteck-Schnitt, für die Warnung im Grundriss-Editor (Überlappung ist eine **Warnung**, kein Fehler: zweistöckige Stellagen und Frühbeete über Beeten gibt es real).
- [ ] **Step 2:** `KulturRegeln` implementieren, bis die Tests grün sind.
- [ ] `dotnet test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj` grün.

---

### Task 4: Infrastructure — EF-Configurations, DbSets, Migration `GaertnereiKultur`

**Files:**
- Create: `Persistence/Configurations/KulturstufeConfiguration.cs`, `GaertnereiplanConfiguration.cs`, `SektionConfiguration.cs`
- Modify: `ArtikelBestandConfiguration.cs`, `LagerbewegungConfiguration.cs`, `LagerortConfiguration.cs`, `ArtikelConfiguration.cs`, `BelegPositionConfiguration.cs`, `InventurPositionConfiguration.cs`, `MiletDbContext.cs`
- Create: Migration `GaertnereiKultur`

- [ ] **Step 1:** Configurations für die drei neuen Entitäten (Längen, Unique-Indizes `Kulturstufe.Code`, `Kulturstufe.Reihenfolge`, `(Sektion.LagerortId, Code)`; `RowVersion`; `Sektion.FlaecheQm` per `Ignore()`; alle FKs `DeleteBehavior.Restrict`).
- [ ] **Step 2:** `ArtikelBestandConfiguration`: FKs auf `Sektion`/`Kulturstufe` (Restrict), **Unique-Index von `(ArtikelId, LagerortId)` auf `(ArtikelId, LagerortId, SektionId, KulturstufeId)` erweitern**. Kommentar in der Datei, warum das mit nullable Spalten korrekt ist (E3).
- [ ] **Step 3:** `LagerbewegungConfiguration`: FKs + Index auf die vier Dimensionen (für die Kulturhistorie-Abfragen).
- [ ] **Step 4:** `LagerortConfiguration` (Geometrie, `HasPrecision(9,2)`, FK auf `Gaertnereiplan` Restrict), `ArtikelConfiguration` (`BotanischerName` ≤150), `BelegPositionConfiguration` + `InventurPositionConfiguration` (FKs Restrict).
- [ ] **Step 5:** DbSets `Kulturstufen`, `Gaertnereiplaene`, `Sektionen` in `MiletDbContext`.
- [ ] **Step 6:** Migration erzeugen: `dotnet ef migrations add GaertnereiKultur --project src/Milet.Infrastructure --startup-project src/Milet.Tools.Migrator`. **Erzeugtes SQL prüfen:** der Unique-Index muss gedroppt und neu angelegt werden; alle neuen Spalten müssen `NULL`-fähig sein; es darf **kein** Datenverlust-Warnhinweis auftauchen.
- [ ] **Step 7:** Migration anwenden (`dotnet run --project src/Milet.Tools.Migrator`), per `sqlcmd` gegenprüfen, dass bestehende `ArtikelBestaende`-Zeilen unverändert und mit `NULL`-Dimensionen dastehen.

---

### Task 5: Infrastructure — Seeds (Kulturstufen, Plan, Recht) + Dummy-Gärtnerei

**Files:** Modify `Persistence/Seed/StammdatenSeed.cs`, `DummyDatenSeed.cs`; Modify `src/Milet.Application/Admin/RechtCodes.cs`

- [ ] **Step 1:** `RechtCodes.Gaertnerei = "Gaertnerei"` ergänzen und in `Alle` aufnehmen — `AdminSeed` trägt es dann automatisch je fehlendem Code nach und hängt es an die Administrator-Rolle (bereits vorhandenes Muster, verifiziert).
- [ ] **Step 2:** `StammdatenSeed`: Kulturstufen **je fehlendem Code** ergänzen (nicht „nur wenn Tabelle leer" — dasselbe Muster wie Nummernkreise/Mahnstufen, weil die Entwicklungs-DB bereits migriert ist): `JP`/1/nein, `TP`/2/nein, `VP`/3/**ja**, mit Farben `#8BC34A`, `#4CAF50`, `#2E7D32`.
- [ ] **Step 3:** `StammdatenSeed`: genau einen `Gaertnereiplan` „Gärtnerei" 100 × 60 m anlegen, falls keiner existiert. Bestehendes Hauptlager `HL` bleibt `IstFeld = false`.
- [ ] **Step 4:** `DummyDatenSeed` (idempotent) um eine demonstrierbare Gärtnerei erweitern: drei Felder (`F1` „Feld Nord" 30×20 m bei (5,5), `F2` „Feld Süd" 30×20 m bei (5,30), `F3` „Folientunnel" 20×10 m bei (45,5)), je 4–6 Sektionen, 5 Kulturpflanzen-Artikel mit botanischen Namen (Salvia, Geranium, Echinacea, Hosta, Astilbe), und Bestand über alle drei Stufen verteilt — **gebucht über `BestandService.BucheBewegungAsync`**, nicht per direktem Insert, damit Ledger und Snapshot konsistent sind und der Seed denselben Pfad testet wie die App.
- [ ] Migrator laufen lassen, per `sqlcmd` prüfen: 3 Kulturstufen, 1 Plan, 3 Felder, Sektionen, Bestandszeilen mit gesetzten Dimensionen.

---

### Block B — Bestand & Kulturbuchungen

---

### Task 6: Infrastructure — `BestandService` auf vier Dimensionen + Upsert-Race-Fix ⚠️ **kritischster Task des Plans**

**Files:** Modify `src/Milet.Infrastructure/Services/BestandService.cs`; Modify alle Aufrufer (`LieferscheinBuchenService`, `WareneingangBuchenService`, `InventurService`)
**Tests:** `tests/Milet.IntegrationTests/BestandServiceTests.cs` erweitern

- [ ] **Step 1:** Signatur erweitern — **mit Default-Werten**, damit alle bestehenden Aufrufer unverändert kompilieren und ihr heutiges Verhalten exakt behalten:
```csharp
internal static async Task BucheBewegungAsync(
    MiletDbContext db, int artikelId, int lagerortId, decimal mengeDelta,
    LagerbewegungTyp typ, int? belegPositionId, CancellationToken ct,
    int? sektionId = null, int? kulturstufeId = null)
```
- [ ] **Step 2:** Am Methodenanfang die zentralen Dimensionsregeln über `KulturRegeln.PruefeDimensionen` erzwingen (Artikel + „hat der Lagerort aktive Sektionen?" in **einer** vorgelagerten Abfrage laden). Das ist der Grund, warum die Regel unumgehbar ist: es gibt keinen zweiten Schreibpfad.
- [ ] **Step 3:** `UPDATE` um die NULL-sichere Dimensionsklausel aus E4 erweitern.
- [ ] **Step 4:** Erstanlage durch das Upsert-Muster aus E4 ersetzen (`INSERT … WHERE NOT EXISTS (… WITH (UPDLOCK, HOLDLOCK))`, danach denselben `UPDATE` erneut; bleibt er wieder bei 0 Zeilen, wird geworfen). `db.ArtikelBestaende.Add(...)` entfällt ersatzlos.
- [ ] **Step 5:** `Lagerbewegung` mit den beiden neuen Dimensionen anlegen.
- [ ] **Step 6 (Tests, laufen nur mit Docker — trotzdem schreiben):**
  - Regression: Buchung ohne Dimensionen schreibt weiterhin genau eine Zeile fort, legt keine zweite an.
  - Zwei parallele **Erst**buchungen auf dieselbe Kombination → beide erfolgreich, **eine** Bestandszeile, Menge = Summe (deckt den in E4 beschriebenen Altbestandsfehler ab).
  - Negativsperre je Dimension: 100 Stück in (Sektion A, JP) verhindern eine Abbuchung von 101 aus (Sektion A, JP), aber eine Abbuchung aus (Sektion B, JP) mit eigenem Bestand bleibt möglich.
  - Regelverletzung: Kulturpflanze ohne `KulturstufeId` wirft; Handelsware mit `KulturstufeId` wirft.
  - Ledger-Invariante: `SUM(Lagerbewegungen.Menge)` je vier Dimensionen == `ArtikelBestand.Menge`.
- [ ] `dotnet build` + alle drei Testprojekte einzeln grün (Integrationstests dürfen ohne Docker sauber skippen — das ist **kein** Nachweis, siehe „Verifikation").

---

### Task 7: Application — Modul `Gaertnerei` (DTOs, Interfaces, Validatoren)

**Files:** Create `src/Milet.Application/Gaertnerei/Dtos.cs`, `IGaertnereiServices.cs`, `Validators.cs`; Modify `src/Milet.Application/Lager/Dtos.cs` (`ArtikelBestandDto` um Sektion/Stufe), `src/Milet.Application/Stammdaten/Dtos.cs` (`ArtikelDto` um `IstKulturpflanze`/`BotanischerName`)

- [ ] **Step 1:** DTOs — u. a.
  - `KulturstufeDto`, `GaertnereiplanDto`, `SektionDto`, `FeldDto` (Lagerort + Geometrie + Sektionen)
  - `PflanzeUebersichtDto(ArtikelId, Artikelnummer, Bezeichnung, BotanischerName?, GesamtMenge, IReadOnlyList<MengeJeStufeDto> JeStufe)` — die Sidebar-Zeile
  - `PflanzenVorkommenDto(FeldId, FeldBezeichnung, SektionId, SektionBezeichnung, KulturstufeId, StufeBezeichnung, FarbeHex, Menge)` — eine Fundstelle
  - `KulturZugangDto`, `StufenwechselDto`, `UmsetzenDto`, `AusfallDto` (Artikel, Feld, Sektion, Stufe(n), Menge, Datum, Bemerkung)
  - `VerfuegbarkeitDto(ArtikelId, decimal VerkaufsfaehigGesamt, decimal Reserviert, decimal Frei, IReadOnlyList<PflanzenVorkommenDto> Fundstellen, IReadOnlyList<MengeJeStufeDto> NichtVerkaufsfaehig)`
  - `KulturHistorieZeileDto(Zeitpunkt, Typ, Menge, FeldBezeichnung, SektionBezeichnung, StufeBezeichnung, BelegNummer?)`
- [ ] **Step 2:** Interfaces `IKulturstufenService`, `IGaertnereiplanService`, `IKulturBuchungService`, `IKulturBestandService`, `IVerfuegbarkeitService` (Methoden siehe Tasks 8–10, 13).
- [ ] **Step 3:** Validatoren: Kulturstufe (Code/Bezeichnung Pflicht, `Reihenfolge > 0`, `FarbeHex` gegen `^#[0-9A-Fa-f]{6}$`), Sektion (Maße > 0, Code Pflicht), Plan (Maße > 0), Buchungs-DTOs (`Menge > 0`, Pflichtfelder). Unit-Tests je Validator in `Milet.Application.Tests`.
- [ ] **Step 4:** `ArtikelService`: Umschalten von `IstKulturpflanze` blockieren, solange für den Artikel Bestandszeilen mit unpassender Dimensionierung existieren — mit handlungsleitender Meldung („Artikel hat 320 Stück Bestand ohne Kulturstufe; bitte zuerst über eine Kulturzugangsbuchung auf eine Stufe umbuchen").
- [ ] Application-Tests grün.

---

### Task 8: Infrastructure — `KulturstufenService` + `GaertnereiplanService`

**Files:** Create `src/Milet.Infrastructure/Services/KulturstufenService.cs`, `GaertnereiplanService.cs`, `Mapping/GaertnereiMapping.cs`; Modify `DependencyInjection.cs`

- [ ] **Step 1:** `KulturstufenService`: `ListeAsync`, `SpeichereAsync`, `LoescheAsync` — exakt das Kleinstamm-Muster aus `KleinstammServices.cs`, Löschen über `ConcurrencyHelper.SaveChangesDeletingAsync` (FK-Konflikt → „Kulturstufe wird noch von Bestand oder Bewegungen verwendet und kann nicht gelöscht werden — stattdessen auf ‚inaktiv' setzen").
- [ ] **Step 2:** `GaertnereiplanService`:
  - `LadePlanAsync()` → Plan + alle Felder (Lagerorte mit `IstFeld`) + deren Sektionen, in **einer** Abfrage-Gruppe; das ist die Datenquelle für Grundriss-Editor **und** Pflanzenübersicht.
  - `SpeicherePlanAsync(GaertnereiplanDto)`, `SpeichereFeldAsync(FeldDto)` (legt bei `Id == 0` einen `Lagerort` mit `IstFeld = true` an — inkl. Code-Vergabe), `SpeichereSektionAsync`, `LoescheSektionAsync`, `LoescheFeldAsync`.
  - Geometrieprüfung über `KulturRegeln.LiegtInnerhalb` (Fehler) und `Ueberlappt` (Warnung im Rückgabe-DTO, kein Abbruch).
  - Löschen eines Feldes/einer Sektion mit Restbestand → FK-Restrict, übersetzt in „… enthält noch Bestand".
- [ ] **Step 3:** DI-Registrierung, Build grün.

---

### Task 9: Infrastructure — `KulturBuchungService` (Zugang / Stufenwechsel / Umsetzen / Ausfall)

**Files:** Create `src/Milet.Infrastructure/Services/KulturBuchungService.cs`; Modify `DependencyInjection.cs`
**Tests:** Create `tests/Milet.IntegrationTests/KulturBuchungServiceTests.cs`

- [ ] **Step 1:** `ZugangAsync` — `PruefeRecht(Gaertnerei)`, Validierung, eine Transaktion, ein `BucheBewegungAsync(+menge, Kulturzugang)`.
- [ ] **Step 2:** `StufenwechselAsync` — eine Transaktion, **zwei** Buchungen (`−menge` von Quell-Sektion/-Stufe, `+menge` auf Ziel-Sektion/-Stufe), beide `Stufenwechsel`. Die Abgangsbuchung läuft durch die Negativsperre; scheitert sie, rollt die ganze Transaktion zurück und es entsteht **kein** Zugang (Test!).
- [ ] **Step 3:** `UmsetzenAsync` — wie Step 2, aber Stufe bleibt gleich, Typ `Umsetzen`; `KulturRegeln.PruefeStufenwechsel` verhindert die Nulloperation (gleiche Sektion **und** gleiche Stufe).
- [ ] **Step 4:** `AusfallAsync` — eine negative Buchung, Typ `Ausfall`.
- [ ] **Step 5:** Tests (Testcontainers): Stufenwechsel verschiebt Menge exakt und erzeugt **zwei** Ledger-Zeilen; Stufenwechsel über den Bestand hinaus wirft und hinterlässt **keine** Teiländerung (Rollback-Nachweis); paralleler Stufenwechsel derselben Quelle überzieht nicht; Ausfall reduziert Bestand und ist über den Typ auswertbar.
- [ ] Build + Tests grün.

---

### Task 10: Infrastructure — `KulturBestandService` (Pflanzenliste, Vorkommen, Historie)

**Files:** Create `src/Milet.Infrastructure/Services/KulturBestandService.cs`; Modify `DependencyInjection.cs`

- [ ] **Step 1:** `LadePflanzenAsync(string? suchtext)` → alle Artikel mit `IstKulturpflanze && !Gesperrt`, je Artikel Gesamtmenge und Mengen je Stufe (eine gruppierte Abfrage über `ArtikelBestaende`, kein N+1). Artikel **ohne** Bestand erscheinen mit Menge 0 — der Nutzer will „alle Pflanzen der Gärtnerei" sehen, nicht nur die vorrätigen.
- [ ] **Step 2:** `LadeVorkommenAsync(int artikelId)` → alle Fundstellen (Feld, Sektion, Stufe, Menge, Farbe), sortiert nach Stufe-Reihenfolge, dann Feld, dann Sektion. Das ist die Datenbasis für das Highlighting im Grundriss.
- [ ] **Step 3:** `LadeHistorieAsync(int artikelId, int? sektionId, DateOnly? von, DateOnly? bis)` → Lagerbewegungen dieses Artikels mit Dimensions- und Belegbezug, absteigend nach Zeitpunkt, `Take(500)`. Beantwortet „wann wurde diese Kultur zuletzt umgetopft, wie viel ist ausgefallen".
- [ ] Build grün.

---

### Block C — Integration in bestehende Flüsse

---

### Task 11: Lieferschein- und Wareneingangs-Buchung auf Dimensionen umstellen

**Files:** Modify `LieferscheinBuchenService.cs`, `WareneingangBuchenService.cs`, `BelegUeberleitungService.cs`, `Verkauf/Dtos.cs` (`BelegPositionDto` um Sektion/Stufe), `Mapping/VerkaufMapping.cs`

- [ ] **Step 1:** `BelegPositionDto` + Mapping um `SektionId?`/`KulturstufeId?` erweitern.
- [ ] **Step 2:** `LieferscheinBuchenService`: die beiden Dimensionen der Position an `BucheBewegungAsync` durchreichen. Zusätzliche Vorabprüfung mit klarer Meldung: ist der Artikel eine Kulturpflanze und fehlt die Stufe, dann „Position N: Kulturstufe fehlt" statt der generischen Regelverletzung aus Task 6.
- [ ] **Step 3:** Beim Buchen prüfen, dass die gewählte Stufe **verkaufsfähig** ist — sonst wirft es mit „Position N: Stufe ‚Jungpflanze' ist nicht verkaufsfähig." Das ist die einzige *harte* Verkaufsregel der Phase (E8: beraten beim Auftrag, sperren beim Liefern).
- [ ] **Step 4:** `WareneingangBuchenService`: Dimensionen durchreichen; Zukauf von Jungpflanzen bucht damit direkt in die richtige Stufe.
- [ ] **Step 5:** `BelegUeberleitungService`: beim Auftrag→Lieferschein die Dimensionen aus der übergebenen Auswahl auf die Zielposition schreiben (die Signatur `UeberleitenMitAuswahlAsync(..., int? lagerortId, ...)` bekommt dafür eine Auswahlstruktur je Position statt nur `lagerortId` — bestehendes Verhalten bleibt bei `null`-Dimensionen identisch).
- [ ] Build + Tests grün.

---

### Task 12: Inventur auf Dimensionen umstellen ⚠️ (Datenkorrektheit, siehe E10)

**Files:** Modify `InventurService.cs`, `Lager/Dtos.cs` (`InventurPositionDto`), `Mapping/LagerMapping.cs`
**Tests:** `tests/Milet.IntegrationTests/` — neue Testklasse `InventurServiceTests.cs`

- [ ] **Step 1:** `NeueInventurAsync` verzweigt nach `Lagerort.IstFeld`:
  - **kein Feld:** unveränderte Logik (je lagerfähigem Artikel eine Position, Dimensionen `NULL`) — Regressionspfad.
  - **Feld:** je existierender `ArtikelBestand`-Zeile dieses Lagerorts eine Position mit übernommenen `SektionId`/`KulturstufeId` und eingefrorener `SollMenge`.
- [ ] **Step 2:** Drift-Prüfung im Abschluss auf den Schlüssel `(ArtikelId, SektionId, KulturstufeId)` umstellen; Fehlermeldung um Sektion/Stufe ergänzen, sonst ist sie nicht handlungsleitend.
- [ ] **Step 3:** Korrekturbuchung im Abschluss mit den Dimensionen der Position aufrufen.
- [ ] **Step 4:** Tests: Feld-Inventur mit zwei Sektionen × zwei Stufen erzeugt vier Positionen mit den richtigen Sollmengen; Abschluss bucht jede Differenz auf **ihre** Dimension; Hauptlager-Inventur verhält sich unverändert.
- [ ] Build + Tests grün.

---

### Task 13: `VerfuegbarkeitService` — „ist die Pflanze vorrätig, in welcher Stufe, auf welchem Feld?"

**Files:** Create `src/Milet.Infrastructure/Services/VerfuegbarkeitService.cs`; Modify `DependencyInjection.cs`
**Tests:** `tests/Milet.IntegrationTests/VerfuegbarkeitServiceTests.cs`

- [ ] **Step 1:** `LadeAsync(int artikelId, decimal? benoetigteMenge)`:
  - verkaufsfähiger Bestand = Σ `ArtikelBestand.Menge` über alle Zeilen mit `Kulturstufe.IstVerkaufsfaehig`
  - reserviert = Σ offener Mengen aller Auftragspositionen dieses Artikels (`BelegTyp.Auftrag`, Status `Entwurf`/`Gebucht`), berechnet über die bestehende `UrsprungsPositionId`-Logik (E8)
  - frei = verkaufsfähig − reserviert
  - Fundstellen je (Feld, Sektion, Stufe), inklusive der **nicht** verkaufsfähigen Stufen (der Nutzer will explizit sehen, was in den Vorstufen steht)
  - Ampel: `Gruen` (frei ≥ benötigt), `Gelb` (verkaufsfähig < benötigt, aber Vorstufen vorhanden), `Rot` (nichts vorhanden)
- [ ] **Step 2:** `LadeFuerBelegAsync(int belegId)` → Ampel je Position + Gesamtampel; Datenquelle für die Auftragsliste und das Panel im Editor.
- [ ] **Step 3:** Tests: Reservierung reduziert „frei"; teilgelieferter Auftrag reserviert nur noch die Restmenge; Vorstufenbestand macht Gelb, nicht Grün.
- [ ] Build + Tests grün.

---

### Block D — WinUI

---

### Task 14: Einstellungen — Kulturstufen-Tab; Artikel-Edit — Kulturpflanzen-Felder

**Files:** Modify `Views/Stammdaten/KleinstammPage.xaml`(+`.cs`), `ViewModels/Stammdaten/KleinstammViewModel.cs`, `Views/Stammdaten/ArtikelEditPage.xaml`, `ViewModels/Stammdaten/ArtikelEditViewModel.cs`

- [ ] **Step 1:** Neuer `PivotItem` „Kulturstufen" im Muster der bestehenden Tabs (3-Spalten-Grid `380`/`360`/`*` — die in Phase 1 gefixte Layoutfalle nicht wieder einbauen): Liste links, Formular rechts mit Bezeichnung, Code, Reihenfolge (`NumberBox`), „verkaufsfähig" (`ToggleSwitch`), Farbe (`ColorPicker` oder Vorschau-`Rectangle` + Hex-`TextBox`), Aktiv.
- [ ] **Step 2:** `KleinstammViewModel` um den Abschnitt erweitern (Laden/Neu/Speichern/Löschen), Auswahl **vor** dem Neuladen zurücksetzen (bekannte Absturzfalle), ComboBox-gebundene Ids nullable.
- [ ] **Step 3:** Warnhinweis (`InfoBar`) im Tab, wenn keine aktive Stufe `IstVerkaufsfaehig` gesetzt hat: „Ohne verkaufsfähige Stufe kann kein Lieferschein gebucht werden."
- [ ] **Step 4:** `ArtikelEditPage`: `CheckBox` „Kulturpflanze" + `TextBox` „Botanischer Name" (nur sichtbar bei gesetztem Häkchen).
- [ ] Build App (x64) grün.

---

### Task 15: Grundriss-Editor (`GrundrissPage`)

**Files:** Create `Views/Gaertnerei/GrundrissPage.xaml`(+`.cs`), `ViewModels/Gaertnerei/GrundrissViewModel.cs`, `ViewModels/Gaertnerei/PlanElementViewModel.cs`

**Rendering-Ansatz (verbindlich):** `ItemsControl` mit `Canvas` als `ItemsPanel`; die Positionierung läuft über `ItemContainerStyle` mit klassischen `Binding`-Settern auf `Canvas.Left`/`Canvas.Top` (`x:Bind` kann keine Attached Properties im Style setzen). Die ViewModels (`PlanElementViewModel`) rechnen **Meter → Pixel** selbst (`PixelX = PosXMeter * Zoom`) und melden bei Zoom-Änderung `OnPropertyChanged` — dadurch bleibt die XAML frei von Konvertern und der Zoom kostet keine Neuberechnung im Renderer.
**Plan B (falls das Style-Binding zickt, analog zum DataGrid-Plan-B in `PLAN.md`):** die Rechtecke im Code-Behind direkt in den `Canvas` hängen — die ViewModels bleiben dieselben.

- [ ] **Step 1:** Planfläche: `Border` mit Rasterlinien (1-m-Raster), Zoom-Slider, Maßangaben.
- [ ] **Step 2:** Felder als Rechtecke mit Beschriftung; Sektionen als kleinere Rechtecke **innerhalb** des Feldes (Koordinaten relativ, also `FeldPixelX + SektionPixelX`).
- [ ] **Step 3:** Auswahl per Klick; rechte Eigenschaftsspalte mit Bezeichnung, Code, X/Y/Breite/Höhe als `NumberBox` (`DecimalToDoubleConverter`) + berechnete Fläche in m².
- [ ] **Step 4:** Ziehen und Größenändern per `PointerPressed`/`PointerMoved`/`PointerReleased` (Anfasser unten rechts), Pixel-Delta → Meter, **Raster 0,5 m**, Begrenzung auf die Planfläche bzw. auf das Elternfeld.
- [ ] **Step 5:** Buttons „Feld anlegen", „Sektion anlegen" (legt sie in das gewählte Feld), „Löschen", „Speichern"; Überlappungswarnung als `InfoBar`, kein Abbruch.
- [ ] **Step 6:** Numerische Eingabe ist gleichwertig zur Maus (nicht nur Fallback) — Tastaturbedienung und exakte Maße sind in der Praxis wichtiger als das Ziehen.
- [ ] Build grün + manueller Klick-Test.

---

### Task 16: Pflanzenübersicht (`PflanzenUebersichtPage`) — die Kernanforderung

**Files:** Create `Views/Gaertnerei/PflanzenUebersichtPage.xaml`(+`.cs`), `ViewModels/Gaertnerei/PflanzenUebersichtViewModel.cs`

- [ ] **Step 1:** Zweispaltiges Layout: links (`320`) Suchfeld + `ListView` aller Kulturpflanzen (Bezeichnung, botanischer Name kursiv, Gesamtmenge, kleine farbige Mengen-Badges je Stufe); rechts der Grundriss (dieselben `PlanElementViewModel`s wie Task 15, aber **schreibgeschützt**).
- [ ] **Step 2:** Auswahl einer Pflanze lädt `LadeVorkommenAsync` und färbt die betroffenen Sektionen in der Farbe **ihrer** Kulturstufe; alle übrigen Sektionen werden ausgegraut (Deckkraft ~0,25). Damit ist die vom Nutzer beschriebene Situation — dieselbe Pflanze liegt in mehreren Sektionen, weil sie in mehreren Stufen steht — auf einen Blick lesbar.
- [ ] **Step 3:** Beschriftung der hervorgehobenen Sektionen mit der Menge; `ToolTip` mit Feld/Sektion/Stufe/Menge; Legende der Stufen unter dem Plan.
- [ ] **Step 4:** Unter dem Plan eine Fundstellentabelle (Feld, Sektion, Stufe, Menge) — dieselben Daten in exakter, kopierbarer und barrierefreier Form; ein reiner Grafik-Modus wäre für Screenreader und für „Menge exakt ablesen" unbrauchbar.
- [ ] **Step 5:** Filterleiste „nur verkaufsfähige Stufen anzeigen" und Stufenfilter.
- [ ] Build grün + manueller Klick-Test.

---

### Task 17: Kulturbuchungen (`KulturbuchungPage`) + Historie

**Files:** Create `Views/Gaertnerei/KulturbuchungPage.xaml`(+`.cs`), `ViewModels/Gaertnerei/KulturbuchungViewModel.cs`

- [ ] **Step 1:** Vier Modi in einem `Pivot` oder `SegmentedControl`: Zugang, Stufenwechsel, Umsetzen, Ausfall.
- [ ] **Step 2:** Gemeinsame Auswahl Pflanze → Feld → Sektion → Stufe, wobei **nur Kombinationen mit Bestand** angeboten werden (außer beim Zugang) und die verfügbare Menge live angezeigt wird. Das verhindert die meisten Negativsperren-Fehler, bevor sie entstehen.
- [ ] **Step 3:** Stufenwechsel: Ziel-Stufe vorbelegt mit `KulturRegeln.NaechsteStufe`, Ziel-Sektion vorbelegt mit der Quell-Sektion (Umtopfen bleibt oft am Ort), Menge vorbelegt mit dem vollen Bestand.
- [ ] **Step 4:** Nach jeder Buchung Erfolgsmeldung mit der neuen Bestandslage; Fehler (Negativsperre, Regelverletzung) über den bestehenden `IDialogService`.
- [ ] **Step 5:** Rechte Spalte: Kulturhistorie der gewählten Pflanze (`LadeHistorieAsync`) mit Typ, Menge, Ort, Datum.
- [ ] Build grün + manueller Klick-Test.

---

### Task 18: Bestandsübersicht + Teillieferungsdialog um Sektion/Stufe erweitern

**Files:** Modify `Views/Lager/BestandUebersichtPage.xaml`, `ViewModels/Lager/BestandUebersichtViewModel.cs`, `Views/Lager/TeillieferungDialog.xaml`(+`.cs`)

- [ ] **Step 1:** Bestandsübersicht: Spalten „Feld/Lagerort", „Sektion", „Kulturstufe"; Filter nach Feld und Stufe; Bestandskorrektur-Panel bekommt Sektions-/Stufenauswahl (bei Kulturpflanzen Pflichtfelder).
- [ ] **Step 2:** `TeillieferungDialog`: je Position zusätzlich Sektion und Kulturstufe, vorbelegt nach E9 (verkaufsfähige Stufe mit der größten Menge, darin die größte Sektion), mit Anzeige der dort verfügbaren Menge.
- [ ] **Step 3:** Nicht-Kulturartikel zeigen die neuen Spalten nicht (bzw. leer) — der bestehende Handelsware-Ablauf bleibt optisch unverändert.
- [ ] Build grün.

---

### Task 19: Verfügbarkeitsanzeige im Verkauf

**Files:** Modify `ViewModels/Verkauf/BelegEditViewModelBase.cs`, `Views/Verkauf/AuftragEditPage.xaml`, `AngebotEditPage.xaml`, `ViewModels/Verkauf/AuftragListViewModel.cs`, `Views/Verkauf/AuftragListPage.xaml`

- [ ] **Step 1:** `BelegEditViewModelBase`: bei Auswahl eines Artikels in der Positionszeile `IVerfuegbarkeitService.LadeAsync(artikelId, menge)` aufrufen (der Service wird **optional** injiziert, damit Einkaufs-/Angebots-Pfad und bestehende Tests unverändert bleiben) und das Ergebnis in Anzeige-Properties abbilden.
- [ ] **Step 2:** Panel rechts neben dem Positionsformular: Ampel + „Verkaufsfähig frei: 240 Stück" + Fundstellenliste (Feld / Sektion / Stufe / Menge) + Zeile „zusätzlich in Anzucht: 500 Teenagerpflanze, 3.000 Jungpflanze". Genau die vom Nutzer beschriebene Information beim Auftragseingang.
- [ ] **Step 3:** Sichtbar nur, wenn der gewählte Artikel `IstKulturpflanze` ist — sonst würde das Panel bei Töpfen und Substrat nur stören.
- [ ] **Step 4:** Auftragsliste: Ampelspalte je Auftrag (`LadeFuerBelegAsync`), damit „welche Aufträge kann ich heute ausliefern?" ohne Öffnen beantwortbar ist. Laden asynchron nach der Liste, damit die Liste nicht langsamer wird.
- [ ] Build grün + manueller Klick-Test.

---

### Task 20: Navigation, DI, Rechte

**Files:** Modify `Shell/ShellPage.xaml`(+`.cs`), `App.xaml.cs`

- [ ] **Step 1:** Neuer Top-Level-Punkt „Gärtnerei" (Icon `Globe` oder `Street`) mit „Pflanzenübersicht", „Grundriss", „Kulturbuchungen"; einsortiert **zwischen** Stammdaten und Verkauf, weil der Nutzer hier täglich arbeitet.
- [ ] **Step 2:** Alle neuen Seiten im `NavigationService` registrieren, alle neuen ViewModels transient und alle neuen Services scoped in der DI registrieren.
- [ ] **Step 3:** Menüsichtbarkeit an `RechtCodes.Gaertnerei` binden (Muster aus Phase 7).
- [ ] Build App (x64) grün; App startet, alle drei neuen Seiten navigierbar.

---

### Block E — Auswertung & Verifikation

---

### Task 21: Reporting — Kulturbestand und Ausfallquote

**Files:** Modify `src/Milet.Application/Reporting/Dtos.cs`, `IReportingService.cs`, `src/Milet.Infrastructure/Services/ReportingService.cs`, `Views/Reporting/ReportingPage.xaml`, `ViewModels/Reporting/ReportingViewModel.cs`

- [ ] **Step 1:** Auswertung „Kulturbestand" — Menge je Pflanze × Stufe × Feld × Sektion, filterbar, mit CSV-Export über den vorhandenen `CsvWriter`.
- [ ] **Step 2:** Auswertung „Ausfallquote" — je Pflanze und Stufe: Σ `Ausfall`-Bewegungen gegen Σ Zugänge im Zeitraum, in Prozent. Das ist die betriebswirtschaftlich interessanteste Zahl der ganzen Phase (sie steuert, wie viel man ansetzen muss, um eine Verkaufsmenge zu erreichen).
- [ ] **Step 3:** Auswertung „Flächenbelegung" — belegte m² je Feld (Summe der Sektionsflächen mit Bestand) gegen Gesamtfläche.
- [ ] Build + Tests grün.

---

### Task 22: Verifikation und Fortschreibung der Projektdokumente

- [ ] **Step 1:** `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` → 0 Fehler.
- [ ] **Step 2:** Alle drei Testprojekte **einzeln** (MTP): Domain, Application, IntegrationTests.
- [ ] **Step 3:** Integrationstests **mit Docker** ausführen. Für diese Phase ist ein reiner Skip **nicht** akzeptabel: die Tasks 6, 9 und 12 ändern den einzigen Schreibpfad auf Bestand, den Upsert und die Inventurbuchung. Steht kein Docker zur Verfügung, muss vor der Abnahme ein LocalDB-Fallback für diese Testklassen ergänzt werden (in `STATUS.md` bereits als offener Punkt notiert).
- [ ] **Step 4:** Migration gegen eine Datenbank anwenden, die **vor** Phase 8 bereits Bestandsdaten hatte, und per `sqlcmd` nachweisen: alte Bestandszeilen unverändert, Dimensionen `NULL`, Unique-Index neu, Lieferschein-Buchung auf einer Altzeile weiterhin möglich.
- [ ] **Step 5:** Manueller UI-Smoke-Test, dokumentiert in `docs/smoke-tests.md`: Kulturstufen umbenennen → Änderung schlägt in Pflanzenübersicht und Auftragspanel durch; Grundriss anlegen (Feld + 3 Sektionen); Kulturzugang 1.000 JP; Stufenwechsel 400 JP → TP in andere Sektion; Ausfall 50; Pflanzenübersicht zeigt beide Sektionen in den richtigen Farben; Auftrag über 100 VP → Ampel gelb; Stufenwechsel 300 TP → VP; Auftrag erneut → grün; Lieferschein aus VP-Sektion buchen; Bestand und Ledger per `sqlcmd` gegenprüfen.
- [ ] **Step 6:** `PLAN.md` um Abschnitt „Gärtnerei/Kulturführung" und Phase-8-Zeile ergänzen; `STATUS.md` um den Phase-8-Abschnitt (inkl. dessen, was **nicht** verifiziert werden konnte) fortschreiben. Der in E4 gefundene Alt-Race beim Erstanlegen einer Bestandszeile wird in `STATUS.md` unter „Bekannte Risiken" als **behoben** vermerkt, mit Datum und Testnachweis.

---

## Verifikation (Zusammenfassung)

**Unit (Domain):** Dimensionsregeln (alle 4 gültigen + alle ungültigen Kombinationen), `NaechsteStufe` inkl. „höchste Stufe erreicht", Stufenwechsel-Nulloperation, Geometrie (Sektion innerhalb Feld, Überlappung).
**Unit (Application):** Validatoren für Kulturstufe/Sektion/Plan/Buchungs-DTOs, inkl. Farb-Regex.
**Integration (Testcontainers, Docker Pflicht für die Abnahme):** parallele Erstanlage derselben Bestandszeile (Upsert), Negativsperre je Dimension, Regression dimensionsloser Buchungen, Stufenwechsel-Atomarität und -Rollback, Ledger-Invariante je vier Dimensionen, Feld-Inventur inkl. Drift-Erkennung, Verfügbarkeit mit Reservierung/Teillieferung.
**Manuell (UI):** Ablauf aus Task 22 Step 5.

---

## Risiken

1. **`BestandService` ist der sicherheitskritischste Code des Systems** und wird in Task 6 an zwei Stellen geändert. Mitigation: Default-Parameter halten alle Altaufrufer semantisch identisch; ein expliziter Regressionstest für den dimensionslosen Pfad; die Änderung wird als eigener Commit isoliert, damit sie einzeln revidierbar ist.
2. **Unique-Index über nullable Spalten** ist korrekt, aber nicht offensichtlich (SQL Server behandelt NULL als einen Wert). Wer den Index später „aufräumt", bricht die Eindeutigkeit für Handelsware. Mitigation: Kommentar in der Configuration + Integrationstest, der einen zweiten `(A, L, NULL, NULL)`-Insert erwartet scheitern lässt.
3. **Inventur-Ripple (E10):** würde man ihn auslassen, würde die Inventur auf Feldern still falsch buchen. Deshalb Pflichtbestandteil, nicht verschiebbar.
4. **WinUI-Canvas mit Attached-Property-Bindings** ist der wackeligste UI-Teil (`x:Bind` kann `Canvas.Left` im Style nicht setzen). Plan B (Code-Behind-Rendering) ist in Task 15 benannt; die ViewModels bleiben in beiden Fällen identisch, also kostet ein Umschwenk nur die View.
5. **Kombinatorische Explosion der Bestandszeilen:** 300 Sorten × 40 Sektionen × 3 Stufen sind theoretisch 36.000 Zeilen. Real entstehen Zeilen nur dort, wo tatsächlich gebucht wurde (Upsert), also in der Größenordnung „Anzahl belegter Sektionen" — unkritisch. Die Inventur (E10) darf deshalb **nicht** über das Kreuzprodukt gebildet werden.
6. **Verfügbarkeit ist eine Anzeige ohne Sperre** (E8) — bewusst; die harte Prüfung sitzt beim Lieferschein-Buchen.
7. **Der bereits dokumentierte READ-COMMITTED-Race in `BelegUeberleitungService`** bleibt unverändert bestehen und wird durch diese Phase weder verschärft noch behoben. Er gehört in eine eigene Härtungs-Session (`UPDLOCK` auf dem Quellbeleg).
8. **`Milet.App` ist auf einer Nicht-Windows-Maschine nicht baubar.** Wird diese Phase (wie Phase 5–7) ohne Windows umgesetzt, bleibt der gesamte Block D unverifiziert — das ist dann in `STATUS.md` genauso deutlich zu vermerken wie bei den Vorphasen, nicht zu beschönigen.

## Bewusst außerhalb dieses Plans

- **Kulturplanung/Prognose** („wann sind wie viele Pflanzen verkaufsfähig?", Kulturdauer je Stufe, Terminplanung) — der naheliegende nächste Schritt, aber ein eigenes Thema mit eigener Datenhaltung (Sollkulturzeiten, Kalender).
- **Automatisches Splitten einer Lieferposition über mehrere Sektionen** (v1: eine Sektion je Position, Teilen von Hand).
- **Polygone, Drehung, Luftbild als Zeichenvorlage, mehrere Standorte** (E11) — der `Gaertnereiplan` ist als Tabelle vorbereitet, damit „mehrere Standorte" ohne Schemabruch nachrüstbar ist.
- **Topfgrößen als eigene Dimension** — in der Praxis sind P9 und 2-Liter meist eigene Artikel; erst wenn sich das als falsch erweist, wäre eine fünfte Dimension zu diskutieren (und dann mit derselben Begründungslogik wie E1).
- **Verbrauchsmaterial beim Stufenwechsel** (Töpfe/Substrat automatisch abbuchen) — technisch eine Stückliste, konzeptionell eine eigene Phase.
- **Etiketten-/Schilderdruck je Sektion**, mobile Erfassung im Feld, Bewässerungs-/Pflegeprotokolle.
- **`Gutschrift`** (weiterhin offen aus `PLAN.md`) und der fehlende **Storno-Pfad** — unverändert offen, unabhängig von dieser Phase.
