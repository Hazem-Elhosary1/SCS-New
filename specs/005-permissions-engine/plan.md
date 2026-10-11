# Implementation Plan: Permissions Engine

**Branch**: `005-permissions-engine` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-permissions-engine/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Decide what a signed-in person may do. One catalogue of 83 permissions; a role is a clinic's
named set of them; every account holds at most one role. Every route says who may call it and is
refused for everyone until it does; a missing permission is refused before anything is read, and
the refusal names the permission. A write cannot be stored without the view it needs. The screen
hides what a person may not use and shows the two drawn states: "not in your role" and "view
only". Every clinic gets three ready roles; later specs get the operations to store roles and
assign them.

Technical approach, one line each (detail in [research.md](research.md)):

- The catalogue is code in the shared kernel, generated once from the BRD table and held there
  by tests; a constant per code makes a misspelt code a compile error.
- Declaring is endpoint metadata; enforcing is one gate after authorization. A route that
  declares nothing answers 403 for everyone, and an integration test over the real route table
  fails naming it.
- Roles are two clinic-owned tables in `Identity`, and a `RoleId` on the account. The owner role
  stores no rows: it holds whatever the catalogue holds.
- Permissions are read once per request and never cached between requests, so a change to a
  role is seen by the next request.
- The migration that creates the tables also gives every existing clinic its ready roles and its
  oldest account the owner role; a new clinic gets its roles when it is registered.
- The client gets the catalogue as a generated file and the person's codes with the session;
  a destination is two routes on one path, so a page the person may not open is never loaded.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new. ASP.NET Core routing and authentication; EF Core; Angular
router, Transloco, Tailwind and the shared components of specs 003 and 004.

**Storage**: SQL Server 2022, schema `identity`: two new clinic-owned tables (`Role`,
`RolePermission`), one new column on `StaffAccount`. One migration, which also fills the tables
for existing clinics. The catalogue is not stored.

**Testing**: xUnit on the SQL Server Testcontainers fixture as the application login; NetArchTest;
Angular unit tests; Playwright for the five screen states in both languages, with pictures for
the design comparison. Test-first for route declaration, the gate over all 83 codes, the
requirement rule, immediacy of a role change, session ending on assignment, the last owner, and
the roll-out migration.

**Target Platform**: as spec 001. Nothing new locally.

**Project Type**: web application — additions to the shared server projects, to the `Identity`
module, and to the core, ui and clinic web projects.

**Performance Goals**: the check adds one indexed read on routes that need a permission; target
under 5 ms at the median on the developer machine, measured against an own-account route
(SC-006). Routes open to everyone pay nothing.

**Constraints**: refuse by default; the permission is checked before the handler; the refusal
says nothing about a record; no cache of permissions between requests; a clinic can never add to
the catalogue; role names and codes may be logged, no personal data in the refusal log.

**Scale/Scope**: about 2,000 staff accounts and a few roles per clinic. Scope here is 36
functional requirements, 83 catalogue rows, 4 route declarations, 1 gate, 2 tables, 6 operations
for later specs, 1 new endpoint and 1 changed, 5 error codes, 2 shared screen parts, 1 route
with 5 states.

**Open items carried from research** (defaults chosen): the «مقترحة» tag in the frames; how a
custom role's name shows in English; the roles address before spec 006; BRD and design updates
(codes of rows 80–83, "next request", the roll-out rule, 79 → 83). Settled on 11 Oct 2026: the
four codes; effect at the next request; oldest account becomes owner and seeds assign the rest;
the refusal names the permission.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite SH-050 to SH-056 and Chapter 10; the catalogue is generated from the BRD table, not typed. The spec names the file, the page and four frames; wording is quoted from them. Undrawn and listed as such: the console bars around the states (spec 006), the "no role" sentence. **Open**: three owner decisions not yet in the BRD; the frames count 79; the «مقترحة» tag |
| II. Tenant isolation in the database (NON-NEGOTIABLE) | Pass | Both tables are clinic-owned with the policy of spec 002 in the creating migration and an isolation sample each. No new crossing: a new clinic's roles are created in a scope with that clinic established, not on the operator connection (R12). The filling of existing clinics is part of the migration, run by the migration login like every schema change (R13). A permission never widens isolation |
| III. The server decides | Pass | This spec is the principle's second half. Every route is refused unless it declares; the gate runs before the handler; the client's list is a convenience the server corrects. Nothing before sign-in changes: an unauthenticated caller still gets only 401 (SH-043). Assigning a role ends sessions in the same transaction (SH-045) |
| IV. Financial records immutable and audited | Not applicable | No money. Role operations return what changed, for the audit trail of spec 007 |
| V. Patient data, secrets and AI contained | Pass | No patient data and no secret. The refusal log carries identifiers and a code only |
| VI. Bilingual and complete screens | Pass | Both states in Arabic and English, mirrored, names read by assistive technology; permission names come from the catalogue in both languages |
| VII. Tested before merge, verified after implementation | Pass | Failing tests first for the items above, on a real SQL Server as the application login. After implementation: full suites, the real-stack walk of the quickstart with three seeded people, each state beside its frame in both languages, and `validation.md` saying what was run, walked, compared and not compared |
| Technology constraints | Pass | Shared kernel and building blocks for what every module needs; data access in Identity's Infrastructure; own components; Transloco |
| Workflow | Pass | The isolation and guardrail steps of the gates cover the new tables; the route-declaration test joins the gates |

