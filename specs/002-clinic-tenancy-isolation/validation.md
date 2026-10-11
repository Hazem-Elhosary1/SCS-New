# Validation Record: Clinic Tenancy and Data Isolation

Evidence for the success criteria of [spec.md](spec.md), added as each scenario of
[quickstart.md](quickstart.md) is run.

## 2026-10-10 — Setup, Foundational, User Stories 1 and 2 (the MVP)

Branch `002-clinic-tenancy-isolation`, cut from `develop` after the spec 001 work was committed
and merged. Machine: Windows 11, .NET SDK 10.0.301, Docker 28.3.3, SQL Server 2022 container.

### Automated checks

| Check | Result |
|---|---|
| Server build (warnings as errors), `dotnet format --verify-no-changes` | Pass |
| Unit tests | 82 of 82 |
| Architecture tests | 15 of 15 |
| Integration tests, against a real SQL Server container | 174 of 174 |
| Of which tagged `Category=Isolation` | all pass |
| Spec 001 suite before starting (task T001) | 181 of 181, including the 17 database tests |

### What is proven, and how

Every isolation test uses raw SQL as the application login, so nothing in the application can
be what makes it pass.

| Claim | Evidence |
|---|---|
| **SC-001** one clinic cannot read, change, delete, create for, or move a row to another | `IsolationBehaviourTests` for every `IClinicOwned` entity found by reflection; `PredicateBehaviourTests` for a table the application may write, so the predicates alone are what refuse |
| **SC-002** no clinic means nothing visible or writable | Same tests; also `SessionContextTests` for a malformed value, a zero id, a number, and other session keys |
| One clinic per piece of work, enforced by the database | `SessionContextTests`: a second, different clinic on the same connection is refused by SQL Server |
| Nothing carries over between pieces of work | `SessionContextTests`: with a pool of exactly one connection, the next work on the *same* server session sees no clinic |
| The database is the authority, not the ORM | `ApplicationLayerTests`: with the application's filter switched off, only the established clinic's rows come back; with no clinic, none |
| The application cannot switch isolation off or widen its access | `LoginPermissionTests`: disabling or dropping the policy, dropping the function, joining the cross-clinic role, impersonating, granting itself rights — all without effect |
| A clinic cannot change its own record | `LoginPermissionTests`: a suspended clinic's own work cannot set its state back, rename or delete itself |
| Report queries are isolated and cannot write | `ClinicConnectionTests` on the read-only login |
| **SC-003** an unprotected table fails the build by name | `IsolationGuardrailTests` applies every migration one at a time and checks the catalog after each; `GuardrailCatchesTests` proves nine ways of leaving a table unprotected are each reported |
| **SC-008** the places that open connections are the declared ones | `TenancyRulesTests`: only the shared project references the SQL client; no module sets a session value or writes a policy |

### Found by the tests and fixed

- A number in the session value made the isolation function raise a conversion error instead of
  matching nothing. It failed closed either way; it now returns no rows without an error.
- `GRANT … TO scs_app` run by the application login is not an error in SQL Server. The test now
  asserts the outcome (no permission gained) instead of expecting an exception.

### Analysis findings applied (approved by the product owner on 10 Oct 2026)

| Finding | What was done |
|---|---|
| C1 a clinic could update its own row | The application and report logins are denied writes on schema `tenancy` |
| C2 the report connection was read-only in name only | It uses a fourth, read-only login, `scs_report` |
| F1 FR-026 contradicted the design | FR-026 now lists the five crossings and says the lookup needs none |
| F2 tests seeded as the administrator | Tests seed through the operator login |
| C3 the short name stored twice | `Clinic.ShortName` removed; the directory is the one source |

### Differences from the task text

| Task | Difference | Reason |
|---|---|---|
| T009 | `IClinicOwned` is in the shared kernel, not the shared infrastructure project | Domain code may reference only the shared kernel |
| T020, T021 | One base class, `ClinicAwareDbContext`, carries the session interceptor, the filter and the save rules | One thing for a module to derive from instead of three to wire |
| T028 | Stepping through migrations is two methods on `IMigratableModule` | The migrator and the guardrail use the same code path |
| T002 | The script also creates schema `tenancy`, the login `scs_report`, and grants the migrator `REFERENCES` | See the findings above |

