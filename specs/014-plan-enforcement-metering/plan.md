# Implementation Plan: Plan Enforcement and Limit Metering

**Branch**: `014-plan-enforcement-metering` | **Date**: 2026-10-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-plan-enforcement-metering/spec.md`

## Summary

A clinic's plan starts to mean something.

**The `Subscriptions` module.** A new module holds:

- `subscriptions.ClinicPlan`: a clinic's link to its plan, clinic-owned, empty until 015;
- `subscriptions.MeterEntry`: the append-only ledger of the four monthly limits, clinic-owned,
  with the month worked out in Cairo;
- `IClinicEntitlements`, the one answer to "what does this clinic have". It is worked out per
  request from the link, 013's `IPlanBook`, 012's catalogue and the clinic's specialty, by one
  pure function that holds the BRD's rules ق-٣٧ … ق-٤١ and ق-٤٧ … ق-٤٩.

**The two refusals.** Both live in the building blocks, so any module can use them without
referencing another:

- `RequiresFeature(key)` on a route, read by a plan gate placed after the access gate:
  `PLAN_FEATURE_NOT_INCLUDED`;
- `PlanLimits.EnsureRoomAsync`, which takes a lock per clinic and feature, lets the owner count,
  and refuses with `PLAN_LIMIT_REACHED`.

**Identity.** Identity wires the users limit into every way an account comes to count: create,
re-enable, re-invite, re-issue. So 026 and the operators' actions of 015/017 inherit it.
`max_users` is marked built.

**The clinic console.** It learns its withheld features from the session answer, re-read on
opening a feature page. Its not-in-plan page draws the frames' card: «الباقة الحاليّة لا تشمل …»,
with «فتح الملف والاشتراك» only for holders of «عرض الاشتراك».

**What stays at the counting specs.** The other seven limits are readable and the monthly ones are
metered. Their counting and consuming come with the specs that build what they count.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: no new package.

- Server: EF Core, the access gate (005), the audit writer (007), the problem writer, 012's
  catalogue and 013's `IPlanBook`.
- Clinic console: the `ui` and `core` libraries (`scs-empty-state`, the shell's `PlanSource`,
  `AccessService`), Transloco and Tailwind.

**Storage**: SQL Server 2022. Three migrations:

- **`Subscriptions`** (new schema `subscriptions`): `ClinicPlan` and `MeterEntry`, both
  clinic-owned, with the row level security predicate and policy in that migration.
- **`Tenancy`**: `Clinic.Specialty nvarchar(40) NULL`.
- **`Plans`**: the data migration `MaxUsersBuilt`.

No new platform-wide table.

**Testing**:

- **Test-first** (plan entitlements and limits, constitution VII): the entitlement rules, the plan
  gate, the seat checks and the race, the meter's refusals and its race. These are written and
  seen to fail before the code.
- **Also**:
  - the isolation guardrail and test for the two new tables;
  - the architecture rules for the new module;
  - the error catalogue and audit pins;
  - the session answer;
  - a timing test (SC-008).
- **Web**: Vitest for `PlanSource`, the destination table and the card. Playwright for the card
  in both languages at 1440 and 390, beside its frames.
- **The real-stack walk** of `quickstart.md`.

**Target Platform**: the platform API; the clinic console (`clinic`, 4201 locally).

**Project Type**: web application.

- A new server module.
- Small, additive changes in `Identity`, `Tenancy`, `Plans`, the building blocks and the host.
- The clinic shell's not-in-plan page and plan source.

**Performance Goals**: the check answers within 50 ms for 95% of requests on the local stack
(SC-008). It is four indexed reads, memoised per request, with no cross-request cache, so a saved
plan applies to the next check (SC-005).

**Constraints**:

- The permission is decided before the plan (FR-009).
- Refuse before writing; nothing changes on a refusal (FR-008, FR-011).
- Never backwards (ق-٤١).
- One success per last unit (ق-أ-١٩).
- An upcoming feature is never enforced (ق-٤٩).
- No limit hard-coded (SH-020). The eight keys are the BRD's fixed list; their numbers live in
  the database.
- `Plans` never reads a clinic, and building blocks never reference a module.
- Every write to the link and its audit record are in one transaction (constitution IV).
- Words in «الباقة», never «الخطة», with no gendered address (constitution VI).

**Scale/Scope**:

- 31 functional requirements and 6 user stories.
- 1 new module, 2 tables, 3 migrations.
- 5 published interfaces (`IClinicEntitlements`, `IClinicPlans`, `IMonthlyMeter`,
  `IClinicLimits`, `ILimitCounter`) and 2 building-block mechanisms.
- 4 error codes and 1 recorded action.
- 1 field on the session answer and 1 local-only route.
- 1 page changed in the clinic console, 7 destinations naming features.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. The BRD and the design file are the source of truth

**Pass.**

- The requirements cite Shared §4 (SH-014 … SH-018, read with SH-010 … SH-012, SH-019 … SH-021,
  SH-097), Ops module 16 (ق-٣٦ … ق-٤١, ق-٤٧ … ق-٤٩, ق-١٠٣) and Clinic modules 01, 03, 06, 07, 10
  and 12 (the not-in-plan ruling, the users count, CL-AD-029/044, ق-أ-١٩).
- The frames are «Inbox / Not In Plan» and «Financial / Not In Plan», RTL and LTR. The frames of
  later specs are named and not built.
- The owner's six answers are in the spec. The frame's «الخطة» is a designer item. The undrawn
  card without its link and the undrawn feature words are listed for the designer.

### II. Tenant isolation in the database (non-negotiable)

**Pass.**

- `subscriptions.ClinicPlan` and `subscriptions.MeterEntry` are clinic-owned (`IClinicOwned`),
  with the row level security policy in the migration that creates them.
- The guardrail lists them, and the isolation test proves that clinic A cannot read or write B's
  link or meter from the application's identity.
- No bypass:
  - 015 sets the link inside the new clinic's context (`IClinicProvisioner`);
  - the development route establishes the named clinic first;
  - the eight-limit read and the entitlements require an established clinic.

### III. The server decides: permissions and plan entitlements

**Pass. This spec builds the one enforcement point.**

- `IClinicEntitlements` is the single read, and both refusals are one code each (SH-014, ق-٤٠).
- The limit is checked in the owning module's write, whatever the entry point, clinic or
  operator (SH-017). The plan gate runs after the permission gate.
- Nothing is hidden in place of refusing: the console's card mirrors the server.

### IV. Financial records are immutable; changes are audited

**Pass.**

- No money moves.
- Changing a clinic's plan link is a plan change. It records `subscription.plan-set` in the
  clinic's and the platform's trail, before and after, in the same transaction.
- The meter is append-only (a give-back is a new row).

### V. Secrets and patient data

**Pass.**

- No secret and no patient data. The meter's `Purpose` and `Reference` are codes and ids, and
  `By` is an id.
- Logs carry ids and codes only.

### VI. Bilingual, complete screens

**Pass.**

- The not-in-plan card in Arabic and English, right to left in Arabic, Latin digits.
- Its two variants (with and without the link).
- Words from the frames and the owner, in «الباقة».
- The page keeps its header. The state is the screen state SH-097 names, and other screens' states
  are unchanged.

### VII. Tested before merge, test-first where it hurts, verified after implementation

**Pass.**

- Plan entitlements and limits are written first and seen to fail.
- Refusal paths are tested: not included, limit reached, the permission first, and the race.
- Full gates, the real-stack walk, and the card beside its frames in both languages.
- `validation.md` and `design-vs-real.png` close the work.

**Result**: gate passed.

**Post-design re-check** (after Phase 1): passed. Stated plainly:

- **A new module, `Subscriptions`, with schema `subscriptions`.** Specs 015–021 grow it (research
  R1).
- **Changed in built specs' code, recorded in research.md, their documents untouched**
  (constitution 1.3.0):
  - 002 gains `Specialty`;
  - 003 and 004 gain the seat check and the session's `plan`;
  - 006's `PlanSource`, destinations and not-in-plan page;
  - 012's `IFeatureCatalogue.AllAsync` and `max_users` built.
- **Building blocks gain the plan gate and `PlanLimits`.** They depend on two small interfaces
  that the module implements, so no reference runs from building blocks to a module.

## Project Structure

### Documentation (this feature)

```text
specs/014-plan-enforcement-metering/
├── spec.md              # the specification (clarified 17 Oct 2026)
├── plan.md              # this file
├── research.md          # Phase 0 decisions R1–R15, and what changes in built specs' code
├── data-model.md        # the link, the meter, the specialty, the rules table, refusals
├── quickstart.md        # how to check it, automatically and by hand
├── contracts/
│   ├── enforcement-api.md     # the interfaces, the gate, the limit helper, the session field, codes
│   └── not-in-plan-screen.md  # the clinic console's card, its words, states and tests
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── modules/Subscriptions/                                   # NEW module (docs/adding-a-module.md)
│   ├── Scs.Subscriptions/
│   │   ├── SubscriptionsModule.cs                           # registrations, migrations, MapDevClinicPlan
│   │   ├── Domain/Entitlements.cs                           # the pure rules (R4)
│   │   ├── Domain/CairoMonth.cs                             # yyyy-MM in Africa/Cairo
│   │   ├── Domain/ClinicPlan.cs · MeterEntry.cs             # the two rows
│   │   ├── Infrastructure/Persistence/SubscriptionsDbContext.cs
│   │   ├── Infrastructure/Migrations/                       # …_Subscriptions (tables + RLS)
│   │   ├── Infrastructure/ClinicEntitlements.cs             # IClinicEntitlements, IPlanAnswers
│   │   ├── Infrastructure/ClinicPlans.cs                    # IClinicPlans + audit
│   │   ├── Infrastructure/MonthlyMeter.cs                   # IMonthlyMeter
│   │   ├── Infrastructure/ClinicLimits.cs                   # IClinicLimits (ILimitCounter[] + meter)
│   │   └── Api/DevClinicPlanEndpoints.cs                    # POST /v1/dev/clinic-plan (local only)
│   └── Scs.Subscriptions.Contracts/                         # the five interfaces and their records
├── modules/Identity/Scs.Identity/
│   ├── Infrastructure/Seats.cs                              # NEW: count + EnsureSeatAsync; ILimitCounter(max_users)
│   ├── Infrastructure/StaffAccounts.cs · AccountHandover.cs # call Seats at the five places (R8)
│   └── Api/AuthEndpoints.cs                                 # session answer + plan
├── modules/Tenancy/…                                        # Clinic.Specialty, ClinicProfile.Specialty, migration
├── modules/Plans/…                                          # IFeatureCatalogue.AllAsync; migration MaxUsersBuilt
├── shared/Scs.BuildingBlocks/Plans/PlanGate.cs              # NEW: RequiresFeature, UseScsPlanGate, IPlanAnswers
├── shared/Scs.BuildingBlocks/Plans/PlanLimits.cs            # NEW: EnsureRoomAsync, PlanLimitReachedException
├── shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs        # + four codes' words
├── shared/Scs.BuildingBlocks/Audit/AuditRecord.cs           # + AuditTarget.Clinic
├── shared/Scs.SharedKernel/ErrorCodes.cs                    # + four codes
├── shared/Scs.SharedKernel/Plans/FeatureKeys.cs             # NEW: keys code refers to; CountedLimits
├── shared/Scs.SharedKernel/Audit/AuditActions.cs · audit-actions.lock   # + subscription.plan-set
├── host/Scs.Api/Program.cs                                  # + module, UseScsPlanGate after the access gate
└── tests/
    ├── Scs.UnitTests/Subscriptions/                         # Entitlements, CairoMonth
    ├── Scs.IntegrationTests/Subscriptions/                  # gate, declarations, seats, race, meter, link, limits, session, timing, isolation
    ├── Scs.ArchitectureTests/                               # the module's boundaries
    └── (pins: error catalogue, audit actions/facts, route audit declarations, isolation guardrail)

