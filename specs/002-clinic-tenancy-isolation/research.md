# Phase 0 Research: Clinic Tenancy and Data Isolation

Fixed by the constitution and not re-opened: SQL Server Row-Level Security keyed on
`SESSION_CONTEXT`, set per request; EF Core for all writes; Dapper only for read-only reports on
a connection that carries the clinic's context; modular monolith; Hangfire. Spec 001 supplies the
host, the migrator, the two database logins, the job interface and the real-SQL-Server test
fixture.

## R1. How the database enforces isolation

- **Decision**: every clinic-owned table has a `ClinicId uniqueidentifier NOT NULL` column and
  is covered by a security policy with five predicates, all calling one inline, schema-bound
  function `tenancy.fn_ClinicAccess(@ClinicId)`:
  - `FILTER` — which rows can be seen (and therefore updated or deleted);
  - `BLOCK … AFTER INSERT` and `AFTER UPDATE` — a row cannot be created for, or moved to,
    another clinic;
  - `BLOCK … BEFORE UPDATE` and `BEFORE DELETE` — belt and braces on rows already filtered.

  The function returns a row when `@ClinicId` equals
  `CAST(SESSION_CONTEXT(N'ClinicId') AS uniqueidentifier)`, or when the current database user is
  a member of the role `scs_cross_clinic`.
- **Rationale**: `SESSION_CONTEXT` is `NULL` until set, and `NULL = x` is never true, so no
  clinic means no rows and no writes (FR-014). SQL Server applies policies to every principal
  including the database owner, so nothing bypasses them by accident.
- **Alternatives considered**: a database per clinic (rejected by BRD §2, "one database");
  a schema per clinic (hundreds of schemas, migrations per clinic); filtering in the ORM only
  (forbidden by SH-104).

## R2. Setting the clinic for a piece of work

- **Decision**: an `IClinicContext` scoped to the request or job holds the clinic id. A
  connection interceptor runs
  `EXEC sp_set_session_context N'ClinicId', @id, @read_only = 1` every time a connection is
  opened for that scope. With no clinic established it sets nothing.
- **Rationale**: `@read_only = 1` makes the database itself refuse a second, different value on
  the same connection, which is FR-008 ("fixed until the work ends") enforced below the
  application. Returning a connection to the pool resets session context, so nothing carries
  over to the next piece of work (FR-019); the interceptor sets it again on the next open.
- **Cost**: one extra round trip per connection open. A request keeps its connection open for
  its unit of work, so this is once per request in practice; SC-010 is measured, not assumed.
- **Alternatives considered**: a transaction per request with `SET LOCAL`-style scoping (SQL
  Server has no equivalent; the earlier PostgreSQL design needed a transaction held for the
  whole request); `CONTEXT_INFO` (128 bytes, writable by anyone, not typed).

## R3. A second layer in the application

- **Decision**: clinic-owned entities implement `IClinicOwned`. A model convention adds a global
  query filter on `ClinicId`, and a save interceptor stamps `ClinicId` on new entities and
  refuses a change to it. These duplicate what the database does.
- **Rationale**: defence in depth, and better errors: the application refuses a cross-clinic
  write with a clear failure before the database blocks it. The database remains the authority;
  tests prove isolation with the application layer bypassed (raw SQL as the application login).

## R4. Crossing the boundary

- **Decision**: three things, deliberately different in strength.
  1. **Finding a clinic by short name** needs no crossing: `tenancy.ClinicDirectory`
     (short name → clinic id) is declared platform-wide. It holds only what appears in a public
     address. After the clinic is established, its own row is read under isolation.
  2. **Operator operations** (register, rename, list) run on a separate connection as the login
     `scs_ops`, a member of `scs_cross_clinic`. The application login `scs_app` is not a member
     and cannot add itself.
  3. **The migrator** (`scs_migrator`) is also a member, so a data migration can touch
     clinic-owned rows. It is used only by the pipeline.
- **Declared crossings**: the type that hands out the `scs_ops` connection
  (`ICrossClinicConnectionFactory`) may be injected only into types listed in
  `DeclaredCrossings`. An architecture test fails for any other dependent (FR-025, SC-008).
