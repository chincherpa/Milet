# Phase 5 „Finanzen+E-Mail" Implementation Plan

> **Hinweis für die Umsetzung:** Task-für-Task abgearbeitet wie Phase 2–4. Jeder Task wird einzeln gebaut/getestet/committet. Diese Session läuft in einer Linux-Cloud-Umgebung ohne Windows/WinUI-Toolchain — siehe „Verifikations-Realität dieser Session" unten für den genauen Umfang dessen, was hier tatsächlich build-/testverifiziert werden kann.

**Goal:** OP-Liste mit Aging, Zahlungserfassung mit Skonto-Vorschlag (Teilzahlung → Status `TeilweiseBezahlt`/`Ausgeglichen`), Mahnwesen (konfigurierbare Mahnstufen, Mahnlauf mit Kandidaten-Selektion, Mahnung-PDF), E-Mail-Versand von Beleg-/Mahnung-PDFs über Microsoft Graph (MSAL/WAM) mit Versand-Log — deckungsgleich mit PLAN.md-Zeile „5 Finanzen+E-Mail".

**Architektur:** `OffenerPosten` (Phase 2/4) bekommt ein `Status`-Feld (`Offen`/`TeilweiseBezahlt`/`Ausgeglichen`) statt einer Ableitung nur aus `OffenerBetrag` — explizit und in der OP-Liste direkt filterbar. Zahlungen sind ein neues, eigenständiges Aggregat `Zahlung` (Kopf) + `ZahlungZuordnung` (Zeilen, eine Zahlung kann mehrere OPs ausgleichen — genau wie in PLAN.md vorgesehen), **kein** Beleg-TPH-Subtyp (Zahlungen sind kein Dokumenttyp im Beleg-Sinn, brauchen keine GoBD-Nummernkreis-Lückenlosigkeit, keine Immutability-Sperre). Mahnwesen: `Mahnstufe` (Config-Tabelle, Kleinstamm-Muster: Stufe/Karenztage/Gebühr) + `Mahnung`/`MahnungPosition` (Ergebnis eines Mahnlaufs, ebenfalls kein Beleg-Subtyp — PLAN.md sagt das explizit: „Mahnung (kein Beleg-Subtyp)"). Die Selektionslogik (welcher OP ist für welche Mahnstufe fällig) und die Skonto-Berechnung sind **reine Domain-Services** (`MahnSelektionService`, `SkontoRechner`) nach dem Muster von `SteuerRechner`/`PreisfindungService` — unit-testbar ohne DB, das ist der Kern der fachlichen Logik dieser Phase. E-Mail-Versand läuft hinter der bereits in PLAN.md vorgesehenen `IEmailService`-Abstraktion (existiert noch nicht, wird hier erstmals angelegt); zwei Implementierungen: `GraphEmailService` (MSAL/WAM-Broker, echter Versand, nur unter Windows mit registrierter Entra-App nutzbar) und `NichtKonfigurierterEmailService` (Fallback, wenn `Graph`-Konfiguration fehlt — wirft eine sprechende `EmailNichtKonfiguriertException` statt stillschweigend zu scheitern oder den Rest der App zu blockieren, Risiko #4 aus PLAN.md). Jeder Versandversuch (Erfolg wie Fehler) wird in `EmailVersand` protokolliert (Versand-Log je Beleg/Mahnung).

**Tech Stack:** .NET 10, EF Core 10, FluentValidation 12, CommunityToolkit.Mvvm 8.4, WinUI 3, QuestPDF, xUnit v3. **Neu:** `Microsoft.Graph` + `Microsoft.Identity.Client.Broker` (bereits in PLAN.md als vorgesehene Pakete gelistet, aber bisher nicht eingebunden — werden in Central Package Management ergänzt).

**Spec:** `PLAN.md` (Abschnitte „Datenmodell (Kern) → Finanzen", „Geschäftsprozesse" Punkt 4 „Rechnung→OP→Mahnlauf", Phasen-Tabelle Zeile „5 Finanzen+E-Mail", Risiko #4 „Graph Desktop-Auth"). Konventionen aus bestehendem Code: `RechnungBuchenService`/`EingangsrechnungBuchenService` (OP-Anlage), `BestellVorschlagService`+`BestellVorschlagPage` (Selektions-UI-Muster für den Mahnlauf), `KleinstammServices`/`KleinstammPage` (Config-CRUD-Muster für Mahnstufen), `WareneingangMengenDialog` (Dialog-Muster für die Zahlungserfassung), `PdfService`/`BelegPdfDocument` (PDF-Erweiterung für Mahnung).

## Verifikations-Realität dieser Session

Diese Session läuft **headless auf Linux** (keine Windows-VM, kein Display). `dotnet` (10.0.111, via `apt`, da `dotnet-install.sh`/`builds.dotnet.microsoft.com` durch den Netzwerk-Proxy blockiert sind — `global.json` fordert `10.0.400`, das nirgends installierbar war; Builds/Tests laufen deshalb aus einer Scratch-Kopie mit lokal auf `10.0.111` abgesenktem `global.json`, das reale, committete `global.json` bleibt unverändert bei `10.0.400`) kompiliert und testet **`Milet.Domain`, `Milet.Application`, `Milet.Infrastructure`, `Milet.Tools.Migrator`, alle drei Testprojekte** real (nicht nur „compile-verifiziert", sondern `dotnet build`+`dotnet test` laufen tatsächlich, MTP-Modus, pro Projekt einzeln wie in `CLAUDE.md` vorgeschrieben). Docker ist auch hier nicht verfügbar → Integrationstests skippen sauber wie in jeder vorherigen Phase.

**`Milet.App` (WinUI, `net10.0-windows10.0.19041.0`) kann in dieser Session nicht gebaut werden** — das Windows App SDK/WinUI-Toolchain existiert nur unter Windows, kein Linux-Workaround möglich. Alle Änderungen an `Milet.App` in diesem Plan sind also **nicht einmal compile-verifiziert** (schlechter als der bisher dokumentierte Zustand „Build grün, nur UI-Smoke-Test ausstehend" aus Phase 3/4) — sie folgen so exakt wie möglich bereits verifizierten Mustern (Converter-Nutzung, `x:Bind`, ViewModel-Struktur), aber ein Build auf einer echten Windows-Maschine ist vor Abnahme dieser Phase zwingend nötig, nicht nur der übliche manuelle UI-Smoke-Test.

**Graph-Mail ist zusätzlich funktional nicht verifizierbar ohne eine echte, vom Nutzer registrierte Entra-App** (ClientId/TenantId/Redirect, Mail.Send-Consent) — das kann keine Agentensession für den Nutzer anlegen. Der Code wird lauffähig nach MSAL/WAM-Standardmuster gebaut, inkl. sauberem Fallback (`NichtKonfigurierterEmailService`), aber „Mail mit PDF kommt an" (Testkriterium aus PLAN.md) kann erst der Nutzer selbst mit seiner eigenen Entra-App-Registrierung auf Windows verifizieren.

## Architektur-Entscheidungen

1. **`OffenerPosten.Status` statt reiner Ableitung aus `OffenerBetrag`.** Neues Enum `OffenerPostenStatus { Offen, TeilweiseBezahlt, Ausgeglichen }`. Explizit statt implizit, weil die OP-Liste (Testkriterium „Teilzahlung→TeilBezahlt") direkt danach filtert/anzeigt und weil `OffenerBetrag == 0` bei Rundungsdifferenzen (Skonto-Cent-Reste) unzuverlässig als alleiniges Signal wäre. Migration setzt den Status für alle bestehenden OPs anhand von `OffenerBetrag` vs. `Betrag` (nicht-destruktiv, reine Herleitung beim Rollout).
2. **Zahlung ist kein Beleg-Subtyp.** Anders als Angebot/Auftrag/Rechnung/Lieferschein/Bestellung/Wareneingang/Eingangsrechnung ist eine Zahlung kein Dokument mit GoBD-Nummernkreis-Pflicht und keine Handelsurkunde, die eingefroren werden muss — sie ist eine reine Buchungsoperation auf `OffenerPosten`. Eigenes Aggregat `Zahlung`+`ZahlungZuordnung`, `AuditableEntity`+`IHasRowVersion` wie alle anderen Aggregate Roots, aber **nicht** in der `Belege`-TPH-Hierarchie und **nicht** vom `BelegImmutabilityInterceptor` betroffen.
3. **Mahnung ebenfalls kein Beleg-Subtyp** (PLAN.md sagt das wörtlich). `Mahnung`+`MahnungPosition` sind das Ergebnis eines Mahnlaufs — angelehnt an Struktur, aber ohne die Beleg-Basisklasse (keine Adress-Snapshots im Kopf nötig, die zieht sich die PDF-Erzeugung direkt aus `Kunde`/`Firmenstamm`, wie es `RechnungBuchenService`-Analoga für Kopf-Digitalisierung ohnehin schon zur Buchungszeit tun).
4. **Skonto-Fenster wird ab `Beleg.BelegDatum` gerechnet, nicht ab `Faelligkeit`.** Das ist deutsche Kaufmannspraxis (Skontofrist läuft ab Rechnungsdatum) und deckt sich mit der bereits vorhandenen Snapshot-Struktur (`Beleg.ZahlungsbedingungSkontoTage`/`-SkontoProzent`, beide bereits auf `Beleg` vorhanden seit Phase 2 — keine Schemaänderung an `Beleg` nötig).
5. **`IEmailService` liegt in `Milet.Application.Abstractions`** (wie `IPdfService`/`ICurrentUserService`) — Signatur nimmt fertige Bytes (PDF) + Empfänger/Betreff/Text, kein Wissen über Beleg-/Mahnung-Interna. Zwei Infrastructure-Implementierungen, DI wählt anhand `configuration.GetSection("Graph").Exists()`: vorhanden → `GraphEmailService`, fehlt → `NichtKonfigurierterEmailService` (wirft beim Versandversuch eine sprechende Exception, blockiert aber nie Buchen/PDF/Drucken — die App funktioniert ohne Graph-Konfiguration vollständig, nur der „E-Mail senden"-Button meldet einen Fehler statt zu senden).
6. **`EmailVersand`-Log referenziert `BelegId?`/`MahnungId?` (genau eines gesetzt, wie `OffenerPosten.KundeId?/LieferantId?`)** — ein Log-Eintrag pro Versandversuch (auch bei Fehlschlag, mit `Erfolgreich=false`+`Fehlermeldung`), nie überschrieben, nie gelöscht (Audit-Charakter, kein `RowVersion` nötig — insert-only).
7. **Mahnstufen sind eine reine Config-Tabelle** (`Mahnstufe`: `Stufe int`, `Karenztage int`, `Gebuehr decimal`, `Mahntext string?`) ohne `RowVersion`/`AuditableEntity` (wie `Zahlungsbedingung`/`Versandart`) — UI-Muster identisch zu `KleinstammPage`-Tabs, neuer sechster Tab „Mahnstufen".
8. **Mahnlauf-Selektion und -Durchführung sind zwei getrennte Schritte** (wie Bestellvorschlag: Vorschlag anzeigen → Nutzer wählt/bestätigt → Erzeugen), nicht ein automatischer Batch-Job ohne Interaktion — PLAN.md verlangt „Mahnlauf-Selektion getestet" als Testkriterium, das setzt eine Selektionsliste vor der Ausführung voraus.
9. **Bewusst außerhalb dieses Plans:** automatischer/geplanter Mahnlauf (Scheduler/Windows-Task — v1 ist manuell ausgelöst wie jeder andere Workflow), Mahnung-Storno/-Widerruf, Teilzahlungs-Rückabwicklung (Zahlungen sind append-only, Korrektur = neue Gegenzahlung, analog zu „Storno = Gegenbuchung" bei Belegen), Zahlungsimport (Kontoauszug/CSV) — reine manuelle Erfassung in v1, automatischer Abgleich ist eine spätere Phase.

## Global Constraints

- Neue Aggregate Roots: `Zahlung` (`AuditableEntity`+`IHasRowVersion`). Modifizierte Aggregate Roots: `OffenerPosten` (neues `Status`-Feld, bleibt `AuditableEntity`+`IHasRowVersion`). Neue Nicht-Aggregate: `ZahlungZuordnung`, `Mahnstufe`, `Mahnung`, `MahnungPosition`, `EmailVersand`.
- Geldbeträge `decimal(18,2)`, Rundung `Math.Round(..., 2, MidpointRounding.ToEven)` — wie überall. `Mahnstufe.Karenztage`/`Stufe` sind `int`.
- Jede Service-Methode eigener kurzlebiger `IDbContextFactory<MiletDbContext>`-Context; Reads `AsNoTracking()`; Speichern nutzt `SaveChangesTranslatingConcurrencyAsync` (Zahlung/OP) bzw. `SaveChangesDeletingAsync` (Mahnstufen-Löschen) — wie Phase 1–4.
- DTOs: `sealed record` mit `init`, in `src/Milet.Application/Finanzen/Dtos.cs`; Interfaces in `IFinanzenServices.cs`; Validatoren in `Validators.cs` — wie `Verkauf`/`Lager`/`Einkauf`-Module.
- `dotnet` in dieser Session: `dotnet build`/`dotnet test --project <csproj>` aus einer Scratch-Kopie mit abgesenkter SDK-Version (s. oben) — **niemals** das reale `global.json` im Repo verändern/committen.
- Deutsche Bezeichner für alles Fachliche.

---

### Task 1: Domain — `OffenerPosten.Status`, `Zahlung`/`ZahlungZuordnung`, `SkontoRechner` + Tests
- [ ] `OffenerPostenStatus` Enum (`Offen=0, TeilweiseBezahlt=1, Ausgeglichen=2`) in `src/Milet.Domain/Entities/Finanzen/`.
- [ ] `OffenerPosten.Status` Property (Default `Offen`).
- [ ] `Zahlung` Entity (`AuditableEntity`+`IHasRowVersion`): `Id`, `KundeId?`, `LieferantId?` (+ Navigations), `Typ` (`OffenerPostenTyp`), `Zahlungsdatum` (`DateOnly`), `Gesamtbetrag`, `Zahlungsart?` (string), `Referenz?` (string), `Zuordnungen` (`List<ZahlungZuordnung>`), `RowVersion`.
- [ ] `ZahlungZuordnung` Entity: `Id`, `ZahlungId`, `OffenerPostenId` (+ Navigations), `Betrag`, `SkontoBetrag`.
- [ ] `SkontoRechner` (statischer Domain-Service, `src/Milet.Domain/Services/SkontoRechner.cs`): `BerechneSkonto(DateOnly rechnungsdatum, DateOnly zahlungsdatum, int? skontoTage, decimal? skontoProzent, decimal betrag) -> decimal` — 0 wenn `skontoTage`/`skontoProzent` null oder `zahlungsdatum > rechnungsdatum.AddDays(skontoTage)`, sonst `Round(betrag * skontoProzent / 100, 2, ToEven)`.
- [ ] Tests (`tests/Milet.Domain.Tests/SkontoRechnerTests.cs`): Skonto innerhalb Frist, exakt am letzten Tag (Kante), einen Tag zu spät, ohne Skonto-Vereinbarung, Rundungsfall.
- [ ] Build Domain + `dotnet test` Domain.Tests, commit.

### Task 2: Domain — `Mahnstufe`, `Mahnung`/`MahnungPosition`, `MahnSelektionService` + Tests
- [ ] `Mahnstufe` Entity (`src/Milet.Domain/Entities/Finanzen/Mahnstufe.cs`): `Id`, `Stufe int`, `Karenztage int`, `Gebuehr decimal`, `Mahntext string?`.
- [ ] `Mahnung` Entity (`AuditableEntity`, kein RowVersion — insert-only nach Erzeugung): `Id`, `KundeId` (+ Navigation), `MahnDatum DateOnly`, `Mahnstufe int`, `Gebuehr decimal`, `Gesamtbetrag decimal`, `Positionen List<MahnungPosition>`.
- [ ] `MahnungPosition` Entity: `Id`, `MahnungId`, `OffenerPostenId` (+ Navigations), `BelegNummerSnapshot string`, `OffenerBetragSnapshot decimal`.
- [ ] `MahnSelektionService` (statisch, `src/Milet.Domain/Services/MahnSelektionService.cs`): `ErmittleFaelligeStufe(OffenerPosten op, DateOnly heute, IReadOnlyCollection<Mahnstufe> stufen) -> int?` — `null` wenn `Mahnsperre`/`OffenerBetrag<=0`/`Status==Ausgeglichen` oder keine passende nächste Stufe (`op.Mahnstufe + 1`) mit `heute >= op.Faelligkeit.AddDays(stufe.Karenztage)` existiert; sonst die nächste fällige Stufe.
- [ ] Tests: kein Kandidat vor Karenzablauf, genau am Kantentag, Mahnsperre blockt, bereits `Ausgeglichen` blockt, übersprungene Stufe (keine Stufe-2-Config vorhanden) blockt korrekt statt falsch zu eskalieren, mehrfacher Aufruf (Stufe 1→2) mit fortgeschrittenem Datum.
- [ ] Build + Test, commit.

### Task 3: Domain — `EmailVersand` Entity
- [ ] `EmailVersand` (`src/Milet.Domain/Entities/Finanzen/EmailVersand.cs`): `Id`, `BelegId?`, `MahnungId?` (+ Navigations), `Empfaenger string`, `Betreff string`, `GesendetAm DateTime`, `Erfolgreich bool`, `Fehlermeldung string?`.
- [ ] Build Domain, commit (kleiner Task, kein eigener Test — reines Datenmodell ohne Logik, analog `OffenerPosten`-Anlage in Phase 2).

### Task 4: Application — Finanzen-DTOs, `IOffenePostenService`, `IZahlungService`, Validatoren + Tests
- [ ] `src/Milet.Application/Finanzen/Dtos.cs`: `OffenePostenDto` (inkl. `TageUeberfaellig` berechnet, `Status`, `PartnerName`), `OffenePostenFilterDto` (Typ?, NurUeberfaellige bool, Status?), `ZahlungDto`+`ZahlungZuordnungDto`, `SkontoVorschlagDto`.
- [ ] `src/Milet.Application/Finanzen/IFinanzenServices.cs`: `IOffenePostenService` (`ListeAsync(filter)`, `LadeAsync(id)`), `IZahlungService` (`SkontoVorschlagAsync(offenerPostenId, zahlungsdatum)`, `ErfasseZahlungAsync(ZahlungDto)`).
- [ ] `src/Milet.Application/Finanzen/Validators.cs`: `ZahlungValidator` (mind. eine Zuordnung, `Betrag+SkontoBetrag <= OffenerBetrag` je Zeile — Cross-Check gegen mitgelieferte OP-Daten wo möglich, Rest serverseitig in Task 10), `Zahlungsdatum` nicht in der Zukunft.
- [ ] Tests (`tests/Milet.Application.Tests/FinanzenValidatorTests.cs`).
- [ ] Build Application + Test, commit.

### Task 5: Application — `IMahnwesenService`, `MahnstufeDto`-Validator + Tests
- [ ] DTOs ergänzen: `MahnstufeDto`, `MahnKandidatDto` (OP + ermittelte nächste Stufe, gruppiert nach Kunde in `MahnlaufGruppeDto`), `MahnungDto`+`MahnungPositionDto`.
- [ ] `IMahnwesenService`: `ListeStufenAsync`, `SpeichereStufeAsync`, `LoescheStufeAsync` (Kleinstamm-CRUD-Muster), `ErmittleFaelligeAsync()`, `MahnlaufDurchfuehrenAsync(IReadOnlyList<int> offenerPostenIds)`.
- [ ] `MahnstufeValidator` (Stufe > 0 eindeutig, Karenztage >= 0, Gebühr >= 0).
- [ ] Tests, Build + Test, commit.

### Task 6: Application — `IEmailService` Abstraction + `IPdfService` um Mahnung erweitern
- [ ] `src/Milet.Application/Abstractions/IEmailService.cs`: `SendeMailMitAnhangAsync(string empfaenger, string betreff, string text, byte[] anhang, string anhangDateiname, CancellationToken ct)`; `EmailNichtKonfiguriertException` (eigene Exception-Klasse, analog `NotFoundException`/`ConcurrencyConflictException` in `Application/Common`).
- [ ] `IPdfService.GeneriereMahnungPdfAsync(int mahnungId, CancellationToken ct)`.
- [ ] Build Application, commit.

### Task 7: Infrastructure — EF Configurations + Migration `FinanzenMahnwesen`
- [ ] Configurations: `ZahlungConfiguration`, `ZahlungZuordnungConfiguration`, `MahnstufeConfiguration`, `MahnungConfiguration`, `MahnungPositionConfiguration`, `EmailVersandConfiguration` (CHECK-Constraints: `Zahlung` Kunde-XOR-Lieferant wie `OffenerPosten`; `EmailVersand` Beleg-XOR-Mahnung).
- [ ] `OffenerPostenConfiguration`: `Status`-Property (`HasConversion<int>` oder direkt int-backed enum, wie andere Enums im Projekt — siehe `BelegStatus`-Mapping als Referenz) ergänzen.
- [ ] `MiletDbContext`: neue `DbSet<>`s.
- [ ] Migration erzeugen (`dotnet ef migrations add FinanzenMahnwesen` aus der Scratch-Kopie, dann die generierten Dateien 1:1 zurück ins echte Repo kopieren) — inkl. `UPDATE`-SQL im `Up()`, das bestehende `OffenePosten`-Zeilen anhand `OffenerBetrag`/`Betrag` auf den richtigen Status setzt (nicht einfach alle auf `Offen`).
- [ ] Build Infrastructure, Migration gegen keine echte DB anwendbar hier (kein SQL Server) — Migrations-Code wird stattdessen durch erfolgreichen `dotnet build`+Modell-Snapshot-Konsistenz (`dotnet ef migrations has-pending-model-changes` wenn verfügbar, sonst zweite `migrations add` die „no changes" meldet) verifiziert, echte DB-Anwendung bleibt manueller Schritt für den Nutzer (wie immer, via `Milet.Tools.Migrator`).
- [ ] Commit.

### Task 8: Infrastructure — `MahnstufenSeed` (StammdatenSeed erweitern)
- [ ] `StammdatenSeed.ApplyAsync` um Default-Mahnstufen ergänzen (3 Stufen: 7/14/21 Tage Karenz, 0/5/10 € Gebühr) — „je fehlender Stufe ergänzen"-Muster wie bei Nummernkreisen (Task 5 aus Phase-4-Plan), nicht „nur wenn Tabelle leer".
- [ ] Build + Migrator-Projekt build, commit.

### Task 9: Infrastructure — `OffenePostenService`
- [ ] `src/Milet.Infrastructure/Services/OffenePostenService.cs`: Liste mit Filter (Typ, Status, NurUeberfaellige), `TageUeberfaellig = (heute - Faelligkeit).Days` (negativ = noch nicht fällig, in DTO-Mapping berechnet), `PartnerName` aus Kunde/Lieferant-Include.
- [ ] DI-Registrierung.
- [ ] Build, commit.

### Task 10: Infrastructure — `ZahlungService`
- [ ] `SkontoVorschlagAsync`: lädt OP+Beleg, ruft `SkontoRechner.BerechneSkonto`.
- [ ] `ErfasseZahlungAsync`: eine Transaktion — pro Zuordnung: lädt OP mit `RowVersion` (Concurrency-Schutz gegen parallele Zahlungserfassung auf denselben OP, analog Bestandssperre-Prinzip aber hier via natives EF-RowVersion statt atomarem UPDATE, weil kein Hochfrequenz-Pfad), validiert `Betrag+SkontoBetrag <= OffenerBetrag` (sonst `InvalidOperationException`, klare Meldung „Zahlungsbetrag übersteigt offenen Posten"), reduziert `OffenerBetrag`, setzt `Status` (`Ausgeglichen` wenn `<= 0`, sonst `TeilweiseBezahlt`), legt `Zahlung`+`Zuordnungen` an, `SaveChangesTranslatingConcurrencyAsync`.
- [ ] DI-Registrierung.
- [ ] Build, commit.

### Task 11: Infrastructure — `MahnwesenService`
- [ ] Mahnstufen-CRUD (Kleinstamm-Muster, `SaveChangesDeletingAsync` beim Löschen).
- [ ] `ErmittleFaelligeAsync`: lädt offene/teilweise bezahlte OPs ohne Mahnsperre + alle `Mahnstufe`n, ruft je OP `MahnSelektionService.ErmittleFaelligeStufe`, gruppiert Ergebnis nach Kunde in `MahnlaufGruppeDto`.
- [ ] `MahnlaufDurchfuehrenAsync`: eine Transaktion — je Kunden-Gruppe der übergebenen OP-Ids: legt `Mahnung`+`MahnungPosition`en an (Snapshot `BelegNummer`/`OffenerBetrag`), setzt `OffenerPosten.Mahnstufe` auf die neue Stufe, summiert `Gesamtbetrag` (Σ OffenerBetrag + Gebühr der höchsten beteiligten Stufe).
- [ ] DI-Registrierung.
- [ ] Build, commit.

### Task 12: Infrastructure — Mahnung-PDF
- [ ] `MahnungPdfDocument` (QuestPDF, `src/Milet.Infrastructure/Pdf/`) — Briefkopf (Firmenstamm, wie `BelegPdfDocument`), Mahnstufe-Titel/-Text, Positionstabelle (Belegnummer/Fälligkeit/Betrag), Gebühr, Gesamtbetrag.
- [ ] `PdfService.GeneriereMahnungPdfAsync` implementieren.
- [ ] Build, commit.

### Task 13: Infrastructure — `IEmailService`: `GraphEmailService` + `NichtKonfigurierterEmailService`
- [ ] `Directory.Packages.props`: `Microsoft.Graph`, `Microsoft.Identity.Client.Broker` ergänzen (Versionen wie PLAN.md, aktuelle 5.x/4.x-Reihe zum Zeitpunkt der Umsetzung prüfen).
- [ ] `GraphSettings` (ClientId/TenantId/RedirectUri) als Options-Klasse, gebunden aus `appsettings.json`-Sektion `Graph`.
- [ ] `GraphEmailService`: `PublicClientApplicationBuilder` mit `.WithBroker(...)` (WAM), interaktives Sign-In beim ersten Versand pro App-Sitzung (Token-Cache in-memory für die Laufzeit reicht für v1), `Mail.Send`-Scope, sendet über Graph SDK `me/sendMail` mit PDF als Attachment.
- [ ] `NichtKonfigurierterEmailService`: wirft `EmailNichtKonfiguriertException` mit Hinweistext („Graph-Konfiguration fehlt in appsettings.json, siehe README/STATUS.md").
- [ ] DI: `services.AddScoped<IEmailService>(...)` wählt anhand `configuration.GetSection("Graph").Exists()`.
- [ ] Build, commit.

### Task 14: Infrastructure — `EmailVersand`-Logging + DI-Vervollständigung
- [ ] `EmailVersandLogService` oder Erweiterung von `ZahlungService`/direkt in App-Layer? → Entscheidung: eigener kleiner `IEmailVersandService` (Application+Infrastructure), der `IEmailService` wrapped, Erfolg/Fehlschlag protokolliert und **immer** ein `EmailVersandDto`-Ergebnis zurückgibt (nie eine Exception nach oben durchreicht) — UI-Layer muss nur das Ergebnis anzeigen, nicht try/catch um Graph-Fehler bauen.
- [ ] `Milet.Infrastructure/DependencyInjection.cs` vervollständigen (alle Task 9–13 Services + `IEmailVersandService`).
- [ ] `appsettings.json`/`appsettings.Development.json` um leere/Beispiel-`Graph`-Sektion ergänzen (auskommentiert per Default, damit `NichtKonfigurierterEmailService` greift, bis der Nutzer eigene Werte einträgt).
- [ ] Build, commit.

### Task 15: App (WinUI, unverifiziert) — OP-Liste
- [ ] `OffenePostenListViewModel`/`Page` (`src/Milet.App/ViewModels/Finanzen/`, `Views/Finanzen/`): Filter (Debitor/Kreditor/Status/nur überfällige), Liste mit `TageUeberfaellig`-Spalte (rot hervorgehoben wenn > 0 — `IValueConverter` falls nötig), „Zahlung erfassen"-Button pro Zeile bzw. für Mehrfachauswahl.
- [ ] DI + Navigation-Registrierung, `ShellPage.xaml` „Finanzen"-Menüpunkt aktivieren (`IsEnabled` entfernen) mit Unterpunkt „Offene Posten".

### Task 16: App (WinUI, unverifiziert) — Zahlungsdialog
- [ ] `ZahlungDialog` (`ContentDialog`, Muster `WareneingangMengenDialog`): Zahlungsdatum (`DatePicker`+`DateOnlyToDateTimeOffsetConverter`), je ausgewähltem OP eine Zeile (Betrag editierbar, Skonto-Vorschlag-Button ruft `SkontoVorschlagAsync` und befüllt `SkontoBetrag`), Summenzeile.
- [ ] `ZahlungDialog` aus `OffenePostenListViewModel` aufgerufen, ruft `IZahlungService.ErfasseZahlungAsync`, Fehler über `IDialogService.ZeigeFehlerAsync`, Liste danach neu laden.

### Task 17: App (WinUI, unverifiziert) — Mahnstufen-Tab in `KleinstammPage`
- [ ] Sechster Pivot-Tab „Mahnstufen" (Muster: bestehende 5 Tabs, Liste+Formular Stufe/Karenztage/Gebühr/Mahntext), `KleinstammViewModel` um Abschnitt erweitern.

### Task 18: App (WinUI, unverifiziert) — Mahnlauf-Seite
- [ ] `MahnlaufViewModel`/`Page` (Muster `BestellVorschlagPage`): „Fällige ermitteln"-Button lädt Kandidaten gruppiert nach Kunde mit Checkbox-Auswahl, „Mahnlauf durchführen" erzeugt Mahnungen, Ergebnisliste mit „PDF"/„E-Mail senden"-Buttons je erzeugter Mahnung.
- [ ] DI + Navigation, `ShellPage.xaml` Unterpunkt „Mahnlauf".

### Task 19: App (WinUI, unverifiziert) — E-Mail-Versand auf Rechnung/Mahnung
- [ ] `RechnungEditViewModel`: „E-Mail senden"-Button neben PDF-Button (nur wenn `Gebucht`), ruft `IEmailVersandService`, zeigt Ergebnis (Erfolg/`EmailNichtKonfiguriertException`-Text) über `IDialogService`.
- [ ] Gleicher Button in der Mahnlauf-Ergebnisliste (Task 18) je Mahnung.

### Task 20: Verifikation & Doku
- [ ] Alle Nicht-App-Projekte einzeln bauen (`Domain`, `Application`, `Infrastructure`, `Tools.Migrator`) + alle drei Testprojekte einzeln (MTP) — real in dieser Session, s. „Verifikations-Realität" oben.
- [ ] `STATUS.md` aktualisieren: Phase 5 Abschnitt (was verifiziert wurde, was nicht — insbesondere `Milet.App` compile-unverifiziert und Graph-Mail funktional unverifiziert, explizit hervorgehoben, nicht nur „manueller Smoke-Test ausstehend" wie bei Phase 3/4).
- [ ] `PLAN.md` Fußzeile „Stand" aktualisieren.
- [ ] Commit.