**Result**: gate passed. The open items under Principle I block approval of the spec, not the
design.

**Post-design re-check** (after Phase 1): unchanged. Two things to state plainly. First, the
change of default: until now an undeclared route was open to any signed-in person; after this
spec it is closed to everyone — the two existing session routes are declared own-account in the
same change, and test probes that relied on the old default are updated. Second, one real
guarded route (`GET /v1/roles`) is added beyond what the spec lists, so that the real stack can
be walked (R15).

## Project Structure

### Documentation (this feature)

```text
specs/005-permissions-engine/
├── plan.md
├── research.md          # 19 decisions, 4 items to confirm
├── data-model.md        # catalogue shape, 2 tables, 1 column, migration, seed, 5 codes
├── quickstart.md        # 20 automated scenarios, 7 by hand, the timing measure
├── contracts/
│   ├── permission-catalogue.md     # the 83 rows, generated from the BRD
│   ├── access-engine.md            # declaring routes, refusals, IStaffAccess, IClinicRoles
│   ├── access-api.openapi.yaml     # session (changed), roles list (new)
│   └── access-screens.md           # frames, parts, wording, the roles address, 5 states
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/
│   ├── Access/                        # PermissionCatalogue, Permissions (constants),
│   │                                  # PermissionGroups, PermissionSets (the rule), ReadySets
│   └── ErrorCodes.cs                  # 5 new codes
├── shared/Scs.BuildingBlocks/
│   ├── Security/AccessControl.cs      # the declarations, IStaffAccess, IStaffAccessSource,
│   │                                  # IRecordOwnerCheck, UseScsAccessGate
│   ├── Security/StaffSessionAuthentication.cs   # the two own-account routes' metadata helper
│   └── Errors/ErrorMessages.cs        # titles for the 5 codes
├── modules/Tenancy/
│   ├── Scs.Tenancy.Contracts/         # IClinicProvisioner
│   └── Scs.Tenancy/Infrastructure/ClinicRegistry.cs   # calls provisioners after registering
├── modules/Identity/
│   ├── Scs.Identity/
│   │   ├── Domain/                    # Role, RolePermission; StaffAccount (+RoleId)
│   │   ├── Infrastructure/            # Migrations/ (one, with the roll-out), ReadyRoles,
│   │   │                              # ClinicRoles, StaffAccessSource, RolesProvisioner
│   │   ├── Api/AccessEndpoints.cs     # GET /v1/roles
│   │   ├── Api/AuthEndpoints.cs       # session answer + role and permissions; OwnAccountOnly
│   │   ├── Api/PasswordEndpoints.cs   # change: OwnAccountOnly
│   │   └── IdentityModule.cs          # registrations; seed roles
│   └── Scs.Identity.Contracts/        # IClinicRoles and its records
├── host/Scs.Api/Program.cs            # UseScsAccessGate after UseAuthorization
├── tools/Scs.Migrator/                # verb `catalogue <path>` writes the client file
└── tests/
    ├── Scs.UnitTests/Access/          # catalogue, sets, ready sets, client file
    ├── Scs.IntegrationTests/Access/   # route declaration, gate over 83 codes, order, role
    │                                  # change, assignment, role operations, owner, timing
    ├── Scs.IntegrationTests/Fixtures/ProbeEndpoints.cs   # one probe per code; owner probe
    ├── Scs.IntegrationTests/Isolation/IdentitySamples.cs # + 2 tables
    ├── Scs.IntegrationTests/Persistence/MigrationTests.cs # the roll-out
    └── Scs.ArchitectureTests/         # only the shared code decides access; modules reach
                                       # roles through IClinicRoles and IStaffAccess

src/web/projects/
├── core/src/lib/access/               # permission-catalogue.json (generated), access.service,
│                                      # holds guard, scsCan directive + specs
├── core/src/lib/auth/auth.service.ts  # session carries role and permissions; refresh; the
│                                      # interceptor handles PERMISSION_REQUIRED
├── ui/src/lib/no-access/              # scs-no-access + spec
├── ui/src/lib/controls/controls.ts    # scs-banner: tone `info`, title
├── ui/i18n/                           # ar.json, en.json — the two states, ready role names
├── clinic/src/app/administration/     # roles.page (not yet + notice), no-access.page
├── clinic/src/app/app.routes.ts       # /administration/roles, twice
├── clinic/public/i18n/                # breadcrumb, title, the view-only sentence
└── e2e/                               # access.spec, access-states.spec; staff-platform.ts
                                       # answers for session with role, /v1/roles

docs/adding-a-module.md                # "declare who may call every route"
docs/setup.md                          # the seeded roles; who sees which state
specs/001-platform-foundation/contracts/conventions.md   # the 5 codes
```

