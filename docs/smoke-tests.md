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

## Darstellung — Hell/Dunkel-Umschaltung

**Noch nicht durchgeführt** (Umsetzungssession lief headless auf Linux, in deren Container war nicht
einmal ein .NET SDK vorhanden — es wurde also weder gebaut noch getestet, s. `STATUS.md`). Diese Änderung
betrifft ausschließlich WinUI-Code; die drei Testprojekte sind inhaltlich nicht berührt und müssen
lediglich unverändert grün bleiben.

1. `dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64` auf einer Windows-Maschine → muss
   0 Fehler liefern, bevor die folgenden Schritte sinnvoll sind. Besonderes Augenmerk: die beiden neuen
   ResourceDictionaries unter `src/Milet.App/Themes/` müssen vom XAML-Compiler eingesammelt werden. Die
   csproj enthält keinerlei `<Page>`-Items und verlässt sich auf das Standard-Globbing der
   WindowsAppSDK-Targets (so wie bei allen 43 bestehenden XAML-Dateien). Schlägt der Build mit „kann
   ms-appx:///Themes/Farben.xaml nicht auflösen" fehl, in `Milet.App.csproj` ergänzen:
   `<Page Include="Themes\*.xaml"><Generator>MSBuild:Compile</Generator></Page>`.
2. App starten → beim allerersten Start existiert noch keine gespeicherte Einstellung, das Anmeldefenster
   erscheint im Windows-Systemtheme.
3. Anmelden, unten in der Navigationsleiste auf das Darstellungssymbol → **Hell** wählen. Navigationsleiste
   und Inhaltsseite müssen **sofort** umschalten, ohne Neustart.
4. App schließen und neu starten → schon das **Anmeldefenster** kommt hell, und zwar ohne sichtbares
   Flackern (das Theme wird vor `Activate()` gesetzt). Die Datei
   `%LOCALAPPDATA%\Milet\ui-einstellungen.json` existiert und enthält `"Theme": "Light"`.
5. Windows-Systemtheme auf Dunkel stellen, während Milet auf „Hell" steht → Milet bleibt hell. Dann auf
   **Systemvorgabe** schalten → Milet wird dunkel.
6. Titelleiste in allen drei Einstellungen prüfen: Farbe passt zur App. Gilt nur unter Windows 11 —
   `AppWindowTitleBar.IsCustomizationSupported()` liefert unter Windows 10 `false`, dort bleibt es beim
   Systemverhalten (bekannte Einschränkung, kein Fehler).
7. Fehlerdialog auslösen (z. B. Kunde mit leerem Pflichtfeld speichern) → der `ContentDialog` erscheint in
   der gewählten Darstellung, nicht im Systemtheme.
8. **Offener Punkt:** eine ComboBox öffnen (z. B. Stufenfilter in der Pflanzenübersicht) und einen ToolTip
   anzeigen lassen, jeweils nach einem Umschalten zur Laufzeit. Popups hängen im Popup-Baum des `XamlRoot`
   und erben das auf `Window.Content` gesetzte `RequestedTheme` nicht zwangsläufig. Ziehen sie nicht mit,
   ist der Ausweg, beim Start zusätzlich `Application.Current.RequestedTheme` aus dem gespeicherten Wert zu
   setzen — dann stimmen Popups ab Programmstart, ein Laufzeitwechsel bleibt für sie bis zum Neustart
   inkonsistent.
9. Gärtnerei → Grundriss in **beiden** Darstellungen: Raster sichtbar, Feld- und Sektionsflächen
   unterscheidbar, und der Größen-Anfasser des ausgewählten Elements in beiden sichtbar (er war fest weiß
   mit schwarzem Rand, im Dunkelmodus also invertiert falsch). Element auswählen, verschieben, Größe
   ändern → Farben bleiben korrekt.
10. Bei geöffnetem Grundriss die Darstellung umschalten → der Canvas zeichnet sich neu, es bleiben keine
    Elemente in den alten Farben stehen. Danach ein Element per Maus verschieben und eines über die
    numerischen Felder → beide Wege müssen die Zeichnung weiterhin genau einmal nachführen (die
    PropertyChanged-Handler werden beim Neuzeichnen jetzt abgemeldet).
11. Gärtnerei → Pflanzenübersicht: die Mengen-Chips in Kulturstufenfarbe sind in beiden Darstellungen
    lesbar. Eine Kulturstufe testweise auf eine sehr helle Farbe stellen (`#FFFF00`) → die Zahl muss
    schwarz werden; auf eine dunkle (`#1B5E20`) → weiß.
12. Eine Kulturstufe testweise auf einen ungültigen Wert setzen, falls die Maske das zulässt → die
    Pflanzenübersicht darf beim Zeichnen nicht mehr abstürzen (bisher `FormatException` beim
    Inline-Parsen), sondern zeigt die neutrale Ersatzfarbe.
13. Verkauf → Auftragsliste und Auftragseditor mit Kulturpflanzenposition: die Verfügbarkeits-Ampel zeigt
    in beiden Darstellungen genau **einen** Punkt in der richtigen Farbe (grün/gelb/rot), gut erkennbar
    gegen den Kartenhintergrund.
14. Administration und Stammdaten → Einstellungen öffnen: die Listen sehen unverändert aus. Das ist die
    Regressionsprüfung dafür, dass der aus beiden Seiten herausgezogene `MasterListStyle` jetzt aus
    `Themes/Styles.xaml` greift.
15. `%LOCALAPPDATA%\Milet\ui-einstellungen.json` von Hand mit unsinnigem Inhalt füllen und App starten →
    App startet mit Systemvorgabe, stürzt nicht ab, und in `logs/milet-*.log` steht eine Warnung.
