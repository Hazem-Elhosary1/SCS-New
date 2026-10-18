# Implementation Plan: Clinic Onboarding

**Branch**: `015-clinic-onboarding` | **Date**: 2026-10-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-clinic-onboarding/spec.md`

## Summary

Operators can add, find, read and correct clinics.

**The operators' clinic routes** live in `Subscriptions` (`/v1/ops/clinics…`), the module 016–021
grow. Each piece of data stays with the module that owns it and is written only through that
module's contract:

- **Tenancy**: the clinic's names, slug, specialty, address, tax number, language, state, the
  account manager and who added it. It is written on the crossing login, as 002 designed.
- **Subscriptions**: the plan link and the trial's end.
- **ClinicData**, a new module: the main branch.
- **Identity**: the owner's account, name, mobile, role and invitation, and changing the owner's
  email.
- **Operators**: the published list of active operators.

**The add is all or nothing as anyone sees it** (research R2). Under one platform-wide add lock:

1. Every check that can refuse runs before any write: fields, specialty, email, plan, users limit.
2. The clinic is registered in `setting-up`, invisible.
3. In the new clinic's context, each module writes its part: first the owner with the owner role
   (the one write another writer can refuse, before anything of the add is recorded), then the
   main branch, then the plan and trial with `subscription.plan-set`.
4. Activation turns the clinic `active` and writes `clinic.created` to both trails.
5. The invitation is then emailed.

A fault after step 1 is undone as far as it can be, and nothing of it is ever visible.

**An operator acting on one clinic** establishes it (`ClinicSource.Operator`) and works under its
row level security. The operator is recorded as the actor in the clinic's trail too.

**Reading across clinics** (the list and the plans' figures) uses three narrow, read-only
crossings.

**013's plan screens** now count real clinics. Their revenue is 0 until 018 gives clinics a
billing cycle.

**The ops console** gains the «العيادات» section: the list, the add and edit form, the detail with
«نظرة عامة» and «الإعدادات», a sortable column header, and a copy button in `ui`.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: no new package.

- **Server**: EF Core, the access gate (005), the audit writer (007), the mail template (094),
  013's `IPlanBook`, and 014's `IClinicPlans`, seats and `PlanLimits`.
- **Ops console**: the `ui` library (toolbar, filter popover, chips, table, footer, KPI card,
  tabs, menu, badge, breadcrumb, select, text field, banner, toast), Transloco and Tailwind.

**Storage**: SQL Server 2022. Four migrations, each additive:

- **Tenancy `ClinicProfile`**: `Address`, `TaxNumber`, `DefaultLanguage`, `AccountManagerId`,
  `CreatedBy`, `RowVersion`, and the checks on state, specialty and tax number.
- **Identity `OwnerDetails`**: `FullName`, `Mobile`, `LastSignInAtUtc`, and the mobile check.
- **Subscriptions `Trial`**: `ClinicPlan.TrialEndsAtUtc`.
- **ClinicData `Branches`** (new schema `clinicdata`): `Branch`, clinic-owned, with its row level
  security policy and the one-main index in the same migration.

**Testing**:

- **Test-first** (constitution VII), written and seen to fail before the code:
  - the permission on every new route;
  - isolation for `clinicdata.Branch` and for operator routes;
  - the crossings count;
  - the users limit at the add.
- **Also**:
  - the slug derivation and the status function (unit tests);
  - the add's refusals leaving nothing;
  - the fault-and-undo after each write stage;
  - the two races of SC-002;
  - the invitation's states, cooldown and failures;
  - the owner's email change (sessions, links, notice);
  - the plans' figures;
  - the audit and error pins;
  - the list's timing (SC-007).
- **Web**:
  - Vitest for the service, the words and the two new `ui` pieces;
  - Playwright for every screen and state in both languages at 1440 and 390;
  - the email samples.
- **The real-stack walk** of `quickstart.md`, and the frames laid beside the build.

**Target Platform**: the platform API and the operators console (`ops`, 4203 locally).

**Project Type**: web application.

- A new server module, `ClinicData`.
- Additive changes in `Tenancy`, `Identity`, `Subscriptions`, `Operators`, `Plans`, `Audit`, the
  building blocks and the kernel.
- A new ops section, and two pieces in `ui`.

**Performance Goals**:

- The list answers a search or a filter in under 1 s with a few hundred clinics (SC-007): three
  indexed reads joined in memory.
- An add completes in under 2 s on the local stack. Adds queue behind one lock.

**Constraints**:

- Refuse before writing (FR-003, SC-003).
- Never visible half-made (FR-004, research R2).
- One email is one account on the platform (003).
- The application login never writes `tenancy` (002).
- Modules meet only through contracts.
- The audit record goes in the same unit as its change (constitution IV).
- No link is shown to an operator (OP-A-027).
- Words in «الباقة», with no gendered address. The BRD's own imperatives are quoted and raised
  (research R17).
- The time zone is always Cairo.

**Scale/Scope**:

- 30 functional requirements and 6 user stories.
- 1 new module, 1 new table, 4 migrations.
- 8 ops routes.
- 2 new contracts (`IClinicOwners`, `IBranches`), and 4 that grow (`IClinicRegistry`,
  `IClinicProfile`, `IClinicPlans`, `IOperatorDirectory`).
- 7 error codes and 5 audit actions.
- 3 new crossings (two reads, and `update-clinic`) and 1 changed (`list-clinics`).
- 1 email.
- 5 pages and 2 shared pieces.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. The BRD and the design file are the source of truth

**Pass.**

- **The BRD**: the spec cites Ops module 16: §1–§3, §4-1, §4-7, §5-1 … §5-4, §5-17, §5-18,
  §5-42, §6-1, §6-15 and §7-1 … §7-3, with OP-A-001 … OP-A-049 where they apply. It also cites
  SH-003, SH-013, SH-044, SH-052, SH-055, SH-056, SH-092, SH-097 and SH-098.
- **The frames**: every screen and state names its AR and EN frame
  (`contracts/clinics-screens.md`). The frames of later specs are named and not built.
- **The owner's answers**: the nine answers are in the spec's Clarifications.
- **Conflicts recorded for the owner and the designer**:
  - the per-clinic sign-in address (SH-003) against the constitution;
  - «رقم الهاتف» against «رقم الموبايل»;
  - one name field against two;
  - the Egyptian-only mobile against any country;
  - "unique within the clinic" against one email on the platform;
  - the BRD's imperatives in two messages.
- **Undrawn, built from drawn parts, and raised**:
  - the English name field;
  - the sort header's ascending arrow;
  - the email-changed notice.

### II. Tenant isolation in the database (non-negotiable)

**Pass.**

- **The new clinic-owned table**: `clinicdata.Branch` has its policy in the migration that
  creates it, its guardrail entry and its isolation sample.
- **Operator routes on one clinic** establish that clinic (`ClinicSource.Operator`) and work under
  ordinary row level security. A test proves that a route for clinic A cannot read or write B.
- **Narrow crossings**, each limited to a named type and justified in research R13:
  - `read-clinic-plans-as-operator` (Subscriptions);
  - `read-clinic-owners-as-operator` (Identity);
  - `update-clinic` (Tenancy, recorded), because the application login is denied `tenancy`
    writes;
  - `list-clinics` (exists), which now skips invisible clinics.
  Every crossing is a read except `update-clinic`.
- **Not a new crossing**: the email check reuses the declared sign-in procedure, which answers
  true or false (research R6).

### III. The server decides: permissions and plan entitlements

**Pass.**

- **Permissions**: every route is guarded by `clinics.view` or `clinics.edit` (SH-055, SH-056).
  The console removes controls by the answer's `can` block and never relies on hiding them.
- **The plan limit**: the owner's seat is checked at the add through 014's seat check, under its
  lock (SH-017). A plan whose users limit is 0 is refused before anything is written.
- **Before sign-in** nothing new is revealed (SH-043): the email check is for signed-in operators.
- **Ending sessions**: changing a signed-in owner's email ends every session (SH-045's spirit,
  FR-021a).

### IV. Financial records are immutable; changes are audited

**Pass.**

- **No money moves.** Revenue reads 0 until 018.
- **Plan changes**: setting the plan at the add records `subscription.plan-set` with before and
  after, in its transaction.
- **Every write of the spec** is recorded in both trails in its own unit, with the operator as
  actor and with before and after:
  - `clinic.created`, `clinic.updated`, `clinic.owner-updated`;
  - `clinic.account-manager-assigned`, `clinic.invitation-resent`.
- An edit that touches both the clinic and its owner writes two entries, one per unit (research
  R5).

### V. Secrets and patient data

**Pass.**

- No patient data.
- No link or token is answered or logged. The invitation token is only in the email.
- The email-changed notice masks the new address.
- Logs carry ids, codes and stage names only. Emails and mobiles appear only as audit change
  values, which 007 already allows for account emails.

### VI. Bilingual, complete screens

**Pass.**

- **Language and direction**: every screen is in Arabic and English, right to left in Arabic,
  with Western digits and Cairo time.
- **States**: each screen has loading, empty or no-results, error, offline (through `scsWrite`),
  view only, and not found equal to error.
- **Not in the plan**: this does not apply. The operators console is not plan-gated.
- **Words**: from the BRD and the frames. The proposed words are marked and use no gendered
  address.
- **SH-005**: the clinic's own view of what is built here comes with 078 and 024. The operator's
  flow ends on the detail.

### VII. Tested before merge, test-first where it hurts, verified after implementation

**Pass.**

- **Test-first**: permissions, isolation and the plan limit are written first.
- **Refusal paths are tested**:
  - no permission and view only;
  - not found;
  - every refusal of the add, the edit, the assignment and the resend.
  A read-only clinic is 016's.
- **Verification**: full suites, the real-stack walk, and the frames beside the build in both
  languages. `validation.md` and `design-vs-real.png` close the work.

**Result**: the gate passed.

**Post-design re-check** (after Phase 1): passed. Stated plainly:

- **A new module, `ClinicData`** (research R3). 028 and 029 grow it.
- **The add's guarantee is "never visible half-made"**, not one database transaction. No
  transaction can span the two logins, and research R2 records the rejected alternatives. The
  spec's FR-004 and its edge case say so.
- **Changes in built specs' code**: listed in research R16, with their documents untouched
  (constitution 1.3.0). They are 002, 003/004, 007, 009/010, 011, 012/013 and 014.

## Project Structure

### Documentation (this feature)

```text
specs/015-clinic-onboarding/
├── spec.md              # the specification (clarified 17 Oct 2026, 9 answers)
├── plan.md              # this file
├── research.md          # R1–R17: where each part lives, the add's guarantee, crossings, words
├── data-model.md        # the new columns and table, the derived status and invitation, audit, checks
├── quickstart.md        # automated suites, the walk, the design check
├── contracts/
│   ├── ops-clinics-api.md   # the 8 routes and the module contracts
│   ├── clinics-screens.md   # the 5 pages, their words, states, frames and tests
│   └── emails.md            # the invitation (unchanged) and the email-changed notice
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── modules/ClinicData/                                       # NEW module (docs/adding-a-module.md)
│   ├── Scs.ClinicData/
│   │   ├── ClinicDataModule.cs
│   │   ├── Domain/Branch.cs
│   │   ├── Infrastructure/Persistence/ClinicDataDbContext.cs
│   │   ├── Infrastructure/Migrations/…_Branches.cs           # table + UX_Branch_OneMain + CreateClinicIsolation
│   │   └── Infrastructure/Branches.cs                        # IBranches
│   └── Scs.ClinicData.Contracts/IBranches.cs
├── modules/Tenancy/
│   ├── Scs.Tenancy/Domain/Clinic.cs · ShortName.cs           # profile fields, states, Derive
│   ├── Scs.Tenancy/Infrastructure/ClinicRegistry.cs          # add turn, register, activate, discard, update, assign
│   ├── Scs.Tenancy/Infrastructure/Migrations/…_ClinicProfile.cs
│   ├── Scs.Tenancy/TenancyModule.cs                          # seed through ClinicRegistration
│   └── Scs.Tenancy.Contracts/…                               # ClinicStates, ClinicRegistration, ClinicProfile, IClinicRegistry
├── modules/Identity/
│   ├── Scs.Identity/Domain/StaffAccount.cs                   # FullName, Mobile, LastSignInAtUtc, ChangeEmail
│   ├── Scs.Identity/Infrastructure/ClinicOwners.cs           # NEW: IClinicOwners
│   ├── Scs.Identity/Infrastructure/OperatorClinicOwners.cs   # NEW: crossing read-clinic-owners-as-operator
│   ├── Scs.Identity/Infrastructure/AccountEmails.cs          # + EmailChanged; IdentityEmailSamples
│   ├── Scs.Identity/Infrastructure/SessionService.cs         # LastSignInAtUtc on open
│   ├── Scs.Identity/Infrastructure/Migrations/…_OwnerDetails.cs
│   └── Scs.Identity.Contracts/StaffContracts.cs              # IClinicOwners, records, SessionEndings.EmailChanged
├── modules/Subscriptions/
│   ├── Scs.Subscriptions/Domain/ClinicStatus.cs              # NEW: trial / active
│   ├── Scs.Subscriptions/Domain/ClinicPlan.cs                # TrialEndsAtUtc
│   ├── Scs.Subscriptions/Infrastructure/ClinicPlans.cs       # StartAsync, StateAsync, PLAN_DISABLED
│   ├── Scs.Subscriptions/Infrastructure/ClinicOnboarding.cs  # NEW: the add (R2) and its undo
│   ├── Scs.Subscriptions/Infrastructure/ClinicsReading.cs    # NEW: list join, cards, facets, sort, pages
│   ├── Scs.Subscriptions/Infrastructure/OperatorClinicPlans.cs # NEW: crossing read-clinic-plans-as-operator
│   ├── Scs.Subscriptions/Infrastructure/PlanSubscribers.cs   # NEW: IPlanSubscribers
│   ├── Scs.Subscriptions/Api/OpsClinicScope.cs               # NEW: establish the clinic or NOT_FOUND
│   ├── Scs.Subscriptions/Api/OpsClinicsEndpoints.cs          # NEW: the 8 routes
│   └── Scs.Subscriptions/Infrastructure/Migrations/…_Trial.cs
├── modules/Operators/…                                       # IOperatorDirectory.ActiveAsync
├── modules/Plans/…                                           # Specialties from the kernel
├── shared/Scs.SharedKernel/Clinics/Specialties.cs            # NEW (moved)
├── shared/Scs.SharedKernel/ErrorCodes.cs · Audit/AuditActions.cs · Audit/audit-actions.lock
├── shared/Scs.BuildingBlocks/Tenancy/ClinicContext.cs        # ClinicSource.Operator
├── shared/Scs.BuildingBlocks/Tenancy/Crossing.cs             # 2 new crossings + update-clinic
├── shared/Scs.BuildingBlocks/Audit/AuditWriter.cs            # operator actor in the clinic trail; RecordForClinicAsync
├── shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs         # 7 codes, field words
├── shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs     # Subscriptions.TrialDays
├── host/Scs.Api/ModuleRegistration.cs                        # + ClinicData
├── tools/Scs.Migrator/…                                      # + ClinicData migrations
└── tests/
    ├── Scs.UnitTests/{Tenancy/ShortNameDeriveTests, Subscriptions/ClinicStatusTests, Tenancy/ClinicProfileTests}
    ├── Scs.IntegrationTests/Clinics/                         # routes, add, refusals, undo, races, detail, edit, owner email, invitation, manager, list, plans' figures, timing
    ├── Scs.IntegrationTests/Isolation/ClinicDataSamples.cs   # BranchSample
    ├── Scs.ArchitectureTests/                                # ClinicData boundaries; DeclaredCrossingsTests 11
    └── (pins: error catalogue, audit actions, route audit declarations, route permissions, email samples)

