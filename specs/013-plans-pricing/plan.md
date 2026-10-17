# Implementation Plan: Plans and Their Prices

**Branch**: `013-plans-pricing` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013-plans-pricing/spec.md`

## Summary

The platform gets its plans. The `Plans` module of spec 012 gains two platform-wide tables —
`plans.Plan` (code derived once from the English name, names, description, four prices before
tax in EGP and USD, an optional badge held by one plan at most, active or disabled with its
reason) and `plans.PlanFeature` (what a plan includes from the catalogue, a limit or none). Its
addresses under `/v1/ops/plans` read with «عرض الباقات والفواتير» and write with «إدارة الباقات
والاشتراكات»; a save is checked whole against the catalogue and replaces the plan's grid in one
transaction with its audit record. The console's «الباقات» now opens the plans list (four cards,
search, a status filter with chips, 8 a page, the row menu by permission), with the add and edit
form and its grid, the plan's detail, and the disable dialog; the catalogue of 012 now counts
real plans.

The module publishes `IPlanBook` (what a plan includes, which plans may be given to a clinic) for
014–016, and `IPlanSubscribers`, answering "no clinics" until 015/016 supply the clinics, so the
screens' figures are built once. Nothing a clinic sees changes.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new as packages. EF Core, the operators' gate and the audit
writer (011), 012's catalogue; in the console the `ui` and `core` libraries (spec 095's cards,
toolbar and filter with chips, table with avatars, row menu, form controls, switch, select,
textarea, dialog, empty and error states), the console's format helpers (`ops-format.ts`),
Transloco and Tailwind.

**Storage**: SQL Server 2022 — schema `plans`, tables `plans.Plan` and `plans.PlanFeature` (new,
platform-wide, listed with their reason); one migration. Amounts `decimal(12,2)`. No clinic-owned
table; no seed (the BRD fixes no plan — SH-020).

**Testing**: xUnit on the SQL Server fixture: access tests first for every address; the grid's
rules first (plan entitlements — constitution VII): unknown key, limit on a switch, unsold or
retired newly included, replace, kept retired; the plan rules' unit tests (code derivation,
amounts, badge, states); concurrency (two adds with one code, two badges at once, stale
version); the trail's records; the published reads. Vitest for the console's service, words and
components; Playwright for every screen, dialog and state in both languages at 1440 and 390 with
the platform intercepted; the real-stack walk of `quickstart.md`; every screen beside its frame.

**Target Platform**: the platform API; the operators console (`ops`, 4203 locally).

**Project Type**: web application — new entities, a migration, endpoints and contracts in the
existing `Plans` module; new pages in `ops/src/app/plans/`; two icons through the icon tool.

**Performance Goals**: the list (a handful to a few dozen plans) and a plan's grid (35 to a few
hundred features) read in under a second; search, filter and paging on the loaded list without a
round trip; a save back in under a second.

**Constraints**: deny by default (SH-056); a save refuses rather than corrects and writes nothing
when it refuses (ق-٤٢); the grid is replaced, not merged (ق-٤٣); one badge at most, none on a
disabled plan, enforced by the database too (ق-٤٥); the code never changes (ق-٤٦); disabling
never moves a clinic (ق-٤٧); every write and its audit record in one transaction (constitution
IV); no limit or price hard-coded (SH-020); the `Plans` module does not reference `Tenancy`.

**Scale/Scope**: 30 functional requirements (with FR-009a), 6 user stories; 2 tables; 8
addresses; 3 pages (list, add/edit form, detail) and 1 dialog in two languages; 4 recorded
actions; 6 refusal codes; 2 published interfaces; 2 icons; 012's catalogue counts and side-bar
entry changed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, differences recorded | Requirements cite module 16 (§3-5, §3-7, §4-2, §5-43 … §5-46, §6-12, §6-13, §7-9 … §7-11, §7-19, ق-٣٥ … ق-٤٩, OP-A-187 … OP-A-208, OP-A-262) and SH-010 … SH-021; the frames `1180:70`, `1250:70`, `1253:5666`, `1202:70`, `1214:70`, `1214:601`, `2016:963` and their English twins. Six frame/BRD differences and the owner's eight answers are in the spec; the undrawn parts (USD prices, change note, badge, enable item, tax line) are marked *(not drawn)* and go to `validation.md` for the designer |
| II. Tenant isolation in the database (non-negotiable) | Pass | No clinic-owned table. `plans.Plan` and `plans.PlanFeature` are the product's offer — no clinic's data — and are added to `PlatformWideTables` with their reason; the guardrail test proves the list complete. Clinic figures come through `IPlanSubscribers`, which 015/016 implement inside their own isolation |
| III. The server decides; plans from a database catalogue | Pass — this spec builds the plans | Every route names its permission through the operators' gate; a plan is made from the catalogue's rows with no code change (SH-010); no price or limit hard-coded (SH-020); `IPlanBook` is the single read 014's enforcement point will build on |
| IV. Financial records are immutable; changes are audited | Pass | No money moves and no financial document is made. "Changes a plan" → `plan.created`, `plan.updated`, `plan.disabled`, `plan.enabled` in the append-only `audit.PlatformEntry`, before and after (the grid per feature), in the same transaction; a price change reaches only invoices issued later (016 onward reads the price when issuing) |
| V. Secrets and patient data | Pass | No secret, no patient or personal data in the tables; logs carry ids and codes only |
| VI. Bilingual, complete screens | Pass | Three pages and a dialog in Arabic and English, right to left in Arabic, Latin digits; loading, empty, no results, failed, view-only and offline states; every word from the frames and the BRD, the undrawn ones written and listed |
| VII. Tested before merge, test-first where it hurts, verified after implementation | Pass | Access tests and the grid's rules written first and seen to fail (permissions, plan entitlements); full gates; the real-stack walk; every screen beside its frame in both languages; `design-vs-real.png` and `validation.md` close the work |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged. Stated plainly: no new module or schema
(the `Plans` module and `plans` schema of 012 grow); two published interfaces are added to
`Scs.Plans.Contracts`, one of them (`IPlanSubscribers`) with a "no clinics" default that 015/016
replace — an inversion so the clinic can depend on the plan and never the other way; the side
bar's «الباقات» moves from the catalogue to the plans list, as 012 announced.

## Project Structure

### Documentation (this feature)

```text
specs/013-plans-pricing/
├── spec.md              # the specification (clarified 16 Oct 2026)
├── plan.md              # this file
├── research.md          # Phase 0 decisions R1–R15
├── data-model.md        # the plan, its grid, states, the published reads
├── quickstart.md        # how to check it, automatically and by hand
├── contracts/
│   ├── plans-api.md         # the eight addresses, permissions, bodies, refusals, records
│   └── plans-screens.md     # the screens, their states, words and test ids
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── modules/Plans/
│   ├── Scs.Plans/
│   │   ├── PlansModule.cs                          # + PlanBook, the default IPlanSubscribers, MapPlans
│   │   ├── Domain/Plan.cs                          # NEW: the plan, its rules, code derivation, states, badge
│   │   ├── Domain/PlanLists.cs                     # NEW: badges, states
│   │   ├── Infrastructure/Persistence/PlansDbContext.cs   # + Plan, PlanFeature, constraints, indexes
│   │   ├── Infrastructure/Migrations/              # + …_Plans
│   │   ├── Infrastructure/PlanBook.cs              # NEW: reads, add, edit, disable, enable, audit; IPlanBook
│   │   ├── Infrastructure/NoSubscribers.cs         # NEW: IPlanSubscribers' "no clinics" default
│   │   ├── Infrastructure/FeatureCatalogue.cs      # counts plans; PlansOfAsync lists them
│   │   └── Api/PlansEndpoints.cs                   # NEW: /v1/ops/plans… ({id:guid})
│   └── Scs.Plans.Contracts/PlanContracts.cs        # NEW: IPlanBook, IPlanSubscribers and their records
├── shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs   # + plans.Plan, plans.PlanFeature
├── shared/Scs.BuildingBlocks/Audit/AuditRecord.cs             # + AuditTarget.Plan
├── shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs          # + six PLAN_* words
├── shared/Scs.SharedKernel/ErrorCodes.cs                      # + six PLAN_* codes
├── shared/Scs.SharedKernel/Audit/AuditActions.cs              # + four plan actions
├── shared/Scs.SharedKernel/Audit/audit-actions.lock           # + the four codes, at the end
└── tests/
    ├── Scs.UnitTests/Plans/                        # + PlanTests (code, amounts, badge, states, grid rules)
    ├── Scs.IntegrationTests/Plans/                 # + access, add, edit, grid, badge, disable, trail, published reads, catalogue counts
    └── (pins of earlier specs: routes, audit actions, error catalogue)

