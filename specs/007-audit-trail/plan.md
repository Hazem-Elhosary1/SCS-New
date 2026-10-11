# Implementation Plan: Audit Trail

**Branch**: `007-audit-trail` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-audit-trail/spec.md`

## Summary

One append-only record of who did what, to what, when and from where, with the value before and
after wherever money, a permission or a plan changes — written in the same transaction as the
change, never altered, never holding a secret, and readable by a clinic about itself.

The approach: a new server module, `Scs.Audit`, owning one clinic-owned table, `audit.Entry`,
protected like the sign-in events of spec 003 (isolation policy, a trigger that refuses update
and delete). Actions come from a closed catalogue in the shared kernel, frozen by a lock file.
A module records through the trail on its own database connection, so the change and the entry
commit together. Every changing route declares what it records, checked by a test in the style
of spec 005's route declarations. The role service of spec 005 records its four changes; the
sign-in service of specs 003 and 004 writes an entry beside each event that belongs in the
trail. One reading address, `GET /v1/audit/entries`, answers a clinic's page of entries as words
in the reader's language. No screen.

Two items carried over from spec 006 by the owner's decision are settled with this work: the
clinic app loads destinations' pages when opened, which brings its first download under the
warning limit and gives the timed test of spec 005 its margin back (research R16).

## Technical Context

**Language/Version**: C# on .NET 10 (as pinned by spec 001). TypeScript on Angular 22 for the
carried-over loading change only.

**Primary Dependencies**: none new. EF Core for the module's context and migration; the entry is
inserted with a parameterised command on the caller's connection.

**Storage**: SQL Server 2022, new schema `audit`: one clinic-owned table, `Entry`, with the
isolation policy of spec 002 and an append-only trigger. Two JSON columns for facts and changes.
One data migration in `identity` that carries existing sign-in events and roles into the trail.

**Testing**: xUnit on the SQL Server Testcontainers fixture. Isolation and permission tests are
written first (Constitution, testing). A volume test with one million entries. Unit tests for
the catalogue. No Playwright test and no design comparison: no screen.

**Target Platform**: the platform API.

**Project Type**: web service — a server module, its contracts, and changes inside Identity.

**Performance Goals**: a page of a clinic's last ninety days and its counts in under one second
at one million entries (SC-007); recording adds under a tenth of a second to an action (SC-010).

**Constraints**: the change and its entry are one transaction (FR-009); nothing the application
can do changes or removes an entry (FR-020, FR-021); only allowed facts are stored (FR-010);
actor, clinic, address and time are never supplied by the caller (FR-014); no module reads
another's tables — Identity is asked through its contract for names and for the count of a run.

**Scale/Scope**: 45 functional requirements (six added by clarification, two of them carried
over from spec 006); 12 actions; 1
table; 1 reading address; about a dozen routes to declare; 2 services to change (roles,
sign-in).

**Open items carried from research**: none that block. Decided by the owner on 11 Oct 2026:
sign-in and password events under security (R13); wording from the Activity log frames (R15);
the operators' side as a contract only (R5); role entries verified by tests (R14); nothing
removed (R4); one data migration from Identity (R2); the two items of spec 006 (R16). For the
owner's documents: the BRD's «أمنية» tab should name the sign-in and password events, and
eleven of the twelve sentences are undrawn.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite SH-092 to SH-095, Chapter 09 §3-5 and its rules ق-٣٩ to ق-٤٢, Chapter 17 §3-11, Chapter 10 §3-8. No screen is built. The words of entries were read from the frames of the Activity log page (`5450:4`); the spec names the frames, and lists as undrawn the eleven sentences the frames do not show |
| II. Tenant isolation in the database (non-negotiable) | Pass | `audit.Entry` is clinic-owned with a required clinic, its policy in the same migration, its sample in the isolation suite. Entries with no clinic are not created at all yet (R5) rather than weakening the policy |
| III. The server decides; deny by default | Pass | The reading address requires `reports.activity`; every changing route declares what it records, and an undeclared one fails the build's tests |
| IV. Financial records are immutable | Pass, and extended | The trail is the record that makes later money changes traceable; it is itself immutable by the same means as sign-in events |
| V. Secrets and patient data | Pass | A white list of fact keys per action, a never-list no action may allow, the address and user agent marked as personal data so operational logs never carry them; no clinical content by rule (FR-017) |
| VI. Bilingual, complete screens | Pass | No screen. Every action has its name and sentence in Arabic and English, checked by a test; the reading answers in the reader's language |
| VII. Tested before merge, verified after implementation | Pass | Full suites, the browser suite included because of the loading change; the real-stack walk of quickstart "By hand"; no screen of this spec to compare, stated in `validation.md`; role entries verified by tests only, by the owner's decision |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged, with two things stated plainly. First, the
plan changes one assumption of the spec: sign-in events that belong in the trail are also
written as entries (R2), so the trail has one store to read; the spec's assumption is reworded
with this plan. Second, one migration in `identity` writes rows into `audit.Entry` to carry
existing data over — a module writing another's table, once, recorded below.

## Project Structure

### Documentation (this feature)

```text
specs/007-audit-trail/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── audit-trail.md            # how a later spec records an action
│   └── audit-api.openapi.yaml    # GET /v1/audit/entries
├── checklists/requirements.md
└── tasks.md                      # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/Audit/
│   ├── AuditActions.cs               # the catalogue: code, category, names, sentences, facts
│   ├── AuditCategories.cs
│   └── audit-actions.lock            # codes, frozen
├── shared/Scs.BuildingBlocks/Security/
│   └── AuditDeclarations.cs          # .Records(…), .RecordsNothing(reason), .RecordedBySignIn()
├── modules/Audit/
│   ├── Scs.Audit.Contracts/
│   │   └── AuditContracts.cs         # IAuditTrail, AuditRecord, AuditedSave, IClinicAuditReading,
│   │                                 # IOperatorAuditReading
│   └── Scs.Audit/
│       ├── AuditModule.cs
│       ├── Domain/Entry.cs
│       ├── Infrastructure/
│       │   ├── Persistence/AuditDbContext.cs
│       │   ├── Migrations/…_InitialAuditTrail.cs
│       │   ├── AuditTrail.cs         # the writer: checks, takes actor/clinic/address/time, inserts
│       │   ├── AuditReading.cs       # period, facets, counts, paging
│       │   ├── Sentences.cs          # facts → words in the reader's language
│       │   └── DeviceText.cs         # user agent → "Chrome on Windows"
│       └── Api/AuditEndpoints.cs     # GET /v1/audit/entries
├── modules/Identity/
│   ├── Scs.Identity.Contracts/StaffContracts.cs   # + names of accounts, the count of a run
│   └── Scs.Identity/
│       ├── Infrastructure/ClinicRoles.cs          # records the four role changes
│       ├── Infrastructure/ReadyRoles.cs           # records the first roles, platform as actor
│       ├── Infrastructure/SignInService.cs, SessionService.cs, PasswordSetter.cs,
│       │   AccountHandover.cs                     # an entry beside each event of R2
│       ├── Infrastructure/Migrations/…_SignInEventsIntoTrail.cs
│       └── Api/*.cs                               # declarations on every changing route
├── modules/Platform, Tenancy/…/Api/*.cs           # declarations
├── host/Scs.Api/Program.cs                        # registers the module
└── tests/
    ├── Scs.UnitTests/Audit/AuditActionsTests.cs
    ├── Scs.ArchitectureTests/…                    # Audit obeys the module rules
    └── Scs.IntegrationTests/Audit/*.cs, Access/RouteAuditDeclarationTests.cs,
        Isolation/AuditSamples.cs

src/web/projects/clinic/src/app/
├── app.routes.ts            # pages loaded when opened
└── shell/destinations.ts    # registerDestinationPage takes a loader
src/web/e2e/access.spec.ts   # the timed test, measured alone

docs/adding-a-module.md      # recording an action; declaring a route; registering a page by loader
docs/setup.md                # the reading address
ops/runbooks/data-handling.md  # what the trail holds, and that nothing removes it
```

**Structure Decision**: a module of its own, because every module writes to the trail and none
owns it. The catalogue is in the shared kernel so that modules name actions without depending on
the audit module, as they name permissions. The web apps change only for the loading of pages
(R16).

**Terms**: the spec's "entry" is a row of `audit.Entry`; "action" is a row of `AuditActions`;
"facts" is the `Facts` column and "before and after" the `Changes` column; a "declaration" is
route metadata.

## Delivery Order

1. **Foundation** — the catalogue and its lock; the module, the table with policy and trigger;
   the writer with its checks; the isolation sample. Isolation tests first.
2. **US2** — together or not at all; staying.
3. **US4** — the white list, the never-list, nothing secret in what is stored.
4. **US1** — the role service records; the first roles by the platform.
5. **US3** — sign-in events also write entries; the run of failed attempts; the migration that
   carries existing events and roles over.
6. **US5** — declarations on every changing route and the test that requires them.
7. **US6** — reading: the address, the permission, facets, counts, paging, the operators'
   contract. Permission test first.
8. **US7** — sentences and device text in two languages.
9. **Carried over** — pages loaded when opened; the build with no size warning; the timed test
   of spec 005 measured alone.
10. **Polish** — the volume test, documentation, every gate, the real-stack walk, `validation.md`.

Stories 1 to 4 are the first deliverable: a trail that exists, is trustworthy, and already holds
the one kind of change the platform can make.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| One migration in `identity` inserts into `audit.Entry` | Events and roles recorded before this spec must be in the trail (US3, scenario 6; FR-027) | Reading old events from `SignInEvent` at read time would bring back the two-store reading rejected in R2; a run-once job would leave a window in which the trail is incomplete |
| The audit writer uses the caller's `DbContext` connection | The change and its entry must commit together (FR-009) | An outbox is eventual; a transaction across two connections is a distributed transaction |
