# Implementation Plan: The Feature Catalogue

**Branch**: `012-feature-catalog` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-feature-catalog/spec.md`

## Summary

The platform gets the catalogue that plans will be made of. A new `Plans` server module holds
one platform-wide table of features — key, both languages' names and descriptions, group, type
(switch or limit), units and baseline for a limit, specialty, built or upcoming, sold, retired,
order — seeded with the 35 features of the design and the BRD, every one «قادمة». Its addresses
under `/v1/ops/features` read with «عرض الباقات والفواتير» and write with «إدارة الباقات
والاشتراكات», every write recorded in the platform's trail. The operators console gets the
section «الباقات», which opens the catalogue until spec 013 builds the plans page: four cards,
the table with search, filter, chips and paging, the add and edit form, «أين تُستعمل», «إيقاف
الميزة…» and «إعادة بيع الميزة».

The module also publishes, in its contracts, the one read later specs need — "what is this
key" — so plans (013) and their enforcement (014) never repeat the catalogue. Nothing a clinic
sees changes.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new as packages. EF Core, the operators' gate and the audit
writer of spec 011; in the console the `ui` and `core` libraries (spec 095's table, toolbar and
filter, chips, form controls, confirmation, empty and error states), Transloco and Tailwind.

**Storage**: SQL Server 2022 — schema `plans`, table `plans.Feature` (new, platform-wide, listed
with its reason). Seeded by the migration that follows the one creating it (research R4). No clinic-owned table.

**Testing**: xUnit on the SQL Server fixture: access tests first for every address (each
refused without its permission); the feature rules' unit tests (key, units, baseline, order,
retire and sell again); the seed's tests (35 rows, idempotent, edited rows untouched); an
architecture test that every `/v1/ops/features` route names its permission. Vitest for the
console's service and components; Playwright for every screen, dialog and state in both
languages at 1440 and 390, with the platform intercepted; a real-stack walk per `quickstart.md`.

**Target Platform**: the platform API; the operators console (`ops`, 4203 locally).

**Project Type**: web application — a new server module with its contracts project, its
migration and endpoints; a feature folder in the `ops` application; one line in the console's
sections; icons through the icon tool.

**Performance Goals**: the catalogue (35 to a few hundred rows) reads in under a second; search
and filter work on the loaded catalogue without a round trip; a save is back in under a second.

**Constraints**: deny by default (SH-056); the key never changes once made (ق-٤٦); a limit has
both units and a switch has neither unit nor baseline, refused by the server (OP-A-215);
retiring never changes a plan or a clinic (ق-٤٨); every write in the platform's trail; the seed
never overwrites an operator's edit; no clinic-owned data.

**Scale/Scope**: 28 functional requirements (with FR-008a and FR-021a), 5 user stories; 1 table, 35 seeded
rows; 7 addresses; 2 pages (catalogue, add/edit form) and 3 dialogs (where used, retire, sell
again) in two languages; 4 recorded actions; 1 section in the side bar; 3 icons.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, differences recorded | Requirements cite module 16 (§3-6, §4-3, §5-47 … §5-49, §5-61, §6-14, §7-12, §7-19, ق-٣٥ … ق-٤٩, OP-A-209 … OP-A-223, OP-A-263) and SH-010 … SH-013, SH-021; the frames `2503:1262`, `2509:1305`, `2783:30449`, `2783:31078` and their English twins. Four frame/BRD differences and the two undrawn things (sell again, its messages) are in the spec with which side is followed and go to `validation.md` |
| II. Tenant isolation in the database (non-negotiable) | Pass | No clinic-owned table. `plans.Feature` is product data of the whole platform — names and rules of capabilities, no clinic's data — and is added to `PlatformWideTables` with its reason; the isolation guardrail test proves the list is complete |
| III. The server decides; plans from a database catalogue | Pass — this spec builds the catalogue | Every route names «عرض الباقات والفواتير» or «إدارة الباقات والاشتراكات» through the operators' gate; a feature is added without a code change (SH-010); no limit value is hard-coded (baselines are rows); the published "what is this key" read is the single source for 013 and 014 |
| IV. Financial records are immutable; changes are audited | Pass | No money moves. Four actions (the spec's `feature_created` … `feature_resumed`, coded `feature.created`, `feature.updated`, `feature.retired`, `feature.resumed` as the trail's convention and its lock file ask — research R11) are written to the append-only `audit.PlatformEntry` with the values before and after |
| V. Secrets and patient data | Pass | No secret, no patient data, no personal data in the table; logs carry identifiers and keys only |
| VI. Bilingual, complete screens | Pass | Both pages and three dialogs in Arabic and English, right to left in Arabic, Western digits; loading, empty, nothing-matching, failed and no-access states; every word from the frames and the BRD, the undrawn ones written and listed |
| VII. Tested before merge, verified after implementation | Pass | Access tests first; the rules' and the seed's tests; full gates; the real-stack walk of `quickstart.md`; every screen beside its frame in both languages; `design-vs-real.png` and `validation.md` close the work |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged. Three things stated plainly: a new module
and schema are added (`Plans`, `plans`), as "one module per BRD area" asks for module 16's plans
and catalogue; the operators' permission catalogue does not change (its two billing codes start
guarding routes, which spec 011's "every code guards a route" test then requires of them); and
the console's «الباقات» section points at the catalogue only until spec 013 gives it the plans
page.

## Project Structure

### Documentation (this feature)

```text
specs/012-feature-catalog/
├── spec.md              # the specification (clarified 16 Oct 2026)
├── plan.md              # this file
├── research.md          # Phase 0 decisions
├── data-model.md        # the feature, groups, specialties, the seed
├── quickstart.md        # how to check it, automatically and by hand
├── contracts/
│   ├── features-api.md      # the seven addresses, their permissions, bodies and refusals
│   └── catalogue-screens.md # the screens, their states, words and test ids
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── modules/Plans/                                  # NEW module (module 16: plans and catalogue)
│   ├── Scs.Plans/
│   │   ├── PlansModule.cs                          # registration, endpoints, the seed
│   │   ├── Domain/Feature.cs                       # the feature and its rules
│   │   ├── Domain/FeatureGroups.cs, Specialties.cs # the closed lists of seven and fifteen
│   │   ├── Infrastructure/Persistence/PlansDbContext.cs
│   │   ├── Infrastructure/Seed/FeatureSeed.cs      # the 35 rows, in code, for the seed and its tests
│   │   ├── Infrastructure/Migrations/              # …_FeatureCatalog (the table), …_FeatureSeed (the rows)
│   │   ├── Infrastructure/FeatureCatalogue.cs      # reads, writes, retire, sell again
│   │   └── Api/FeaturesEndpoints.cs                # /v1/ops/features…
│   └── Scs.Plans.Contracts/FeatureContracts.cs     # "what is this key" for 013 and 014
├── shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs   # + plans.Feature
├── shared/Scs.SharedKernel/Audit/AuditActions.cs              # + four feature actions
├── shared/Scs.SharedKernel/Audit/audit-actions.lock           # + the four codes, at the end
├── host/Scs.Api/ModuleRegistration.cs              # module registered (+ project reference)
├── tools/Scs.Migrator/Migrator.cs                  # module registered (it owns a table)
└── tests/
    ├── Scs.UnitTests/Plans/                        # the feature's rules, the closed lists, the seed's rows
    ├── Scs.IntegrationTests/Plans/                 # access, the addresses, the seed, the trail
    └── Scs.ArchitectureTests/                      # picks the module up; every route named