- **Rationale**: FR-028 asks that the lookup read almost nothing; making it need no privilege at
  all is stronger than a narrow privilege.
- **Alternatives considered**: a "system" value in `SESSION_CONTEXT` that the predicate honours
  (the application login could set it, so it could switch isolation off — violates FR-018);
  `EXECUTE AS` (more moving parts for the same effect).

## R5. Recording cross-clinic access

- **Decision**: `crossing.CrossClinicAccess` (platform-wide): operation name, clinic id, operator
  id, time, request reference. Written in the same transaction as the operation. Until operator
  sign-in exists (spec 010) the operator id is the name of the tool or test that ran it.
- **Rationale**: FR-027. Identifiers only, so it holds no patient data by construction.
- **Retention** (clarified 10 Oct 2026, FR-032): a platform setting starting at 5 years, held as
  the configuration value `SCS_RETENTION__CROSS_CLINIC_ACCESS_DAYS` (default 1825) until the
  operators' settings screen exists (spec 024). A daily job removes older rows through a fifth
  declared crossing, `purge-access-records`. The purge itself is not recorded row by row; it
  logs the count removed.

## R6. The guardrail

- **Decision**: an integration test, `IsolationGuardrailTests`, queries the catalog of a freshly
  migrated database. Every user table is either listed in `PlatformWideTables` or must have a
  non-null `ClinicId` and an enabled policy carrying all five predicates bound to
  `tenancy.fn_ClinicAccess`. Anything else fails, by name. The test applies migrations **one at
  a time** and checks after each, so a table created in one migration and protected in the next
  fails on the first (FR-021).
- **Developer path**: `migrationBuilder.CreateClinicIsolation("schema", "Table")` emits the
  policy; the guide in `docs/adding-a-module.md` gains the three steps.
- **Rationale**: the database's own catalog is the only complete list of tables; a list kept in
  code would be the thing that gets forgotten.
- **Gate**: the test is tagged `Category=Isolation` and runs as a named step in `pr.yml`.

## R7. Proving isolation for every table without a list

- **Decision**: `IsolationBehaviourTests` discovers every `IClinicOwned` entity across all
  module contexts by reflection. Each entity type must have an `IIsolationSample<T>` in the test
  project that builds a valid instance; a type with no sample fails the test by name. For each
  type, with raw SQL as `scs_app`: clinic A cannot select, update or delete clinic B's row,
  cannot insert for B, cannot move a row to B; with no context nothing is visible or writable.
- **Rationale**: FR-022. The only thing a developer adds is the sample, and forgetting it fails.

## R8. Knowing the clinic on the patient site

- **Decision**: the patient app takes the short name from the first path segment and sends it
  as the header `X-Scs-Clinic` on every platform call. Endpoints marked as patient-surface
  resolve it through `tenancy.ClinicDirectory` and establish the clinic. An unknown name answers
  `404 CLINIC_NOT_AVAILABLE`, identically for every unknown name. An earlier name resolves and
  the answer says the current one, so the app redirects.
- **Why a header**: the browser calls `/api/...` on the same origin; the page path is not part
  of an API call. The header is not a credential: it selects which public clinic page is being
  viewed. From spec 070 a signed-in patient's session fixes the clinic, and a header that
  disagrees is refused (FR-011).
- **Staff**: the header is ignored on staff endpoints; spec 003 establishes the clinic from the
  account through `IClinicContext.Establish`.

## R9. Short names

- **Decision**: canonical form is lower case with surrounding slashes and spaces removed;
  pattern `^[a-z][a-z0-9]*(-[a-z0-9]+)*$`, length 3 to 40. Reserved words are one constant list
  in the Tenancy module. `tenancy.ClinicDirectory` has the short name as its primary key and
  keeps every name a clinic has had, with one marked current; the key makes reuse by another
  clinic impossible in the database, not only in code.
- **Rationale**: FR-002 to FR-006, SC-011.

## R10. Clinic identifier