deploy/compose/sql/create-logins.sql                          # grants for schema clinicdata
deploy/compose/.env.example · docs/setup.md                   # SCS_SUBSCRIPTIONS__TRIAL_DAYS

src/web/projects/
├── ui/src/lib/table/sort-header.directive.ts                 # NEW scsSortHeader (+ gallery section)
├── ui/src/lib/copy/copy-button.component.ts                  # NEW scs-copy-button (+ gallery section)
├── ops/src/app/shell/sections.ts                             # + clinics
├── ops/src/app/app.routes.ts                                 # + clinics routes (new before :id)
├── ops/src/app/access/ops-access.service.ts                  # OpsCan.clinicsView / clinicsEdit
├── ops/src/app/i18n/ops-format.ts                            # opsCairoDate, opsCairoDateTime
├── ops/src/app/clinics/                                      # NEW
│   ├── clinics.service.ts · clinic-words.ts
│   ├── clinics-list.page.ts
│   ├── clinic-form.page.ts
│   ├── clinic-detail.page.ts · clinic-overview.tab.ts · clinic-settings.tab.ts
│   └── *.spec.ts
└── ops/public/i18n/{ar,en}.json                              # app.clinics.*, app.shell.nav.clinics
src/web/e2e/
├── ops-clinics-platform.ts                                   # NEW fake API
├── ops-clinics.spec.ts                                       # NEW
└── emails (samples include the notice)
docs/adding-a-module.md (if ClinicData shows a gap) · docs/database-changes.md (the setting-up state)
```

**Structure Decision**:

- **`ClinicData`** follows the `Subscriptions` layout: domain, infrastructure, API (none yet), and
  a contracts project.
- **The onboarding orchestrator** is in `Subscriptions`. It calls only published contracts:
  - Tenancy's `IClinicRegistry` and `IClinicProfile`;
  - Identity's `IClinicOwners` and `IAccountHandover`;
  - ClinicData's `IBranches`;
  - Plans' `IPlanBook`;
  - Operators' `IOperatorDirectory`.
- **The ops pages** follow `plans/*`. The two missing controls go into `ui` with gallery entries,
  and no third-party component is added.

## Complexity Tracking

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A `setting-up` state and a best-effort undo instead of one database transaction | The add spans the crossing login (`tenancy`) and the application login (four modules' tables). No connection or transaction spans both. | One shared connection per scope changes how 47 places open transactions. The whole add on the crossing login would have one module write others' tables. Distributed transactions are unavailable on Linux. |
| Two audit entries for an edit that changes both the clinic and its owner | Each entry must be in the unit of its change (constitution IV). | One entry after both parts would leave the first change unrecorded if the second failed. |
| Three cross-clinic reads for the list | The list must search and filter on data owned by three modules. | A denormalised copy of the owner's contact in `tenancy` would be a second source of truth, out of date after every edit. |
