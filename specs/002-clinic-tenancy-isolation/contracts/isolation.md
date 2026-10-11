# Contract: Clinic Isolation

What every module and every later spec may rely on, and what each must do. Changing any of this
is a change to this contract, reviewed as such.

## 1. What the platform guarantees

- Work done on behalf of clinic A sees and changes only clinic A's rows, in every clinic-owned
  table, whatever the query.
- Work with no clinic established sees no clinic-owned row and changes none.
- A row cannot be created for another clinic or moved to one.
- Another clinic's row is indistinguishable from a row that does not exist.
- The guarantees hold for the application login used directly against the database, not only
  through the application.

## 2. Establishing the clinic

```text
IClinicContext
  ClinicId : Guid?                      null until established
  Establish(Guid clinicId, source)      once; same clinic again is fine; another is refused
  Require() : Guid                      the clinic, or refusal CLINIC_REQUIRED
```

| Where | Who establishes it |
|---|---|
| Patient-surface endpoint (`.ForPatientSurface()`) | The platform, from the `X-Scs-Clinic` header |
| Staff endpoint | Spec 003, from the signed-in account |
| Background job | The platform, from the job's clinic parameter |
| Operator operation | Nobody: it runs on the crossing connection instead (§5) |

A module never sets `SESSION_CONTEXT` itself and never opens a database connection itself.

## 3. Adding a clinic-owned table

1. The entity implements `IClinicOwned` (`Guid ClinicId { get; }`). Do not assign it: it is
   stamped from the clinic context when the entity is saved.
2. In the migration that creates the table, call
   `migrationBuilder.CreateClinicIsolation("<schema>", "<Table>")`.
3. In the integration test project, add an `IIsolationSample<TEntity>` that builds one valid row.

The build fails, naming the table or entity, when step 2 or 3 is missing, when the policy is in
a later migration than the table, or when `ClinicId` is nullable.

## 4. A table that belongs to no clinic

Add its name to `PlatformWideTables` with a one-line reason. That file is the single place such
tables are declared; a table found in the database that is neither declared there nor isolated
fails the build. Platform-wide tables never hold patient data.

## 5. Crossing the boundary

- `ICrossClinicConnectionFactory` hands out the operator connection. It may be injected only
  into the types listed in `DeclaredCrossings`; the architecture tests fail for any other use.
- Each declared crossing writes a `crossing.CrossClinicAccess` row in the same transaction.
- A module that needs something from another clinic's data does not get a crossing. It needs a
  spec that says why, and a new entry reviewed in `DeclaredCrossings`.

Declared in this spec: `register-clinic`, `rename-clinic`, `list-clinics`,
`list-clinic-ids-for-jobs`.

## 6. Reading with hand-written SQL

Report queries use `IClinicConnection`, which opens a connection that already carries the
clinic. `new SqlConnection(...)` anywhere outside `Scs.BuildingBlocks` fails the architecture
tests. Such queries are read-only.

## 7. Background work

```text
IJobScheduler
  EnqueueForClinic<TJob>(Guid clinicId, job => job.RunAsync(...))
  AddOrUpdateRecurringPerClinic<TJob>(name, job => job.RunAsync(...), cron)
```

The job body runs with the clinic established. A per-clinic recurring job is run once per
clinic, each as its own job with its own retries; one clinic's failure does not stop the others.
A job enqueued without a clinic sees no clinic-owned rows.

## 8. Errors added by this spec

| Code | Status | When |
|---|---|---|
| `CLINIC_REQUIRED` | 400 | Work that needs a clinic was started without one |
| `CLINIC_NOT_AVAILABLE` | 404 | The address names no clinic. Same answer for every unknown name |
| `CLINIC_MISMATCH` | 400 | The work carries two different clinics, or tries to change clinic |

Field codes for a refused short name: `REQUIRED`, `TOO_SHORT`, `TOO_LONG`, `INVALID_FORMAT`,
`RESERVED`, `TAKEN`.

## 9. Logs

Every entry written while a clinic is established carries `clinicId`. Nothing else about the
clinic is added, and the short name is not logged (it is public, but it is not an identifier we
correlate on).

## 10. Patient site

- The first part of the path is the clinic's short name; the app sends it as `X-Scs-Clinic` on
  every platform call.
- `GET /v1/clinic` answers which clinic that is
  ([tenancy-api.openapi.yaml](tenancy-api.openapi.yaml)). `renamed: true` → the app moves to the
  current name, keeping the rest of the path.
- `CLINIC_NOT_AVAILABLE`, or no first path part → the "not available" page.
