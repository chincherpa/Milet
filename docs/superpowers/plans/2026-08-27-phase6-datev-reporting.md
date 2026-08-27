# Phase 6: DATEV + Reporting — Implementierungsplan

Ziel laut `PLAN.md`: FibuKonten-UI, EXTF-CSV-Export (DATEV-Buchungsstapel, Golden-File-Tests),
Auswertungen (Umsatz je Kunde/Artikel/Monat, Artikelbewegungen, Top-Artikel, offene Aufträge) + CSV-Export.

**Umgebung:** wie Phase 5 — diese Session läuft headless auf Linux ohne Windows-Toolchain.
`dotnet-sdk-10.0` wird per `apt` installiert (liefert 10.0.111 statt der in `global.json` geforderten
10.0.400); Builds/Tests laufen aus einer **Scratch-Kopie** des Repos mit lokal abgesenktem
`global.json` — das echte, committete `global.json` bleibt unverändert. `Milet.App` (WinUI) kann in
dieser Session nicht gebaut werden; alle WinUI-Änderungen folgen strikt bestehenden Mustern
(Kleinstamm-Pivot-Tab, `BelegEditViewModelBase`-Speichern-Pattern, `FileSavePicker` wie beim
PDF-Export) und müssen vor Abnahme auf Windows nachgebaut/getestet werden — wie in Phase 5 in
`STATUS.md` dokumentiert.

**Scope-Einschränkungen (bewusst, dokumentiert statt verschwiegen):**
- Gutschrift existiert im Code bisher gar nicht (kein `BelegTyp.Gutschrift`, in keiner der Phasen 1–5
  implementiert, obwohl in `PLAN.md`s Datenmodell erwähnt) — DATEV-Export umfasst daher nur die
  tatsächlich vorhandenen gebuchten Typen Rechnung/Eingangsrechnung + Zahlung. Kein Scope-Erweiterung
  in Phase 6, nur Dokumentation der bestehenden Lücke.
- Der EXTF-Buchungsstapel-Export bildet die zentralen, buchhalterisch relevanten Spalten sauber ab
  (Umsatz, Soll/Haben, Konto, Gegenkonto, BU-Schlüssel, Belegdatum, Belegfeld1, Buchungstext u. a.),
  nicht alle ~125 offiziellen DATEV-Spalten. Golden-File-Test sichert Regressionsfreiheit des eigenen
  Outputs ab, ersetzt aber nicht die im Plan explizit geforderte externe Prüfung „Import beim
  Steuerberater validiert" — das bleibt ein offener Punkt für den Nutzer (analog Graph-Mail in Phase 5).

## Tasks

1. **Domain:** `Kontenrahmen`-Enum (SKR03/SKR04), `FibuKonfiguration`-Entity (Singleton wie
   `Firmenstamm`: Kontenrahmen, BeraterNr, MandantNr, WirtschaftsjahrBeginnMonat, SachkontenLaenge,
   BankkontoNr). `MwStSatz` um `ErloeskontoNr`/`AufwandskontoNr` (int?) erweitert. `Beleg` und
   `Zahlung` um `ExportiertAm` (DateTime?) erweitert (Doppelexport-Marker).
2. **Domain:** `DatevBuchungszeile` (reines Datenobjekt: Umsatz, SollHaben, Konto, Gegenkonto,
   BuSchluessel, Belegdatum, Belegfeld1, Buchungstext) + `DatevExtfWriter` (reine Formatierungslogik:
   Header + Spaltenzeile + Datenzeilen, CRLF, Komma-Dezimal, keine DB/IO-Abhängigkeit) im
   `Milet.Domain.Services`-Namespace neben `SteuerRechner`/`PreisfindungService`. Domain-Tests inkl.
   Golden-File-Test (fester erwarteter String für einen festen Satz `DatevBuchungszeile`n).
3. **Domain-Tests:** `DatevExtfWriterTests` (Golden-File/Snapshot-artig), Edge Cases (leere Liste,
   Negativbeträge/Gutschrift-Fall für später, Sonderzeichen im Buchungstext → Escaping/Kürzung).
4. **Application:** `IFibuKonfigurationService` (Laden/Speichern, Singleton-Pattern wie
   `IFirmenstammService`) + `FibuKonfigurationDto` + Validator. `MwStSatzDto` um die zwei neuen
   Konto-Felder erweitert (+ Validator-Erweiterung: Kontonummer wenn gesetzt vierstellig+ plausibel).
5. **Application:** `IDatevExportService` (`VorschauAsync(von, bis)` liefert Anzahl+Summen je Typ ohne
   zu markieren, `ExportierenAsync(von, bis)` liefert CSV-Bytes + markiert `ExportiertAm`) +
   `DatevExportVorschauDto`/`DatevExportZeileDto`. `IReportingService` mit
   `UmsatzJeKundeAsync`/`UmsatzJeArtikelAsync`/`UmsatzJeMonatAsync`/`ArtikelbewegungenAsync`/
   `TopArtikelAsync`/`OffeneAuftraegeAsync` (je Zeitraum-Parameter wo sinnvoll) + zugehörige DTOs.
