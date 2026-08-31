# Milet — Benutzeranleitung

Diese Anleitung richtet sich an Anwender der Milet-Warenwirtschaft (nicht an Entwickler —
für Architektur/Technik siehe `CLAUDE.md`/`PLAN.md`, für den aktuellen Umsetzungsstand
`STATUS.md`, für die Installation `docs/deployment.md`). Sie beschreibt, was die App tut und
wie die einzelnen Bereiche im Alltag bedient werden.

> **Hinweis zum Umsetzungsstand:** Milet wird phasenweise gebaut. Alle unten beschriebenen
> Funktionen sind implementiert, aber einige Programmteile (u. a. weite Teile ab Phase 5) sind
> bislang nicht auf einer echten Windows-Maschine durchgeklickt worden — Details dazu stehen in
> `STATUS.md`. Weicht das Verhalten der App von dieser Anleitung ab, gilt der tatsächliche
> Programmablauf, und der Unterschied sollte gemeldet werden.

## Inhalt

1. [Anmeldung](#1-anmeldung)
2. [Dashboard](#2-dashboard)
3. [Stammdaten](#3-stammdaten)
4. [Gärtnerei](#4-gärtnerei)
5. [Verkauf](#5-verkauf)
6. [Einkauf](#6-einkauf)
7. [Lager](#7-lager)
8. [Finanzen](#8-finanzen)
9. [Reporting](#9-reporting)
10. [Administration](#10-administration)
11. [Allgemeine Bedienhinweise](#11-allgemeine-bedienhinweise)
12. [Was Milet (noch) nicht kann](#12-was-milet-noch-nicht-kann)

---

## 1. Anmeldung

Beim Start zeigt Milet zuerst ein Anmeldefenster (Benutzername/Passwort). Die App prüft dabei
automatisch, ob die Datenbank auf dem aktuellen Stand ist — fehlt eine Migration, erscheint
statt eines Logins eine Fehlermeldung ("Datenbankschema ist nicht aktuell"); in dem Fall muss
zuerst der Migrator (`Milet.Tools.Migrator`) durch die Administration ausgeführt werden.

Bei der Ersteinrichtung existiert genau ein Benutzer:

- Benutzername: `admin`
- Passwort: `Milet!Admin1`

**Dieses Passwort muss nach der ersten Anmeldung sofort geändert werden** (Administration →
Benutzer → Feld „Neues Passwort"). Es steht öffentlich im Quellcode und darf im Produktivbetrieb
nicht bestehen bleiben. Nach jedem Passwort-Reset durch eine Administratorin/einen Administrator
verlangt Milet beim nächsten Login zwingend ein neues, selbst gewähltes Passwort — auch beim
allerersten Login mit dem Seed-Passwort oben (**Backend fertig, der erzwingende Dialog im
Programmfenster steht noch aus**).

Nach fünf falschen Passworteingaben hintereinander sperrt Milet das betroffene Benutzerkonto für
15 Minuten — auch das richtige Passwort wird in dieser Zeit abgelehnt. Ein Passwort-Reset durch
eine Administratorin/einen Administrator hebt die Sperre sofort auf.

Nach dem Login zeigt die linke Navigationsleiste nur die Bereiche, für die die zugewiesene
Rolle ein Recht besitzt (z. B. ist „Einkauf" ausgegraut, wenn die Rolle kein Einkaufs-Recht
hat) — siehe [Administration](#10-administration).

## 2. Dashboard

Startseite nach dem Login. Begrüßt mit Benutzername und Rolle — das ist zugleich der sichtbare
Beleg dafür, dass die Anmeldung funktioniert hat. Von hier aus über die linke Navigationsleiste
in die Fachbereiche wechseln.

## 3. Stammdaten

Grundlegende Daten, auf denen alle anderen Module aufbauen.

### Kunden / Lieferanten / Artikel

Je ein eigener Bereich mit Liste (links, mit Suchfeld) und Formular (rechts) im selben Muster:

- **Suchen:** Text ins Suchfeld eingeben, die Liste filtert live.
- **Neu anlegen:** „Neu"-Schaltfläche, Formular ausfüllen, „Speichern". Die Nummer (Kundennummer
  `KD-...`, Lieferantennummer `LF-...`, Artikelnummer `ART-...`) wird automatisch vergeben —
  nicht manuell eintragen.
- **Bearbeiten:** Eintrag in der Liste anklicken, Formular lädt die Daten, ändern, „Speichern".
- **Löschen:** Eintrag auswählen, „Löschen", Sicherheitsabfrage bestätigen. Ist der Datensatz
  bereits in Belegen referenziert, meldet die App das verständlich, statt abzustürzen.
- **Gleichzeitige Bearbeitung:** Ändert eine zweite Person denselben Datensatz zwischenzeitlich,
  erscheint beim Speichern ein Hinweis „Datensatz wurde geändert — neu laden?". „Ja" lädt den
  aktuellen Stand (eigene Änderung geht verloren), „Abbrechen" verwirft nur den Dialog.

Artikel-Besonderheiten: Einkaufs-/Listenpreis, Mindestbestand (Basis für den Bestellvorschlag,
s. [Einkauf](#6-einkauf)), sowie — falls es sich um eine Kulturpflanze handelt — botanischer
Name und die Kennzeichnung „ist Kulturpflanze" (s. [Gärtnerei](#4-gärtnerei)).

**Zahlenformat:** Milet läuft in deutscher Locale. Bei Geldbeträgen/Mengen ist das **Komma**
das Dezimaltrennzeichen, ein Punkt wird als Tausendertrennzeichen gelesen (`12.345` wird zu
`12345`, nicht zu `12,345`).

### Einstellungen

Sammelseite für kleinere Stammdaten, als Reiter (Pivot) organisiert, jeweils Liste+Formular
wie oben:

| Reiter | Zweck |
|---|---|
| Einheiten | Mengeneinheiten (Stück, kg, ...) |
| MwSt-Sätze | Steuersätze inkl. Erlös-/Aufwandskonto für den DATEV-Export |
| Zahlungsbedingungen | Zahlungsziele/Skontovorgaben |
| Versandarten | Versandoptionen für Belege |
| Preislisten | Preislisten inkl. Staffelpreise (dritte Spalte: Staffelpreis je gewählter Preisliste — Menge/Preis je Artikel) |
| Lagerorte | Physische Lagerorte, inkl. Kennzeichnung als „Feld" mit Breite/Höhe in Metern für den Gärtnerei-Grundriss |
| Mahnstufen | Konfiguration des Mahnwesens (Fristen je Stufe) |
| FibuKonten | Kontenrahmen (SKR03/SKR04), Standardkonten sowie die Skontokonten für den DATEV-Export (bleiben die Skontokonten leer, verwendet der Export das Standardkonto des gewählten Kontenrahmens) |
| Firmenstamm | Briefkopfdaten (Firmenname, Adresse, ...) für Belegdrucke |
| Kulturstufen | Reihenfolge/Farbe/„verkaufsfähig" der Kulturstufen (Jungpflanze → Teenagerpflanze → Verkaufspflanze) |

## 4. Gärtnerei

Zusatzmodul zur Führung von Kulturpflanzen über Wachstumsstufen und einen physischen Plan
(Feld → Sektion) — für Handelsware ohne Kulturführung ist dieses Modul ohne Bedeutung, sie
läuft weiterhin als reine Lagerort-Gesamtmenge.

- **Pflanzenübersicht:** Kulturpflanze auswählen → zeigt, in welchen Sektionen welche
  Kulturstufe in welcher Menge steht, farblich nach Kulturstufe hervorgehoben auf dem
  Grundriss, darunter als Tabelle.
- **Grundriss:** Felder und darin liegende Sektionen anlegen — per Maus (Ziehen/Größe ändern)
  oder durch direkte Zahleneingabe. Überlappende Sektionen werden mit einer Warnung markiert,
  das Speichern wird dadurch nicht blockiert.
- **Kulturbuchungen:** Drei Buchungsarten:
  - *Kulturzugang* — neue Pflanzen (z. B. Jungpflanzen) in einer Sektion einbuchen.
  - *Stufenwechsel* — Menge von einer Kulturstufe/Sektion in die nächste Stufe/Sektion
    umbuchen; das Formular schlägt automatisch die nächsthöhere Stufe vor.
  - *Ausfall* — Verlust (Frost, Schädlinge, ...) mit Bemerkung buchen; das ist eine eigene
    Bewegungsart, keine einfache Bestandskorrektur.

Beim Anlegen eines Verkaufsauftrags für eine Kulturpflanze zeigt ein Verfügbarkeits-Panel eine
Ampel: Grün = genug verkaufsfähiger Bestand, Gelb = nur Vorstufen vorhanden, Rot = nicht genug
Bestand in keiner Stufe.

## 5. Verkauf

Abbildung des Verkaufsprozesses **Angebot → Auftrag → Lieferschein → Rechnung**. Alle vier
Belegarten teilen sich Kopf (Kunde, Datum, Zahlungsbedingung, ...) und eine Positionstabelle
(Artikel-Auswahl mit automatischem Preisvorschlag aus der Preisfindung, Menge, Rabatt).

- **Angebot anlegen:** Kunde wählen, Positionen erfassen (Preisvorschlag-Schaltfläche zieht den
  passenden Listen-/Staffelpreis), speichern → Angebotsnummer `AN-JJJJ-nnnn` wird vergeben.
- **„→ Auftrag":** übernimmt alle Positionen 1:1 in einen neuen Auftrag, das Angebot wechselt
  auf Status „Erledigt".
- **„→ Lieferschein":** aus dem Auftrag heraus, mit Dialog zur Mengenauswahl — Teillieferungen
  sind möglich (nicht die volle bestellte Menge muss auf einmal geliefert werden); offene
  Menge je Position bleibt im Auftrag sichtbar. Details zum Buchen s. [Lager](#7-lager).
- **„→ Rechnung":** aus einem oder mehreren Lieferscheinen (Sammelrechnung über mehrere
  Lieferscheine desselben Kunden möglich). Die Rechnungsnummer wird **erst beim Buchen**
  vergeben (`RE-JJJJ-nnnn`), nicht schon beim Anlegen.
- **Buchen:** Sobald eine Rechnung gebucht ist, ist sie unveränderlich (GoBD-Sperre) — jede
  weitere Änderung wird von der App verweigert. Beim Buchen entsteht automatisch ein offener
  Posten (s. [Finanzen](#8-finanzen)).
- **PDF:** Jeder Beleg (Angebot/Auftrag/Rechnung) kann als PDF ausgegeben werden
  (Speichern-Dialog).
- **E-Mail:** Bei gebuchten Rechnungen zusätzlich „E-Mail senden" — verschickt das PDF per
  Microsoft Graph, sofern in `appsettings.json` eine `Graph`-Konfiguration hinterlegt ist;
  ohne diese Konfiguration meldet der Button einen sprechenden Fehler, die übrige App bleibt
  voll funktionsfähig.

> **Storno (Backend fertig, Bedienung im Fenster steht noch aus):** Eine gebuchte, noch nicht
> bezahlte Rechnung kann storniert werden — es entsteht automatisch eine Storno-Gutschrift, der
> ursprüngliche offene Posten wird ausgeglichen. Eine bereits (teilweise) bezahlte Rechnung lässt
> sich damit bewusst **nicht** automatisch stornieren (Zahlungsausgleich dafür manuell klären).
> Diese Funktion ist als Dienst bereits eingebaut und durchgetestet, aber noch **ohne eigene
> Schaltfläche im Programmfenster** — bis die Storno-/Gutschrift-Seiten ergänzt sind, ist sie nur
> über einen technischen Weg auslösbar (s. `STATUS.md`, Phase 9).

## 6. Einkauf

Spiegelbildlich zum Verkauf: **Bestellvorschlag → Bestellung → Wareneingang → Eingangsrechnung**.

- **Bestellvorschlag:** zeigt Artikel, deren Bestand unter dem hinterlegten Mindestbestand
  liegt, mit einer vorgeschlagenen Nachbestellmenge. Lieferant auswählen, „Bestellung
  erzeugen" — legt daraus eine Bestellung an.
- **Bestellungen:** wie Angebote/Aufträge editierbar, „→ Wareneingang" mit Mengenauswahl-Dialog
  (auch hier sind Teil-Wareneingänge möglich).
- **Wareneingänge:** beim Buchen wird der Bestand erhöht (s. [Lager](#7-lager)); benötigt der
  Artikel Seriennummern, fragt ein Dialog die neuen Seriennummern ab.
- **Eingangsrechnungen:** „→ Eingangsrechnung" aus dem Wareneingang, Buchen legt einen
  Kreditoren-Posten an. Weicht der Rechnungsbetrag vom zugrunde liegenden Wareneingang ab,
  warnt die App (nicht blockierend).
- **Storno (Backend fertig, Bedienung im Fenster steht noch aus):** Ein gebuchter Wareneingang
  kann storniert werden — der Bestand wird zurückgebucht; ist die Ware nicht mehr vollständig
  vorhanden (bereits weiterverkauft) oder ist der Artikel seriennummernpflichtig, lehnt die
  App das Storno mit einer klaren Meldung ab, statt einen falschen Bestand zu erzeugen.

## 7. Lager

- **Lieferscheine:** eigene Liste (aus Verkaufsaufträgen heraus erzeugt, s. oben). Mehrere
  Lieferscheine markieren → „→ Sammelrechnung" fasst sie zu einer Rechnung zusammen. Beim
  Buchen eines Lieferscheins werden Lagerbewegungen gebucht (Bestand wird verringert); ist der
  Artikel seriennummernpflichtig, wählt ein Dialog die auszuliefernden Seriennummern aus.
  **Storno (Backend fertig, Bedienung im Fenster steht noch aus):** Ein gebuchter Lieferschein
  kann storniert werden — Bestand und ggf. verknüpfte Seriennummern gehen zurück auf „auf
  Lager". Ist der Lieferschein bereits (auch nur teilweise) abgerechnet, lehnt die App das
  Storno ab — zuerst müsste die Rechnung storniert werden.
- **Bestandsübersicht:** zeigt den aktuellen Bestand je Artikel/Lagerort (bei Kulturpflanzen
  zusätzlich je Feld/Sektion/Kulturstufe, mit entsprechenden Filtern). Zwei Funktionen in
  derselben Seite:
  - *Bestandskorrektur* — manuelle Zu-/Abbuchung mit Grund, z. B. bei Inventurdifferenzen
    außerhalb einer förmlichen Inventur.
  - *Seriennummern-Erfassung* — Status einzelner Seriennummern einsehen/erfassen.
  - Ein Bestand kann **nicht** unter Null fallen — ein Buchungsversuch, der das täte, wird von
    der App abgelehnt (Fehlermeldung statt stillem Fehlbestand).
- **Inventur:** Inventur je Lagerort anlegen → Ist-Mengen erfassen → „Abschließen" bucht die
  Differenz zum eingefrorenen Soll-Bestand automatisch als Korrekturbuchungen. Pro Lagerort
  kann nur eine Inventur gleichzeitig offen sein; hat sich der Bestand seit der Momentaufnahme
  verändert, verweigert der Abschluss und verlangt eine neue Inventuraufnahme statt still
  falsch zu buchen.

## 8. Finanzen

- **Offene Posten:** Liste aller offenen Debitoren-/Kreditoren-Posten, filterbar nach Typ,
  Status (Offen/Teilweise bezahlt/Ausgeglichen) und Fälligkeit ("nur überfällige"). Eine
  Zahlung erfassen öffnet einen Dialog mit automatischem Skonto-Vorschlag (abhängig von der
  Zahlungsbedingung und dem Zahlungsdatum); der Status des Postens aktualisiert sich danach
  automatisch.
- **Mahnlauf:** ermittelt fällige Mahnungen anhand der konfigurierten Mahnstufen, gruppiert je
  Kunde und Zielstufe. Ablauf: fällige Posten anzeigen lassen → auswählen → Mahnlauf
  durchführen → Ergebnisliste mit PDF-/E-Mail-Button je erzeugter Mahnung. Der Mahnlauf ist
  bewusst manuell auszulösen, es gibt keinen automatischen/geplanten Lauf.
- **DATEV-Export:** Zeitraum wählen, Vorschau ansehen (zählt/summiert, ohne etwas zu
  markieren), dann exportieren — erzeugt eine CP1252-kodierte DATEV-EXTF-Datei
  (Buchungsstapel) über einen Speichern-Dialog. Erst nach erfolgreichem Speichern werden die
  exportierten Belege/Zahlungen als „exportiert" markiert (verhindert Doppelexport, ein
  Abbruch im Speichern-Dialog markiert nichts). Belege/Zahlungen ohne vollständig gepflegte
  Konten (Debitor/Kreditor/Erlös/Aufwand/Bank, s. Einstellungen → FibuKonten/MwSt-Sätze)
  erzeugen keine Buchungszeile und bleiben unmarkiert — kein stiller Datenverlust, aber auch
  keine automatische Fehlerkorrektur.

## 9. Reporting

Sechs Auswertungen als Reiter, jeweils mit „Laden" und CSV-Export (für Excel geeignet, ein
eigenes Format — nicht dasselbe wie der DATEV-Export):

- Offene Aufträge
- weitere Standardauswertungen (Umsatz, Lagerbestand, ...) sowie — bei Nutzung des
  Gärtnerei-Moduls — Kulturbestand, Ausfallquote und Flächenbelegung.

## 10. Administration

Nur sichtbar/nutzbar mit dem Recht „Administration". Drei Reiter:

- **Benutzer:** anlegen/bearbeiten, Rolle zuweisen, Passwort zurücksetzen (Feld „Neues
  Passwort" bei bestehendem Benutzer leer lassen, wenn es nicht geändert werden soll).
- **Rollen:** anlegen/bearbeiten, Rechte per Checkbox zuweisen (ein Recht je Hauptmenüpunkt:
  Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Gärtnerei, Administration).
- **AuditLog:** durchsuchbares Protokoll aller Änderungen an Stammdaten/Belegen (wer hat wann
  was angelegt/geändert), gefüllt automatisch im Hintergrund.

Der letzte aktive Administrator kann weder gelöscht noch aus der Administrator-Rolle entfernt
werden — das verhindert, dass sich die App versehentlich aussperrt.

## 11. Allgemeine Bedienhinweise

- **Suchen/Filtern:** In allen Listen ein einfaches Textfeld oben — filtert live während der
  Eingabe.
- **Speichern-Button vs. Fokus:** Eingaben in Textfeldern/Zahlenfeldern werden erst beim
  Verlassen des Feldes übernommen (Tab-Taste oder Klick woanders hin), nicht sofort beim
  Tippen — vor dem Klick auf „Speichern" also kurz woanders hinklicken, wenn der letzte Wert
  frisch eingegeben wurde.
- **Rechteklärung bei Fehlern:** Fehlt einer Rolle ein Recht, verweigert die App die Aktion
  auch dann, wenn der Menüpunkt über einen Umweg erreichbar wäre (serverseitige Prüfung,
  unabhängig von der Menüsichtbarkeit).
- **Gleichzeitige Bearbeitung:** s. Abschnitt Stammdaten — derselbe Mechanismus (Neu-laden-
  Dialog) gilt für alle Belege und Stammdaten mit Änderungsverfolgung.

## 12. Was Milet (noch) nicht kann

Diese Punkte sind bewusste Lücken der aktuellen Version, keine Bedienfehler:

- **Storno/Gutschrift ohne Fenster-Bedienung:** Rechnung/Lieferschein/Wareneingang lassen sich
  stornieren (s. jeweiliger Abschnitt), aber noch nicht per Schaltfläche im Programmfenster —
  nur ein Fachdienst dafür ist bereits fertig. Eine fachliche Gutschrift ohne Storno-Bezug
  (z. B. für eine Kulanz-Rückerstattung ohne stornierte Rechnung) gibt es weiterhin nicht.
- **Erzwungener Passwortwechsel ohne Fenster-Bedienung:** Der Dienst dahinter erzwingt den
  Wechsel bereits (s. Abschnitt Anmeldung), der dazugehörige Dialog im Programmfenster fehlt noch.
- **Kein automatischer Mahnlauf:** Muss jedes Mal manuell angestoßen werden.
- **Granularität der Rechte:** Rechte gelten je Hauptmodul, nicht je einzelne Aktion.
- **DATEV-Export kennt keine Gutschriften:** Solange es keine fachliche Gutschrift ohne
  Storno-Bezug gibt, ist das folgenlos — sobald diese Funktion nachgerüstet wird, muss der
  DATEV-Export sie noch mit abbilden.
- **Kein Mobil-/Web-Zugriff:** Milet ist eine Windows-Desktop-Anwendung (WinUI 3), kein Web-
  oder Mobilclient ist vorgesehen.

Aktuelle Details und Risiken: `STATUS.md`, Abschnitt „Bekannte Risiken"/„Nicht durchgeführt".