src/web/projects/
├── core/src/lib/auth/auth.service.ts                        # StaffSession.plan
├── core/src/lib/shell/sources.ts                            # PlanSource reads session.plan.withheld (any-of)
├── clinic/src/app/shell/destinations.ts                     # feature per destination (R13)
├── clinic/src/app/shell/shell.routes.ts                     # outsidePlan re-reads the session first
├── clinic/src/app/shell/pages.ts                            # NotInPlanPage: the frames' card (R14)
└── clinic/public/i18n/{ar,en}.json                          # app.plan.notIn.*, app.plan.places.*
src/web/e2e/
├── not-in-plan.spec.ts                                      # NEW
├── staff-platform.ts                                        # session answer + plan
└── placeholder.spec.ts · shell-banners.spec.ts · shell-states.spec.ts   # driven by session.plan
docs/setup.md · docs/database-changes.md · docs/adding-a-module.md (if the new module shows a gap)
```

**Structure Decision**:

- `Subscriptions` follows the `Plans` layout: domain, infrastructure, API, and a contracts
  project.
- Building blocks host the two generic mechanisms. Each depends on an interface the module
  implements (`IPlanAnswers`: `IsWithheldAsync` and `LimitOfAsync`), so every module can refuse
  without referencing `Subscriptions`' internals.
- The clinic console changes only its shell.
- No new shared component is expected: the card is `scs-empty-state`. If its spacing needs an
  input, it is added to `ui` by hand with its gallery entry.

## Complexity Tracking

No violation to justify.
