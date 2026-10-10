# Quickstart: Validating Clinic Tenancy and Isolation

How to prove the feature works. Each scenario maps to a user story in [spec.md](spec.md).
Rules and shapes are in [contracts/](contracts/) and [data-model.md](data-model.md).

## Prerequisites

- Spec 001 running locally (`docs/setup.md`), Docker running.
- `deploy/compose/.env` recreated from `.env.example`, which now also carries
  `SCS_DB__CROSSING_CONNECTION` and the `scs_ops` password. Delete the old `.env` and run
  `scripts/dev.ps1` once so the new login and role are created.

## Scenario 1 — One clinic cannot reach another (Story 1, SC-001, SC-002)

```bash
dotnet test src/server/Scs.sln --filter Category=Isolation
```

Expected: all pass. `IsolationBehaviourTests` runs, for every clinic-owned entity it discovers,
as the application login with raw SQL: clinic A cannot select, update, delete, insert for, or
move a row to clinic B; with no clinic, nothing is visible or writable.

By hand, against the local database as `scs_app`:

```sql
SELECT COUNT(*) FROM tenancy.Clinic;                                   -- 0: no clinic established
EXEC sp_set_session_context N'ClinicId', '<id of noor>', @read_only = 1;
SELECT ShortName FROM tenancy.Clinic;                                  -- noor only
EXEC sp_set_session_context N'ClinicId', '<id of shifa>';              -- refused: read-only
```

## Scenario 2 — An unprotected table cannot be merged (Story 2, SC-003, SC-004)

On a throwaway branch, add a migration that creates `tenancy.Probe (Id int, ClinicId
uniqueidentifier NOT NULL)` with no call to `CreateClinicIsolation`, then:

```bash
dotnet test src/server/Scs.sln --filter IsolationGuardrailTests
```

Expected: fails, naming `tenancy.Probe`. Add the call in a *second* migration: still fails, on
the first. Put the call in the same migration: passes. Add an `IClinicOwned` entity without an
isolation sample: `IsolationBehaviourTests` fails, naming the entity.

## Scenario 3 — A patient address leads to its clinic (Story 3, SC-006)

With the local stack running and the seed applied (clinics `noor` and `shifa`):

| Open | Expected |
|---|---|
| `http://localhost:4202/noor` | The page shows the clinic's name |
| `http://localhost:4202/NOOR/` | Same clinic; address normalised to `/noor` |
| `http://localhost:4202/nosuch` | "Not available" page |
| `http://localhost:4202/api` and `http://localhost:4202/` | "Not available" page |

```bash
npm --prefix src/web run e2e
```

## Scenario 4 — Short names (Story 4, SC-011)

```bash
dotnet test src/server/Scs.sln --filter "ShortName|ClinicRegistry"
```

Covers: valid, too short, too long, bad format, reserved, taken; rename keeps the earlier name
leading to the clinic; an earlier name can never be given to another clinic. By hand: rename
`noor` to `alnoor` with the seed tool, open `/noor`, and land on `/alnoor`.

## Scenario 5 — The one door across clinics (Story 5, SC-007, SC-008)

```bash
dotnet test src/server/Scs.sln --filter "CrossClinic|DeclaredCrossings"
```

Covers: each registry operation writes one `crossing.CrossClinicAccess` row with identifiers
only; the architecture test fails when a type outside `DeclaredCrossings` depends on the
crossing connection; the application login can neither read the access records nor join the
crossing role.

## Scenario 6 — Background work, one clinic at a time (Story 6)

```bash
dotnet test src/server/Scs.sln --filter ClinicJob
```

Covers: a job enqueued for clinic A sees only A; a per-clinic recurring job runs once per
clinic; one clinic failing leaves the others completed; a job with no clinic sees nothing.

## Scenario 7 — Many clinics at once (SC-009, SC-010)

```bash
dotnet test src/server/Scs.sln --filter IsolationUnderLoadTests
```

Seeds 200 clinics, runs work for 50 of them in parallel repeatedly, and asserts that no result
ever contains another clinic's row. Records the added time per request with and without a
clinic established.

## Done

The feature is accepted when scenarios 1, 2, 4, 5, 6 and 7 pass in CI on a real SQL Server,
scenario 3 passes in the browser tests, and the "Tenant isolation" step is a required check on
pull requests.
