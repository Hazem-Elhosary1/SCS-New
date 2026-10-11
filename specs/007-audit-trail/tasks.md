---

description: "Task list for 007-audit-trail"
---

# Tasks: Audit Trail

**Input**: Design documents from `/specs/007-audit-trail/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 006 merged into `develop` and Docker running.

**Tests**: Included, and first. Isolation and the reading permission are test-first areas (constitution, testing): each of those test tasks is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Design**: no screen is built. The words of entries come from the frames named in spec.md "Design source"; the table to implement is contracts/audit-trail.md §7.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US7)
- Paths are relative to the repository root

## Path Conventions

- Audit module: `src/server/modules/Audit/Scs.Audit/` (`Domain/`, `Infrastructure/`, `Api/`) and `src/server/modules/Audit/Scs.Audit.Contracts/`
- Catalogue: `src/server/shared/Scs.SharedKernel/Audit/`
- Identity: `src/server/modules/Identity/Scs.Identity/` and `Scs.Identity.Contracts/`
- Server tests: `src/server/tests/Scs.IntegrationTests/Audit/`, `src/server/tests/Scs.UnitTests/Audit/`
- Web (carried-over items only): `src/web/projects/clinic/src/app/`, `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: A known-good start and the empty module.

- [X] T001 Confirm the branch is `007-audit-trail` from `develop` with spec 006 merged, and that `dotnet test src/server/Scs.sln` (Debug) and `npm --prefix src/web run ci` pass before any change (temp folder on `E:` as `docs/setup.md`)
- [X] T002 Create the projects `src/server/modules/Audit/Scs.Audit/Scs.Audit.csproj` and `src/server/modules/Audit/Scs.Audit.Contracts/Scs.Audit.Contracts.csproj` like the Tenancy module's, add them to `src/server/Scs.sln`, reference `Scs.Audit` from `src/server/host/Scs.Api/Scs.Api.csproj` and from the migrator, reference `Scs.Audit.Contracts` from `Scs.Identity`, add `AuditModule` to the ordered list in `src/server/tools/Scs.Migrator/Migrator.cs` before `IdentityModule` (Identity's data migration of T019 writes into its table), and add the module to the architecture tests' list of modules in `src/server/tests/Scs.ArchitectureTests/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The catalogue, the table, and the one way to write an entry.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] Write failing `src/server/tests/Scs.UnitTests/Audit/AuditActionsTests.cs`: the catalogue holds exactly the twelve codes of `data-model.md` §2 with their categories; every code is in `audit-actions.lock` and every locked code is in the catalogue with the same category; codes are unique; no action allows a fact key of the never-list (`password`, `code`, `token`, `link`, `secret`, `key`, `email`, `proof`, `cookie`); every action has a non-empty `NameAr`, `NameEn`, `SentenceAr`, `SentenceEn`; the four role actions and no other need before and after
- [X] T004 Create `src/server/shared/Scs.SharedKernel/Audit/AuditCategories.cs` (`security`, `changes`, `records`, `users`), `AuditActions.cs` (code, category, names, sentences from `contracts/audit-trail.md` §7, allowed fact keys, `NeedsBeforeAfter`; the twelve actions of `data-model.md` §2; `Find(code)`), and `audit-actions.lock` embedded in the assembly; T003 passes
- [X] T005 [P] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditIsolationTests.cs` and add `audit.Entry` to the isolation suite of spec 002 through `src/server/tests/Scs.IntegrationTests/Isolation/AuditSamples.cs`: an entry written for one clinic is not returned for another by a query, by its id, or with no clinic context; an insert with another clinic's id is refused; the table has its policy
- [X] T006 Create `src/server/modules/Audit/Scs.Audit/Domain/Entry.cs` with the columns of `data-model.md` §1 — `ActorKind` varchar(8) "`staff`, `operator` or `system`", `ActorName` nvarchar(254) null, `ActionCode` varchar(64), `Category` varchar(16) "`security`, `changes`, `records`, `users`", `TargetKind` varchar(32) null, `TargetName` nvarchar(200) null, `TargetReadyKey` varchar(16) null, `Facts` and `Changes` nvarchar(max) null "JSON when present", `IpAddress` varchar(45) null, `UserAgent` nvarchar(256) null, `TraceId` char(32) — as `IClinicOwned` with `IpAddress`, `UserAgent` and `ActorName` marked personal data; `Infrastructure/Persistence/AuditDbContext.cs` with the checks and the two indexes; the migration `InitialAuditTrail` with `CreateClinicIsolation("audit", "Entry")` and an append-only trigger like `IdentitySql.CreateEventsAppendOnlyTrigger`; `AuditModule.cs` registered in `src/server/host/Scs.Api/Program.cs` and migrated before Identity; T005 passes
- [X] T007 Create `src/server/modules/Audit/Scs.Audit.Contracts/AuditContracts.cs`: `AuditRecord` (action, target kind/id/name/ready key, facts, changes, subject account), `AuditChange` (`{field, before, after}` and `{field, added, removed}`), `IAuditTrail` (`RecordAsync(DbContext, AuditRecord, ct)`, `RecordAsSystemAsync(DbContext, AuditRecord, ct)`), `ISignInTrail` (`RecordForAccountAsync(DbContext, Guid? actorAccountId, string? actorName, AuditRecord, ct)`, for Identity's sign-in code where no one is signed in yet, FR-014a; an architecture test allows it from `Scs.Identity` alone), `AuditedSave.SaveAsync(db, trail, records, ct)`, `IClinicAuditReading`, `IOperatorAuditReading`; add `DisplayName` to `ICurrentStaff` in `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs` (the email address until spec 026, read from the signed-in account where the session is resolved)
- [X] T008 Create `src/server/modules/Audit/Scs.Audit/Infrastructure/AuditTrail.cs`: inserts the entry with a parameterised command on the caller's connection inside the caller's transaction; takes the actor and display name from `ICurrentStaff`, the clinic from the clinic context, the address and user agent from the request (truncated to 45 and 256), the trace, and the time from `TimeProvider`; `RecordAsSystemAsync` writes `ActorKind` `system` with no person, address or device; refuses with no clinic context

**Checkpoint**: An entry can be written, belongs to one clinic, and its catalogue is fixed.

---

## Phase 3: User Story 2 - A change and its entry stand or fall together, and the entry stays (Priority: P1)

**Goal**: Atomic with the change; never changed, never removed.

**Independent Test**: make recording fail and see the change did not happen; try to change and remove an entry.

- [X] T009 [P] [US2] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditWritingTests.cs`: a change saved through `AuditedSave` and its entry are both there; when the entry is refused the change is not saved; when the change fails after the entry was added no entry remains; `RecordAsync` outside a transaction that holds the change is refused; the entry's actor, clinic, address, device, trace and time come from the session, the request and the clock and cannot be set by the caller; the time is the platform's clock
- [X] T010 [P] [US2] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditImmutabilityTests.cs`: `UPDATE` and `DELETE` of an entry are refused for the application's login, through EF and through raw SQL; an entry still reads the actor's and the role's name after the account is disabled or deleted and the role is renamed or deleted
- [X] T011 [US2] Make `AuditedSave.SaveAsync` in `src/server/modules/Audit/Scs.Audit.Contracts/AuditContracts.cs` open a transaction when none is open, save, record each `AuditRecord`, and commit; make `AuditTrail` refuse when the connection has no transaction; T009 and T010 pass

**Checkpoint**: The trail can be trusted before anything is written to it.

---

## Phase 4: User Story 4 - The trail never holds what it must not (Priority: P1)

**Goal**: A white list per action; nothing secret, clinical or beyond the allowed facts.

**Independent Test**: write entries while passing secrets and extra facts alongside; find none of them stored.

- [X] T012 [P] [US4] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditFactsTests.cs`: an entry with a fact key the action does not allow is refused and the caller's change with it; a code that is not in the catalogue is refused; an action that needs before and after is refused with no changes; an entry whose changes are all "before equals after" with nothing added or removed is not written and the change still saves; fact values longer than 200 characters are refused; after writing every action while a password, a sign-in code, a link token, a session proof and an email address are present in the request and the session, none of those values is found in any column of `audit.Entry`; `PUT /v1/account/preferences` writes no entry; an action added to the catalogue by the test alone records with no change to the audit module (SC-009)
- [X] T013 [US4] Add the checks to `src/server/modules/Audit/Scs.Audit/Infrastructure/AuditTrail.cs`: the code is in the catalogue (FR-008); every fact key is allowed (FR-010); values are text up to 200 characters, numbers, booleans or lists of codes; changes are present when needed (FR-011); the skip of FR-012; `Facts` and `Changes` serialised as JSON; T012 passes