6. **Infrastructure:** EF-Configuration `FibuKonfigurationConfiguration` (Singleton-Muster
   `FirmenstammConfiguration`), `MwStSatzConfiguration`-Erweiterung, Migration `DatevReporting`
   (FibuKonfiguration-Tabelle, MwStSaetze/Belege/Zahlungen neue Spalten). Modellkonsistenz-Check via
   zweitem `dotnet ef migrations add` (leere Diff-Migration = kein Drift), wie in Phase 5 üblich.
7. **Infrastructure:** Seed-Erweiterung in `StammdatenSeed` — `FibuKonfiguration`-Singleton-Default
   (SKR03) nach vorhandenem Firmenstamm-Muster; SKR03-Default-Konten je `SteuerSchluessel` für
   `ErloeskontoNr`/`AufwandskontoNr` **nur wo noch NULL** (Update-in-place, kein Insert — analog
   dem dokumentierten "je fehlendem Wert ergänzen"-Muster, niemals bereits gesetzte Werte
   überschreiben).
8. **Infrastructure:** `FibuKonfigurationService` (Kleinstamm-Singleton-Muster wie
   `FirmenstammService`).
9. **Infrastructure:** `DatevExportService` — Query gebuchter Rechnungen/Eingangsrechnungen (Status
   Gebucht, BelegDatum im Zeitraum, `ExportiertAm == null` außer bei erzwungenem Re-Export) je
   `BelegSteuerSumme`-Gruppe eine Buchungszeile (Konto=Debitor/Kreditor, Gegenkonto=Erlös/Aufwand nach
   `MwStSatz`, BU-Schlüssel=`SteuerSchluessel`), plus Zahlungen (Konto=Bankkonto aus
   `FibuKonfiguration`, Gegenkonto=Debitor/Kreditor) im Zeitraum. Baut `DatevBuchungszeile`-Liste,
   reicht sie an `DatevExtfWriter` weiter, kodiert als CP1252-Bytes. `ExportierenAsync` markiert
   `ExportiertAm` in derselben Transaktion wie der Export (Vorschau markiert nichts).
10. **Infrastructure:** `ReportingService` — LINQ-Aggregationen `AsNoTracking` über `Belege`/
    `BelegPositionen`/`Lagerbewegungen` für alle sechs Auswertungen. Generischer CSV-Export-Helper
    (`Infrastructure/Common/CsvWriter.cs`) für die Reporting-DTOs (nicht DATEV-Format — normales
    Komma/Semikolon-CSV, UTF-8, für Excel-Import gedacht).
11. **Infrastructure:** DI-Registrierungen (`IFibuKonfigurationService`, `IDatevExportService`,
    `IReportingService`) in `DependencyInjection.cs`.
12. **Application-Tests:** Validator-Tests für `FibuKonfigurationValidator`/erweiterten
    `MwStSatzValidator`.
13. **Integrationstests (Testcontainers, wie gehabt Docker-Skip erwartet):** `DatevExportServiceTests`
    (Export markiert `ExportiertAm`, zweiter Export im selben Zeitraum liefert 0 Zeilen ohne
    `Force`-Flag), `ReportingServiceTests` (Stichproben je Auswertung gegen bekannte Testdaten).
14. **App (WinUI, unverifiziert bis Windows-Build):** Siebter Pivot-Tab „FibuKonten" in
    `KleinstammPage` (Kontenrahmen/Berater/Mandant/WJ-Beginn/Sachkontenlänge/Bankkonto); bestehender
    MwSt-Tab um Erlös-/Aufwandskonto-Felder erweitert. Neue `DatevExportPage`/-`ViewModel`
    (Zeitraum-Picker, Vorschau-Liste, Export-Button → `FileSavePicker` wie beim PDF-Export, schreibt
    CSV-Bytes, zeigt Ergebnis). Neue `ReportingPage`/-`ViewModel` (Pivot mit sechs Tabs, je eine
    Liste + „CSV-Export"-Button). Reporting-Menüpunkt in `ShellPage.xaml` aktiviert (`IsEnabled` weg),
    DATEV-Menüpunkt unter Finanzen ergänzt. Alle neuen/geänderten XAML-Dateien als wohlgeformtes XML
    geprüft (wie Phase 5) — ersetzt keinen echten Compile/Codegen-Durchlauf.
15. **Verifikation & Dokumentation:** Build+Tests aller Nicht-WinUI-Projekte einzeln (Domain/
    Application/Infrastructure/Migrator/IntegrationTests) auf der Scratch-Kopie, Modellkonsistenz-
    Check der Migration, `STATUS.md`/`PLAN.md` aktualisiert (Phase 6 → Done mit denselben
    Einschränkungen wie Phase 5: WinUI-Build/Migration-auf-DB/manueller Smoke-Test ausstehend).
