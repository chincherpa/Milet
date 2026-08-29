# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"Milet" — German warehouse/ERP system ("Warenwirtschaftssystem", orientation "Rita Bosse") covering Stammdaten, Verkauf, Einkauf, Lager, Finanzen, Reporting, Administration. Greenfield, built in phases (see `PLAN.md`, `STATUS.md`).

Stack: .NET 10, WinUI 3 (Windows App SDK) desktop app, MVVM via CommunityToolkit.Mvvm, EF Core + SQL Server (multi-user, optimistic concurrency via RowVersion), QuestPDF (Community license) for documents, FluentValidation. Deliberately **not** used: MediatR, AutoMapper, a repository layer over EF (DbContext *is* the unit of work) — plain services + explicit mapping instead.

`PLAN.md` is the full architecture/design spec (data model, workflows, phases) — read it for anything not covered here. `STATUS.md` tracks what's actually implemented/verified per phase, known bugs, and open risks — check it before assuming a feature exists.

## Build & Test

Windows-only (WinUI). Requires .NET 10 SDK (`global.json` pins `10.0.400`). If `dotnet` on PATH resolves to an empty install, use `%USERPROFILE%\.dotnet\dotnet.exe` explicitly.

Tests use the **Microsoft.Testing.Platform (MTP) runner**, not VSTest (`global.json` → `test.runner`). Running `dotnet test` across multiple projects at once can report "no tests found" — **run each test project individually**:

```
dotnet build src/Milet.App/Milet.App.csproj -p:Platform=x64
dotnet test tests/Milet.Domain.Tests/Milet.Domain.Tests.csproj
dotnet test tests/Milet.Application.Tests/Milet.Application.Tests.csproj
dotnet test tests/Milet.IntegrationTests/Milet.IntegrationTests.csproj
```

- `Milet.App` targets `net10.0-windows10.0.19041.0`, platform `x64` only (`RuntimeIdentifier win-x64`) — always pass `-p:Platform=x64` when building it.
- `TreatWarningsAsErrors` is `true` solution-wide (`Directory.Build.props`) **except** `Milet.App`, where WinUI XAML codegen warnings are unavoidable.
- Central Package Management: all package versions live in `Directory.Packages.props`; don't put `Version=` on `PackageReference` in individual `.csproj` files.
- `Milet.IntegrationTests` uses Testcontainers.MsSql — without Docker available, these tests **skip cleanly** rather than fail. A skip is not a pass; don't treat integration coverage as verified unless Docker actually ran.

### Database / migrations

EF Core migrations are applied **only** via `Milet.Tools.Migrator` (the console app), never from the WinUI app directly — multi-user deployment requires a controlled migration step, and WinUI can't be an EF Core design-time startup project anyway.

```
dotnet run --project src/Milet.Tools.Migrator
```

It applies pending migrations, then runs `StammdatenSeed` (base data: units, VAT rates, payment terms, number ranges) and `DummyDatenSeed` (idempotent test data), in that order. Connection string comes from `appsettings.json` (`ConnectionStrings:Milet`) or env var `MILET_CONNECTIONSTRING` / `--connection=` arg override. Local dev target is LocalDB (`(localdb)\MSSQLLocalDB`, database `Milet`).

`Milet.Infrastructure` is the EF Core design-time project (`DesignTimeDbContextFactory`) for `dotnet ef` commands, since WinUI can't host that role.

## Architecture

Layered: `Milet.Domain` → `Milet.Application` → `Milet.Infrastructure` → `Milet.App` (WinUI), plus `Milet.Tools.Migrator` (console, references Infrastructure only).

- **Milet.Domain**: no external dependencies. Entities, enums, value objects, pure domain services (`PreisfindungService` — pricing/tiered discounts, `SteuerRechner` — tax calculation with `MidpointRounding.ToEven`), `AuditableEntity`, `IHasRowVersion`.
- **Milet.Application**: → Domain only. Abstractions (`IEmailService`, `IPdfService`, `ICurrentUserService`, `INumberRangeService`), plain services + DTOs (records) + FluentValidation validators, organized per module folder (`Stammdaten`, `Verkauf`, `Lager`, `Admin`, ...). Validation runs explicitly at the start of service methods; a `ValidationException` maps to a UI dialog.
- **Milet.Infrastructure**: → Application. `MiletDbContext`, EF configurations, migrations, `SaveChangesInterceptor`s, QuestPDF documents, concrete service implementations, DI registration (`DependencyInjection.AddInfrastructure`).
- **Milet.App**: WinUI 3. `Host.CreateApplicationBuilder`-based DI root in `App.xaml.cs`, `ShellPage` + `NavigationView`, one List-ViewModel/Page + Edit-ViewModel/Page pair per entity (Kunden/Lieferanten/Artikel/... follow the same pattern), `INavigationService` (dictionary VM→Page), `IDialogService`. ViewModels are registered transient; services from Infrastructure are scoped/singleton per `DependencyInjection.cs`.

