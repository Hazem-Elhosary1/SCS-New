# Implementation Plan: Console Frame (App Shell)

**Branch**: `006-app-shell` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-app-shell/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give the console its frame: one top bar, one side bar and a content area that every page after
sign-in sits in, with the side bar showing what the person's role opens. Add the account menu
with sign-out, the language switch saved for the person, folding, the environment strip and the
offline alert. Build the bell, the branch switch, the subscription banner and "not in your
plan" with all their states, fed by sources that say "nothing yet" until later specs supply
data. Put the parts and rules of every list, table, dialog and passing notice in the shared
library, and prove them on the roles list (for reading) and on a sample list.

Technical approach, one line each (detail in [research.md](research.md)):

- One `ShellComponent` in the clinic app; every address after sign-in is its child route.
- One table of destinations drives the side bar, the generated routes and the tests.
- What a person keeps is one clinic-owned row per account in `Identity`, read with the session
  and written by one own-account route.
- Each part that waits for data reads a source from the core library with a "nothing" default;
  a later spec provides the source and touches nothing in the frame.
- Overlays (menus, panel, popover, tooltip) are built on `@angular/cdk`, which is added.
- A `scsWrite` directive marks every control that changes data; offline disables them.
- The list parts live in the shared library; their rules are a written contract.

## Technical Context

**Language/Version**: TypeScript on Angular 22; C# on .NET 10 (as pinned by spec 001).

**Primary Dependencies**: one new — `@angular/cdk` (overlay, menu, a11y), the version matching
Angular 22. Otherwise Angular router, Transloco, Tailwind and the shared components of specs
003 to 005; ASP.NET Core and EF Core on the server.

**Storage**: SQL Server 2022, schema `identity`: one new clinic-owned table,
`StaffPreference`. One migration, no data to bring over.

**Testing**: xUnit on the SQL Server Testcontainers fixture for preferences, isolation and the
roles' holders; Angular unit tests for the destinations table, the side bar, the sources and
each shared part; Playwright for every state of the frame in both languages with pictures for
the design comparison, keyboard use, and four widths. Test-first for the side bar by
permission, every address inside the frame, and preferences.

**Target Platform**: desktop browsers at 1280 wide and up; usable down to 1024.

**Project Type**: web application — mostly the clinic app and the two web libraries; a small
addition to the `Identity` module.

**Performance Goals**: language switch under one second with no reload (SC-004); offline
alert and disabled controls within 3 seconds (SC-006); moving between pages does not re-create
the bars.

**Constraints**: the server decides — the side bar only hides, spec 005's gate refuses; no
preference or frame state is a secret, but each person's row is their own and isolated; nothing
of a destination is loaded for a person who may not open it; the environment strip never shows
in production; no clinic data in the sample list.

**Scale/Scope**: 43 functional requirements; 20 destinations under 3 parents, 2 more in the
account menu; 1 table, 1 new endpoint, 2 changed answers; 6 sources; about 14 shared parts; 1
frame with about 30 pictured states in two languages; the roles list; the sample list.

**Open items carried from research** (defaults chosen): three wordings where the design and the
BRD differ (empty notifications panel, banner action, environment strip); the addresses; the
"not in your plan" card, not found in the components pages. Settled on 11 Oct 2026: frame now,
data later; states built from the design, found on page "✓ Home"; the chip shows the email
until spec 026; the roles list for reading plus a sample list; complete from 1280 wide.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite CL-SH-001 to CL-SH-064 and §A-7. The spec names the file, the pages and every frame, found by listing the file's pages; structure, sizes and wording in both languages were read from them before this plan was written (research, opening table). Undrawn and listed: the panel's error state, the "not available yet" page, the "not in your plan" card. **Open**: three wordings where the BRD's recorded rulings differ from the design's placeholder text (R13) |
| II. Tenant isolation in the database (NON-NEGOTIABLE) | Pass | The one new table is clinic-owned with the policy of spec 002 in its creating migration and an isolation sample. No new crossing. The row is found by the session's own account under ordinary isolation |
| III. The server decides | Pass | The side bar hides; it grants nothing. Every address is still guarded by spec 005's route guard on the client and its gate on the server; a person who types an address they may not open loads nothing of it. The new route is own-account and is added to the written list. Nothing before sign-in changes |
| IV. Financial records immutable and audited | Not applicable | No money |
| V. Patient data, secrets and AI contained | Pass | No patient data. The sample list holds made-up rows and exists outside production only. Preferences are not personal data beyond a language |
| VI. Bilingual and complete screens | Pass | Every part in Arabic and English, mirrored, with Arabic-Indic digits in Arabic; loading, empty, error, offline, view only and not-in-plan come from shared parts (FR-025); keyboard use for every control (FR-040) |
| VII. Tested before merge, verified after implementation | Pass | Failing tests first for the items above. After implementation: every suite, the real-stack walk of the quickstart with three seeded people, every state of the frame beside its frame in both languages, and `validation.md` saying what was run, walked, compared and not compared |
| Technology constraints | Pass | Own components on Tailwind and the Angular CDK — the CDK is the one the constitution names and is added here; Transloco; data access in Identity's Infrastructure |
| Workflow | Pass | The isolation and route-declaration gates cover the new table and route |

**Result**: gate passed. The open items under Principle I block approval of the spec, not the
design.

**Post-design re-check** (after Phase 1): unchanged. Two things to state plainly. First, a new
dependency is added (`@angular/cdk`); it is the kit the constitution already names. Second, the
frame replaces the temporary landing page of spec 003, so the browser tests of specs 003 to 005
that look for that page's markers are updated to the frame's.

## Project Structure

### Documentation (this feature)

