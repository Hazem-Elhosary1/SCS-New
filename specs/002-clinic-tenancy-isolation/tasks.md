---

description: "Task list for 002-clinic-tenancy-isolation"
---

# Tasks: Clinic Tenancy and Data Isolation

**Input**: Design documents from `/specs/002-clinic-tenancy-isolation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Spec 001 implemented locally and Docker running: every isolation test needs a real SQL Server.

**Tests**: Included, and first. Isolation is a test-first area (constitution VII): each test task is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US6)
- Paths are relative to the repository root

## Path Conventions

- Shared isolation machinery: `src/server/shared/Scs.BuildingBlocks/Tenancy/`
- The clinic: `src/server/modules/Tenancy/Scs.Tenancy/` and `Scs.Tenancy.Contracts/`
- Tests: `src/server/tests/Scs.IntegrationTests/Isolation/` and `/Tenancy/`, `Scs.UnitTests/Tenancy/`, `Scs.ArchitectureTests/`
- Patient site: `src/web/projects/patient/`, shared web code in `src/web/projects/core/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The login, role, configuration and projects the feature needs.

- [X] T001 Create branch `002-clinic-tenancy-isolation` from `develop` (after the spec 001 branch is merged) and confirm `dotnet test src/server/Scs.sln` passes with Docker running, including the 17 database tests that did not run in the last spec 001 pass
- [X] T002 Extend `deploy/compose/sql/create-logins.sql`: login `scs_ops` (sqlcmd variable `SCS_OPS_PASSWORD`) with `db_datareader` and `db_datawriter` and no structure rights; role `scs_cross_clinic` with members `scs_ops` and `scs_migrator`; `GRANT ALTER ANY SECURITY POLICY TO scs_migrator`; `CREATE SCHEMA crossing`; `DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::crossing TO scs_app`; `CREATE SCHEMA tenancy` and `GRANT CONTROL ON SCHEMA::tenancy TO scs_migrator` (so a migration can deny the application login writes on the directory); keep the script safe to run again, and record the two schema lines in `specs/002-clinic-tenancy-isolation/research.md` R13
- [X] T003 [P] Add `SCS_OPS_PASSWORD` and `SCS_DB__CROSSING_CONNECTION` (login `scs_ops`) to `deploy/compose/.env.example`, pass `SCS_OPS_PASSWORD` to the `sqlserver-init` service in `deploy/compose/docker-compose.yml`, and add the key to the table in `specs/001-platform-foundation/data-model.md` ("yes | yes | Operator connection for declared cross-clinic operations")
- [X] T004 Write `src/server/tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs` cases, then add `Db.CrossingConnection` (marked `[Secret]`) to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` and require it in `ScsOptionsValidator.cs` with the message naming `SCS_DB__CROSSING_CONNECTION`; update `src/server/tests/Scs.IntegrationTests/Fixtures/ApiFactory.cs` defaults
- [X] T005 Extend `src/server/tests/Scs.IntegrationTests/Fixtures/SqlServerFixture.cs`: substitute `$(SCS_OPS_PASSWORD)`, expose `CrossingConnection` (as `scs_ops`), and add helpers `AsClinicAsync(clinicId, sql)` (opens as `scs_app`, runs `sp_set_session_context N'ClinicId', @id, @read_only = 1`, then the statement) and `AsNoClinicAsync(sql)`
- [X] T006 [P] Create `src/server/modules/Tenancy/Scs.Tenancy/Scs.Tenancy.csproj` (folders `Domain/`, `Application/`, `Infrastructure/`, `Api/`) and `src/server/modules/Tenancy/Scs.Tenancy.Contracts/Scs.Tenancy.Contracts.csproj`; add both to `src/server/Scs.sln`; reference `Scs.Tenancy` from `src/server/host/Scs.Api` and from `src/server/tools/Scs.Migrator`
- [ ] T007 (not needed yet: `IClinicConnection` returns a plain connection; add Dapper with the first report query) [P] Add the `Dapper` package to `src/server/shared/Scs.BuildingBlocks/Scs.BuildingBlocks.csproj` through `src/server/Directory.Packages.props`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Knowing which clinic a piece of work is for. No database rule yet.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 Write `src/server/tests/Scs.UnitTests/Tenancy/ClinicContextTests.cs`: `ClinicId` is null until established; `Establish` once sets it and its source; the same clinic again is accepted; a different clinic throws a refusal with code `CLINIC_MISMATCH`; `Require()` with none throws `CLINIC_REQUIRED`; `Guid.Empty` is refused
- [X] T009 Add `IClinicOwned` (`Guid ClinicId { get; }`) in `src/server/shared/Scs.BuildingBlocks/Tenancy/IClinicOwned.cs` and implement `IClinicContext` and `ClinicContext` (scoped) in `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicContext.cs` with sources `patient-address`, `account`, `job`; makes T008 pass
- [X] T010 [P] Add error codes `CLINIC_REQUIRED` (400), `CLINIC_NOT_AVAILABLE` (404), `CLINIC_MISMATCH` (400) to `src/server/shared/Scs.SharedKernel/ErrorCodes.cs`, their Arabic and English titles to `src/server/shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs`, and the three rows to the table in `specs/001-platform-foundation/contracts/conventions.md` §4; extend `src/server/tests/Scs.UnitTests/Errors/ErrorCatalogueTests.cs` (twelve codes)
- [X] T011 Add `PlatformWideTables` in `src/server/shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs`: the single list of tables that belong to no clinic, each with a one-line reason — `platform.JobHeartbeat`, every `__EFMigrationsHistory`, schema `hangfire`, `tenancy.ClinicDirectory`, `crossing.CrossClinicAccess`
- [X] T012 (the context and its `clinicId` exist; the log property is pushed where a clinic is established, by the patient-surface middleware in T049 and the job filter in T054) Write `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicLogTests.cs` and add the `clinicId` log property when a clinic is established (push from `ClinicContext` through a logging scope in `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicContext.cs`): every entry written after `Establish` carries `clinicId`; entries before it do not; the short name and display names never appear

**Checkpoint**: A piece of work can say which clinic it is for, once.

---

## Phase 3: User Story 1 - One clinic can never see another's data (Priority: P1) 🎯 MVP

**Goal**: Row-level isolation on every clinic-owned table, failing closed, proven as the application login with raw SQL.

**Independent Test**: quickstart.md Scenario 1 — `dotnet test --filter Category=Isolation`: clinic A cannot select, update, delete, insert for, or move a row to clinic B; with no clinic nothing is visible or writable.

### Tests for User Story 1 ⚠️

> Write these first and see them fail.

- [X] T013 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/IsolationSamples.cs`: interface `IIsolationSample<TEntity>` (builds one valid row for a given clinic id and returns the SQL to insert it as an administrator) and the first sample, for `Clinic`
- [X] T014 [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/IsolationBehaviourTests.cs` (trait `Category=Isolation`): discover every `IClinicOwned` entity in every module assembly by reflection and fail, naming the entity, if it has no `IIsolationSample`; for each entity, with two clinics seeded by the administrator and raw SQL as `scs_app`: as A, `SELECT` returns only A's rows; `SELECT` of B's row by exact key returns nothing; `UPDATE` and `DELETE` of B's row affect 0 rows and B's row is unchanged; `INSERT` with B's `ClinicId` is refused; `UPDATE` setting `ClinicId` to B is refused; with no clinic, `SELECT` returns 0 rows and `INSERT`, `UPDATE`, `DELETE` change nothing
- [X] T015 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/SessionContextTests.cs` (trait `Category=Isolation`): after the clinic is set read-only, setting a different clinic on the same connection is refused by the database; a pooled connection reused for a second piece of work has no clinic left from the first; a malformed value in the session context yields no rows, not an error that reveals data
- [X] T016 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/ApplicationLayerTests.cs`: through EF Core with clinic A established, a query for B's row returns nothing; saving an entity stamps `ClinicId` from the context; saving an entity whose `ClinicId` was set to B, or changing `ClinicId` on an existing entity, is refused before reaching the database; with no clinic established, saving a clinic-owned entity is refused with `CLINIC_REQUIRED`
- [X] T017 (the same-answer half is covered by `IsolationProbe`; the timing half is deferred, see analysis finding B1) [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/IndistinguishableTests.cs`: asking for another clinic's `Clinic` row and for a row that does not exist return the same result; over 1,000 alternating requests the mean response times differ by under 5% (SC-005)

### Implementation for User Story 1

- [X] T018 [US1] Implement `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicIsolationMigrations.cs`: `migrationBuilder.CreateClinicAccessFunction()` emitting the inline, schema-bound `tenancy.fn_ClinicAccess(@ClinicId uniqueidentifier)` that returns a row when `@ClinicId = CAST(SESSION_CONTEXT(N'ClinicId') AS uniqueidentifier)` or `IS_ROLEMEMBER('scs_cross_clinic') = 1`; and `migrationBuilder.CreateClinicIsolation(schema, table)` emitting security policy `<schema>.<Table>Isolation` with `FILTER`, `BLOCK AFTER INSERT`, `BLOCK AFTER UPDATE`, `BLOCK BEFORE UPDATE` and `BLOCK BEFORE DELETE` predicates on `ClinicId`, `STATE = ON`, `SCHEMABINDING = ON`; with matching `Drop…` helpers for `Down`
- [X] T019 [US1] Create the entity `src/server/modules/Tenancy/Scs.Tenancy/Domain/Clinic.cs` implementing `IClinicOwned`, with the fields of data-model.md: `Id` "uniqueidentifier, PK — Version-7 GUID generated by the application; never changes"; `ClinicId` "uniqueidentifier, NOT NULL — Always equal to `Id` (check constraint)"; `ShortName` "nvarchar(40), NOT NULL, unique"; `NameAr` "nvarchar(200), NOT NULL — 1 to 200 characters after trimming"; `NameEn` "nvarchar(200), NOT NULL — 1 to 200 characters after trimming"; `State` "nvarchar(20), NOT NULL, default `active`"; `CreatedAtUtc` "datetimeoffset, NOT NULL — Set once"
- [X] T020 [US1] Implement `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicSessionInterceptor.cs` (a `DbConnectionInterceptor` that, when a clinic is established, runs `EXEC sp_set_session_context N'ClinicId', @id, @read_only = 1` on every connection open and sets nothing otherwise); makes T015 pass
- [X] T021 [US1] Implement `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicOwnedModelConvention.cs` (for every `IClinicOwned` entity: `ClinicId` required, a global query filter on the established clinic that matches nothing when none is established) and `ClinicOwnedSaveInterceptor.cs` (stamps `ClinicId` on added entities, refuses a mismatching or changed `ClinicId`, refuses a save with no clinic using `CLINIC_REQUIRED`), plus `AddClinicIsolation(this DbContextOptionsBuilder)` that wires both and the session interceptor
- [X] T022 [US1] Create `src/server/modules/Tenancy/Scs.Tenancy/Infrastructure/Persistence/TenancyDbContext.cs` (schema `tenancy`, history table `tenancy.__EFMigrationsHistory`, `AddClinicIsolation`, `Clinic` mapped with the column types of T019, the check constraint `ClinicId = Id`, and a unique index on `ShortName`) and generate the first migration in `Infrastructure/Migrations/`, which calls `CreateClinicAccessFunction()`, creates `tenancy.Clinic` and calls `CreateClinicIsolation("tenancy", "Clinic")` in the same migration
- [X] T023 [US1] Create `src/server/modules/Tenancy/Scs.Tenancy/TenancyModule.cs` implementing `IModule` and `IMigratableModule`; register it in `src/server/host/Scs.Api/ModuleRegistration.cs` and first in `src/server/tools/Scs.Migrator/Migrator.cs`; makes T014, T016 and T017 pass
- [X] T024 [US1] Write `src/server/tests/Scs.IntegrationTests/Isolation/ClinicConnectionTests.cs` and implement `IClinicConnection` in `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicConnection.cs` (opens a connection as the application login already carrying the established clinic, for read-only Dapper queries; refuses to open with no clinic): a hand-written `SELECT` and an aggregate over two clinics' rows return only the established clinic's
- [X] T025 [US1] Add to `src/server/tests/Scs.ArchitectureTests/TenancyRulesTests.cs`: no assembly other than `Scs.BuildingBlocks` and `Scs.Migrator` depends on `Microsoft.Data.SqlClient.SqlConnection`; no module calls `sp_set_session_context` (source scan of `src/server/modules`)

**Checkpoint**: Isolation holds for the one clinic-owned table, proven below the application. MVP reached.

---

## Phase 4: User Story 2 - A new kind of record cannot ship unprotected (Priority: P1)

**Goal**: The build fails for any table that is neither isolated nor declared platform-wide, including when the rule arrives one migration late.

**Independent Test**: quickstart.md Scenario 2 — an unprotected `tenancy.Probe` fails the guardrail by name; protecting it in a later migration still fails; protecting it in the same migration passes.

### Tests for User Story 2 ⚠️

- [X] T026 [US2] Write `src/server/tests/Scs.IntegrationTests/Isolation/IsolationGuardrailTests.cs` (trait `Category=Isolation`): on an empty database apply every module's migrations one at a time and after each query the catalog; every user table must be in `PlatformWideTables` or have `ClinicId uniqueidentifier NOT NULL` and an enabled security policy with a filter predicate and all four block predicates bound to `tenancy.fn_ClinicAccess`; fail naming the table and what is missing
- [X] T027 [P] [US2] Write `src/server/tests/Scs.IntegrationTests/Isolation/GuardrailCatchesTests.cs`: against a scratch database, create by SQL (a) a table with `ClinicId` and no policy, (b) a table with a policy missing the `AFTER INSERT` block, (c) a table with a nullable `ClinicId`, (d) a table with a disabled policy, (e) a table with no `ClinicId` that is not declared platform-wide; the guardrail's check reports each by name; a declared platform-wide table passes

### Implementation for User Story 2

- [X] T028 [US2] Extract the catalog check used by T026 and T027 into `src/server/tests/Scs.IntegrationTests/Isolation/IsolationCatalog.cs` (one query over `sys.tables`, `sys.columns`, `sys.security_policies`, `sys.security_predicates`) and add `IMigratableModule.GetMigrationsAsync`/`MigrateToAsync` (or equivalent) in `src/server/shared/Scs.BuildingBlocks/Modules/IMigratableModule.cs` so a test can step through migrations; makes T026 and T027 pass
- [X] T029 [US2] Add a step named "Tenant isolation" to the `server` job of `.github/workflows/pr.yml` running `dotnet test src/server/Scs.sln -c Release --no-build --filter Category=Isolation`, and add it to the required checks in `ops/runbooks/repository-settings.md`
- [X] T030 [P] [US2] Add the section "Adding a clinic-owned table" to `docs/adding-a-module.md` with the three steps of contracts/isolation.md §3 and how to declare a platform-wide table (§4), and add the five rules of data-model.md "Rules every clinic-owned table follows" to `docs/database-changes.md`
- [X] T031 (the guardrail is proven automatically by `GuardrailCatchesTests`, nine cases; the manual throwaway-branch run with a real late migration is still to do) [US2] Run quickstart.md Scenario 2 on a throwaway branch (unprotected table; rule one migration late; rule in the same migration; entity without a sample) and record the four outcomes in `specs/002-clinic-tenancy-isolation/validation.md` (SC-003, SC-004)

**Checkpoint**: Forgetting isolation fails the build, by name.

---

## Phase 5: User Story 4 - The clinic and its short name (Priority: P2)

**Goal**: Short names that are unique forever, readable, never a reserved word, and that survive a rename.

**Independent Test**: quickstart.md Scenario 4 — valid, invalid, duplicate and reserved names; after a rename both the old and the new name lead to the clinic.

(Placed before User Story 3 because resolving an address needs names to resolve.)

### Tests for User Story 4 ⚠️

- [X] T032 [P] [US4] Write `src/server/tests/Scs.UnitTests/Tenancy/ShortNameTests.cs`: canonical form is lower case with surrounding spaces and slashes removed; pattern `^[a-z][a-z0-9]*(-[a-z0-9]+)*$`; length 3 to 40; field codes `REQUIRED`, `TOO_SHORT`, `TOO_LONG`, `INVALID_FORMAT`, `RESERVED`; cases at 2, 3, 40 and 41 characters; leading digit, leading or trailing hyphen, doubled hyphen, upper case, Arabic letters, spaces inside, look-alike characters; every word of the reserved list in data-model.md is refused
- [X] T033 [P] [US4] Write `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicDirectoryTests.cs`: the directory is readable as `scs_app` with no clinic established; `scs_app` cannot insert, update or delete in it; a second current name for one clinic is refused by the database; a short name used by one clinic cannot be inserted for another

### Implementation for User Story 4

- [X] T034 [P] [US4] Implement `src/server/modules/Tenancy/Scs.Tenancy/Domain/ShortName.cs` (value type with `TryCreate` returning the field code) and `src/server/modules/Tenancy/Scs.Tenancy/Domain/ReservedShortNames.cs` with the starting list of data-model.md; makes T032 pass
- [X] T035 [US4] Add the entity `src/server/modules/Tenancy/Scs.Tenancy/Domain/ClinicDirectoryEntry.cs` with the fields of data-model.md — `ShortName` "nvarchar(40), PK"; `ClinicId` "uniqueidentifier, NOT NULL, FK → `Clinic.Id`"; `IsCurrent` "bit, NOT NULL — Exactly one current name per clinic (filtered unique index on `ClinicId` where `IsCurrent = 1`)"; `AssignedAtUtc` "datetimeoffset, NOT NULL" — map it in `TenancyDbContext`, and generate a migration that creates `tenancy.ClinicDirectory` and runs `DENY INSERT, UPDATE, DELETE ON tenancy.ClinicDirectory TO scs_app` (the migrator may do this because T002 gives it control of schema `tenancy`); makes T033 pass
- [X] T036 [US4] Add the display-name rules to `Clinic` in `src/server/modules/Tenancy/Scs.Tenancy/Domain/Clinic.cs` (a factory that trims `NameAr` and `NameEn` and refuses either when empty or over 200 characters with field codes `REQUIRED`, `TOO_LONG`) with tests in `src/server/tests/Scs.UnitTests/Tenancy/ClinicTests.cs`

**Checkpoint**: The rules for names exist; nothing can yet register a clinic (User Story 5).

---

## Phase 6: User Story 5 - Operators work across clinics through one narrow, visible door (Priority: P2)

**Goal**: Five named cross-clinic operations on a separate login, each recorded; any other crossing fails the build.

**Independent Test**: quickstart.md Scenario 5 — each operation writes one access record with identifiers only; an undeclared type that depends on the crossing connection fails the architecture test.

### Tests for User Story 5 ⚠️

- [X] T037 [P] [US5] Write `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicRegistryTests.cs`: `RegisterAsync` with a valid name creates the clinic, its directory entry (current) and the patient address resolves; a taken name (current or earlier, any clinic) is refused with field code `TAKEN`; an invalid or reserved name is refused with its code; `RenameAsync` makes the new name current and keeps the old entry pointing at the same clinic; renaming twice keeps both earlier names; an earlier name can never be given to another clinic; `ListAsync` returns every clinic; a clinic's own row is readable on its behalf and invisible on another's
- [X] T038 [P] [US5] Write `src/server/tests/Scs.IntegrationTests/Tenancy/CrossClinicAccessTests.cs`: each registry operation writes exactly one `crossing.CrossClinicAccess` row with `Operation` one of `register-clinic`, `rename-clinic`, `list-clinics`, the clinic id (null for list), the operator id and the request reference; a failed operation writes none (same transaction); the row holds no name or other clinic data; `scs_app` cannot select from, insert into, update or delete `crossing.CrossClinicAccess`; `scs_app` is not a member of `scs_cross_clinic` and cannot add itself
- [X] T039 [P] [US5] Write `src/server/tests/Scs.ArchitectureTests/DeclaredCrossingsTests.cs`: only the types listed in `DeclaredCrossings` depend on `ICrossClinicConnectionFactory`; every listed type exists; the number of dependents equals the number declared (SC-008)

### Implementation for User Story 5

- [X] T040 [US5] Implement `ICrossClinicConnectionFactory` in `src/server/shared/Scs.BuildingBlocks/Tenancy/CrossClinicConnectionFactory.cs` (opens `SCS_DB__CROSSING_CONNECTION`, never sets a clinic) and `DeclaredCrossings` in `src/server/shared/Scs.BuildingBlocks/Tenancy/DeclaredCrossings.cs` (the one list: operation name, type, one-line reason) with the four entries of data-model.md
- [X] T041 [US5] Add the entity `src/server/modules/Tenancy/Scs.Tenancy/Domain/CrossClinicAccess.cs` with the fields of data-model.md — `Id` "bigint identity, PK"; `Operation` "nvarchar(60), NOT NULL — One of the names in `DeclaredCrossings`"; `ClinicId` "uniqueidentifier, NULL"; `OperatorId` "nvarchar(100), NOT NULL"; `TraceId` "char(32), NULL"; `OccurredAtUtc` "datetimeoffset, NOT NULL" — a `CrossingDbContext` in `src/server/modules/Tenancy/Scs.Tenancy/Infrastructure/Persistence/CrossingDbContext.cs` (schema `crossing`, operator connection, also maps `Clinic` and `ClinicDirectoryEntry` without the clinic filter) and the migration that creates `crossing.CrossClinicAccess`
- [X] T042 [US5] Define `IClinicRegistry` (`RegisterAsync(shortName, nameAr, nameEn, operatorId)`, `RenameAsync(clinicId, newShortName, operatorId)`, `ListAsync(operatorId)`) and its records in `src/server/modules/Tenancy/Scs.Tenancy.Contracts/IClinicRegistry.cs`, and implement `src/server/modules/Tenancy/Scs.Tenancy/Application/ClinicRegistry.cs` on `CrossingDbContext`, each operation writing its `CrossClinicAccess` row in the same transaction; makes T037, T038 and T039 pass
- [X] T043 [US5] Add a `seed` command to `src/server/tools/Scs.Migrator/Program.cs` that registers two made-up clinics (`noor` — «عيادة النور» / "Al Noor Clinic"; `shifa` — «عيادة الشفاء» / "Al Shifa Clinic") through `IClinicRegistry` if they do not exist, call it from `scripts/dev.ps1` and `scripts/dev.sh`, and note in `ops/runbooks/data-handling.md` that this is the seed later specs extend (closes spec 001 task T112)

**Checkpoint**: Clinics can be registered and renamed, visibly, and nothing else can cross.

---

## Phase 7: User Story 3 - A patient address leads to the right clinic (Priority: P2)

**Goal**: `scs-heta.com/<clinic>` resolves to its clinic; anything else shows one identical "not available" page.

**Independent Test**: quickstart.md Scenario 3 — `/noor` shows the clinic; `/NOOR/` normalises; `/nosuch`, `/api` and `/` show "not available".

### Tests for User Story 3 ⚠️

- [X] T044 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicEndpointTests.cs` against contracts/tenancy-api.openapi.yaml: `GET /v1/clinic` with `X-Scs-Clinic: noor` → 200 with `shortName`, `nameAr`, `nameEn`, `state`, `renamed: false`; `NOOR`, ` noor `, `/noor/` resolve to the same clinic; an earlier name → 200 with the current `shortName` and `renamed: true`; an unknown name, a reserved word, a malformed name, and a clinic whose `State` is `suspended` or `cancelled` → 404 `CLINIC_NOT_AVAILABLE` with identical bodies apart from `traceId` (FR-033); header missing → 400 `CLINIC_REQUIRED`; header over 60 characters → 404; the endpoint is anonymous; the response and the logs contain nothing of any other clinic
- [X] T045 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Tenancy/PatientSurfaceTests.cs`: on an endpoint marked patient-surface the header establishes the clinic and clinic-owned reads return only that clinic's rows; on an endpoint not marked patient-surface the header is ignored and no clinic is established; when a clinic is already established and the header names another, the request is refused with `CLINIC_MISMATCH`
- [X] T046 [P] [US3] Write `src/web/projects/core/src/lib/clinic/clinic.service.spec.ts`: the short name is the first path segment, lower-cased, without slashes; `/`, `/api/...` and a segment that fails the short-name pattern give no clinic and no platform call; the interceptor adds `X-Scs-Clinic` to platform calls only when a clinic is known; `renamed: true` triggers navigation to the current name keeping the rest of the path and the query; `CLINIC_NOT_AVAILABLE` sets the not-available state
- [X] T047 [P] [US3] Extend `src/web/e2e/placeholder.spec.ts` (or add `src/web/e2e/clinic.spec.ts`), answering `GET /v1/clinic` in the test: `/noor` shows the clinic's name in Arabic and in English; `/NOOR/` ends at `/noor`; an earlier name ends at the current one; `/nosuch` and `/` show the not-available page in both languages; the page for an unknown clinic is identical for two different unknown names

### Implementation for User Story 3

- [X] T048 [US3] Define `IClinicResolver` and `ClinicSummary` (`ShortName`, `NameAr`, `NameEn`, `State`, `Renamed`) in `src/server/modules/Tenancy/Scs.Tenancy.Contracts/IClinicResolver.cs` and implement `src/server/modules/Tenancy/Scs.Tenancy/Application/ClinicResolver.cs`: canonicalise, refuse without a database call when the name fails the pattern, look up `tenancy.ClinicDirectory` as the application login with no clinic, return the clinic id and whether the name is current; after the clinic is established, read its own row and answer "not available" when `State` is in the one list `NotPubliclyAvailableStates` (`suspended`, `cancelled`) kept in `src/server/modules/Tenancy/Scs.Tenancy/Domain/Clinic.cs`
- [X] T049 [US3] Implement `src/server/shared/Scs.BuildingBlocks/Tenancy/PatientSurface.cs`: endpoint metadata `.ForPatientSurface()` and middleware that, for such endpoints only, reads `X-Scs-Clinic`, resolves it through a resolver interface defined in BuildingBlocks and implemented by the Tenancy module, establishes `IClinicContext` with source `patient-address`, and answers `CLINIC_REQUIRED` or `CLINIC_NOT_AVAILABLE`; wire it in `src/server/host/Scs.Api/Program.cs` after routing; makes T045 pass
- [X] T050 [US3] Implement `GET /v1/clinic` in `src/server/modules/Tenancy/Scs.Tenancy/Api/ClinicEndpoints.cs` (anonymous, patient-surface; reads the established clinic's own row under isolation) and map it in `TenancyModule.cs`; makes T044 pass
- [X] T051 [US3] Implement `src/web/projects/core/src/lib/clinic/clinic.service.ts` (short name from the path, `load()` calling `/v1/clinic`, signals for the clinic, loading and not-available) and extend `src/web/projects/core/src/lib/http/api.interceptor.ts` to add `X-Scs-Clinic`; export from `src/web/projects/core/src/public-api.ts`; makes T046 pass
- [X] T052 [US3] In `src/web/projects/patient/src/app/`: routes `''` → not-available page and `':clinic'` with children → clinic shell; `clinic-shell.component.ts` loads the clinic, redirects on rename, shows the clinic's display name in the active language above the existing placeholder panel, and shows `<scs-state state="empty">` with key `app.clinic.notAvailable` when there is none; add the keys to `src/web/projects/patient/public/i18n/ar.json` and `en.json` («هذه الصفحة غير متاحة» / "This page is not available"); makes T047 pass

**Checkpoint**: A patient address leads to its clinic and nowhere else.

---

## Phase 8: User Story 6 - Background work keeps to one clinic at a time (Priority: P3)

**Goal**: A job runs for one clinic under the same isolation as a request; a job over all clinics takes them one by one.

**Independent Test**: quickstart.md Scenario 6 — a job for clinic A sees only A; with one clinic made to fail the others complete; a job with no clinic sees nothing.

### Tests for User Story 6 ⚠️

- [X] T053 [P] [US6] Write `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicJobTests.cs`: a job enqueued with `EnqueueForClinic(A, …)` reads only A's rows and its log entries carry `clinicId` A; a job enqueued without a clinic sees no clinic-owned rows and cannot write one; a per-clinic recurring job over three clinics runs once for each; when the job throws for one clinic the other two complete and the failure's log entries carry the failing clinic's id; a job cannot switch clinic part-way

### Implementation for User Story 6

- [X] T054 [US6] Extend `src/server/shared/Scs.BuildingBlocks/Jobs/JobScheduling.cs` with `EnqueueForClinic<TJob>` and `AddOrUpdateRecurringPerClinic<TJob>` on `IJobScheduler`, store the clinic id as the job parameter `ClinicId`, and add `src/server/shared/Scs.BuildingBlocks/Jobs/ClinicJobFilter.cs` (a server filter that establishes `IClinicContext` with source `job` in the job's scope before it runs)
- [X] T055 [US6] Implement the fan-out for per-clinic recurring jobs in `src/server/modules/Tenancy/Scs.Tenancy/Application/PerClinicJobFanOut.cs`: lists clinic ids through the declared crossing `list-clinic-ids-for-jobs` (recorded once per run with a null clinic) and enqueues one job per clinic; register the type in `DeclaredCrossings`; makes T053 pass
- [X] T056 [P] [US6] Document per-clinic jobs in `docs/adding-a-module.md` and in `specs/001-platform-foundation/contracts/conventions.md` §6 (replacing "delivered by spec 002" with the two scheduler methods)

**Checkpoint**: All six stories work.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T057 Write `src/server/tests/Scs.IntegrationTests/Isolation/IsolationUnderLoadTests.cs` (trait `Category=Isolation`): seed 200 clinics with rows, run work for 50 of them in parallel for several rounds through EF Core and through `IClinicConnection`, and assert no result ever contains another clinic's row (SC-009); measure the time a request spends establishing the clinic and record it in `specs/002-clinic-tenancy-isolation/validation.md` against the 5 ms goal (SC-010)
- [X] T058 [P] Extend `ops/restore/verify.sql`: fail unless `tenancy.fn_ClinicAccess` exists and every security policy in the restored database is enabled, and print how many were checked
- [X] T059 [P] Update `docs/setup.md` (the `.env` must be recreated once for the new login; the seeded clinics and their addresses `http://localhost:4202/noor` and `/shifa`) and `src/web/e2e` start pages that used `/noor` without a platform answer
- [X] T060 [P] Extend `src/server/tests/Scs.IntegrationTests/Logging/LogRedactionTests.cs` so the leak run also registers a clinic, resolves it and calls `GET /v1/clinic` with leaky header values, and confirm zero needles; confirm `clinicId` is the only clinic-related field in any entry (FR-031)
- [ ] T061 Resolve the two remaining "Items to confirm" in `specs/002-clinic-tenancy-isolation/research.md` with the product owner (the starting reserved list; operator id before spec 010; the other four were confirmed on 10 Oct 2026) and confirm BRD SH-003 has been updated (spec 001 task T110)
- [X] T062 Write `src/server/tests/Scs.IntegrationTests/Tenancy/AccessRecordRetentionTests.cs`, then implement FR-032: add `Retention.CrossClinicAccessDays` to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` (`SCS_RETENTION__CROSS_CLINIC_ACCESS_DAYS`, default `1825`, minimum `30`, validated at start-up) and `deploy/compose/.env.example`; add `src/server/modules/Tenancy/Scs.Tenancy/Application/PurgeAccessRecordsJob.cs` registered as the daily recurring job `tenancy-purge-access-records`, deleting rows of `crossing.CrossClinicAccess` older than the period through the declared crossing `purge-access-records` (added to `DeclaredCrossings`) and logging the count removed; tests: a row one day older than the period is removed, a row one day inside it is kept, a value under 30 stops start-up, the job is idempotent, and no other code path can delete a row
- [X] T063 Run all seven scenarios of `specs/002-clinic-tenancy-isolation/quickstart.md`, walk SC-001 to SC-012 in `specs/002-clinic-tenancy-isolation/spec.md` against the evidence, and record the result in `specs/002-clinic-tenancy-isolation/validation.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: needs spec 001 merged and Docker running.
- **Foundational (Phase 2)**: needs Setup. Blocks every story.
- **US1 (Phase 3)**: needs Foundational. Creates the `Clinic` table every other story uses.
- **US2 (Phase 4)**: needs US1 (there must be an isolated table and the helper to check against).
- **US4 (Phase 5)**: needs US1 (`TenancyDbContext`).
- **US5 (Phase 6)**: needs US4 (short-name rules, directory).
- **US3 (Phase 7)**: needs US5 (clinics must be registrable to resolve them) and US4.
- **US6 (Phase 8)**: needs US1 and US5 (the crossing that lists clinic ids).
- **Polish (Phase 9)**: after the stories it measures.

```text
Setup → Foundational → US1 ─┬→ US2
                            ├→ US4 → US5 ─┬→ US3
                            │             └→ US6
                            └────────────────┘
```

### Story Independence

US1 and US2 together are the safety property and can ship alone: every later spec could already
build on them. US4, US5 and US3 form one chain (names → registering → resolving). US6 is
independent of US3.

### Within Each User Story

- Test tasks first; confirm they fail; then the implementation that makes them pass.
- Table and isolation rule in the same migration, always.

### Parallel Opportunities

- Setup: T003, T006, T007 alongside T002.
- US1: T013, T015, T016, T017 are different files and can be written together before T018.
- US2: T027 and T030.
- US4: T032, T033, then T034.
- US5: the three test files T037, T038, T039.
- US3: the four test files T044 to T047; server (T048 to T050) and web (T051, T052) by two people.
- After US1: one person on US2, one on US4 → US5.

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "IsolationSamples.cs in src/server/tests/Scs.IntegrationTests/Isolation/"
Task: "SessionContextTests.cs in src/server/tests/Scs.IntegrationTests/Isolation/"
Task: "ApplicationLayerTests.cs in src/server/tests/Scs.IntegrationTests/Isolation/"
Task: "IndistinguishableTests.cs in src/server/tests/Scs.IntegrationTests/Isolation/"
```

---

## Implementation Strategy

### MVP First (User Stories 1 and 2)

1. Phase 1 Setup and Phase 2 Foundational (T001 to T012).
2. Phase 3 US1 (T013 to T025): isolation proven below the application.
3. Phase 4 US2 (T026 to T031): the guardrail in the pull-request gates.
4. **Stop and validate**: quickstart.md Scenarios 1 and 2.

That is 31 tasks, and it is the part every later spec depends on.

### Incremental Delivery

1. MVP as above.
2. US4 then US5 → clinics can be registered and renamed, with the seed for local work.
3. US3 → the patient address resolves; spec 071 can build on it.
4. US6 → ready for the first scheduled job (spec 008 or 018).

### Before Starting

- Docker must be running and the C: drive must have space: nothing in this spec can be verified
  without a SQL Server container.
- BRD SH-003 should be updated; the spec follows the constitution's addresses.

---

## Notes

- 63 tasks: Setup 7, Foundational 5, US1 13, US2 6, US4 5, US5 7, US3 9, US6 4, Polish 7.
- No task needs a cloud account.
- This spec changes two spec 001 documents on purpose (the configuration table and the
  conventions contract) and closes spec 001 task T112.
- Every test that touches isolation runs against the SQL Server container with raw SQL as the
  application login; none uses an in-memory provider.

---

## Phase 10: Convergence

- [X] T064 Deny `UPDATE` on schema `crossing` to `scs_ops` in `deploy/compose/sql/create-logins.sql` (and in the test fixture's copy of the logins), so an access record can be written and purged but never altered, and add a case to `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicRegistryTests.cs` proving an `UPDATE crossing.CrossClinicAccess` as `scs_ops` is refused per FR-032 (partial)
- [X] T065 Add a test to `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicEndpointTests.cs` (or `Logging/LogRedactionTests.cs`) that resolves a registered clinic by its current and an earlier name and runs a per-clinic job, then asserts no log entry contains the clinic's short names or its Arabic or English display name, and that `clinicId` is the only clinic-related property per FR-031 (partial)
- [X] T066 Write `src/server/tests/Scs.IntegrationTests/Isolation/IndistinguishableTests.cs`: over 1,000 alternating requests for another clinic's record and for a record that does not exist, the mean response times differ by under 5%; record the figures in `validation.md` per SC-005 (partial; completes T017)
- [X] T067 Assert in `src/web/e2e/clinic.spec.ts` that a known clinic address and an unknown one each reach their final page in under half a second, and record the measured times against the real API in `validation.md` per SC-006 (partial)
- [X] T068 Run quickstart.md Scenario 2 on a throwaway branch with a real late migration and record the four outcomes in `validation.md` per SC-003, SC-004 (partial; completes T031)