### Key architectural decisions (don't relitigate these — see `PLAN.md` for rationale)

- **Beleg (document) model**: all document types share **one** `Beleg` table + one `BelegPosition` table via EF TPH (table-per-hierarchy) with a thin subclass per type (e.g. `Rechnung : Beleg`). Seven types exist today: Angebot, Auftrag, Lieferschein, Rechnung, Bestellung, Wareneingang, Eingangsrechnung. `Gutschrift` is planned in `PLAN.md` but **not implemented** — there is no `Gutschrift : Beleg` subclass and the seeded `GS` number range is unused. Line items reference their origin line via `UrsprungsPositionId` (self-reference) — this single column drives partial delivery, partial invoicing, and collective invoicing (open quantity = quantity minus sum of referencing follow-up lines).
- **Tax**: computed per VAT-rate group on the sum of net lines (not summed per line) into `BelegSteuerSumme`, to avoid off-by-one-cent DATEV mismatches.
- **Booking / immutability (GoBD)**: once a document is `Gebucht` (booked), a `SaveChangesInterceptor` (`BelegImmutabilityInterceptor`) throws on any modification. Invoice numbers are assigned atomically only at booking time (gapless sequence required by §14 UStG), via `NumberRangeService`'s atomic `UPDATE ... OUTPUT` (drawn on the *booking transaction's* context, so a failed booking rolls the number back). Corrections are meant to be counter-postings, never deletes/edits — but note that the counter-posting path itself does **not exist yet**: `BelegStatus.Storniert` is never assigned anywhere and there is no Storno service, so a wrongly booked invoice currently cannot be corrected in the app at all (see `STATUS.md`).
- **Stock (Lager)**: append-only ledger (`Lagerbewegung`) + a derived snapshot (`ArtikelBestand`). The snapshot is updated in the same transaction via an atomic `UPDATE ... SET Menge = Menge + @delta WHERE ... >= 0` (single round-trip; zero affected rows ⇒ stock lock / `InvalidOperationException`) — never read-modify-write. `BestandService.BucheBewegungAsync` is the **only** write path onto stock; every stock-affecting flow (Lieferschein booking, manual correction, Inventur close-out) goes through it.
- **Documents flow through** a generic `BelegUeberleitungService.Ueberleiten*` (Angebot→Auftrag→Lieferschein→Rechnung, incl. collective invoicing across multiple Lieferscheine and partial-quantity selection) — see `IVerkaufServices.cs` for the interface.
- **DbContext usage**: `IDbContextFactory<MiletDbContext>` as a singleton factory; each service method creates its own short-lived context. Reads use `AsNoTracking`; saves re-attach the DTO with its original `RowVersion` for concurrency detection. `DbUpdateConcurrencyException` surfaces as a standard "reload?" dialog (no merge UI).
- **Rounding**: `decimal` everywhere, `MidpointRounding.ToEven`, stored invariant; `de-DE` formatting only at the UI/PDF boundary. Note: WinUI `NumberBox` in de-DE locale expects **comma** as decimal separator — a `.` is read as a thousands separator (relevant when scripting/testing input).

### Known risks / traps (see `STATUS.md` "Bekannte Risiken" for full detail — don't rediscover these)

- The in-transaction "open quantity" re-check in `BelegUeberleitungService` likely does **not** protect against concurrent partial-delivery/collective-invoicing races under SQL Server's default READ COMMITTED isolation — two parallel transactions can both see "nothing delivered yet." Suspected fix is `UPDLOCK` on the source document(s); not yet verified or fixed.
- `StammdatenSeed` only creates number ranges (`Nummernkreise`) when the table is completely empty, not per missing code — an already-migrated DB won't backfill a newly introduced code.
- Integration tests requiring Docker (Testcontainers) skip silently on machines without Docker (true here) — a green integration test run on this machine has **not** exercised the parallelism/race-condition tests; treat those as unverified until run with Docker.

## Language convention

Domain/code identifiers, comments, commit messages, and planning docs are in **German** (matching the business domain — Beleg, Lagerbewegung, Nummernkreis, etc.). Match this convention when adding to existing German-named modules.
