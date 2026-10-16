# Tasks: Operators and Their Roles

**Input**: Design documents from `/specs/011-operators-roles/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (operators-roles-api.md, operator-access.md, operator-screens.md), quickstart.md

**Tests**: required by the constitution (Principle VII). Access tests, the grant rule's tests and the last-super-admin tests are written before what they test and must fail first (permissions are a test-first area). Tests of spec 010 are not loosened: where a route's declaration is renamed, its behaviour and its test stay.

**Organization**: by user story. Server paths are under `src/server/`, console paths under `src/web/`. `Ops/` means `src/server/modules/Operators/Scs.Operators/`; `ops-app/` means `src/web/projects/ops/src/app/`; `ui/` means `src/web/projects/ui/src/lib/`; test folders are `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/` and `Scs.ArchitectureTests/`; browser tests are `src/web/e2e/`. Frames are in the operators' design file `SetO9EhDivNcmurAMfcflY`, page "Template — Operators" (`1615:70`). Operators console code stays in `Ops/` and `ops-app/`; only what a second console could use goes into the shared projects.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Record the baseline in `specs/011-operators-roles/validation.md` as spec 094's last full run (server 823 unit, 63 architecture, 751 integration; web 68 / 164 / 102 / 11 / 1; browser 691 passed, 4 skipped), saying whether it was run again before this work
- [X] T002 Read every frame of page `1615:70` not yet seen as a picture, in Arabic and English, through the Figma connector — edit role (`1751:813`, `1752:884`), delete role blocked (`2788:16350`, `2788:16128`), reset confirm (`1720:112`, `1720:304`), edit operator (`1703:70`, `1705:70`), last super admin (`2260:1198`, `2259:1108`), and the English twins of the list, the operator's page, add operator, the roles list and add role — and the component pages the new controls come from (Checkbox `35:2`, Dropdown `57:2`, Switch `35:4`, Textarea `41:3`, Badge `8:36`, KPI Card `8:39`, Navigation `65:2`, Table `273:2`, Modal `62:2`, Menu `368:2`); correct `contracts/operator-screens.md` where a word or a part differs, and note each difference
- [X] T003 [P] Add the error codes of `contracts/operators-roles-api.md` to `shared/Scs.SharedKernel/ErrorCodes.cs` (`OPS_EMAIL_TAKEN`, `OPS_ROLE_NOT_GRANTABLE`, `OPS_OWN_ROLE`, `OPS_LAST_SUPER_ADMIN`, `OPS_OWN_ACCOUNT`, `OPS_STATE_CHANGED`, `OPS_LINK_TOO_SOON`, `OPS_CHANGED_ELSEWHERE`, `OPS_LINK_NOT_SENT`, `OPS_ROLE_NAME_TAKEN`, `OPS_PERMISSIONS_NOT_HELD`, `OPS_OWN_ROLE_EDIT`, `OPS_SYSTEM_ROLE`, `OPS_ROLE_IN_USE`) and their words in both languages to `shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs`, with the unit test that every code has both

---

## Phase 2: Foundational (blocks every story)

- [X] T004 Write failing `tests/Scs.UnitTests/Operators/OperatorPermissionsTests.cs`: the catalogue has exactly 28 codes in nine groups with the counts 1 · 7 · 6 · 1 · 2 · 3 · 5 · 1 · 2; codes are unique; every "needs" names a code of the catalogue and no chain is longer than one; `security.manage` needs nothing and `billing.managePlans` needs `billing.view`; each entry has an Arabic and an English name equal to `data-model.md` §1; `WithRequired(codes)` adds what is needed and nothing else
- [X] T005 Create `shared/Scs.SharedKernel/Access/OperatorPermissions.cs` per `data-model.md` §1 — the 28 codes as constants, their groups, names in both languages and "needs"; `WithRequired`, `IsKnown`, `Groups`; and the three system roles' sets (`viewer`: the eleven views of `data-model.md` §3; `billing-admin`: its eleven; `super-admin`: all); T004 passes
- [X] T006 Write failing `tests/Scs.IntegrationTests/Operators/OperatorGateTests.cs` against probe routes: no session → 401; a clinic's session → 401; an operator whose role lacks the code → 403 `PERMISSION_REQUIRED` with `permission`; one whose role holds it → served; a route that declares nothing → 403 `FORBIDDEN`; the answer is the same whether the record asked for exists; removing the code from the role refuses the same session's next request without ending it, and adding it serves the next
- [X] T007 Create `shared/Scs.BuildingBlocks/Security/OperatorAccessControl.cs`: `.RequiresOperator(code)` and its metadata; the gate that runs before the handler; `IOperatorAccess` (`HasAsync`, `GetAsync`, `IsSuperAdminAsync`) as a scoped contract the Operators module implements; a start-up check that every declared code is in the catalogue; rename `.OperatorsOnly()` to `.OperatorOwnAccount()` in `shared/Scs.BuildingBlocks/Security/OperatorSessionAuthentication.cs` and at every use in `Ops/Api/OperatorEndpoints.cs`, changing nothing of what those routes do (research R2)
- [X] T008 [P] Create `Ops/Domain/Role.cs` (`Role`, `RolePermission`, `RoleSystemKeys`) per `data-model.md` §2–§3: `Name` "1–100 characters after trimming", `NameKey` trimmed and lower-cased, `Description` "up to 500", `SystemKey` one of `super-admin`, `billing-admin`, `viewer` or null, `DeletedAtUtc`, `RowVersion`; a custom role "has at least one" permission and only codes of the catalogue; a system role cannot be renamed, changed or deleted; with unit tests in `tests/Scs.UnitTests/Operators/RoleTests.cs`
- [X] T009 [P] Extend `Ops/Domain/Operator.cs` per `data-model.md` §4: `RoleId`, `Department` (one of `support`, `billing`, `success`, `sales`, `management`), `JobTitle` "up to 150", `TwoStepRequired` (default true), `InvitedById`, `InvitedByName`, `ActivatedAtUtc`, `SuspendedAtUtc`; `Invite(...)` takes them; `Edit(...)`, `ChangeRole(...)`, `Suspend` (active only), `Reactivate` (suspended only; clears wrong tries, spent codes, lock), `Unlock` (active and locked only); `Activate` stamps `ActivatedAtUtc`; unit tests for every guard in `tests/Scs.UnitTests/Operators/OperatorPiecesTests.cs`
- [X] T010 Map both in `Ops/Infrastructure/Persistence/OperatorsDbContext.cs` (the filtered unique index on `NameKey`, the unique `SystemKey`, the check that a system role is never deleted, `RoleId` required with restrict, the department check, the indexes `(CreatedAtUtc DESC)` and `(RoleId)`) and add the migration `Ops/Infrastructure/Migrations/…_OperatorsRoles.cs`: create the tables, insert the three system roles with fixed ids and their codes, add the columns, give every existing operator the super admin role, fill `ActivatedAtUtc` from the first-password record of active accounts, then make `RoleId` required; add `ops.Role` and `ops.RolePermission` to `shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs` with the reasons of `data-model.md` §6; cover the migration's effect on existing rows in `tests/Scs.IntegrationTests/Operators/OperatorsRolesMigrationTests.cs` (FR-027b)
- [X] T011 Create `Ops/Infrastructure/OperatorAccess.cs` (`IOperatorAccess` over the caller's role, read once per request, never cached between requests) and register it in `Ops/OperatorsModule.cs`; add role fixtures to `tests/Scs.IntegrationTests/Fixtures/OperatorFixtures.cs` — an operator with a named system role, or with a custom role of given codes; T006 passes
- [X] T012 Add the platform's own trail (research R11): the migration `modules/Audit/Scs.Audit/Infrastructure/Migrations/…_PlatformEntry.cs` creating `audit.PlatformEntry` per `data-model.md` §5 with its append-only trigger and the index `(ActorAccountId, OccurredAtUtc DESC)`; the line in `PlatformWideTables.cs`; in `shared/Scs.BuildingBlocks/Audit/AuditWriter.cs` and `AuditRecord.cs` a way to record an entry with no clinic whose actor is the operator of the session, saved in the caller's transaction; the ten action codes and two target kinds of `data-model.md` §5 in the shared kernel's catalogue of actions; `IOperatorAuditReading` in `modules/Audit/Scs.Audit/Infrastructure/AuditReading.cs` gains "the last entries by this operator"; tests in `tests/Scs.IntegrationTests/Audit/PlatformEntryTests.cs` — written with its change or not at all, never changed or removed, never visible to a clinic's reading, the actor never taken from a caller
- [X] T013 Extend `tests/Scs.ArchitectureTests/OperatorRulesTests.cs`: every route under `/v1/ops/` declares exactly one of `.Open()`, `.OperatorOwnAccount()`, `.RequiresOperator(code)`; the own-account routes are exactly the list of `contracts/operator-access.md` §1; every code of the operators group (`operators.view`, `operators.manage`, `operators.manageRoles`) guards at least one route (it fails until US3 and US6 add them — leave it failing and say so); no code outside `Scs.SharedKernel` compares a role's name or counts permissions to decide who is a super admin
- [X] T014 Make the session and the profile carry the role: `Ops/Infrastructure/OperatorSessions.cs` and `OperatorProfile.cs` and `Ops/Api/OperatorEndpoints.cs` return `role { id, name, systemKey }` and `permissions[]` on `GET /v1/ops/auth/session`, and the role on `GET /v1/ops/profile`, per `contracts/operators-roles-api.md` §1; `Ops/../Scs.Operators.Contracts/OperatorContracts.cs` adds the role to `OperatorSummary`; update spec 010's tests of those two reads for the added fields only
- [X] T015 [P] Create `ops-app/i18n/ops-format.ts` (research R15): `opsNumber`, `opsDate`, `opsDateTime`, `opsRelative` — always Western digits with the language's words, the relative forms of `contracts/operator-screens.md` §0 — with `ops-app/i18n/ops-format.spec.ts` covering both languages, yesterday/today, and that no Arabic-Indic digit ever comes out
- [X] T016 [P] Add to the shared `ui` library the controls that are missing there, each from its component page of the design file and each with its spec file: checkbox, select, switch, textarea and status mark in `ui/controls/`, KPI card and breadcrumb in `ui/list/` (research R16); disabled states carry a reason that a screen reader reads; all work in both directions and at 390 wide
- [X] T017 Create the console's access pieces (research R14, `contracts/operator-access.md` §6): `ops-app/access/ops-access.service.ts` (the role and codes as signals, `can(code)`), `ops-can.guard.ts` (sends to the "can't open this page" state), `ops-can.directive.ts` (`*opsCan`); `ops-app/auth/ops-auth.service.ts` reads the role and codes with the session and reads the session again on navigation and after a 403 `PERMISSION_REQUIRED`; `ops-app/shell/sections.ts` gives each section its permission and adds «المشغلون» behind `operators.view`; `ops-app/shell/ops-shell.component.ts` shows the role's name under the operator's name; unit tests beside each
- [X] T018 Extend `src/web/e2e/ops-platform.ts` to answer the session with a role and codes, and to hold operators and roles in memory for the addresses of `contracts/operators-roles-api.md` with their refusals, so that browser tests can sign in as a super admin, a viewer or a custom role

**Checkpoint**: a route can name a permission and is refused without it; roles exist in the database; every existing operator is a super admin; the console knows what its operator may do.

---

## Phase 3: User Story 1 - What an operator can do is what their role allows (P1) 🎯 MVP

**Goal**: the rule under every request, and the console showing only what the role allows.

**Independent test**: sign in as a super admin, a viewer and a one-permission role; compare the side bar, the pages that open and direct requests.

- [X] T019 [P] [US1] Write `tests/Scs.IntegrationTests/Operators/OperatorAccessTests.cs` (failing first): every route spec 010 declared as own-account still answers for an operator of any role, including one whose role holds a single unrelated code (FR-003's three); none of them answers for a clinic's session; the session read returns the role and exactly the role's codes plus what they need; after a role's codes change, the same session's next `GET /auth/session` returns the new set and no session was ended (FR-005, SC-009)
- [X] T020 [US1] Make T019 pass in `Ops/Infrastructure/OperatorAccess.cs` and the session read; make the deployment command `operator invite` in `Ops/Infrastructure/OperatorInvite.cs` and `tools/Scs.Migrator/` give the super admin role and record `operator.invited` with `by: deployment` (FR-027a), with its integration test updated in `tests/Scs.IntegrationTests/Operators/`
- [X] T021 [P] [US1] Write `src/web/e2e/ops-access.spec.ts` (failing first), in Arabic and English: a super admin sees «الرئيسية» and «المشغلون»; an operator without `operators.view` does not see «المشغلون» and its address shows "can't open this page" with a way back; a viewer opening `/operators/new` or `/operators/roles/new` by address gets the same state; the top bar shows a system role's name in the page's language and a custom role's as written; a 403 `PERMISSION_REQUIRED` from any request re-reads the session and the side bar follows
- [X] T022 [US1] Build what T021 needs in `ops-app/app.routes.ts` (each route behind `opsCan`), `ops-app/shell/` and a "can't open this page" page in `ops-app/home/home.pages.ts`, with the words of `contracts/operator-screens.md` §0 in `src/web/projects/ops/public/i18n/{ar,en}.json`
- [X] T023 [US1] Move every number, countdown, date and time of spec 010's screens onto `ops-format.ts` — `ops-app/sign-in/`, `ops-app/password/password.pages.ts`, `ops-app/profile/profile.page.ts` (FR-049, `contracts/operator-screens.md` §11); extend `src/web/e2e/ops-look.spec.ts` so that every `ops` screen in Arabic fails on any Arabic-Indic digit; update spec 010's browser tests that read a digit

**Checkpoint**: the console answers to roles; nothing of it shows or serves what the role does not allow.

---

## Phase 4: User Story 3 - The team in one list, each operator on one page (P1)

**Goal**: the «المشغلون» tab and an operator's page. (Built before US2 because inviting ends on the operator's page.)

**Independent test**: with a dozen operators in different states and roles — search, filter, page, open one.

- [X] T024 [P] [US3] Write `tests/Scs.IntegrationTests/Operators/OperatorsListTests.cs` (failing first): access (no session, clinic's session, without `operators.view`, with it); newest created first; `search` matches part of a name and of an email, case-insensitively; `state` and `roleId` filter; `total`, `page`, `pageSize` 8; the cards — total, active, activated since the first of the month in Cairo time, invited, two-step on — agree with the rows; `filters` offer only the states and roles that exist; no row carries a hash, a link or a lock's details; 200 operators answer within a second (SC-008)
- [X] T025 [P] [US3] Write `tests/Scs.IntegrationTests/Operators/OperatorDetailTests.cs` (failing first): access; the fields of `contracts/operators-roles-api.md` §2; `invitedBy` null for an account made by the deployment command; `locked` true only while locked; `permissions` are the role's; `performance` holds `activeSessions`; `activity` is the newest ten of the account's successful sign-ins and of the platform entries it is the actor of, merged by time; `can` reflects the caller (nothing for a viewer; no `suspend` on one's own account; `unlock` only when locked; `reactivate` only when suspended); 404 for an unknown id
- [X] T026 [US3] Create `Ops/Infrastructure/OperatorsDirectory.cs` and `Ops/Api/OperatorsEndpoints.cs` with `GET /v1/ops/operators` and `GET /v1/ops/operators/{id}` behind `operators.view`, each declaring `.RecordsNothing(...)`; T024 and T025 pass
- [X] T027 [P] [US3] Create `ops-app/operators/operators.service.ts` (the reads and, later, the writes) and `ops-app/operators/activity-sentences.ts` (the sentences of `contracts/operator-screens.md` §2 in both languages), with unit tests
- [X] T028 [US3] Build the page with its two tabs and the operators tab per `contracts/operator-screens.md` §1 and frames `1615:71`/`1615:225`: `ops-app/operators/operators.page.ts`, `operators-list.component.ts`, `operator-filters.component.ts` — header, cards, search, filter panel with only the values that exist, table, row menu, count and pager, and the four states (loading, empty, nothing matching, failed); the add button and «تعديل المشغل» only with `operators.manage`; words in the two i18n files; unit tests for the states and for what a viewer does not see
- [X] T029 [US3] Build an operator's page per `contracts/operator-screens.md` §2 and frames `1658:70`/`1663:70` in `ops-app/operators/operator.page.ts`: breadcrumb, head, the buttons the read's `can` allows (their actions come with US4 and US5), «معلومات المشغل», the role's permissions as marks, the active-sessions card, the activity list, and its loading, not-found and failed states
- [X] T030 [US3] Write `src/web/e2e/ops-operators.spec.ts` for the list and the page, in Arabic and English at 1440 and 390: every part and word of §1 and §2, the four states, search and filters, the pager, a row opening the page, what a viewer sees and does not; pictures when `SCS_SHOTS` is set; `opsLooksWrong` on each
- [X] T031 [US3] Take the pictures, **open every one**, lay the list and the page beside `1615:71`, `1615:225`, `1658:70`, `1663:70`, and correct what differs; record what stays different by decision (no export button, one performance card, the phone field's name)

**Checkpoint**: the team can be seen, searched and opened.

---

## Phase 5: User Story 2 - An operator invites a colleague, who joins by email (P1)

**Goal**: «إضافة مشغل جديد», the invitation email naming the inviter and the role, and no link on any screen.

**Independent test**: invite an address, open the email in the local mailbox, set a password, sign in.

- [X] T032 [P] [US2] Write failing `tests/Scs.UnitTests/Operators/RoleGrantsTests.cs` over every combination (research R4): a role may be given when the giver is a super admin, or when it is not the super admin's role and each of its codes is held by the giver; the super admin's role only by a super admin; nobody their own role; a code may be added to a role by a super admin or by a holder of it, and what the role had stays; a non-super-admin may not edit the role they hold; copying takes the intersection with the creator's codes and reports what was left out
- [X] T033 [US2] Create `Ops/Domain/RoleGrants.cs`; T032 passes
- [X] T034 [P] [US2] Write `tests/Scs.IntegrationTests/Operators/OperatorInviteTests.cs` (failing first): access for `POST /operators` and `GET /roles/options`; a valid body creates an `invited` account with no usable password, the given fields, `invitedBy` the caller, and an invitation owed that the job sends in the invited person's language naming the inviter and the role with "72" hours; `sendInvitation: false` owes nothing and sends nothing; the answer and every later read hold no link or token; each limit of FR-028 ("1–200", a well-formed and unique email kept in lower case, mobile "up to 30", the five departments, job title "up to 150", `ar`/`en`) refuses with `VALIDATION_FAILED` naming its field; a used email → 409 `OPS_EMAIL_TAKEN`; a role with a code the caller lacks, and the super admin's role for a non-super-admin → 403 `OPS_ROLE_NOT_GRANTABLE`; a deleted or unknown role → refused; `roles/options` marks exactly those roles not grantable with their reason; `operator.invited` is recorded with the role's name and holds no link; the email, the name and the link are in no log line
- [X] T035 [US2] Extend `Ops/Infrastructure/OperatorInvite.cs` with the role, the optional fields, the inviter and "send now"; create `Ops/Infrastructure/OperatorManagement.cs` with inviting; add `POST /v1/ops/operators` to `Ops/Api/OperatorsEndpoints.cs` behind `operators.manage` declaring `.Records("operator.invited")`; create `Ops/Infrastructure/Roles.cs` with the options read and `Ops/Api/RolesEndpoints.cs` with `GET /v1/ops/roles/options` behind `operators.manage`; T034 passes
- [X] T036 [US2] Give the invitation email its inviter and role: `Ops/Infrastructure/OperatorEmails.cs` `FirstPassword` takes them as optional values and writes the frame's body (`2227:216`/`2227:195`) with 72 hours when they are present, today's body when they are not; add the variant to `Ops/Infrastructure/OperatorEmailSamples.cs`; update `tests/Scs.UnitTests/Operators/OperatorPiecesTests.cs`, `specs/094-email-template/contracts/emails.md` §C3 and the catalogue's expected list in `tests/Scs.UnitTests/Mail/EmailCatalogueTests.cs`; the emails' browser tests still pass
- [X] T037 [US2] Build the form per `contracts/operator-screens.md` §3 and frames `1690:70`/`1692:70` in `ops-app/operators/operator-form.page.ts` (add mode) behind `operators.manage`: the fields with their limits and placeholders, the role picker drawn from `roles/options` with roles that may not be granted disabled and their reason under them, the role's permissions once one is chosen, the two-step switch on and locked with its line, the invitation switch, the errors at their fields with focus on the first, and after success the new operator's page with its toast — no link anywhere; words in the i18n files; unit tests
- [X] T038 [US2] Extend `src/web/e2e/ops-operators.spec.ts`: the add form in both languages at 1440 and 390 — every word, required and malformed fields, an email already used, a disabled role with its reason, the server's refusal shown at the role field, the switch off changing the button, success landing on the operator's page; nowhere on any screen after inviting is there an address containing a token; pictures; then **open every picture**, lay the form beside `1690:70`/`1692:70`, and correct what differs
- [X] T039 [US2] Hold the two lives end to end in `tests/Scs.IntegrationTests/Operators/OperatorInviteTests.cs`: an invitation link opened after 72 hours answers as ended and refers the person to whoever invited them; a reset link after 30 minutes offers a new one (FR-043, `OP-B-392`; built by spec 010, proved here for an account invited from the console)

**Checkpoint**: the team grows from the console; a new operator joins with nothing but their mailbox.

---

## Phase 6: User Story 4 - An operator's details and role are changed, within guards (P2)

**Goal**: «تعديل بيانات المشغل» with its three refusals; a changed role signs the person out.

**Independent test**: edit a department and a role; try own role, a role not allowed, the last super admin.

- [X] T040 [P] [US4] Write `tests/Scs.IntegrationTests/Operators/OperatorEditTests.cs` (failing first): access; every field but the email changes, and a body that carries an email changes nothing of it; `note` "up to 500"; a stale `version` → 409 `OPS_CHANGED_ELSEWHERE`; one's own role → 403 `OPS_OWN_ROLE` while one's other fields save; a role not grantable → 403 `OPS_ROLE_NOT_GRANTABLE`; the only active super admin's role → 409 `OPS_LAST_SUPER_ADMIN`; a changed role ends every session of the account with the reason "role changed" in the same transaction, and a change of name or department ends none; `operator.edited` holds each changed field before and after and the note, `operator.role-changed` the two role names
- [X] T041 [P] [US4] Write `tests/Scs.IntegrationTests/Operators/LastSuperAdminTests.cs` (failing first): with two active super admins, two requests at the same moment that each change one of them to another role leave exactly one refused and one super admin active; the same for one role change and one suspension; an invited or suspended super admin does not count as active (SC-005)
- [X] T042 [US4] Create `Ops/Infrastructure/SuperAdminGuard.cs` (the application lock and the count, research R5) and add editing to `Ops/Infrastructure/OperatorManagement.cs` and `PUT /v1/ops/operators/{id}` to `Ops/Api/OperatorsEndpoints.cs` behind `operators.manage` declaring `.Records("operator.edited", "operator.role-changed")`; T040 and T041 pass
- [X] T043 [US4] Add edit mode to `ops-app/operators/operator-form.page.ts` per `contracts/operator-screens.md` §4 and frames `1703:70`, `2260:1012`, `2260:1105`, `2260:1198` and their English twins: the email shown and not editable, no invitation switch, the two-step line of edit, «ملاحظة على التعديل (اختياري)» with its hint, the own-role field locked with its sentence, the two server refusals at the role field, the "changed elsewhere" banner with reload, and after saving the operator's page with «تم تحديث بيانات المشغل.»; wire «تعديل» on the operator's page and «تعديل المشغل» in the row menu
- [X] T044 [US4] Extend `src/web/e2e/ops-operators.spec.ts` for editing in both languages at 1440 and 390 — each of the three refusals as its frame draws it, the banner, the success — and for the other browser: an operator whose role was changed is on the sign-in screen with «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.»; pictures; **open every one** and lay them beside their frames

**Checkpoint**: roles move with people, and nobody can climb or lock the console out.

---

## Phase 7: User Story 5 - Suspend, reactivate, send a link, unlock (P2)

**Goal**: the account's four actions from the operator's page.

**Independent test**: suspend a signed-in operator; reactivate; send a link twice in a minute; lock and unlock.

- [X] T045 [P] [US5] Write `tests/Scs.IntegrationTests/Operators/OperatorAccountActionsTests.cs` (failing first): access for the four addresses; suspend — active only (else 409 `OPS_STATE_CHANGED`), `reason` "up to 500", one's own account → 403 `OPS_OWN_ACCOUNT`, the last active super admin → 409 `OPS_LAST_SUPER_ADMIN`, every session ended with the reason "suspended", and a later sign-in with the right password answered «هذا الحساب موقوف.» while a wrong one gets the single answer (FR-047); reactivate — suspended only, state `active`, wrong tries, spent codes and lock cleared; password link — active gets a reset link of 30 minutes, invited gets the invitation anew of 72 hours and the earlier link stops working, suspended → 409, a second within a minute → 409 `OPS_LINK_TOO_SOON` with nothing sent and nothing recorded, a mail failure → 503 `OPS_LINK_NOT_SENT` with nothing stored; unlock — only an active locked account, cleared, and the right password then signs in; each success recorded (`operator.suspended` with the reason, `.reactivated`, `.link-sent` with its kind, `.unlocked`); no answer or entry holds a link
- [X] T046 [US5] Add the four actions to `Ops/Infrastructure/OperatorManagement.cs` (suspension through `SuperAdminGuard` and `OperatorSessions.EndAllAsync` in one transaction; the link through `Ops/Infrastructure/OperatorPasswords.cs` with the one-a-minute rule of research R9) and their addresses to `Ops/Api/OperatorsEndpoints.cs` behind `operators.manage` with their `.Records(...)`; T045 passes
- [X] T047 [US5] Build `ops-app/operators/suspend.dialog.ts` and `send-link.dialog.ts` per `contracts/operator-screens.md` §5–§6 and frames `1708:70`/`1713:70`, `1720:112`/`1720:304`, and wire the five buttons of the operator's page — suspend (with its reason field and the last-super-admin sentence inside the dialog), reactivate (no dialog, its working label), reset password, unlock, edit — each shown only when the read's `can` allows; toasts and failures in both languages; the page re-reads after each
- [X] T048 [US5] Extend `src/web/e2e/ops-operators.spec.ts` in both languages at 1440 and 390: each dialog's words, its busy and refused states, focus kept inside and returned; no suspend button on one's own page; the suspended operator's other browser showing «تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.»; the "less than a minute" sentence; unlock appearing only for a locked account; pictures; **open every one** and lay the two dialogs beside their frames

**Checkpoint**: a person leaves the team or gets back in without the deployment command.

---

## Phase 8: User Story 6 - Roles are made from the catalogue, within guards (P2)

**Goal**: the «الأدوار والصلاحيات» tab, the role form, and deleting a role.

**Independent test**: create a role from an existing one, edit it, try to delete it while held, move the holder, delete it.

- [X] T049 [P] [US6] Write `tests/Scs.IntegrationTests/Operators/RolesTests.cs` (failing first): access for the six role addresses and `GET /permissions` (`operators.view` reads the list; the form's reads and every write need `operators.manageRoles`); the list — system roles first then custom oldest first, the count of holders, `inUse`, the number of permissions, the four cards, `can` for the caller; create — name "1–100 characters after trimming", description "up to 500", at least one code, only codes of the catalogue, a name already used (compared trimmed and without case, system names included) → 409 `OPS_ROLE_NAME_TAKEN`, the needed codes saved with the given ones, codes the caller does not hold → 403 `OPS_PERMISSIONS_NOT_HELD` naming them, `basedOnId` giving the intersection with the caller's codes; edit — a system role → 403 `OPS_SYSTEM_ROLE`, the role the caller holds when not a super admin → 403 `OPS_OWN_ROLE_EDIT` while a super admin may, codes the role already had stay even when the caller lacks them, a stale `version` → 409, holders' sessions not ended and their next request answered by the new set; delete — a held role → 409 `OPS_ROLE_IN_USE` with the count, an unheld one hidden from the list, from `roles/options` and from `GET /roles/{id}` while earlier entries keep its name, a system role refused; `GET /permissions` gives the nine groups and `mayGrant` for the caller; the three actions recorded with what `data-model.md` §5 says
- [X] T050 [US6] Complete `Ops/Infrastructure/Roles.cs` (list, read, catalogue, create, edit, delete) and `Ops/Api/RolesEndpoints.cs` with their declarations and `.Records(...)`; T049 passes; the architecture test of T013 "every code of the operators group guards a route" now passes
- [X] T051 [US6] Build the roles tab per `contracts/operator-screens.md` §7 and frames `1735:557`/`1735:772` in `ops-app/operators/roles-list.component.ts`: the header and its button with `operators.manageRoles`, the four cards, the search (on the client), the table with its six columns and Arabic counts in their grammatical form, no menu on a system role's row, the count with no pager, and its loading, nothing-matching and failed states
- [X] T052 [US6] Build `ops-app/operators/role-form.page.ts` per §8 and frames `1744:676`/`1749:744`, `1751:813`/`1752:884` behind `operators.manageRoles`: name, "base on an existing role" with its hint of what was left out (add only), description, «حدد الكل» over the nine groups in the catalogue's order and names, a needed box ticked and disabled with its reason and freed when nothing needs it, a box the caller may not tick disabled with its reason, the note (edit only), the read-only form with its banner for the role one holds, the errors at their fields, and the return to the roles tab with its toast
- [X] T053 [US6] Build `ops-app/operators/delete-role.dialog.ts` per §9 and frames `2788:15904`/`2788:15681`, `2788:16350`/`2788:16128`: the two states — nobody holds it (the sentence, the note, the danger button) and held (the two sentences with the count, no note, the button disabled) — and the server's refusal if a holder appeared meanwhile
- [X] T054 [US6] Write `src/web/e2e/ops-roles.spec.ts` in both languages at 1440 and 390: the tab and its states; the form — the 28 boxes in nine groups, «إرسال إشعارات للعيادات» alone under «التنبيهات» and ticking it ticking «عرض العيادات», «عرض كل التذاكر» locking «عرض التذاكر المُسندة إليّ», "select all" ticking only what the caller may, a disabled box with its reason, copying a role as a non-super-admin with its hint, the read-only form for one's own role; the two delete dialogs; keyboard through the form and the dialogs; pictures; then **open every one**, lay each beside its frame, and correct what differs

**Checkpoint**: the team is given exactly what each job needs, and no role outgrows the person who made it.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T055 [P] Write `tests/Scs.IntegrationTests/Operators/NobodyGainsTests.cs` for SC-006 across every path at once: for a caller holding an arbitrary subset of codes, after inviting, editing an operator, creating, editing and copying a role through the addresses, no operator other than a super admin's grantee holds a code the caller did not hold
- [X] T056 [P] Extend the log tests of spec 010 (`tests/Scs.IntegrationTests/Operators/OperatorPasswordAndSessionTests.cs`, "no password, code, link or address reaches the logs") to the new addresses: after inviting, editing, suspending with a reason, sending a link and deleting a role with a note, the captured logs hold no email, name, link, reason or note — identifiers only (FR-051)
- [X] T057 [P] Hold the keyboard and screen-reader promises of `contracts/operator-screens.md` §12 in `src/web/e2e/ops-operators.spec.ts` and `ops-roles.spec.ts`: the tab list, the table's caption and headers, the row menu by Enter and Escape with focus returned, dialogs named and trapping focus, a disabled box or role describing its reason, toasts announced, focus on the first field in error, controls at least 40 by 40 at 390
- [X] T058 [P] Update `docs/setup.md` (roles and who may do what in the console; the walk accounts), `ops/runbooks/operators.md` (the deployment command as the first operator and as recovery when no super admin can sign in; that it always gives «مسؤول عام»), and `docs/adding-a-module.md` (a console route declares one of three, per `contracts/operator-access.md`)
- [X] T059 Run every gate with nothing else running: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test`; fix what fails; no size warning in the `ops` build
- [X] T060 Walk the real stack as `quickstart.md` "By hand", all sixteen steps, with walk accounts only — never the seeded operator; read the API's log of the walk
- [X] T061 Look at everything once more as a whole: with `SCS_SHOTS`, take every screen and state of this spec and of spec 010's screens in Arabic and English at 1440 and 390, **open every picture**, and fix what is cut off, misaligned, in the wrong direction, in the wrong language, or different from its frame; re-take after any fix; also in Edge
- [X] T062 Make `specs/011-operators-roles/design-vs-real.png`: every frame of page `1615:70` in both languages beside the screen as built, and open it
- [X] T063 Write `specs/011-operators-roles/validation.md`: what was run with its counts against the baseline of T001; what was walked and seen; which pictures were opened; beside the design, with every difference and whose decision it is; what was built differently from the plan; the words marked **(owner)** and the items for the designer and for the BRD; and what was **not** checked