**Checkpoint**: What is stored is only what each action allows.

---

## Phase 5: User Story 1 - A change to who may do what is on the record (Priority: P1) 🎯 MVP

**Goal**: The four role changes of spec 005 record, with before and after.

**Independent Test**: change a role and a person's role; find one entry for each with its before and after.

- [X] T014 [US1] Write failing `src/server/tests/Scs.IntegrationTests/Audit/RoleEntriesTests.cs`: creating a role writes `role.created` naming the role and the permissions added; editing writes one `role.updated` with the name and description before and after and the permissions added and removed, one entry when several change together; deleting writes `role.deleted` with the role's name, readable after the role is gone; giving a person another role writes `account.role-changed` with the account as target and the role before and after, the ready key kept for a ready role; a refused change (`ROLE_FIXED`, `ROLE_IN_USE`, `LAST_OWNER`, `OWN_ROLE`, an invalid set) writes none; saving a role with the set and name it already has writes none; the three ready roles a clinic gets when it is established are recorded with `ActorKind` `system`
- [X] T015 [US1] Record through `AuditedSave` in `CreateAsync`, `UpdateAsync`, `RemoveAsync` and `AssignAsync` of `src/server/modules/Identity/Scs.Identity/Infrastructure/ClinicRoles.cs`, inside the transaction that already holds the application lock; record `role.created` with `RecordAsSystemAsync` in `Infrastructure/ReadyRoles.cs` when `EnsureAsync` creates a role; T014 passes

