# Implementation Plan: Clinic Tenancy and Data Isolation

**Branch**: `002-clinic-tenancy-isolation` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-clinic-tenancy-isolation/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Make the clinic the owner of its data and make the separation between clinics a property of the
database. Every clinic-owned table carries `ClinicId` and a row-level security policy; the
clinic for a piece of work is set once on the connection, read-only, and with none set nothing
is visible or writable. A guardrail reads the database's own catalog after each migration and
fails the build for any table that is neither isolated nor declared platform-wide. Operators
cross the boundary on a separate login through five named operations, each recorded. The
patient site resolves a clinic from the first part of its address.

Technical approach, one line each (detail in [research.md](research.md)):

- One schema-bound predicate function, `tenancy.fn_ClinicAccess`, called by every policy with
  filter and block predicates.
- `sp_set_session_context N'ClinicId', @id, @read_only = 1` on every connection open, from a
  scoped `IClinicContext`.
- A second, weaker layer in EF Core (query filter, stamping, refusal to change `ClinicId`).
- Resolving a short name needs no privilege: the directory of names is platform-wide.
- Operator operations use the login `scs_ops`, a member of a role the predicate honours; the
  application login is not a member and cannot become one.
- Tests first, on a real SQL Server, with the application layer bypassed.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: EF Core (SQL Server provider), Microsoft.Data.SqlClient, Hangfire,
Dapper (introduced here, for the read-only connection contract only); Angular, Transloco. No
new front-end dependency.

**Storage**: SQL Server 2022. New schemas `tenancy` (module) and `crossing` (access records).
Row-level security policies; one inline predicate function; one database role; one new login.

**Testing**: xUnit with the SQL Server Testcontainers fixture from spec 001; NetArchTest for
declared crossings and connection rules; Angular unit tests and Playwright for the patient site.
Isolation tests are written first and run with raw SQL as the application login.

**Target Platform**: as spec 001.

**Project Type**: web application — server module plus a small change to the patient app.

**Performance Goals**: establishing the clinic adds under 5 ms to a request on the local
network; resolving a patient address answers in under 100 ms at the server (SC-006 allows half
a second as seen by the patient); no measurable difference between "another clinic's row" and
"no such row" (SC-005).

**Constraints**: fail closed in every path; the application login cannot disable or widen
isolation; one clinic per piece of work, enforced by the database; no patient data in the
access record or in logs; nothing in a module opens its own database connection.

**Scale/Scope**: 200 clinics with 50 working concurrently as the test target (SC-009); hundreds
of clinic-owned tables by spec 092, each costing one helper call and one sample. Scope here is
33 functional requirements, 1 endpoint, 3 tables, 1 module, 1 patient-app page.

**Open items carried from research** (defaults chosen): short-name format and no Arabic names;
earlier names kept forever; the starting reserved list; operator id before spec 010.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD is the source of truth | Pass, one open item | FRs cite SH-003, SH-004, SH-104. **Open**: SH-003 still states per-clinic subdomains; the spec follows constitution v1.1.0 and cannot be approved until the BRD is updated (spec 001, T110). Four defaults the BRD does not state are labelled assumptions |
| II. Tenant isolation in the database (NON-NEGOTIABLE) | Pass — this spec implements it | Row-level security on every clinic-owned table from the migration that creates it (R1, R6); fail closed because an unset context compares as unknown; an automated catalog check fails the build for an unprotected table (R6); an automated test proves isolation as the application login with raw SQL (R7); bypass limited to one role, two logins and four named operations, each justified (R4) |
| III. The server decides | Pass | The clinic is established on the server; the patient header only selects a public page and is refused when it disagrees with a session (R8). The one new endpoint is explicitly anonymous; operator operations have no route until operator sign-in exists |
| IV. Financial records immutable and audited | Not applicable | No money. The cross-clinic access record is append-only by the same reasoning |
| V. Patient data, secrets and AI contained | Pass | Access records and logs carry identifiers only; the new connection string is a secret read from the environment; platform-wide tables hold no patient data by rule |
| VI. Bilingual and complete screens | Pass | The "not available" page and the clinic name on the placeholder use translation keys in both languages and the shared state component; the clinic has an Arabic and an English display name |
| VII. Tested before merge, test-first where it hurts | Pass | Isolation is a test-first area: guardrail and behaviour tests are written and fail before the policy helper exists (R15). Refusal paths covered: no clinic, wrong clinic, conflicting clinic, unknown name. Real SQL Server only |
| Technology constraints | Pass | Row-level security keyed on `SESSION_CONTEXT`, set per request; EF Core for writes; Dapper read-only on a connection carrying the context; a job sets its clinic before touching data and a multi-clinic job handles one clinic at a time; no per-clinic subdomain; module boundaries kept (shared primitives in BuildingBlocks, the clinic in its own module) |
| Workflow | Pass | The isolation guardrail and test become a named required step in the pull-request gates, as the constitution lists them |

**Result**: gate passed. One open item under Principle I blocks approval of the spec, not the
design.

**Post-design re-check** (after Phase 1): unchanged. The contracts add one anonymous endpoint
that returns only a clinic's public names and state; the directory of short names is the only
clinic-related data readable without a clinic, and it holds nothing that is not already in a
public address.

## Project Structure

### Documentation (this feature)