---

## Dependencies & Execution Order

- **Setup (T001–T003)** then **Foundational (T004–T018)**: block everything. T004 before T005; T006 before T007 and T011; T005, T008, T009 before T010; T010 before T011; T007 before T013 and T014; T015–T016 before any screen; T017 after T014; T018 after T017.
- **US1 (T019–T023)**: needs the foundation. **US3 (T024–T031)**: after US1. **US2 (T032–T039)**: after US3 (it lands on the operator's page); T032–T033 before T035; T036 after T035.
- **US4 (T040–T044)**: after US2 (the same form). **US5 (T045–T048)**: after US3; uses `SuperAdminGuard` of T042, so after US4.
- **US6 (T049–T054)**: after the foundation and T033; independent of US4 and US5. T050 closes the architecture test left failing by T013.
- **Polish (T055–T063)**: last; T059 → T060 → T061 → T062 → T063 in order.

### Parallel opportunities

- T003 beside T002. T008, T009, T015, T016 together once T005 is done.
- The test tasks that open each story — T019 and T021; T024 and T025; T032 and T034; T040 and T041 — in pairs.
- US6's server work (T049–T050) beside US4 and US5's.
- T055–T058 together.

## Implementation Strategy

1. **MVP**: Setup, Foundational and US1 — the console answers to roles. Stop and look: three operators, three side bars, direct requests refused.
2. Then US3 and US2 — the team is seen and grows from the screen. Walk an invitation end to end on the real stack before going on.
3. Then US4, US5 and US6, and the polish phase with the gates, the walk, every picture opened and `validation.md`.

## Notes

- Requirements → tasks: FR-001, FR-002 T004–T005 · FR-003, FR-004 T006–T007, T013 · FR-005 T006, T019, T049 · FR-006–FR-008 T017, T021–T022, T028 · FR-009 T034, T037, T043, T052 · FR-010, FR-011 T005, T008, T010, T032–T033 · FR-012–FR-021 T049–T054 · FR-022–FR-025 T024, T026, T028, T030 · FR-026 T025, T029 · FR-027 T024–T026 (no address deletes) · FR-027a T020 · FR-027b T010 · FR-028–FR-034 T034–T038 · FR-035–FR-039 T040–T044 · FR-040 T037, T043 · FR-041–FR-047 T045–T048 · FR-048 T012 and each story's tests · FR-049 T015, T023 and each screen's task · FR-050 T017, T021 · FR-051 T056.
- Success criteria: SC-001 T006, T013, T019 · SC-002 T034, T038, T060 · SC-003 T034, T045, T056 · SC-004 T040, T044, T045, T048 · SC-005 T041 · SC-006 T032, T055 · SC-007 T031, T038, T044, T048, T054, T061–T062 · SC-008 T024 · SC-009 T019, T049 · SC-010 T012 and each story's tests.
- Not built by any task, and said so in T063: export to Excel (023); the three other performance cards (015, 082); the routes of the 22 other permissions; the bell (093).
- The words marked **(owner)** in `contracts/operator-screens.md` are built as drafted; the owner reads them on the screens.
- The implementation is not complete until T059 to T063 are done and written down (constitution VII), and every picture has been opened.