## Second pass — 10 Oct 2026: User Stories 3 to 6, retention and Polish

Run on the developer machine (Windows 11, Docker 28.3.3, SQL Server 2022 in a container).

| Check | Result |
|---|---|
| `dotnet test src/server/Scs.sln` | 122 unit, 19 architecture, 213 integration: all pass |
| `dotnet format --verify-no-changes` | clean |
| `npm run ci` (lint, translations, unit tests, three builds) | pass; 77 unit tests, 10 tool tests |
| `npx playwright test` | 29 of 29 pass |
| Local stack through `scripts/dev.ps1 -NoWeb` | logins, structure and the two made-up clinics applied; a second run changes nothing |

### Success criteria

| Criterion | Evidence |
|---|---|
| **SC-001, SC-002** no read or write across clinics | First pass, plus `IsolationUnderLoadTests` |
| **SC-003, SC-004** an unprotected table cannot be merged | `IsolationGuardrailTests`, `GuardrailCatchesTests` (nine cases). The manual run on a throwaway branch (T031) is still open |
| **SC-005** another clinic's row and a missing row are the same answer | `IsolationProbe`; `ClinicEndpointTests`: an unknown, a suspended and a cancelled clinic give byte-identical answers. The timing half (T017) is still open |
| **SC-006** a patient address leads to its clinic | `ClinicEndpointTests`; `e2e/clinic.spec.ts`; in the browser against the real API: `/noor` shows «عيادة النور», `/SHIFA/` ends at `/shifa` and shows «عيادة الشفاء», `/nosuch` and `/` show «هذه الصفحة غير متاحة» |
| **SC-007** every crossing is recorded | `ClinicRegistryTests`: each operation by a person leaves exactly one access record, with identifiers only; a refused operation leaves none; the application login can neither read nor write the records |
| **SC-008** the crossings are the declared ones | `DeclaredCrossingsTests`: five declared, five found, no other type depends on the crossing connection |
| **SC-009** 200 clinics, 50 at once | `IsolationUnderLoadTests`: 8 rounds of 50 clinics on a pool of 12 connections, 1,200 rows read, none foreign; every update touched its own clinic's rows only |
| **SC-010** no delay a user would notice | Same class: 0.83 ms per request without a clinic, 1.60 ms with; **0.77 ms added**, against a goal of 5 ms |
| **SC-011** short names | `ShortNameTests`, `ClinicRegistryTests`: format, length, reserved names, a name used once is never given to another clinic, an earlier name leads to the current one |
| **SC-012** logs | `LogRedactionTests` now sends personal data in the clinic header to every endpoint, zero needles; `ClinicJobTests` and `ClinicEndpointTests`: entries carry `clinicId` and nothing else about the clinic |
| Story 6, per-clinic jobs | `ClinicJobTests`: a job sees its clinic only; a job without a clinic sees nothing; three clinics run once each; one clinic's failure leaves the other two complete |
| FR-032 retention | `AccessRecordRetentionTests`: records older than the setting (five years by default) are removed and newer ones kept; a period under 30 days stops start-up |
| Restore check | `ops/restore/verify.sql` fails when the isolation function is missing, no policy is present, or a policy is switched off |

### Found in this pass and fixed

- The request-completed entry and the entry for a job that used up its retries did not carry
  `clinicId`. Both do now.
- Domain types could not implement `IClinicOwned` from the shared infrastructure project without
  breaking the layering rule; it moved to the shared kernel.

### Differences from the task text

