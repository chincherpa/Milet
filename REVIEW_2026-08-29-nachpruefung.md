# Code Review — Nachprüfung der Review-Fixes

**Datum:** 2026-08-29
**Basis:** `d98bc10` „Code-Review 2026-08-29: Befunde behoben (26 von 30)" (PR #6, gemerged als `83b09f4`)
**Umfang:** die 37 geänderten Dateien dieses Commits (~1.240 hinzugefügte Zeilen), nicht die
gesamte Codebasis — der Vollreview liegt in `REVIEW_2026-08-29.md`.

## Vorbemerkung zur Verifikation

Auch diese Session hatte **kein .NET SDK** (`dotnet: command not found`; der Download von
`dot.net/v1/dotnet-install.sh` scheitert am Proxy mit `CONNECT tunnel failed, response 403`).
Es wurde also erneut **nichts gebaut, nichts ausgeführt, kein Test gefahren.** Damit ist die
gesamte Kette Vollreview → Fixes → diese Nachprüfung rein statisch. Alle Befunde stammen aus
dem Lesen des Codes; wo ein Befund an Laufzeitverhalten hängt (Interceptor-Reihenfolge,
EF-Change-Tracking, SQL-Server-Fehlerklassen), ist das vermerkt.

Der Warnhinweis am Ende der Commit-Message von `d98bc10` — „vor Abnahme zwingend bauen und
testen" — ist unverändert gültig und wird durch Befund 1 dringlicher.

---

## Zusammenfassung

Die Fixes treffen die Befunde des Vollreviews überwiegend präzise und begründen sich am Ort
ihrer Wirkung; das gilt besonders für den Jahreswechsel im `NumberRangeService`, die
Skonto-Gegenbuchung und die Ausweitung des Immutability-Interceptors auf Positionen und
Steuersummen. Neun Punkte sind offen, davon zwei blockierend:

1. **Befund 1 legt die Belegüberleitung vollständig still.** Die neu eingebaute Ausnahme
   „Gebucht → Erledigt ist erlaubt" kann im Produktivbetrieb nie greifen, weil der
   Audit-Interceptor vorher zwei weitere Properties am selben Beleg ändert. Jede
   Lieferschein-→-Rechnung- und Wareneingang-→-Eingangsrechnung-Überleitung wirft. Die
   Integrationstests können das nicht sehen, weil sie den Audit-Interceptor nicht registrieren.
2. **Befund 2 macht eine Inventur unrettbar.** Der neue Abbruch bei zwischenzeitlicher
   Bestandsänderung und der neue Guard gegen eine zweite offene Inventur bilden zusammen eine
   Sackgasse, aus der das Interface keinen Ausweg anbietet.

Die übrigen sieben sind Regressionen mittlerer Schwere (3, 4, 7), Robustheitsmängel (5, 6, 9)
und eine stehengebliebene Dokumentation (8).

---

## Befund 1 — Die Überleitung eines gebuchten Belegs wirft immer (Blocker)

`src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs:68`
`src/Milet.Infrastructure/DependencyInjection.cs:37-39`

Der Interceptor erlaubt am gebuchten Beleg genau eine Änderung — die reine Statusfortschreibung
auf `Erledigt`:

```csharp
var geaendertePropertien = entry.Properties.Where(p => p.IsModified).ToList();
var nurStatusFortschreibungAufErledigt =
    geaendertePropertien.Count == 1
    && geaendertePropertien[0].Metadata.Name == nameof(Beleg.Status)
    && entry.Entity.Status == BelegStatus.Erledigt;
```

`Count == 1` ist im Produktivbetrieb nie erfüllt. Die Interceptoren werden in
Registrierungsreihenfolge aufgerufen, und registriert ist zuerst der Audit-Interceptor:

```csharp
.AddInterceptors(
    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
    sp.GetRequiredService<BelegImmutabilityInterceptor>()));
```