**Checkpoint**: Stories 2, 4 and 1 are the MVP: a trustworthy trail that already holds the one kind of change the platform can make.

---

## Phase 6: User Story 3 - Sign-in and account security are part of the same trail (Priority: P1)

**Goal**: An entry beside each sign-in event that belongs in the trail; old events carried over.

**Independent Test**: sign in, fail until the account locks, reset a password; find each in the trail, in order.

- [X] T016 [US3] Write failing `src/server/tests/Scs.IntegrationTests/Audit/SignInEntriesTests.cs`: a successful sign-in writes `signin.succeeded` with the account as actor and target, the address and the device, although no one was signed in when it was recorded (FR-014a); the first wrong password on an account with no failed attempts writes one `signin.failed-run` with `ActorKind` `staff`, no actor account and the account as target, and the second to fifth write none; a wrong password after a successful sign-in starts a new run; sign-out writes `session.signed-out`; sessions ended by another person or by a change of role write `sessions.ended` with `reason` and `scope` and the other person as actor; a completed reset writes `password.reset-completed`; a replaced temporary password writes `password.temporary-replaced`; an accepted invitation writes `invitation.accepted`; a re-issued link or temporary password writes `account.access-reissued` with `way`; `reset-requested`, `wrong-code`, `code-spent` and `renewal-reused` write none; an attempt on an address that matches no account writes no entry in any clinic; each entry is in the same transaction as its sign-in event
- [X] T017 [US3] Add the entries of research R2 beside each `db.Events.Add(SignInEvent.Record(…))` in `src/server/modules/Identity/Scs.Identity/Infrastructure/SignInService.cs`, `SessionService.cs`, `PasswordSetter.cs` and `AccountHandover.cs`, through one helper `Infrastructure/TrailEntries.cs` that maps a sign-in event to its `AuditRecord` or to none; a run starts when `FailedAttempts` was 0; T016 passes
- [X] T018 [US3] Add `IStaffDirectory` to `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs` and implement it in `Scs.Identity/Infrastructure/StaffDirectory.cs`: display names for a set of account ids (missing ids are deleted accounts), and for a set of runs (account id and start time) the number of wrong passwords until the next successful sign-in or the lock, whether the run ended in a lock, and the lock's minutes; with tests in `SignInEntriesTests.cs` for runs of one, two and five attempts, with and without a lock
- [X] T019 [US3] Create the migration `SignInEventsIntoTrail` in `src/server/modules/Identity/Scs.Identity/Infrastructure/Migrations/`: inserts into `audit.Entry` the events recorded before this spec by the mapping of research R2 (a run's entry at each `wrong-password` whose previous event for that account is not `wrong-password`), and `role.created` by the platform for every existing role, dated at the role's creation; adds only what is not there; with a test in `SignInEntriesTests.cs` that seeds events and roles as spec 006 left them, runs the migration twice, and finds each entry once

**Checkpoint**: All P1 stories are done; the trail holds everything the platform does today.

---

## Phase 7: User Story 5 - A later spec cannot forget to record (Priority: P2)

**Goal**: Every changing route declares what it records.

**Independent Test**: add a changing route with no declaration and see the test fail.

- [X] T020 [US5] Write failing `src/server/tests/Scs.IntegrationTests/Access/RouteAuditDeclarationTests.cs`: every mapped `POST`, `PUT`, `PATCH` and `DELETE` route carries exactly one of `Records`, `RecordsNothing` with a non-empty reason, `RecordedBySignIn`; every scheduled job (`IClinicJob`) carries the attribute `[Records(…)]` or `[RecordsNothing(reason)]`; the test names each route that carries none; every code in a `Records` declaration is in the catalogue; a test host with one undeclared route fails the check; the routes of `data-model.md` §5 carry the declarations listed there
- [X] T021 [US5] Create `src/server/shared/Scs.BuildingBlocks/Security/AuditDeclarations.cs` (`AuditDeclarationMetadata`, `.Records(params string[])`, `.RecordsNothing(string reason)`, `.RecordedBySignIn()`) with the two attributes for jobs, put `[RecordsNothing(…)]` on `CleanupJob`, and declare every changing route in `src/server/modules/Identity/Scs.Identity/Api/AuthEndpoints.cs`, `PasswordEndpoints.cs`, `PreferencesEndpoints.cs`, `DevEndpoints` if present, `src/server/modules/Platform/Scs.Platform/Api/ClientErrorEndpoints.cs` and any other changing route, as `data-model.md` §5; T020 passes

**Checkpoint**: Forgetting to record fails the tests.

---

## Phase 8: User Story 6 - Each side reads its own part of the trail (Priority: P2)

**Goal**: The clinic's reading address; the operators' reading as a contract.

**Independent Test**: record entries by a clinic's person, by the platform and for a second clinic; read as the first clinic.

- [X] T022 [P] [US6] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditReadingAccessTests.cs` (first, a permission): `GET /v1/audit/entries` answers 401 with no session, 403 `PERMISSION_REQUIRED` with `permission` `reports.activity` for a person without it, 200 for a holder; the route is declared with `.Requires(Permissions.ReportsActivity)`; a holder in one clinic never receives an entry of another, whatever `actor`, `action` or `q` is asked
- [X] T023 [P] [US6] Write failing `src/server/tests/Scs.IntegrationTests/Audit/AuditReadingTests.cs`: entries newest first; with no period the last ninety days; `from` after `to` and a period longer than 366 days answer `VALIDATION_FAILED` with `period`; `category`, `actor` and `action` narrow, and an unknown category or action is `INVALID`; `q` matches the actor's name and the action's name in the reader's language; `counts` hold the period's entries matching every facet except the category, and `total` matches every facet; `page` and `size` (default 25, at most 100) page without repeating or skipping an entry when entries share a time; entries whose `ActorKind` is `system` or `operator` are never returned; a `signin.failed-run` entry is returned with `actor.known` false; the answer has the shape of `contracts/audit-api.openapi.yaml`; `IOperatorAuditReading` returns entries whose actor is an operator across two clinics and none by staff or the platform
- [X] T024 [US6] Create `src/server/modules/Audit/Scs.Audit/Infrastructure/AuditReading.cs` (`IClinicAuditReading`: period, facets, search, counts, paging by `(OccurredAtUtc DESC, Id DESC)`; `IOperatorAuditReading` over clinics through the crossing connection) and `Api/AuditEndpoints.cs` with `GET /v1/audit/entries` `.Requires(Permissions.ReportsActivity)`, mapped in `AuditModule.cs`; add the error field codes it uses to the error catalogue if they are new; T022 and T023 pass

**Checkpoint**: A clinic can read its own trail, and only its own.

---

## Phase 9: User Story 7 - An entry reads in the language of whoever reads it (Priority: P3)

**Goal**: Names, sentences and the device in Arabic and English, from stored facts.

**Independent Test**: read the same entries in Arabic and in English.

- [X] T025 [P] [US7] Write failing `src/server/tests/Scs.UnitTests/Audit/SentencesTests.cs`: each of the twelve actions renders its name and sentence in Arabic and in English exactly as `contracts/audit-trail.md` §7; `{attempts}` follows the number in both languages (1, 2, 3, 10, 11) and `{locked}` appears only for a locked run with its minutes; `{count}` of permissions follows the number; `{changes}` of a role covers added only, removed only, both, and a rename; permission lists longer than three show three and "and N more"; a ready role under its own name is named in the reader's language and a clinic's role as written; a missing account reads «حساب محذوف» · "deleted account"; no English sentence contains an Arabic letter except a name a person wrote, and the reverse; digits are Arabic-Indic in Arabic
- [X] T026 [P] [US7] Write failing `src/server/tests/Scs.UnitTests/Audit/DeviceTextTests.cs`: user agents of Chrome, Edge, Firefox and Safari on Windows, macOS, Android and iOS read «متصفح Chrome على Windows» · "Chrome on Windows" and so on; an unrecognised or empty one reads «جهاز غير معروف» · "Unknown device"
- [X] T027 [US7] Create `src/server/modules/Audit/Scs.Audit/Infrastructure/Sentences.cs` and `DeviceText.cs`; make `AuditReading` answer each entry as words in the language of `Accept-Language` — action name, sentence, actor and target names asked from `IStaffDirectory` for the page's accounts, `attempts` and `locked` for the page's runs, the address in Latin digits, the device; T025 and T026 pass, and `AuditReadingTests.cs` gains a case reading one page in both languages

**Checkpoint**: All stories are complete.

---

## Phase 10: Carried over from spec 006

**Purpose**: The two items the owner asked to settle with this work (research R16).

- [X] T028 Measure what the clinic app's first download is made of (`npx ng build clinic --stats-json` and the sizes per input), then bring it under 500 kB by loading on demand what a person who has not signed in does not need: the frame and everything inside it as one chunk (`loadComponent` for `ShellComponent`, `loadChildren` for its routes in a new `src/web/projects/clinic/src/app/shell/shell.routes.ts`), the pages before sign-in other than the sign-in form, and each real destination page through `registerDestinationPage` taking a loader in `shell/destinations.ts`; the frame's own small pages ("not available yet", "not in your role", "not in your plan", "not found") stay in the frame's chunk so opening them costs no further download (FR-040); update `shell/destinations.spec.ts`; `npm --prefix src/web run ci` passes and the clinic build prints no size warning (FR-041); record the sizes before and after
- [X] T029 Run `npx playwright test access.spec.ts -g "within a second" --repeat-each 10` alone and record the ten figures; if any is over one second, reduce what the frame loads before the first page until none is; do not raise the limit (FR-040, SC-011)
- [X] T030 Run the whole browser suite, `npx playwright test`, and fix what the loading change broke

---

## Phase 11: Polish & Cross-Cutting Concerns

- [X] T031 [P] Write `src/server/tests/Scs.IntegrationTests/Audit/AuditVolumeTests.cs`: one million entries for one clinic over two years, inserted by SQL; the first and the last page of the last ninety days and their counts each answer in under one second (SC-007); recording adds under a tenth of a second to a role edit (SC-010); both figures are written to the test's output
- [X] T032 [P] Update `docs/adding-a-module.md` (recording an action: the four steps of `contracts/audit-trail.md` §1; declaring a route; registering a destination's page by loader), `docs/setup.md` (the reading address and how to ask it), and `ops/runbooks/data-handling.md` (what the trail holds, that the address and user agent are personal data, and that nothing removes an entry)
- [X] T033 Run every gate: `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T034 Walk the real stack as `quickstart.md` "By hand": the six steps with `owner@shifa.example`, `doctor@shifa.example` and `reception@shifa.example`, in Arabic and English
- [X] T035 Write `specs/007-audit-trail/validation.md`: what was run with its counts, what was walked, the measured times, that no screen was built and so none was compared, that role entries were verified by tests only, and what was not done; record the owner items — the eleven undrawn sentences, the BRD's «أمنية» tab, the feminine form in the design's invitation row, entries with no clinic deferred

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → stories.
- **US2 (3)** and **US4 (4)** need only the foundation, and are done before anything records.
- **US1 (5)** needs US2 and US4. **US3 (6)** needs US2 and US4; it does not need US1.
- **US5 (7)** needs the foundation only.
- **US6 (8)** needs entries to read: US1 or US3. **US7 (9)** needs US6 and T018.
- **Carried over (10)** depends on nothing in this spec and can be done at any point.
- **Polish (11)** last.

### Within Each Story

- The test task first, seen to fail; then the implementation that makes it pass.
- T005 and T022 are written before any code they test exists (isolation, permission).

### Parallel Opportunities

- T003 and T005 together; T009 and T010 together; T022 and T023 together; T025 and T026 together.
- Phase 10 beside any server phase: it touches only the web app.
- T031 and T032 together.

## Parallel Example: Foundational

```text
T003  AuditActionsTests.cs          (unit)
T005  AuditIsolationTests.cs        (integration)
```

## Implementation Strategy

### MVP First

1. Phases 1 and 2 — the catalogue, the table, the writer.
2. Phases 3 to 5 — together-or-not, the white list, the role changes.
3. **Stop and validate**: change a role in a test and read its entry from the database.

### Incremental Delivery

4. Phase 6 — sign-in events; the trail now holds everything the platform does.
5. Phase 7 — declarations; forgetting fails the tests.
6. Phases 8 and 9 — reading, in two languages; the real stack can now be walked.
7. Phase 10 — the two items of spec 006.
8. Phase 11 — volume, documents, gates, the walk, `validation.md`.

## Notes

- Requirement coverage: FR-001–005 T006–T008 · FR-006–008 T003–T004, T012–T013 · FR-009 T009, T011 · FR-010–012 T012–T013 · FR-013 T014 · FR-014–015 T008–T009 · FR-016–019a T012 · FR-020–022 T006, T010 · FR-023–024 T020–T021 · FR-025–027 T014–T015, T019 · FR-028–029 T016–T019 · FR-030–035 T022–T024 · FR-035a–039 T003, T025–T027 · FR-040 T029 · FR-041 T028.
- The implementation is not complete until T033 to T035 are done and written down (constitution VII).

---

## Phase 12: Convergence

- [X] T036 Refuse `DELETE` of an audit entry for everyone, the cross-clinic role included: add a migration in `src/server/modules/Audit/Scs.Audit/Infrastructure/Migrations/` that replaces `TR_Entry_AppendOnly` (and correct `AuditSql.CreateAppendOnlyTrigger` in `src/server/shared/Scs.BuildingBlocks/Audit/AuditWriter.cs` and its comment) so that any `UPDATE` or `DELETE` throws 51007; add to `src/server/tests/Scs.IntegrationTests/Audit/AuditImmutabilityTests.cs` a `DELETE` attempted with the operator login and with the migrator login, both refused and the entry unchanged; correct the sentence in `ops/runbooks/data-handling.md` if it no longer holds per FR-021 (contradicts)
- [X] T037 Close the way a module could name the actor of an entry: in `src/server/shared/Scs.BuildingBlocks/Audit/AuditWriter.cs` ignore `AuditActorScope` whenever the work is a request (an `HttpContext` exists), so that a request with nobody signed in records the platform or goes through `SignInAudit`; add to `src/server/tests/Scs.ArchitectureTests/AuditRulesTests.cs` a rule that no module assembly depends on `AuditActorScope`, and to `src/server/tests/Scs.IntegrationTests/Audit/AuditWritingTests.cs` a test that setting the scope inside an anonymous request does not change the entry's actor per FR-014 (partial)
- [X] T038 Declare what every scheduled job records, not only the per-clinic ones: put `[RecordsNothing(reason)]` on `HeartbeatJob` in `src/server/modules/Platform/Scs.Platform/Application/HeartbeatJob.cs` and on `PurgeAccessRecordsJob` in `src/server/modules/Tenancy/Scs.Tenancy/Infrastructure/PurgeAccessRecordsJob.cs`, and make `Every_scheduled_job_says_what_it_records_or_why_it_records_nothing` in `src/server/tests/Scs.IntegrationTests/Access/RouteAuditDeclarationTests.cs` find every job type a module schedules through `IJobScheduler` or `IClinicJobScheduler`, failing by name for one that declares nothing; add the two lines to `specs/007-audit-trail/data-model.md` §5 per FR-023, SC-001 (partial)
- [X] T039 Extend `Nothing_secret_that_passed_through_sign_in_is_anywhere_in_the_trail` in `src/server/tests/Scs.IntegrationTests/Audit/AuditFactsTests.cs` so that all twelve actions of the catalogue are written while a password, a sign-in code, a link token, a session proof and an email address are present — adding the four role actions, an accepted invitation, a replaced temporary password and ended sessions — and assert the set of actions found equals the catalogue per SC-005, T012 (partial)
- [X] T040 Give the entries of one act one moment: when a change of role in `src/server/modules/Identity/Scs.Identity/Infrastructure/ClinicRoles.cs` also ends the person's sessions, `account.role-changed` and `sessions.ended` carry the same `OccurredAtUtc` (one moment taken for the piece of work in the shared writer, not one per save); assert it in `A_change_of_role_also_ends_the_persons_sessions_and_says_who_did_it` in `src/server/tests/Scs.IntegrationTests/Audit/RoleEntriesTests.cs` per spec Edge Cases "Clock" (partial)
- [X] T041 Make the seed and the spec agree about the seeded custom role: either stop recording `role.created` for «فريق العيادة» in `SeedRolesAsync` of `src/server/modules/Identity/Scs.Identity/IdentityModule.cs` and leave seeded roles out of `TrailBackfillSql.ExistingRoles`, or keep both and record in `specs/007-audit-trail/validation.md` as an owner item that the edge case "Environment" should read "seeded accounts are created without entries; the roles a seeded clinic is given are recorded as the platform's"; whichever is chosen, a fresh database and a carried-over one hold the same entries for the same seed, with a test per spec Edge Cases "Environment" (contradicts)