| Task | Difference | Reason |
|---|---|---|
| T012 | No separate `ClinicLogTests.cs`; the property is asserted in `ClinicEndpointTests` and `ClinicJobTests` | The property is pushed where a clinic is established, so it is tested there |
| T033, T037, T038 | Directory and access-record cases are in `ClinicRegistryTests.cs` and `LoginPermissionTests.cs` | One fixture, one database per class |
| T045 | Patient-surface cases are in `ClinicEndpointTests.cs`, using the probe endpoints | Same |
| T042, T055 | `ClinicRegistry` is in `Infrastructure/`; the fan-out is `ClinicJobFanOut<TJob>` in the shared project, taking ids through `IClinicIdSource` | Layering rule; the shared project may not reference a module |
| T054 | A runner type, `ClinicJobRunner<TJob>`, instead of a job parameter and a filter; scheduling is its own interface, `IClinicJobScheduler` | The clinic is established before the job object exists, so a job cannot run without it |
| T057 | The load test works on connections opened and marked exactly as the application does, not through EF Core and `IClinicConnection` | Those two paths are covered by `ApplicationLayerTests` and `ClinicConnectionTests`; the load test targets what is shared, the pool and the session value |
| T060 | The leak run sends leaky values in the clinic header to every endpoint; it does not register a clinic first | The run has no database; lookups with a database are covered by `ClinicEndpointTests` |

### Still open

| Item | Owner |
|---|---|
| T007 Dapper | Added with the first report query |
| T061 the starting reserved list; operator id before spec 010; BRD SH-003 still states per-clinic subdomains | Product owner |
| `version.json` answers 404 under `ng serve` (it is written at build time) | Spec 001, cosmetic in local only |

## Third pass — 10 Oct 2026: convergence tasks T064 to T068

| Check | Result |
|---|---|
| `dotnet test src/server/Scs.sln` | 122 unit, 19 architecture, 217 integration: all pass |
| `dotnet format --verify-no-changes` | clean |
| `npx playwright test e2e/clinic.spec.ts` | 7 of 7 pass; lint and formatting clean |

| Task | What was done | Evidence |
|---|---|---|
| T064 (FR-032) | `UPDATE` on schema `crossing` is denied to the operator and migrator logins, so an access record can be written and purged but never altered | `ClinicRegistryTests.An_access_record_cannot_be_altered_by_any_login`: refused for all four logins, the record unchanged |
| T065 (FR-031) | A clinic is registered, renamed and looked up by its current name, an earlier name and an unknown one | `ClinicEndpointTests.Log_entries_carry_the_clinic_id_and_nothing_else_about_the_clinic`: the entries carry `clinicId`; no name the clinic has or had, in either language, appears anywhere in the logs |
| T066 (SC-005, closes T017) | 1,000 alternating requests for another clinic's record and for a record that does not exist | `IndistinguishableTests`: same answer (no row, no error); 0.7618 ms against 0.7613 ms, a difference of **0.06%** against a limit of 5% |
| T067 (SC-006) | The platform's share and the page's share are each measured | `ClinicEndpointTests`: known and unknown both under 100 ms on average. `e2e/clinic.spec.ts`: from asking the platform to the final page under 500 ms for both. In the browser against the real API: known 9 to 13 ms, unknown 4 to 5 ms |
| T068 (SC-003, SC-004, closes T031) | Quickstart Scenario 2 run with real migrations in the Tenancy module, then removed | See below |

### Scenario 2 outcomes

The run was done in the working tree with throwaway files rather than on a separate branch,
because the feature's work was not yet committed; the files were deleted afterwards.

| Step | Expected | Observed |
|---|---|---|
| A migration creates `tenancy.Probe` with a clinic column and no isolation | Fails, naming the table | `After migration Tenancy/20261010200000_ThrowawayProbe: tenancy.Probe: has no isolation policy` |
| A second migration adds the isolation | Still fails, on the first | The same failure, at the first migration |
| The isolation is in the same migration | Passes | `IsolationGuardrailTests` passes |
| An `IClinicOwned` entity with no isolation sample | Fails, naming the entity | `Add an IIsolationSample in the integration test project for: Scs.Tenancy.Domain.ThrowawayProbeEntity` |

### Noted, not changed

- A known clinic answers in about 9 ms and an unknown one in about 5 ms. The spec asks for the
  same answer for every address that leads nowhere (unknown, suspended, cancelled), not for a
  known clinic to take as long as an unknown one; whether a clinic exists at a public address
  is not a secret.
- After taking this change, run the database step of `scripts/dev.ps1` once so the local logins
  get the new denial.