src/web/projects/
├── ops/src/app/plans/                              # NEW feature folder
│   ├── features.service.ts                         # the catalogue's reads and writes
│   ├── feature-catalogue.page.ts                   # cards, toolbar, table, row menus
│   ├── feature-form.page.ts                        # add and edit
│   ├── where-used.dialog.ts · retire-feature.dialog.ts · resume-feature.dialog.ts
│   └── plans.routes.ts
├── ops/src/app/shell/sections.ts                   # + «الباقات» (billing.view)
├── ops/src/app/app.routes.ts                       # + /plans/features …
├── ops/public/i18n/{ar,en}.json                    # app.plans.*
└── ui/src/lib/icon/icon-registry.ts                # + gem, tag, barrier (the icon tool)
src/web/e2e/
├── ops-features.spec.ts                            # NEW: every screen and state, both languages
└── ops-team-platform.ts                            # + the catalogue's intercepted answers
```

**Structure Decision**: a new `Plans` module, following the `Operators` module's layout (domain,
infrastructure, API, a contracts project); the console's feature in `ops/src/app/plans/` built
only from `ui` and spec 095's parts — no new shared component is expected; if one is, it goes
to `ui` with its gallery entry (docs/adding-a-module.md).

## Complexity Tracking

No violation to justify.
