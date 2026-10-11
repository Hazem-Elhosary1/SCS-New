---

description: "Task list for 005-permissions-engine"
---

# Tasks: Permissions Engine

**Input**: Design documents from `/specs/005-permissions-engine/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 004 implemented locally and Docker running: every test that touches roles, accounts or sessions needs a real SQL Server.

**Tests**: Included, and first. Route declaration, the gate over all 83 codes, the requirement rule, immediacy of a role change, session ending on assignment, the last owner and the roll-out migration are test-first areas (constitution VII): each test task is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US8)
- Paths are relative to the repository root

## Path Conventions

- Shared kernel: `src/server/shared/Scs.SharedKernel/Access/`
- Shared server code: `src/server/shared/Scs.BuildingBlocks/Security/`
- Module: `src/server/modules/Identity/Scs.Identity/` (`Domain/`, `Infrastructure/`, `Api/`) and `Scs.Identity.Contracts/`
- Tests: `src/server/tests/Scs.UnitTests/Access/`, `Scs.IntegrationTests/Access/`, `Scs.ArchitectureTests/`
- Web: engine in `src/web/projects/core/src/lib/access/`, components in `src/web/projects/ui/src/lib/`, pages in `src/web/projects/clinic/src/app/administration/`, browser tests in `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: A known-good start and the error codes.

- [X] T001 Confirm the branch is `005-permissions-engine` from `develop` with spec 004 merged, and that `dotnet test src/server/Scs.sln` (Debug) and `npm --prefix src/web run ci` pass before any change (temp folder on `E:` as `docs/setup.md`)
- [X] T002 [P] Add `PERMISSION_REQUIRED` (403), `ROLE_FIXED` (409), `ROLE_IN_USE` (409), `LAST_OWNER` (409) and `OWN_ROLE` (409) to `src/server/shared/Scs.SharedKernel/ErrorCodes.cs` (and `All`), their Arabic and English titles to `src/server/shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs`, the code list in `specs/001-platform-foundation/contracts/conventions.md`, and update the test that counts the codes in `src/server/tests/Scs.UnitTests/Errors/ErrorCatalogueTests.cs` (32)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The catalogue and the rule every story reads, and the tables.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Write failing `src/server/tests/Scs.UnitTests/Access/PermissionCatalogueTests.cs`: 83 permissions numbered 1–83 without gaps; codes unique; group counts bookings 7, patients 20, money 7, inbox 4, tickets 4, reports 7, admin 6, data 8, settings 13, subscription 7 and `home` first with none; kinds view 20, read 5, write 56, scope 2; 61 requirements, each existing and of kind view or read; every constant of `Permissions` is in the table and the reverse; spot rows 1, 47 (requires nothing), 62 (requires nothing), 75 → 74 → 73, 80–83 with the codes `data.consents.view`, `data.consents.manage`, `patients.lab.view`, `patients.lab.manage`
- [X] T004 Create `src/server/shared/Scs.SharedKernel/Access/PermissionCatalogue.cs` (`Permission` record: `Number`, `Code`, `NameAr`, `NameEn`, `Group`, `Kind`, `Requires`; `PermissionKind`; `PermissionGroup` with key, `NameAr`, `NameEn`, order 1–11; `PermissionCatalogue.All`, `Find(code)`, `Groups`, catalogue name `clinic`) and `Permissions.cs` (one constant per code), row for row from `specs/005-permissions-engine/contracts/permission-catalogue.md`; T003 passes
- [X] T005 [P] Write failing `src/server/tests/Scs.UnitTests/Access/PermissionSetsTests.cs`: for each of the 61 requirements a set with the permission and its requirement is accepted and the set without the requirement gives `MISSING_REQUIREMENT` naming both codes; empty → `REQUIRED`; unknown code → `UNKNOWN`; repeated code → `DUPLICATE`; one step only (a set holding 75 and 74 without 73 names the pair 74 → 73, not 75 → 73); `ReadySets.Manager` has 59 and `ReadySets.Doctor` 40, both valid, counts computed from the table
- [X] T006 Create `src/server/shared/Scs.SharedKernel/Access/PermissionSets.cs` (`Check(codes)` → problems `REQUIRED`, `UNKNOWN`, `DUPLICATE`, `MISSING_REQUIREMENT` with the pairs; `ReachOf` helper: the scope permission whose requirement is a given view) and `ReadySets.cs` (the manager and doctor marks of the contract table); T005 passes
- [X] T007 Add to `src/server/modules/Identity/Scs.Identity/Domain/` the entities `Role` (`Name` nvarchar(40) required after trimming; `Description` nvarchar(200) null; `Category` `ready` or `custom`; `ReadyKey` `owner`, `manager`, `doctor` or null, set only when ready; `IsRenamed`; `HoldsEverything` true only for `owner`; `CreatedAtUtc`, `CreatedByAccountId` null = system; `UpdatedAtUtc`, `UpdatedByAccountId`; `RowVersion`) and `RolePermission` (`RoleId`, `Code` varchar(64), `ClinicId`), both `IClinicOwned`; add nullable `RoleId` to `StaffAccount` with `AssignRole(Guid?)`; map them in `Infrastructure/Persistence/IdentityDbContext.cs` (unique `ClinicId, Name`; unique `ClinicId, ReadyKey` when set; key `RoleId, Code` with cascade; `StaffAccount.RoleId` foreign key without cascade; index `ClinicId, RoleId`; the checks of data-model.md §2)
- [X] T008 Add the two tables to `src/server/tests/Scs.IntegrationTests/Isolation/IdentitySamples.cs` and the isolation catalogue so the guardrail tests of spec 002 cover them; see them fail for the missing tables
- [X] T009 Create the migration `RolesAndPermissions` in `src/server/modules/Identity/Scs.Identity/Infrastructure/Migrations/` (cached `dotnet-ef` dll as `docs/setup.md`): the two tables with the isolation policy of spec 002, checks and indexes, and `StaffAccount.RoleId`; T008 passes. The roll-out SQL of T027 goes into this same migration before it is applied anywhere but a throw-away test database