```text
specs/002-clinic-tenancy-isolation/
├── plan.md
├── research.md          # 16 decisions, 4 items to confirm
├── data-model.md        # 3 tables, the predicate function, role and login, rules for later specs
├── quickstart.md        # 7 validation scenarios
├── contracts/
│   ├── tenancy-api.openapi.yaml   # GET /v1/clinic
│   └── isolation.md               # what modules may rely on and must do
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.BuildingBlocks/Tenancy/
│   ├── IClinicOwned.cs                    # marker for clinic-owned entities
│   ├── ClinicContext.cs                   # IClinicContext: establish once, require, refuse a second clinic
│   ├── ClinicSessionInterceptor.cs        # sets SESSION_CONTEXT on connection open
│   ├── ClinicOwnedModelConvention.cs      # query filter, required column, leading index
│   ├── ClinicOwnedSaveInterceptor.cs      # stamps ClinicId, refuses a change to it
│   ├── ClinicIsolationMigrations.cs       # migrationBuilder.CreateClinicIsolation(...)
│   ├── PlatformWideTables.cs              # the one list of tables that belong to no clinic
│   ├── DeclaredCrossings.cs               # the one list of types allowed to cross
│   ├── CrossClinicConnectionFactory.cs    # the scs_ops connection
│   ├── ClinicConnection.cs                # IClinicConnection for read-only report queries
│   └── PatientSurface.cs                  # .ForPatientSurface() + header resolution
├── shared/Scs.BuildingBlocks/Jobs/        # EnqueueForClinic, per-clinic recurring, clinic filter
├── modules/Tenancy/
│   ├── Scs.Tenancy/
│   │   ├── Domain/                        # Clinic, ShortName, ReservedShortNames
│   │   ├── Application/                   # ClinicRegistry, ClinicResolver, CrossClinicRecorder
│   │   ├── Infrastructure/                # TenancyDbContext, CrossingDbContext, Migrations/
│   │   ├── Api/                           # GET /v1/clinic
│   │   └── TenancyModule.cs
│   └── Scs.Tenancy.Contracts/             # IClinicRegistry, IClinicResolver, ClinicSummary
├── tools/Scs.Migrator/                    # Tenancy first in module order; `seed` command
└── tests/
    ├── Scs.UnitTests/Tenancy/             # short-name rules, clinic context
    ├── Scs.IntegrationTests/Isolation/    # guardrail, behaviour, under load, samples
    ├── Scs.IntegrationTests/Tenancy/      # registry, resolver, endpoint, jobs
    └── Scs.ArchitectureTests/             # declared crossings, no SqlConnection in modules

src/web/projects/
├── core/src/lib/clinic/                   # clinic from the path, X-Scs-Clinic, ClinicService
├── patient/src/app/                       # /:clinic routes, "not available" page
└── ui/                                    # translation keys only

deploy/compose/sql/create-logins.sql       # scs_ops, role, schema crossing, grants and denials
deploy/compose/.env.example                # SCS_DB__CROSSING_CONNECTION
ops/restore/verify.sql                     # policies present and enabled
.github/workflows/pr.yml                   # "Tenant isolation" step
docs/adding-a-module.md                    # the three steps for a clinic-owned table
```

**Structure Decision**: the isolation machinery every module needs lives in
`Scs.BuildingBlocks/Tenancy`, because modules may not reference each other's internals. The
clinic record, its short names and the operator operations are a new `Tenancy` module with its
own schema, migrated before any module that has clinic-owned tables. The patient app gains
clinic-aware routing; the clinic and ops apps are untouched.

## Delivery Order

For `/speckit-tasks`. Tests come first throughout; isolation is a test-first area.

1. **Logins and configuration** — `scs_ops`, the role, schema `crossing`, the new connection
   value; the test fixture runs the same script.
2. **Guardrail and behaviour tests, failing** — catalog check per migration; reflection over
   `IClinicOwned`; raw-SQL behaviour test. Stories 1 and 2.
3. **Isolation primitives** — predicate function, migration helper, clinic context, session
   interceptor, model convention, save interceptor. Stories 1 and 2.
4. **Tenancy module** — `Clinic`, directory, first migration, `PlatformWideTables`; the tests of
   step 2 pass. Stories 1 and 4.
5. **Short names and the registry** — value rules, reserved list, register, rename, list;
   access records; declared crossings and their architecture test. Stories 4 and 5.
6. **Resolving the patient address** — resolver, `GET /v1/clinic`, patient-surface marker,
   conflict refusal; patient app routing, redirect on rename, "not available" page. Story 3.
7. **Background work** — clinic parameter and filter, per-clinic recurring jobs. Story 6.
8. **Load and timing** — 200 clinics, 50 concurrent; timing equality; added latency. SC-005,
   SC-009, SC-010.
9. **Gates and guides** — "Tenant isolation" step in `pr.yml`, restore verification, seed with
   two made-up clinics, `docs/adding-a-module.md`.

## Complexity Tracking

No constitution violations to justify.

Choices recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A third database login (`scs_ops`) and a role | Operators must work across clinics, and the application login must be unable to switch isolation off (FR-018) | A flag in the session context that the predicate honours could be set by the application login itself |
| Isolation in the database *and* in EF Core | The database is the authority; the second layer gives clear refusals and catches mistakes earlier | The database alone gives the developer an opaque "block predicate" error; the ORM alone is forbidden by SH-104 |
| A separate schema for access records | The application login must have no rights on them, granted by an administrator rather than by the application | A table in `tenancy` would be readable through the application's general read right |
| Guardrail applies migrations one at a time | FR-021: the table and its rule must arrive in the same change | Checking only the final state would pass a table that was unprotected for one release |