```text
specs/006-app-shell/
├── plan.md
├── research.md          # 14 decisions, 5 items to confirm
├── data-model.md        # 1 table, 2 changed answers, client shapes, sources
├── quickstart.md        # 20 automated scenarios, 11 steps by hand
├── contracts/
│   ├── shell-api.openapi.yaml   # preferences (new), session and roles (changed)
│   ├── shell-frame.md           # layout, bars, menus, banners, pages, what later specs do
│   └── list-contract.md         # anatomy, parts and 23 rules of lists, dialogs and toasts
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
src/server/
├── modules/Identity/
│   ├── Scs.Identity/
│   │   ├── Domain/StaffPreference.cs
│   │   ├── Infrastructure/            # Migrations/ (one), Preferences.cs; ClinicRoles (holders)
│   │   ├── Api/PreferencesEndpoints.cs      # PUT /v1/account/preferences (own account)
│   │   └── Api/AuthEndpoints.cs             # session answer + preferences
│   └── Scs.Identity.Contracts/              # RoleSummary + holders
└── tests/
    ├── Scs.IntegrationTests/Identity/PreferencesTests.cs
    ├── Scs.IntegrationTests/Access/         # own-account list (three); roles' holders
    └── Scs.IntegrationTests/Isolation/IdentitySamples.cs   # + 1 table

src/web/
├── package.json                             # + @angular/cdk
├── projects/core/src/lib/
│   ├── shell/                               # the six sources and their defaults; PreferencesService
│   ├── auth/auth.service.ts                 # session carries preferences
│   └── i18n/language.service.ts             # adopt and save the person's language
├── projects/ui/src/lib/
│   ├── icon/                                # the frame's icons
│   ├── menu/, tooltip/, toast/, confirm/    # overlays on the CDK
│   ├── page-header/, card/, tabs/
│   ├── list/                                # toolbar, filter popover, applied chips, table styles,
│   │                                        # row menu, table footer, skeleton, empty state
│   ├── write/                               # scsWrite
│   └── state/                               # not-in-plan card
├── projects/clinic/src/app/
│   ├── shell/                               # shell, top bar, side bar, account menu, bell panel,
│   │                                        # branch switch, subscription banner, destinations.ts
│   ├── pages/                               # not available yet, not found
│   ├── administration/roles.page.ts         # the roles list, for reading
│   ├── dev/sample-list.page.ts              # outside production only
│   ├── landing/                             # removed
│   └── app.routes.ts                        # the frame and its generated children
├── projects/*/public/i18n, ui/i18n          # the frame's wording
└── e2e/                                     # shell, shell-states, roles-list, sample-list;
                                             # staff-platform.ts (preferences, holders)

docs/adding-a-module.md                      # adding a destination's page; sources; scsWrite; lists
docs/setup.md                                # the frame; the sample list
specs/005-permissions-engine/contracts/access-engine.md   # the own-account list (three)
```

**Structure Decision**: the frame is the clinic app's — the operators console has its own
design and gets its own. What every console page uses (list parts, overlays, the write marker,
toasts) goes in the shared library; what a later spec fills goes in the core library as
sources. The server's part is one table in the module that owns accounts.

**Terms**: the spec's "frame" is the shell; "destination" is a row of `destinations.ts`;
"passing notice" is the toast; "what a person keeps" is `StaffPreference`.

## Delivery Order

For `/speckit-tasks`. Tests first throughout.

1. **Base** — add the CDK; the frame's icons and tokens; `StaffPreference` with its migration,
   isolation sample, endpoint and the session answer; the roles' holders.
2. **The frame and its addresses** — failing tests for "every address inside the frame" and the
   destinations table; then `ShellComponent`, the generated routes, the "not available yet" and
   "not found" pages; the landing page removed; earlier browser tests updated. Story 1.
3. **Side bar** — failing tests over 20 destinations and 3 parents; then the side bar, groups,
   active item, parents. Story 2.
4. **Account chip and menu** — the menu on the CDK, sign-out. Story 3.
5. **Language** — `PreferencesService`, adopt and save. Story 4.
6. **Folding** — fold, tooltips, group from folded, saved, width rule. Story 5.
7. **Banners and states** — environment strip, offline alert and `scsWrite`, subscription
   banner with closing, not-in-plan. Story 6.
8. **Sources** — the six sources with defaults; bell panel in four states; branch switch.
   Story 7.
9. **Lists** — the shared parts; the roles list; the sample list. Story 8.
10. **Both languages, as drawn** — every state pictured and laid beside its frame; keyboard;
    widths. Story 9.
11. **Gates and verification** — docs; every suite; the real-stack walk; `validation.md`.

## Complexity Tracking

No constitution violations to justify.

Choices recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A new dependency, `@angular/cdk` | Menus, the panel, the popover and tooltips must position in two directions, close on outside press and Escape, and return focus (FR-040) | Hand-written overlays repeat that work in five places and are where accessibility faults come from; the constitution already names the CDK |
| Sources with defaults for six parts | The owner chose to build the frame whole; later specs must add data without touching it (SC-008) | Leaving the parts out means the frame is rebuilt by specs 008, 028 and 012 |
| Routes generated from one table | 22 destinations each need a guarded page and a no-access twin; the side bar and the tests need the same list | 44 hand-written routes and a second list for the side bar drift apart |
| Preferences on the server | The language and the folded bar must follow the person to another browser (SC-005) | Browser storage is per browser |
| The roles list built here | The owner chose a real first list to prove the list parts against a design | A sample page alone proves the parts against nothing drawn |
| A sample list outside production | Search, filters, chips and paging have no real list yet (the roles list is short) | Waiting for spec 026 means the first long list also debugs the shared parts |