`Beleg` erbt von `AuditableEntity` (`Beleg.cs:6`), und `AuditSaveChangesInterceptor.Anwenden`
setzt auf jedem `Modified`-Eintrag `GeaendertAm = DateTime.UtcNow` und `GeaendertVonId`
(`AuditSaveChangesInterceptor.cs:95-99`). Der Zugriff auf `ChangeTracker.Entries<Beleg>()` im
Immutability-Interceptor löst anschließend `DetectChanges` aus (`AutoDetectChangesEnabled` ist
nirgends abgeschaltet — geprüft), sodass dort **drei** modifizierte Properties stehen: `Status`,
`GeaendertAm`, `GeaendertVonId`. `nurStatusFortschreibungAufErledigt` ist damit immer `false`,
und es fliegt „Beleg '…' ist bereits gebucht, er kann nicht mehr geändert werden (GoBD)."

Betroffen sind alle drei Stellen, die einen gebuchten Quellbeleg fortschreiben —
`BelegUeberleitungService.cs:146`, `:272` und `:389` —, also konkret:

- Lieferschein (gebucht) → Rechnung
- Wareneingang (gebucht) → Eingangsrechnung
- Sammelrechnung über mehrere gebuchte Lieferscheine

Damit ist der gesamte Weg von der Lieferung zur Rechnung unterbrochen. Das ist genau der Pfad,
den Befund 20 des Vollreviews („Quellbeleg bleibt auf `Erledigt`") reparieren sollte.

**Warum die Tests das nicht zeigen:** `BelegUeberleitungServiceTests.cs:29`,
`LieferscheinBuchenServiceTests.cs:32`, `WareneingangBuchenServiceTests.cs:32`,
`RechnungBuchenServiceTests.cs:29` und `EingangsrechnungBuchenServiceTests.cs:34` bauen ihren
Context mit `.AddInterceptors(new BelegImmutabilityInterceptor())` — **ohne** den
Audit-Interceptor. In dieser Konstellation ist tatsächlich nur `Status` modifiziert und die
Ausnahme greift. Nur `AdminServiceTests.cs:39` registriert den Audit-Interceptor, und dort wird
kein Beleg übergeleitet. Der Kommentar dort („AddInterceptors hier wie in
`DependencyInjection.AddInfrastructure`") zeigt, dass die Abweichung bekannt, aber nicht auf die
Belegtests übertragen wurde.

**Vorschlag:** Die Statusfortschreibung nicht über die Anzahl geänderter Properties erkennen,
sondern über eine Positivliste der dabei zulässigen Properties:

```csharp
static readonly string[] BeiFortschreibungErlaubt =
    [nameof(Beleg.Status), nameof(AuditableEntity.GeaendertAm), nameof(AuditableEntity.GeaendertVonId)];

var nurStatusFortschreibungAufErledigt =
    entry.Entity.Status == BelegStatus.Erledigt
    && geaendertePropertien.All(p => BeiFortschreibungErlaubt.Contains(p.Metadata.Name))
    && geaendertePropertien.Any(p => p.Metadata.Name == nameof(Beleg.Status));
```

Das hält die Absicht („keine inhaltliche Änderung am Status-Flip vorbeischmuggeln") aufrecht,
weil jede fachliche Property weiterhin sperrt. Unabhängig davon sollten die Belegtests **beide**
Interceptoren registrieren — sonst prüfen sie eine Konfiguration, die es im Produktivbetrieb
nicht gibt.

---

## Befund 2 — Eine Inventur mit zwischenzeitlicher Bestandsänderung ist nicht mehr abschließbar (Blocker)

`src/Milet.Infrastructure/Services/InventurService.cs:114` und `:42`

Der neue Abbruch beim Abschluss ist fachlich richtig — additive Delta-Buchung auf einen bereits
veränderten Bestand würde den Abgang doppelt zählen:

```csharp
if (veraendert.Count > 0)
    throw new InvalidOperationException(
        $"Der Bestand hat sich seit Beginn der Inventur bei {veraendert.Count} Artikel(n) verändert … "
        + "Die Inventur muss neu aufgenommen werden.");
```

Die verlangte Neuaufnahme ist aber nicht möglich. Die Inventur bleibt `Offen`, und der ebenfalls
neue Guard in `NeueInventurAsync` verweigert deshalb jede zweite:

```csharp
if (await db.Inventuren.AnyAsync(i => i.LagerortId == lagerortId && i.Status == InventurStatus.Offen, ct))
    throw new InvalidOperationException(
        $"Für Lagerort '{lagerort.Code}' läuft bereits eine Inventur — sie muss erst abgeschlossen werden.");
```

`IInventurService` (`ILagerServices.cs:27-39`) kennt nur `SucheAsync`, `LadeAsync`,
`NeueInventurAsync`, `ErfasseIstMengeAsync` und `AbschliessenAsync` — kein Abbrechen, kein
Verwerfen, kein Neu-Einfrieren der Sollmengen. Der Lagerort ist für Inventuren dauerhaft
gesperrt; herauszukommen ist nur per direktem SQL auf der Datenbank.

Das ist kein Randfall: ausgelöst wird er von jedem gebuchten Lieferschein auf einen der
gezählten Artikel während der Zählung, also vom Normalbetrieb.

**Vorschlag:** eine der beiden Auswege ergänzen, bevor der Guard scharf bleibt —

- `AbbrechenAsync(int inventurId)` (Status → `Abgebrochen`, keine Bestandsbuchung), oder
- `SollmengenNeuEinfrierenAsync(int inventurId)`, das die `SollMenge` je Position auf den
  aktuellen `ArtikelBestand` zurücksetzt und die erfassten Ist-Mengen verwirft.

Die zweite Variante entspricht dem, was der Fehlertext dem Benutzer ohnehin ankündigt.

---

## Befund 3 — Überleitung verlangt Quell- **und** Zielrecht und sperrt damit reguläre Rollen aus

`src/Milet.Infrastructure/Services/BelegUeberleitungService.cs:31-35`

```csharp
private void PruefeUeberleitungsRecht(BelegTyp quellTyp, BelegTyp zielTyp)
{
    berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(quellTyp));
    berechtigung.PruefeRecht(RechtCodes.FuerBelegTyp(zielTyp));
}
```

Mit `RechtCodes.FuerBelegTyp` (`RechtCodes.cs:23-28`), das `Lieferschein → Lager` und alle
übrigen Verkaufsbelege → `Verkauf` abbildet, folgt daraus:

| Überleitung | verlangt |
| --- | --- |
| Auftrag → Lieferschein | `Verkauf` **und** `Lager` |
| Lieferschein → Rechnung | `Lager` **und** `Verkauf` |
| Bestellung → Wareneingang | `Einkauf` (beide Seiten Einkauf) |

Ein Lagermitarbeiter mit ausschließlich `Lager` kann keinen Lieferschein zu einem Auftrag mehr
anlegen; ein Vertriebsmitarbeiter mit ausschließlich `Verkauf` kann einen Lieferschein nicht mehr
fakturieren. Genau diese Arbeitsteilung ist der Zweck getrennter Rechte — in der jetzigen Form
braucht jeder, der irgendeine Verkaufsüberleitung ausführt, beide Rechte, womit die Trennung
zwischen `Verkauf` und `Lager` praktisch wirkungslos wird.

Der Kommentar begründet die UND-Prüfung damit, dass sonst „aus einem Einkaufsbeleg mit reinem
Verkaufsrecht ein Folgebeleg entstehen" könnte. Dieses Risiko trägt aber bereits die Tabelle
`ErlaubteUebergaenge` (`:17-24`): Einkaufs- und Verkaufskette sind dort disjunkt, ein Übergang
zwischen ihnen existiert gar nicht.

**Vorschlag:** das Zielrecht genügt (es ist das Recht für den Beleg, der neu entsteht), oder
alternativ eine ODER-Verknüpfung. Falls die UND-Prüfung bewusst bleiben soll, gehört das in
`STATUS.md` als Rollenanforderung dokumentiert, damit Rollen beim Einrichten passend geschnitten
werden.

---

## Befund 4 — Das Doppelexport-Fenster im DATEV-Export ist jetzt benutzerlang

`src/Milet.Infrastructure/Services/DatevExportService.cs:46`
`src/Milet.App/ViewModels/Finanzen/DatevExportViewModel.cs:55-79`

Der Fix zu Befund 6 des Vollreviews ist in der Sache richtig: vorher wurden Belege als
exportiert markiert, bevor die Datei geschrieben war, sodass ein Abbruch beim Speichern die
Buchungen dauerhaft verschwinden ließ. Jetzt trennt `ExportierenAsync` (Datei bauen) von
`MarkiereAlsExportiertAsync` (Markierung setzen).

Der Preis ist ein deutlich größeres Zeitfenster. Im ViewModel liegt zwischen beiden Aufrufen der
modale Datei-Dialog:

```csharp
var ergebnis = await _datevExportService.ExportierenAsync(Von, Bis);   // :55
…
var datei = await picker.PickSaveFileAsync();                          // :68  — unbegrenzt lang
…
await _datevExportService.MarkiereAlsExportiertAsync(ergebnis.BelegIds, ergebnis.ZahlungIds);  // :79
```

Solange der Dialog offen steht, sieht ein zweiter Exportlauf denselben Zeitraum als unexportiert
und liefert dieselben Buchungszeilen erneut. Vorher schloss die gemeinsame Transaktion das aus —
der zweite Lauf bekam 0 Zeilen. Doppelt importierte DATEV-Buchungen sind teurer zu bereinigen als
ein verlorener Export, weil sie in der Buchhaltung landen.

**Vorschlag:** die Rückgabe von `ExportierenAsync` an eine kurzlebige Reservierung binden — etwa
eine `ExportLauf`-Zeile mit den enthaltenen Ids, die `MarkiereAlsExportiertAsync` auflöst und die
ein späterer Lauf als „in Arbeit" erkennt. Minimal genügt ein Hinweis in der UI, dass ein Export
läuft, plus Deaktivieren des Buttons; das deckt den Einzelplatzfall, nicht den Mehrbenutzerfall.

---

## Befund 5 — `catch (DbException)` verschluckt jeden Datenbankfehler

`src/Milet.Infrastructure/Services/NumberRangeService.cs:108`

```csharp
catch (DbException)
{
    // Paralleler Aufrufer war schneller — s. Kommentar oben. …
}
```

Gefangen werden soll die Verletzung des Unique-Index `(Code, Jahr)`, wenn zwei Aufrufer
gleichzeitig den Jahreskreis anlegen. Gefangen wird aber jeder Datenbankfehler: fehlende
INSERT-Rechte, Verbindungsabbruch, Timeout, Deadlock-Opfer. In all diesen Fällen läuft der Code
weiter, der folgende Vergabeversuch findet den Kreis nicht und meldet dem Benutzer
„Nummernkreis 'RE' existiert nicht" — eine Diagnose, die in die Irre führt.

Der Kommentar begründet außerdem, das sei „unbedenklich auch innerhalb einer laufenden
Transaktion des Aufrufers: eine Verletzung des Unique-Index rollt in SQL Server nur die Anweisung
zurück, nicht die Transaktion." Das stimmt für die Index-Verletzung, aber nicht für alle
`DbException`: ein Deadlock-Opfer (Fehler 1205) rollt die gesamte Transaktion zurück. Da diese
Methode laut Befund 5 des Vollreviews jetzt bewusst auf der Transaktion des Buchungsvorgangs
läuft, wird dem Aufrufer dann eine tote Transaktion untergeschoben, in der er weiterarbeitet.

**Vorschlag:** auf die Index-Verletzung einengen und alles andere durchlassen:

```csharp
catch (DbException ex) when (ex is SqlException { Number: 2601 or 2627 })
```

Soll der Provider-Typ nicht in die Infrastruktur durchschlagen (die im Kommentar genannte
Begründung), ist die Alternative, nach dem Fehlschlag gezielt nachzulesen, ob der Kreis nun
existiert, und nur dann fortzufahren.

---

## Befund 6 — Sync-over-Async im Immutability-Interceptor

`src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs:132-136`

`SavingChangesAsync` ruft `Pruefen`, und `PruefeUntergeordnet` setzt im Ausnahmepfad eine
synchrone Datenbankabfrage ab:

```csharp
var ausDb = context.Set<Beleg>().AsNoTracking()
    .Where(b => ids.Contains(b.Id))
    .Select(b => new { b.Id, b.Status, b.BelegNummer })
    .ToList();
```

Das ist dasselbe Muster, das derselbe Commit im `AuditSaveChangesInterceptor` ausdrücklich
entfernt hat (Befund 23; der Kommentar dort: „Sync-over-Async kann in einem UI-Kontext
blockieren"). Der `CancellationToken` aus `SavingChangesAsync` wird ebenfalls nicht
durchgereicht.

Der Pfad ist selten — er greift nur, wenn Positionen geändert werden, ohne dass der Beleg
mitgeladen ist —, aber er liegt auf dem Speicherweg der UI.

**Vorschlag:** `Pruefen` in eine `async`-Variante spalten, die aus `SavingChangesAsync` mit
`await … ToListAsync(cancellationToken)` aufgerufen wird, und den synchronen Pfad für
`SavingChanges` belassen.

---

## Befund 7 — Textpositionen umgehen die „nichts mehr offen"-Prüfung

`src/Milet.Infrastructure/Services/BelegUeberleitungService.cs:105-132`

Nur Artikelpositionen werden gegen die offene Menge geprüft; alle übrigen (`PositionsTyp` ≠
`Artikel`, also Text-/Zwischensummenzeilen) werden bedingungslos übernommen:

```csharp
var menge = quellPosition.PositionsTyp == PositionsTyp.Artikel
    ? BelegPosition.OffeneMenge(quellPosition, folgepositionen)
    : quellPosition.Menge;

if (quellPosition.PositionsTyp == PositionsTyp.Artikel && menge <= 0)
    continue;
```

Der Schutz gegen die leere Überleitung hängt anschließend allein an der Positionszahl:

```csharp
if (zielBeleg.Positionen.Count == 0)
    throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");
```

Enthält der Quellbeleg auch nur eine Textzeile, ist `Count` nach vollständiger Übernahme aller
Artikelmengen `1` statt `0` — der Guard greift nicht. Es entsteht ein Folgebeleg, der nur die
Textzeile enthält, mit Summen von 0,00 und einer verbrauchten Belegnummer. Bei einer Rechnung
ist das eine Nullrechnung; die Nummer ist dann nicht mehr zurückholbar, weil sie erst beim
Buchen gezogen wird, aber die Angebots-/Auftrags-/Lieferscheinnummern sind es sehr wohl.

Ein Statusfilter fängt das nicht ab: `UeberleitenAsync` prüft den Quellstatus nur für
Lieferschein und Wareneingang (`:77`); ein bereits auf `Erledigt` stehender Auftrag ist erneut
überleitbar.

**Vorschlag:** die Prüfung auf die übernommenen Artikelpositionen beziehen, nicht auf alle:

```csharp
if (!zielBeleg.Positionen.Any(p => p.PositionsTyp == PositionsTyp.Artikel))
    throw new InvalidOperationException("Keine offenen Positionen zum Überleiten vorhanden.");
```

Betroffen sind zwei der drei Überleitungsmethoden:

- `UeberleitenAsync` (Guard `:132`) — immer, sobald eine Nicht-Artikelposition existiert.
- `UeberleitenMehrereAsync` (Guard `:393`) — nur bei genau einem Quellbeleg, denn erst dann ist
  `textPositionenUebernehmen` (`:351`) wahr und Textpositionen werden kopiert.

`UeberleitenMitAuswahlAsync` ist **nicht** betroffen: dort werden Nicht-Artikelpositionen
gleich zu Beginn der Schleife übersprungen (`:226`), sodass der Guard `:263` nur Artikelpositionen
zählt. Diese Zeile ist zugleich die Vorlage für den Fix.

---

## Befund 8 — `Erledigt` ist im Immutability-Interceptor gar nicht gesperrt

`src/Milet.Infrastructure/Persistence/Interceptors/BelegImmutabilityInterceptor.cs:50-79`

`PruefeBelege` behandelt `Deleted` (gesperrt außer `Entwurf`), `Storniert` (gesperrt) und
`Gebucht` (gesperrt außer Statusfortschreibung). Ein Beleg, dessen **ursprünglicher** Status
`Erledigt` ist, fällt durch alle Zweige und ist damit am Kopf frei änderbar.

Relevant wird das erst durch diesen Commit: vorher erreichte praktisch kein Beleg `Erledigt`
(Befund 11 des Vollreviews), jetzt landet dort jeder vollständig übergeleitete — insbesondere
jeder gebuchte, fakturierte Lieferschein. Dessen Kopfdaten (Belegdatum, Adress-Snapshots,
Kopf-/Fußtext, Summenfelder) sind GoBD-relevant und stehen nun wieder offen. Die Positionen sind
abgedeckt, weil `PruefeUntergeordnet` gegen `is not BelegStatus.Entwurf` prüft (`:118`) — die
Lücke betrifft nur den Kopf.

Praktisch ausgenutzt wird sie über die regulären Pfade nicht: `BelegService` sperrt Speichern und
Löschen bereits bei `!= Entwurf` (`:152`, `:238`). Der Klassenkommentar beschreibt den Interceptor
aber ausdrücklich als „die harte Sperre für jeden Codepfad, der ihn umgeht" — dieser Anspruch
ist für `Erledigt` derzeit nicht eingelöst.

**Vorschlag:** `Erledigt` wie `Gebucht` behandeln (gesperrt, ohne die Fortschreibungsausnahme).
Zu beachten ist dabei `BelegService.SetzeQuellbelegeZurueckAsync` (`:296-299`), das beim Löschen
eines Folgebelegs bewusst `Erledigt → Gebucht`/`Entwurf` zurücksetzt — dieser Übergang muss dann
ebenfalls als erlaubte Fortschreibung modelliert werden, sonst entsteht Befund 1 ein zweites Mal
an anderer Stelle.

---

## Befund 9 — Doppelter, inhaltlich falscher `<summary>`-Block

`src/Milet.Application/Finanzen/Dtos.cs:69-76`

```csharp
/// <summary>Ergebnis eines tatsächlichen Exports — die fertige CSV (CP1252-kodiert) plus Vorschlags-
/// dateiname. Markiert die exportierten Belege/Zahlungen mit <c>ExportiertAm</c>.</summary>
/// <summary>Ergebnis eines Exportlaufs. <see cref="BelegIds"/>/<see cref="ZahlungIds"/> sind die Vorgänge,
/// die in der Datei stehen — der Aufrufer meldet sie über <c>IDatevExportService.MarkiereAlsExportiertAsync</c>
/// zurück, sobald die Datei tatsächlich geschrieben ist.</summary>
public sealed record DatevExportErgebnisDto(…);
```

Der alte Block wurde beim Fix zu Befund 6 nicht entfernt, sondern der neue davorgesetzt. Der
erste Satz behauptet weiterhin das Gegenteil des jetzigen Vertrags — dass `ExportierenAsync`
selbst markiert. Genau diese Fehlannahme führt beim nächsten Aufrufer dazu,
`MarkiereAlsExportiertAsync` wegzulassen und den Exportmarker nie zu setzen.

Ein Build-Fehler ist es nicht: `GenerateDocumentationFile` ist in `Directory.Build.props` nicht
gesetzt, das solution-weite `TreatWarningsAsErrors` greift hier also nicht.

**Vorschlag:** den ersten Block ersatzlos streichen.

---

## Anmerkung zur Testkonfiguration

Unabhängig von Befund 1 lohnt der Blick auf das Muster: fünf der sechs Beleg-Integrationstests
konfigurieren ihren `DbContext` mit einer anderen Interceptor-Kette als
`DependencyInjection.AddInfrastructure`. Solange das so bleibt, kann keiner dieser Tests eine
Wechselwirkung zwischen den beiden Interceptoren finden — und Befund 1 ist genau so eine. Eine
gemeinsame Test-Hilfsmethode, die dieselbe Kette wie die Produktivregistrierung aufbaut, würde
diese Klasse von Fehlern künftig abdecken.

Für Befund 1 selbst genügt bereits ein Test ohne Docker: ein `DbContext` mit beiden
Interceptoren, ein `Beleg` im Status `Gebucht`, `Status = Erledigt`, `SaveChanges` — er muss
durchlaufen.
