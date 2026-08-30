# Manuelle UI-Smoke-Tests

Diese Datei sammelt die manuellen End-to-End-Testabläufe, die eine echte WinUI-Desktop-Session
(Windows, Maus/Tastatur oder UI-Automation) durchführen muss, weil sie in einer headless
Linux-Session nicht ausführbar sind. Siehe `STATUS.md` für den Verifikationsstand je Phase.

## Phase 8 — Gärtnerei/Kulturführung

**Noch nicht durchgeführt** (Session lief headless auf Linux ohne Windows/WinUI-Toolchain — s.
`STATUS.md`, Phase-8-Abschnitt). Ablauf nach Plan `docs/superpowers/plans/2026-08-30-phase8-gaertnerei-kultur.md`,
Task 22 Schritt 5:

1. `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf einer Windows-Maschine → muss
   0 Fehler liefern, bevor die folgenden Schritte überhaupt sinnvoll sind (WinUI-Code dieser Phase
   wurde nie kompiliert, nur auf wohlgeformtes XML und Klammerbalance geprüft).
2. Kulturstufen umbenennen (Einstellungen → Kulturstufen-Tab) → prüfen, dass die Änderung sofort in
   der Pflanzenübersicht und im Verfügbarkeits-Panel des Auftragseditors durchschlägt (Referenzen
   laufen über Id, s. E5).
3. Grundriss anlegen: ein Feld + 3 Sektionen per Maus (Ziehen/Größenändern) UND per numerischer
   Eingabe anlegen — beide Wege müssen gleichwertig funktionieren (Plan-Vorgabe Task 15 Schritt 6).
   Zwei Sektionen bewusst überlappend platzieren → Warnung erscheint, Speichern wird nicht blockiert.
4. Kulturbuchungen: Kulturzugang 1.000 Stück Jungpflanze in Sektion 1 buchen.
5. Stufenwechsel 400 Stück Jungpflanze → Teenagerpflanze in eine andere Sektion buchen — im
   Formular muss die Zielstufe automatisch auf die nächsthöhere Stufe vorbelegt sein.
6. Ausfall von 50 Stück (beliebige Stufe) buchen, Bemerkung "Frostschaden" o. ä. eintragen.
7. Pflanzenübersicht öffnen, die gebuchte Pflanze auswählen → beide Sektionen (Jungpflanze- und
   Teenagerpflanze-Bestand) müssen in den jeweiligen Kulturstufen-Farben hervorgehoben erscheinen,
   alle anderen Sektionen ausgegraut; Fundstellentabelle unter dem Plan muss dieselben Zahlen zeigen.
8. Auftrag über 100 Stück der Verkaufsstufe (VP) anlegen, obwohl noch keine VP vorhanden ist →
   Verfügbarkeits-Panel muss Ampel Gelb zeigen (Vorstufen vorhanden, verkaufsfähig nicht ausreichend).
9. Stufenwechsel 300 Stück Teenagerpflanze → Verkaufspflanze buchen.
10. Denselben Auftrag erneut öffnen → Ampel muss jetzt Grün zeigen.
11. Auftrag → Lieferschein überleiten (Teillieferungsdialog): die Sektions-/Kulturstufen-ComboBox der
    Kulturpflanzen-Position muss automatisch die verkaufsfähige Stufe mit der größten Menge
    vorschlagen; Lieferschein buchen.
12. Bestand und Ledger nach dem Buchen per `sqlcmd` gegen die LocalDB/den SQL Server prüfen:
    `ArtikelBestaende`-Zeile der VP-Sektion um 100 reduziert, `Lagerbewegungen` enthält eine
    `Lieferung`-Zeile mit den korrekten `SektionId`/`KulturstufeId`-Werten.
13. Bestandsübersicht (Lager) öffnen: Spalten Feld/Sektion/Kulturstufe zeigen die richtigen Werte;
    Filter nach Feld und nach Kulturstufe schränken die Liste sichtbar ein.
14. Reporting → Kulturbestand/Ausfallquote/Flächenbelegung: alle drei Tabs laden ohne Fehler und
    CSV-Export erzeugt eine lesbare Datei mit den erwarteten Spalten.

**Automatisiert bereits verifiziert** (nicht Teil dieses manuellen Ablaufs, s. `STATUS.md`):
Backend-Build (Domain/Application/Infrastructure/Tools.Migrator) grün, alle drei Testprojekte
einzeln grün (Domain 72/72, Application 66/66, IntegrationTests 78/78 — ECHT gegen
containerisierten SQL Server), Migration `GaertnereiKultur` real gegen eine Datenbank mit
vor-Phase-8-Bestandsdaten angewendet und per `sqlcmd` auf Datenintegrität geprüft (unveränderte
Zeilen, `NULL`-Dimensionen, Unique-Index ohne Filter).