src/web/projects/
├── ops/src/app/plans/
│   ├── plans.service.ts                            # NEW: the plans' reads and writes
│   ├── plan-words.ts                               # NEW: users' limit, money, counts, states
│   ├── plans-list.page.ts                          # NEW: cards, toolbar, table, row menus
│   ├── plan-form.page.ts                           # NEW: add and edit, the grid
│   ├── plan-detail.page.ts                         # NEW: information, features, subscriptions, clinics
│   ├── disable-plan.dialog.ts                      # NEW
│   └── feature-form.page.ts · feature-catalogue.page.ts   # breadcrumb → /plans
├── ops/src/app/shell/sections.ts                   # «الباقات» → /plans
├── ops/src/app/app.routes.ts                       # + /plans, /plans/new, /plans/:id, /plans/:id/edit
├── ops/src/app/operators/activity-sentences.ts     # + the plan.* kinds
├── ops/public/i18n/{ar,en}.json                    # + app.plans.list|form|detail|disable …
└── ui/src/lib/icon/icon-registry.ts                # + trophy, coins (the icon tool)
src/web/e2e/
├── ops-plans.spec.ts                               # NEW: every screen and state, both languages
├── ops-plans-platform.ts                           # NEW: the plans' intercepted answers
└── ops-features-platform.ts · ops-features.spec.ts # plans counted in the catalogue's stand-in
```

**Structure Decision**: the plans grow inside the `Plans` module, following 012's layout
(domain, infrastructure, API, contracts project); the console's pages in `ops/src/app/plans/`
built only from `ui` and spec 095's parts — no new shared component is expected; if one is, it
goes to `ui` with its gallery entry (docs/adding-a-module.md). Shared `ui` files are edited by
hand only, never by a formatter (012's lesson).

## Complexity Tracking

No violation to justify.