**Structure Decision**: the catalogue and the gate are shared code because every module names
codes and declares routes; roles are Identity's because a role is a fact about an account and
changing it must end sessions in one transaction. No new module, no new project.

**Terms**: the spec's "address" is a route; "open to any signed-in person" is `OwnAccountOnly`;
"fixed" is `HoldsEverything`; "effective permissions" is what `IStaffAccess` answers.

## Delivery Order

For `/speckit-tasks`. Tests first throughout.

1. **Catalogue** — failing unit tests for the table and the rule, then `Scs.SharedKernel/Access`
   from the contract table; the five error codes. Stories 3, 4.
2. **Declaring and the gate** — failing tests: every route declares, an undeclared probe is
   refused, the order of refusals; then the declarations, `IStaffAccess`, the gate, the two
   own-account routes, updated probes. Stories 1, 2.
3. **Schema** — the migration with policies, samples and the roll-out; its migration test.
   Story 3.
4. **Roles** — `ReadyRoles`, the provisioner hook in the clinic registry, `StaffAccessSource`;
   then the gate test over all 83 codes passes. Stories 1, 3.
5. **Operations** — failing tests for each refusal, the last owner, session ending and
   immediacy; then `IClinicRoles`. Stories 4, 6.
6. **Scope and owner** — `ReachOf`, `RequiresOrOwner` with its probe. Story 7.
7. **What the screen is told** — the session answer, `GET /v1/roles`, the seed, the migrator
   verb and the generated client file with its test. Stories 3, 5.
8. **Client engine** — `AccessService`, the guard, `scsCan`, the interceptor. Story 5.
9. **Screens** — `scs-no-access`, the banner's `info` tone, the roles destination, every state
   in both languages. Stories 5, 8.
10. **Gates and verification** — architecture rules, timing, docs; the real-stack walk, the
    design comparison, `validation.md`.

## Complexity Tracking

No constitution violations to justify.

Choices recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A gate of our own instead of framework policies | The refusal must name the permission and be written where the missing code is known; an undeclared route must be closed, not merely "authenticated" | 83 policies and a forbid handler that no longer knows which code failed |
| A generated client file checked by a test | The client needs names in two languages without a request, and one source | An endpoint breaks the own-account rule of FR-021; a hand copy drifts |
| The migration fills data for existing clinics | Roles must exist the moment the tables do, with no step to forget at deployment | A roll-out command needs both connections at deploy time |
| A provisioner hook in the clinic registry | A clinic must have its roles from the moment it exists, and Tenancy may not reference Identity or write its rows | Waiting for spec 015 to remember to call it leaves the seed and every test clinic without roles |
| The owner role stores no rows | A permission added later must be held by every owner at once, in every clinic | Inserting a row per clinic at each release is a data migration per catalogue change |
| One real guarded route (`GET /v1/roles`) | The real stack must be walked with and without a permission (constitution VII) | Probe routes exist only in tests |
| A custom role in the seed | All three ready roles hold `admin.roles.view`, so without it nobody can see the no-permission state | Testing that state only with a mocked session would not be the real stack |