**Checkpoint**: The catalogue and its rule exist and are held by tests; the tables exist under isolation.

---

## Phase 3: User Story 2 - No address ships without saying who may call it (Priority: P1)

**Goal**: Every route declares one of four things; a route that declares nothing is refused for everyone and fails the gates.

**Independent Test**: add a probe route with no declaration: the declaration test fails naming it, and calling it as any signed-in person answers 403 `FORBIDDEN`.

*Done before story 1 because story 1's gate reads these declarations.*

### Tests for User Story 2 ⚠️

- [X] T010 [US2] Write failing `src/server/tests/Scs.IntegrationTests/Access/RouteDeclarationTests.cs` over the real `EndpointDataSource`: every route (probes excluded by their `/probe` prefix, listed separately) has exactly one of anonymous, own-account, permission, permission-or-owner; none has two; every declared code is in the catalogue; the own-account routes are exactly `GET /v1/auth/session` and `POST /v1/auth/password/change`; failures name the route
- [X] T011 [US2] In the same file, failing: the undeclared probe `/probe/protected` answers 403 `FORBIDDEN` for a signed-in owner and 401 with no session; `.Requires("no.such.code")` throws when mapped

### Implementation for User Story 2

- [X] T012 [US2] Create `src/server/shared/Scs.BuildingBlocks/Security/AccessControl.cs`: `AccessRuleMetadata` (own-account, permission, permission-or-owner with the check's type); the builders `.OwnAccountOnly()`, `.Requires(code)` and `.RequiresOrOwner<TCheck>(code)` (each throws for a code not in the catalogue or when a rule is already present); `IRecordOwnerCheck`; `IStaffAccess` (`Has`, `ReachOf`, `RoleName`, `Codes`), `StaffReach` (`None`, `Own`, `All`) and `IStaffAccessSource` (`LoadAsync(accountId)` → role name and codes) with the scoped lazy implementation that loads once per request
- [X] T013 [US2] In the same file add `UseScsAccessGate()`: endpoint missing or anonymous → next; own-account → next; nothing declared → 403 `FORBIDDEN`; a permission → next when held (T017 adds the refusal). Register `IStaffAccess` in `AddScsDenyByDefault` of `StaffSessionAuthentication.cs` and place the gate after `UseAuthorization()` in `src/server/host/Scs.Api/Program.cs`
- [X] T014 [US2] Declare `.OwnAccountOnly()` on `GET /v1/auth/session` in `src/server/modules/Identity/Scs.Identity/Api/AuthEndpoints.cs` and on `POST /v1/auth/password/change` in `Api/PasswordEndpoints.cs`; in `src/server/tests/Scs.IntegrationTests/Fixtures/ProbeEndpoints.cs` declare `/probe/whoami` own-account and leave `/probe/protected` undeclared; update `Security/FallbackPolicyTests.cs` and the gate test in `Identity/TemporaryPasswordTests.cs` to the new default; T010 and T011 pass

**Checkpoint**: Nothing can be added to the API without saying who may call it.

---

## Phase 4: User Story 1 - The server refuses what a person's role does not allow (Priority: P1) 🎯 MVP

**Goal**: A request that needs a permission the person's role does not hold is refused before anything is read, naming the permission.

**Independent Test**: sign in as accounts with different roles, call routes that need permissions they hold and do not hold, and confirm each answer and that a refusal ran nothing.

### Tests for User Story 1 ⚠️

- [X] T015 [US1] Add to `src/server/tests/Scs.IntegrationTests/Fixtures/ProbeEndpoints.cs` one probe per catalogue code (`/probe/needs/<code>`, each `.Requires` that code and counting its runs) and `/probe/needs-record/{id}`; add to `Fixtures/` a helper `AccessFixtures.cs` that creates a role with given codes for a clinic, gives it to a new account and signs it in
- [X] T016 [US1] Write failing `src/server/tests/Scs.IntegrationTests/Access/PermissionGateTests.cs`: for each of the 83 codes a holder gets 200 and a person whose role lacks only that code gets 403 `PERMISSION_REQUIRED` with `permission` equal to the code and the probe's counter unchanged; no session → 401; temporary password → `PASSWORD_CHANGE_REQUIRED` before the permission; an account with no role → `PERMISSION_REQUIRED`; the view alone opens a view probe and not its write probe; the refusal for an existing and a missing record id is the same body apart from the trace id; an owner of clinic A cannot reach clinic B's data (answer of spec 002 unchanged); the refusal's log entry holds account, clinic, route and code only

### Implementation for User Story 1

- [X] T017 [US1] Complete the gate in `src/server/shared/Scs.BuildingBlocks/Security/AccessControl.cs`: a missing permission → 403 `PERMISSION_REQUIRED` through `ProblemWriter` with the extension `permission`, and a log entry with identifiers and the code (FR-035); extend `Errors/ProblemWriter.cs` if it cannot carry an extension yet
- [X] T018 [US1] Create `src/server/modules/Identity/Scs.Identity/Infrastructure/StaffAccessSource.cs` implementing `IStaffAccessSource`: one read of the account's role (`HoldsEverything` → every catalogue code; otherwise its `RolePermission` codes that are in the catalogue; no role → none) with the role's name and ready key; register it in `IdentityModule.cs`; T016 passes

**Checkpoint**: Stories 1 and 2 together are the MVP: refuse by default, and by permission.

---

## Phase 5: User Story 3 - One catalogue, and three ready roles in every clinic (Priority: P1)

**Goal**: Every clinic has «مالك العيادة» (all), «مدير العيادة» (59) and «طبيب» (40) from the moment it exists; existing clinics and accounts are brought over; the client has the same catalogue.

**Independent Test**: register a clinic and read its three roles; migrate a database that has clinics and accounts and read theirs; compare the client file with the catalogue.

### Tests for User Story 3 ⚠️

- [X] T019 [P] [US3] Write failing `src/server/tests/Scs.IntegrationTests/Access/ReadyRolesTests.cs`: a registered clinic has exactly three roles with keys `owner`, `manager`, `doctor`, the Arabic names «مالك العيادة», «مدير العيادة», «طبيب», counts 83 (by `HoldsEverything`, no rows), 59 and 40; provisioning twice changes nothing; a clinic that removed «مدير العيادة» does not get it back; an owner holds a code that is in the catalogue and in no row; when the provisioner fails the registration reports it and `ProvisionAsync` afterwards leaves the clinic with its three roles; every stored `RolePermission.Code` is in the catalogue
- [X] T020 [P] [US3] Write failing roll-out cases in `src/server/tests/Scs.IntegrationTests/Persistence/MigrationTests.cs`: migrate to the migration before `RolesAndPermissions`, add two clinics with accounts (one whose oldest account is disabled), migrate up: each clinic has three roles equal — names, keys, flags, codes — to those of a clinic provisioned by `ReadyRoles.EnsureAsync`; the oldest active account without a role holds «مالك العيادة»; no other account has a role; migrating again changes nothing
- [X] T021 [P] [US3] Write failing `src/server/tests/Scs.UnitTests/Access/CatalogueFileTests.cs`: `src/web/projects/core/src/lib/access/permission-catalogue.json` equals what the catalogue writer produces (groups in order with both names; permissions with number, code, both names, group, kind, requires)

### Implementation for User Story 3

- [X] T022 [US3] Create `src/server/modules/Identity/Scs.Identity/Infrastructure/ReadyRoles.cs`: `EnsureAsync` for the established clinic — when the clinic has no role with `HoldsEverything`, add the three ready roles (names of data-model.md §6; descriptions from frame `6675:19376`; manager and doctor rows from `ReadySets`); idempotent
- [X] T023 [US3] Add `IClinicProvisioner` (`ProvisionAsync(clinicId)`) to `src/server/modules/Tenancy/Scs.Tenancy.Contracts/`; call every registered provisioner after the clinic is saved in `Scs.Tenancy/Infrastructure/ClinicRegistry.cs` `RegisterAsync`, each in its own scope with the clinic established; implement `RolesProvisioner` in `Scs.Identity/Infrastructure/` and register it in `IdentityModule.cs`; T019 passes
- [X] T024 [US3] Add `IClinicRoles` with `ProvisionAsync` and `ListAsync` (and the records `RoleSummary`: id, name, ready key, description, category, fixed, permission count) to `src/server/modules/Identity/Scs.Identity.Contracts/`, implemented by `Scs.Identity/Infrastructure/ClinicRoles.cs`; ready roles first in the order owner, manager, doctor, then custom by name
- [X] T025 [US3] Add `GET /v1/roles` in `src/server/modules/Identity/Scs.Identity/Api/AccessEndpoints.cs`, `.Requires(Permissions.AdminRolesView)`, answering `catalogueSize` (counted) and the roles as `contracts/access-api.openapi.yaml`; map it in `IdentityModule.cs`; cases in `src/server/tests/Scs.IntegrationTests/Access/RolesEndpointTests.cs` (holder 200 with three roles; non-holder `PERMISSION_REQUIRED`)
- [X] T026 [US3] Extend the session answer in `src/server/modules/Identity/Scs.Identity/Api/AuthEndpoints.cs` with `role` (`id`, `name`, `readyKey` only while not renamed; null with no role) and `permissions` (codes in catalogue order; empty while `mustChangePassword`); cases in `src/server/tests/Scs.IntegrationTests/Identity/SessionTests.cs`
- [X] T027 [US3] Add the roll-out SQL to the `RolesAndPermissions` migration (data-model.md §5 steps 2 and 3: three ready roles and the 59 and 40 rows for every clinic in `tenancy.Clinic`; «مالك العيادة» for the oldest account without a role per clinic, active first, then `CreatedAtUtc`, then `Id`); T020 passes
- [X] T028 [US3] Extend `IdentityModule.SeedAsync` in `src/server/modules/Identity/Scs.Identity/IdentityModule.cs`: ensure ready roles for each seeded clinic; create «فريق العيادة» in Noor with `bookings.view`, `bookings.manage`, `bookings.attendance`, `patients.view`, `money.view`, `money.collect`; give a role only to a seeded account that has none, as data-model.md §7; update the seed checks in `src/server/tests/Scs.IntegrationTests/Identity/StaffAccountsTests.cs`
- [X] T029 [US3] Add the verb `catalogue <path>` to `src/server/tools/Scs.Migrator/Program.cs` writing the client file from the catalogue (the writer lives in `Scs.SharedKernel/Access/PermissionCatalogueFile.cs`); run it to create `src/web/projects/core/src/lib/access/permission-catalogue.json`; T021 passes

**Checkpoint**: Every clinic, old and new, has its roles; the client holds the same catalogue.

---

## Phase 6: User Story 4 - A write permission needs the view that opens its destination (Priority: P2)

**Goal**: No role can be stored with a set that breaks the requirement rule, an empty set, an unknown code or a bad name.

**Independent Test**: try to store sets that respect and break the rule through `IClinicRoles`.

### Tests for User Story 4 ⚠️

- [X] T030 [US4] Write failing `src/server/tests/Scs.IntegrationTests/Access/ClinicRolesTests.cs` (create and update): a valid set is stored; a set without a requirement → 400, field `permissions` `MISSING_REQUIREMENT` with `missing: [{permission, requires}]`; empty → `REQUIRED`; unknown → `UNKNOWN`; repeated → `DUPLICATE`; name empty → `REQUIRED`, 41 characters → `TOO_LONG`, used in the clinic → `TAKEN` and allowed in another clinic; any update of «مالك العيادة» → 409 `ROLE_FIXED`; updating «طبيب» works and marks it renamed when its name changed; update returns the codes added and removed (changes to one clinic's roles are taken one at a time under the lock, so there is no stale write to refuse here; `RowVersion` is kept for the editor of spec 027)

### Implementation for User Story 4

- [X] T031 [US4] Add `CreateAsync(name, description, codes)` and `UpdateAsync(roleId, name, description, codes)` to `IClinicRoles` and `ClinicRoles.cs`: one transaction under the clinic's application lock `roles` (`sp_getapplock`, as `PasswordLinks`), `PermissionSets.Check`, name rules, `ROLE_FIXED`, rows replaced, `UpdatedAt`/`By` from `ICurrentStaff` when signed in, result `RoleChange(added, removed)`; T030 passes

**Checkpoint**: The requirement rule holds wherever a role is stored.

---

## Phase 7: User Story 6 - A change of role takes effect at once (Priority: P2)

**Goal**: Editing a role is seen by its holders' next request; giving an account another role ends its sessions; a role in use and the last owner are protected.

**Independent Test**: change a role and, separately, an account's role while its holder is signed in; watch the next request.

*Before story 5 because the screen's refresh after a refusal is tested against it.*

### Tests for User Story 6 ⚠️

- [X] T032 [US6] Write failing `src/server/tests/Scs.IntegrationTests/Access/RoleChangeTests.cs`: a signed-in holder is refused on the request right after a code is removed from their role, and allowed right after it is added, with the same session and no sign-in; `AssignAsync` to a different role ends every session of the account with reason `role-changed` and the next request answers `SESSION_ENDED`; assigning the same role ends nothing; `RemoveAsync` of a role an account holds → 409 `ROLE_IN_USE` with `holders`; of «مالك العيادة» → `ROLE_FIXED`; of an unused role works; taking «مالك العيادة» from its last holder → 409 `LAST_OWNER`, also when two requests race; assigning one's own account → 409 `OWN_ROLE`; a role or account of another clinic → 404

### Implementation for User Story 6

- [X] T033 [US6] Add `RemoveAsync(roleId)` and `AssignAsync(accountId, roleId)` to `IClinicRoles` and `ClinicRoles.cs`: same lock and transaction; `ROLE_IN_USE`, `ROLE_FIXED`, `LAST_OWNER` (counting other holders of the owner role), `OWN_ROLE` (when `ICurrentStaff` is the account); on a real change end the account's sessions through `SessionService.EndForAccountCoreAsync` with `SessionEndings.RoleChanged` in the same transaction; T032 passes

**Checkpoint**: Roles can be stored, changed, removed and assigned safely by later specs.

---

## Phase 8: User Story 5 - The screen shows a person only what they may do (Priority: P2)

**Goal**: Controls a person may not use are absent; a destination without its view shows "not in your role"; with the view alone, «للعرض فقط».

**Independent Test**: open `/administration/roles` as a person with both permissions, with the view alone, and with neither.

### Tests for User Story 5 ⚠️

- [X] T034 [P] [US5] Write failing `src/web/projects/core/src/lib/access/access.service.spec.ts`: `holds` follows the session's `permissions`; `role` gives the ready-key name while not renamed and the stored name otherwise; `nameOf(code)` in Arabic and English from the catalogue file; `refresh()` re-reads the session; the timer asks again after 10 minutes and stops at sign-out; the `holds('x')` guard matches only for a holder; `*scsCan` renders nothing for a non-holder (no disabled control) and follows a refresh
- [X] T035 [P] [US5] Extend `src/web/projects/core/src/lib/auth/auth.service.spec.ts`: the session carries `role` and `permissions`; on `PERMISSION_REQUIRED` the interceptor refreshes the session once and re-runs the current navigation, and the error still reaches the caller
- [X] T036 [P] [US5] Write failing `src/web/projects/ui/src/lib/no-access/no-access.component.spec.ts` (mark, title, text with the permission and role names inside the sentence; the no-role sentence) and cases in `src/web/projects/ui/src/lib/controls/sign-in-parts.spec.ts` for `scs-banner` tone `info` with a title (`role="status"`)
- [X] T037 [US5] Write failing `src/web/e2e/access.spec.ts` with answers added to `src/web/e2e/staff-platform.ts` (session with role and codes; `/v1/roles`): owner sees the panel and no notice; view-only person sees «للعرض فقط» naming «إدارة الأدوار والصلاحيات»; a person without the view sees the no-permission state naming «عرض الأدوار والصلاحيات» and their role within one second of opening the address, and no request to `/v1/roles` is made; a `PERMISSION_REQUIRED` from `/v1/roles` under a stale list ends in the no-permission state; `/` opens for a person with no role

### Implementation for User Story 5

- [X] T038 [US5] Create `src/web/projects/core/src/lib/access/access.service.ts` (`AccessService`, catalogue from `permission-catalogue.json`), `access.guard.ts` (`holds(code)` as `CanMatchFn`, loading the session first) and `can.directive.ts` (`*scsCan`); export them from `src/web/projects/core/src/public-api.ts`; extend `StaffSession`, add `refresh()` and the `PERMISSION_REQUIRED` branch of `sessionInterceptor` in `src/web/projects/core/src/lib/auth/auth.service.ts`; T034 and T035 pass
- [X] T039 [P] [US5] Create `src/web/projects/ui/src/lib/no-access/no-access.component.ts` (`scs-no-access`: card, round mark in the danger tint with the `lock` icon, title, text; inputs `permissionName`, `roleName`) from frame `6675:20818`; add tone `info` (`--scs-color-feedback-info-*`) and an optional `title` to `scs-banner` in `src/web/projects/ui/src/lib/controls/controls.ts` from frame `6675:19376`; export from `src/web/projects/ui/src/public-api.ts`; keys in `src/web/projects/ui/i18n/ar.json` and `en.json` (the wording of `contracts/access-screens.md` §3, ready role names); T036 passes
- [X] T040 [US5] Create `src/web/projects/clinic/src/app/administration/roles.page.ts` (breadcrumb, title, the notice for a person without `admin.roles.manage`, the "not available yet" panel, a call to `GET /v1/roles`) and `no-access.page.ts` (breadcrumb, title, `scs-no-access` from route data `permission`); add `/administration/roles` twice to `src/web/projects/clinic/src/app/app.routes.ts` (first `canMatch: [holds('admin.roles.view')]`, then the no-access page, both behind `sessionGuard`); keys in `src/web/projects/clinic/public/i18n/ar.json` and `en.json`; restart the "clinic" preview for the new folder; T037 passes

**Checkpoint**: The two states exist and follow the server.

---

## Phase 9: User Story 7 - Narrow by default: scope permissions and one's own record (Priority: P3)

**Goal**: One way to ask "own only or all", and one way to let a record's owner in.

**Independent Test**: probes for a view with and without its scope permission, and for an owner route.

### Tests for User Story 7 ⚠️

- [X] T041 [P] [US7] Write failing `src/server/tests/Scs.IntegrationTests/Access/ReachAndOwnerTests.cs`: `ReachOf(bookings.view)` and `ReachOf(tickets.view)` answer `None`, `Own`, `All` for no view, the view alone, and with `bookings.viewAll` / `tickets.viewAll`; `ReachOf` of a view with no scope permission answers `All` for a holder; a probe `.RequiresOrOwner<ProbeOwnerCheck>` lets a holder reach any record, an owner reach only their own, and answers the same `PERMISSION_REQUIRED` for another person's record and for a record that does not exist; a scope permission without its view cannot be stored

### Implementation for User Story 7

- [X] T042 [US7] Complete `ReachOf` in the `IStaffAccess` implementation and the owner branch of the gate in `src/server/shared/Scs.BuildingBlocks/Security/AccessControl.cs` (the check is resolved from the request's services and asked only when the permission is missing); add the owner probe and `ProbeOwnerCheck` to `src/server/tests/Scs.IntegrationTests/Fixtures/ProbeEndpoints.cs`; T041 passes

**Checkpoint**: Later modules can ask both questions in one call each.

---

## Phase 10: User Story 8 - The two states are complete, bilingual and as drawn (Priority: P3)

**Goal**: Both states in Arabic and English, mirrored, matching their frames.

**Independent Test**: picture each state in each language and lay it beside its frame.

- [X] T043 [US8] Write `src/web/e2e/access-states.spec.ts`: the five states of `contracts/access-screens.md` §6 in Arabic and English at 1440×900, direction and language attributes right, long permission and 40-character role names wrap inside the card, the names are part of the accessible text; with `SCS_SHOTS` set it saves one picture per state and language
- [X] T044 [US8] Run `SCS_SHOTS=<folder> npx playwright test access-states`, open frames `6675:20818`, `6675:21209`, `6675:19376`, `6675:20097` through the Figma connector, and adjust `no-access.component.ts`, the `info` banner and the two pages until mark, wording, tone, spacing and card match in both languages

**Checkpoint**: All stories are complete.

---

## Phase 11: Polish & Cross-Cutting Concerns

- [X] T045 [P] Add rules to `src/server/tests/Scs.ArchitectureTests/IdentityRulesTests.cs`: only `Scs.BuildingBlocks.Security` writes `PERMISSION_REQUIRED`; no module outside Identity references `Role` or `RolePermission`; `OwnAccountOnly` is used only in `Scs.Identity`
- [X] T046 [P] Write `src/server/tests/Scs.IntegrationTests/Access/GateTimingTests.cs`: 200 calls to a permission probe and 200 to an own-account route with one session; report both medians; fail above 5 ms difference (SC-006)
- [X] T047 [P] Update `docs/adding-a-module.md` ("declare who may call every route", `IStaffAccess`, never compare role names), `docs/setup.md` (seeded roles and who sees which state; the `catalogue` verb) and `ops/runbooks/sign-in.md` ("a person says a page is not in their role")
- [X] T048 [P] Update `specs/003-clinic-sign-in/contracts/` and `specs/004-password-account-setup/contracts/account-handover.md` where they say "permissions arrive with spec 005" and the comment on `ICurrentStaff` in `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs`
- [X] T049 Run every gate: `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T050 Walk the real stack as `quickstart.md` "By hand": migrate and seed, then the seven steps with `owner@noor.example`, `doctor@shifa.example` and `reception@noor.example`, in Arabic and English
- [X] T051 Write `specs/005-permissions-engine/validation.md`: what was run with its counts, what was walked, each state beside its frame in both languages with what matched and differed, the timing figures, and what was not compared (the console bars of spec 006, the «مقترحة» tag, the roles list of spec 027)
- [X] T052 Record the owner items in `validation.md` for the BRD and the design file: codes and English names of rows 80–83; "at the next request" in place of the quarter hour (ق-35); the roll-out rule (FR-036); 79 → 83 in the frames; the «مقترحة» tag; a custom role's name in English; the "no role" sentence; the address `/administration/roles`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → stories.
- **US2 (3)** → **US1 (4)**: the gate reads the declarations.
- **US3 (5)** needs Foundational and US1's access source (T018) for its endpoint and session cases.
- **US4 (6)** needs US3's `ClinicRoles` (T024).
- **US6 (7)** needs US4 (T031).
- **US5 (8)** needs US3 (T025, T026, T029) on the server; its client tasks need nothing else.
- **US7 (9)** needs US1.
- **US8 (10)** needs US5.
- **Polish (11)** last.

### Within Each User Story

Tests are written and seen to fail before the implementation that makes them pass.

### Parallel Opportunities

- T002 alongside T003.
- T005 alongside T003–T004 (different files).
- T019, T020, T021 together.
- T034, T035, T036 together; T039 alongside T038.
- T041 alongside Phase 8.
- T045, T046, T047, T048 together.

## Parallel Example: User Story 3

```text
T019 ReadyRolesTests.cs      (integration)
T020 MigrationTests.cs       (integration)
T021 CatalogueFileTests.cs   (unit)
```

## Implementation Strategy

### MVP First

Phases 1 to 4: the catalogue, declared routes and the gate. After T018 the server refuses by
default and by permission, proven over all 83 codes with test roles. Stop and validate.

### Incremental Delivery

1. Phase 5 — real roles in every clinic, the session answer, the client file.
2. Phases 6 and 7 — the operations later specs call.
3. Phase 8 — the two states on screen.
4. Phases 9 and 10 — scope, owner, and the design comparison.
5. Phase 11 — gates, the real-stack walk, `validation.md`.

## Notes

- 52 tasks. Requirements to tasks: FR-001–007 T003–T006, T021, T029 · FR-008–015, FR-036 T007–T009, T019–T028, T030–T033 · FR-016–018 T005, T006, T030 · FR-019–026 T010–T018, T041, T042 · FR-027–033 T026, T034–T040 · FR-034 T045, T047 · FR-035 T016, T017.
- The implementation is not complete until T049–T051 are done and written down (constitution VII).

---

## Phase 12: Convergence

- [X] T053 Make the `view-only` state of `scs-state` in `src/web/projects/ui/src/lib/state/scs-state.component.ts` draw the same notice as the roles page — the information banner with its mark, the title «للعرض فقط» and a line naming the permission that changing needs (a new input, already translated) — so there is one view-only notice in the shared library and not two that look different; update its cases in `src/web/projects/ui/src/lib/state/scs-state.component.spec.ts`, the demo in `placeholder-panel.component.ts`, and use it from `src/web/projects/clinic/src/app/administration/roles.page.ts`; picture it again against frame `6675:19376` per FR-030 and the assumption "the view-only notice and the no-permission state are shared components" (partial)
- [X] T054 Name the offending codes when a set of permissions is refused: add the unknown and the repeated codes to `PermissionSetProblems` in `src/server/shared/Scs.SharedKernel/Access/PermissionSets.cs` and to the refusal of `ClinicRoles` (`unknown: [...]`, `duplicate: [...]` beside `missing`), with cases in `src/server/tests/Scs.UnitTests/Access/PermissionSetsTests.cs` and `src/server/tests/Scs.IntegrationTests/Access/ClinicRolesTests.cs`, and the two members in `specs/005-permissions-engine/contracts/access-engine.md` §5 per Edge Cases "refused naming the code" (partial)