- **Decision**: `uniqueidentifier`, generated by the application as a version-7 (time-ordered)
  GUID. `ClinicId` leads every clinic-owned table's indexes.
- **Rationale**: not guessable by counting, safe to log (FR-031), and time-ordered values keep
  index inserts sequential.
- **Alternatives considered**: `int` identity (guessable, and reveals how many clinics exist).

## R11. Where the code lives

- **Decision**: cross-cutting pieces every module uses go to
  `Scs.BuildingBlocks/Tenancy` (`IClinicOwned`, `IClinicContext`, the interceptors, the
  migration helper, `PlatformWideTables`, `DeclaredCrossings`). The clinic itself is a new
  module, `Tenancy` (`Scs.Tenancy`, `Scs.Tenancy.Contracts`), schema `tenancy`, migrated first.
- **Rationale**: modules may not reference each other's internals, and every module needs the
  isolation primitives.

## R12. Background work

- **Decision**: `IJobScheduler` gains `EnqueueForClinic` and `AddOrUpdateRecurringPerClinic`.
  The clinic id travels as a job parameter; a server filter establishes `IClinicContext` in the
  job's scope before it runs. A per-clinic recurring job is a small fan-out job that lists
  clinic ids through a declared crossing and enqueues one job per clinic, so each clinic's turn
  is its own job with its own retries and its own failure (FR-029, FR-030).

## R13. Logins and permissions

- **Decision**: `deploy/compose/sql/create-logins.sql` also creates the login `scs_ops` and the
  role `scs_cross_clinic` (members: `scs_ops`, `scs_migrator`), grants
  `ALTER ANY SECURITY POLICY` to `scs_migrator`, creates the schema `crossing`, and denies the
  application login every right on it. New configuration value
  `SCS_DB__CROSSING_CONNECTION` (secret, required).
- **Rationale**: role membership is granted by an administrator outside the application, so the
  application cannot widen its own access (FR-018).
- **Added during implementation** (analysis findings C1, C2, F2):
  - The script also creates schema `tenancy` and denies the application and report logins
    insert, update and delete on it. Without this a clinic's own work could have updated its
    own row, including its `State`.
  - A fourth login, `scs_report` (read only), backs `IClinicConnection`, with the new value
    `SCS_DB__REPORT_CONNECTION`. "Read-only" is thereby enforced by the database rather than
    promised by the code.
  - `GRANT REFERENCES` to the migrator, needed to bind a policy to the function.
  - Tests seed rows through the operator login, never as the administrator.
  - The session value is converted through text (`TRY_CONVERT(uniqueidentifier,
    CONVERT(nvarchar(36), …))`): a value of another type matched nothing but raised a
    conversion error instead of returning no rows.

## R14. Same answer for "another clinic's" and "does not exist"

- **Decision**: no special handling. With the filter predicate, another clinic's row is simply
  not in the result, so both cases run the same query and return the same empty result and the
  same `404`. A test measures the timing difference (SC-005).

## R15. Tests first

- **Decision**: isolation is one of the four test-first areas (constitution VII). The guardrail
  and behaviour tests are written and seen to fail before the policy helper and the Clinic
  table exist. All of them run against the SQL Server container from spec 001; none uses an
  in-memory provider.

## R16. Restore verification

- **Decision**: `ops/restore/verify.sql` gains a check that the security policies are present
  and enabled in the restored database, and that `tenancy.fn_ClinicAccess` exists.

## Items to confirm

| # | Item | Default used | Effect of changing |
|---|---|---|---|
| R9 | Short-name format and no Arabic names | **Confirmed 10 Oct 2026**: 3–40, `a–z 0–9 -` | — |
| R9 | Earlier names kept forever | **Confirmed 10 Oct 2026** | — |
| R5 | Retention of access records | **Confirmed 10 Oct 2026**: setting, 5 years to start | — |
| R8 | Suspended or cancelled clinic on the patient address | **Confirmed 10 Oct 2026**: same answer as unknown | — |
| R9 | Starting reserved list | See data-model.md | One list |
| R5 | Operator id before spec 010 | Tool or test name | Column value only |
