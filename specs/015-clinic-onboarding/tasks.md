# Tasks: Clinic Onboarding

**Input**: Design documents from `/specs/015-clinic-onboarding/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (ops-clinics-api.md,
clinics-screens.md, emails.md), quickstart.md

**Tests**: required by the constitution (Principle VII).

- **Test-first**: permission enforcement, tenant isolation and the plan limit. Their tests are
  written before what they test and are seen to fail; the failing run is recorded in
  `validation.md`.
- **Earlier specs' tests**: none is loosened. Where 015 adds to what an earlier test pins
  (routes, actions, codes, crossings, sections, email samples), the pin is extended, never
  relaxed.

**Organization**: by user story.

- **Path shorthands**:
  - `Ten/` = `src/server/modules/Tenancy/Scs.Tenancy/`, and `TenC/` its `.Contracts`;
  - `Id/` = `src/server/modules/Identity/Scs.Identity/`, and `IdC/` its `.Contracts`;
  - `Subs/` = `src/server/modules/Subscriptions/Scs.Subscriptions/`, and `SubsC/` its
    `.Contracts`;
  - `CD/` = `src/server/modules/ClinicData/Scs.ClinicData/`, and `CDC/` its `.Contracts`;
  - `Ops/` = `src/server/modules/Operators/Scs.Operators/`, and `OpsC/` its `.Contracts`;
  - `BB/` = `src/server/shared/Scs.BuildingBlocks/`; `SK/` = `src/server/shared/Scs.SharedKernel/`;
  - `UT/`, `IT/`, `AT/` = `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/`,
    `Scs.ArchitectureTests/`;
  - `ops-app/` = `src/web/projects/ops/src/app/`; `ui/` = `src/web/projects/ui/src/lib/`;
  - `e2e/` = `src/web/e2e/`.
- **Frames**: the ops design file `SetO9EhDivNcmurAMfcflY`, page «Template — Clinics» `591:2`.
  The ids are in `contracts/clinics-screens.md`.
- **Frozen folders**: 001–014, 094 and 095 are never edited (constitution 1.3.0). Their code is
  changed as research R16 lists.
- **Shared `ui/` files** are edited by hand, never by a formatter.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Create `specs/015-clinic-onboarding/validation.md` and record the baseline: spec 014's
  last full run from `specs/014-plan-enforcement-metering/validation.md`:
  - server unit 960, architecture 72, integration 956 (plus the 145 of the last fix);
  - clinic web 77;
  - Playwright 1223.
  Say whether it was run again before this work.
- [X] T002 Read through the Figma connector, as pictures, every frame named in
  `contracts/clinics-screens.md`:
  - list: normal, loading, error, empty, filter options, status legend;
  - add and edit: normal and the invalid tax number states;
  - detail: normal, loading, error, view only, the sign-in address row states;
  - settings: normal and view only;
  - in both AR and EN.

  Then:
  - write each EN frame's words into the English column of the screens contract;
  - correct the contract where a word, size, colour, icon or part differs, and note each
    difference in `validation.md`;
  - confirm the side bar icon for «العيادات» (research R15).

  If Figma refuses, stop and tell the user.
- [X] T003 Create the module `ClinicData` per `docs/adding-a-module.md` steps 1–4:
  - projects `src/server/modules/ClinicData/Scs.ClinicData/Scs.ClinicData.csproj` and
    `…/Scs.ClinicData.Contracts/Scs.ClinicData.Contracts.csproj`, both in `src/server/Scs.sln`;
  - references to `Scs.BuildingBlocks` and `Scs.Tenancy.Contracts`;
  - `CD/ClinicDataModule.cs`, registered in `src/server/host/Scs.Api/ModuleRegistration.cs` after
    Identity;
  - its migrations listed in `tools/Scs.Migrator`;
  - test project references.
  Build.
- [X] T004 [P] Grant the schema `clinicdata` to `scs_app` (as the other module schemas) and its
  history table to the migrator, in `deploy/compose/sql/create-logins.sql`. Add
  `SCS_SUBSCRIPTIONS__TRIAL_DAYS=14` to `deploy/compose/.env.example`.

---

## Phase 2: Foundational (blocks every story)

### Tests first (seen to fail before T012–T030)

- [X] T005 [P] Write `IT/Clinics/OpsClinicRoutePermissionTests.cs`:
  - every route of `contracts/ops-clinics-api.md` refuses an operator without its permission
    (403 `PERMISSION_REQUIRED`): `clinics.view` for the three GETs, `clinics.edit` for `GET
    /clinics/form` and the four writes;
  - a holder of `clinics.view` alone can read but not write.

  Then:
  - add `ClinicsView` and `ClinicsEdit` to the guarded codes in `IT/Access/RouteDeclarationTests.cs`
    (remove them from `owed`);
  - add the eight routes to `IT/Access/RouteAuditDeclarationTests.cs` with their kinds.

  Run them and see them fail.
- [X] T006 [P] Write `IT/Isolation/ClinicDataSamples.cs` with `BranchSample : IIsolationSample`.
  Write `IT/Clinics/OperatorClinicIsolationTests.cs`: with clinics A and B, an operator route
  establishing A (detail, edit, assign, resend) reads and writes only A's rows, and an id of B on
  a route for A is impossible (`CLINIC_MISMATCH`). Run them and see them fail: no table, no
  policy, no routes.
- [X] T007 [P] Update `AT/DeclaredCrossingsTests.cs`:
  - the count is 12 (nine before, and three new);
  - the recorded set is `["list-clinics","register-clinic","rename-clinic","update-clinic"]`;
  - `Scs.Subscriptions.Infrastructure.OperatorClinicPlans` and
    `Scs.Identity.Infrastructure.OperatorClinicOwners` are the only users of
    `ICrossClinicConnectionFactory` in their modules.
  Add `AT/ClinicDataRulesTests.cs` for the new module's boundaries: nothing but the host
  references `Scs.ClinicData`; others use `Scs.ClinicData.Contracts`. See them fail.
- [X] T008 [P] Write `IT/Clinics/AddClinicPlanLimitTests.cs`: an add on a plan whose `max_users`
  is 0 answers `PLAN_LIMIT_REACHED`, and leaves no row in `tenancy.Clinic`,
  `tenancy.ClinicDirectory`, `subscriptions.ClinicPlan`, `clinicdata.Branch` or
  `identity.StaffAccount`, and no trail entry. Read through the crossing login. See it fail.

### Kernel, building blocks, configuration

- [X] T009 [P] Create `SK/Clinics/Specialties.cs`:
  - `All`, the fifteen keys in the BRD's order: `dental, orthopedics, neurology, pediatrics,
    internal_medicine, general_surgery, dermatology, obgyn, ophthalmology, ent, urology,
    cardiology, psychiatry, nutrition, physiotherapy`;
  - `IsKnown(string?)`.
  Make `src/server/modules/Plans/Scs.Plans/Domain/FeatureLists.cs` use it and delete its internal
  copy. Plans' tests stay green.
- [X] T010 [P] Add `ClinicSource.Operator = "operator"` to `BB/Tenancy/ClinicContext.cs`. Add
  `SubscriptionsOptions { TrialDays = 14 }`, read from `SCS_SUBSCRIPTIONS__TRIAL_DAYS` and
  validated 1–90 at start, to `BB/Configuration/ScsOptions.cs` and its validator, with a unit
  test in `UT/Configuration/`.
- [X] T011 [P] Add the seven codes of research R17 to `SK/ErrorCodes.cs`, with their words in both
  languages in `BB/Errors/ErrorMessages.cs`:
  - `PLAN_DISABLED`, `RESET_EMAIL_COOLDOWN`, `ACCOUNT_LINK_NOT_SENT`;
  - `INVITATION_ALREADY_ACCEPTED`, `OPERATOR_NOT_ACTIVE`;
  - `CLINIC_CHANGED_ELSEWHERE`, `CLINIC_HAS_MAIN_BRANCH`.
  Add the field codes `TAX_NUMBER` and `MOBILE` with their words. Extend `ErrorCatalogueTests` and
  `ProblemLocalizationTests`.
- [X] T012 Add the five actions of data-model §6 to `SK/Audit/AuditActions.cs`, appended to
  `audit-actions.lock`, each with its category, facts, `NeedsBeforeAfter` and names and sentences
  in both languages:
  - `clinic.created`, `clinic.updated`, `clinic.owner-updated`;
  - `clinic.account-manager-assigned`, `clinic.invitation-resent`.
  Add the fact `trial-ends` to `subscription.plan-set`. Extend `UT/Audit/AuditActionsTests.cs`.
- [X] T013 Teach `BB/Audit/AuditWriter.cs` the operator in a clinic trail (research R5):
  - `AmbientAsync` reads `OperatorClaims.OperatorId` when there is no staff claim, and records
    actor kind `operator` with the name from `IOperatorActorNames`;
  - add `RecordForClinicAsync(DbContext db, Guid clinicId, AuditRecord record, ct)` for a context
    on the crossing connection, inside its open transaction.
  Write `IT/Audit/OperatorActorTests.cs`: an ops request that writes a clinic entry records
  `operator`, its id and its name; a staff request is unchanged.

### Tenancy

- [X] T014 Grow `Ten/Domain/Clinic.cs` per data-model §1. Add to
  `TenC/ClinicStates.cs` `SettingUp = "setting-up"` and `Discarded = "discarded"`, both in
  `NotPubliclyAvailable`.
  - **New properties**: `Address` (≤ 300), `TaxNumber` ("`^\d{3}-\d{3}-\d{3}$`"),
    `DefaultLanguage` ("`'ar'` default; `IN ('ar','en')`"), `AccountManagerId`, `CreatedBy`
    (≤ 100), `RowVersion`.
  - **`Create(id, ClinicRegistration, now)`**: starts in `setting-up`. It returns `REQUIRED`,
    `TOO_LONG`, `INVALID` and `TAX_NUMBER` per field. An empty English name takes the Arabic.
  - **`UpdateProfile(ClinicProfileChange)`**: returns `AuditChange`s. The tax number may be kept,
    removed or sent back unchanged (accepted even if it predates the rule). The specialty may be
    NULL in editing only.
  - **`AssignAccountManager(Guid?)`**, **`Activate()`** (only from `setting-up`) and
    **`Discard()`** (only from `setting-up`).
  Write `UT/Tenancy/ClinicProfileTests.cs` covering each rule.
- [X] T015 [P] Add `ShortName.Derive(string? englishName, string arabicName)` to
  `Ten/Domain/ShortName.cs` per research R9:
  1. English, else Arabic;
  2. lower-case runs of `[a-z0-9]` joined by `-`;
  3. leading digits and dashes dropped;
  4. cut to 33;
  5. `clinic-<6 of [a-z0-9]>` when under 3, reserved or empty.
  Write `UT/Tenancy/ShortNameDeriveTests.cs`: English, Arabic-only, mixed, digits first, reserved
  (`admin clinic` → `admin-clinic`, `Admin` → `clinic-xxxxxx`), 60 characters, and every result
  passing `ShortName.TryCreate`.
- [X] T016 Write the migration `Ten/Infrastructure/Migrations/…_ClinicProfile.cs` and the model in
  `Ten/Infrastructure/Persistence/TenancyDbContext.cs`:
  - the five columns and `RowVersion`;
  - `CK_Clinic_State IN ('setting-up','active','suspended','cancelled','discarded')`;
  - `CK_Clinic_Specialty`, NULL or the fifteen keys;
  - `CK_Clinic_TaxNumber`: `[TaxNumber] LIKE '[0-9][0-9][0-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9]'`;
  - `CK_Clinic_DefaultLanguage`.
  The migration only adds, and fills nothing for the seeded clinics.
- [X] T017 Grow `TenC/IClinicRegistry.cs`, `TenC/IClinicProfile.cs` and
  `Ten/Infrastructure/ClinicRegistry.cs` per `contracts/ops-clinics-api.md` "Tenancy":
  - **`TakeAddTurnAsync`**: `sp_getapplock 'scs:clinic-add'`, exclusive, 15 s, on a crossing
    connection held open; a timeout answers `LIMIT_BUSY`.
  - **`RegisterAsync(ClinicRegistration)`**:
    - derives the slug and tries `-2` … `-20` against the directory, retrying on `TAKEN` at
      insert;
    - registers in `setting-up`;
    - runs the provisioners as today;
    - returns `RegisteredClinic(Id, Slug)`.
  - **`ActivateAsync(clinicId, record)`**: in one transaction, changes the state and writes the
    record to both trails (`RecordForPlatformAsync` and `RecordForClinicAsync`).
  - **`DiscardAsync`**: changes the state and retires the directory entry.
  - **`UpdateProfileAsync`** and **`AssignAccountManagerAsync`**: check the version (else
    `CLINIC_CHANGED_ELSEWHERE`) and write `clinic.updated` / `clinic.account-manager-assigned` to
    both trails in the same transaction. Nothing changed writes nothing.
  - **`ListAsync`**: omits `setting-up` and `discarded`, and its items grow.
  - **`ClinicProfile`** grows by the fields listed in the contract.

  Also:
  - declare `update-clinic` (recorded) in `BB/Tenancy/Crossing.cs`;
  - move `TenancyModule.SeedAsync` to `ClinicRegistration`, activating the seeded clinics at once;
  - write `IT/Tenancy/ClinicRegistryProfileTests.cs` for each operation.

### Identity

- [X] T018 Grow `Id/Domain/StaffAccount.cs` and write the migration
  `Id/Infrastructure/Migrations/…_OwnerDetails.cs`:
  - columns: `FullName nvarchar(200) NULL`, `Mobile varchar(20) NULL` with
    `CK_StaffAccount_Mobile` ("`LEN([Mobile]) BETWEEN 8 AND 16 AND [Mobile] NOT LIKE '%[^0-9+]%'
    AND CHARINDEX('+', [Mobile], 2) = 0`"; the full rule, an optional `+` then 8 to 15 digits, is
    in the code), and `LastSignInAtUtc datetimeoffset NULL`, filled for existing accounts from
    their latest `identity.Session.CreatedAtUtc`;
  - methods: `SetOwnerDetails(fullName, mobile)` and `ChangeEmail(email)` (trimmed, lower case);
  - replace the "never changes" comment.

  Also:
  - `SessionService.Open` writes `LastSignInAtUtc`;
  - add `SessionEndings.EmailChanged = "email-changed"` in `IdC/StaffContracts.cs`.
- [X] T019 Add `IClinicOwners` and its records to `IdC/StaffContracts.cs`, per the contract, with
  a skeleton `Id/Infrastructure/ClinicOwners.cs` registered in `Id/IdentityModule.cs`. Add
  `Id/Infrastructure/OperatorClinicOwners.cs` (crossing `read-clinic-owners-as-operator`, not
  recorded, declared in `BB/Tenancy/Crossing.cs`). It reads each clinic's owner name, email and
  mobile with `IgnoreQueryFilters` on the crossing connection, as `OperatorAuditReading` does, and
  backs `IClinicOwners.ListForOperatorsAsync`.

### ClinicData, Subscriptions, Operators

- [X] T020 Create `CD/Domain/Branch.cs`, `CD/Infrastructure/Persistence/ClinicDataDbContext.cs`
  (a `ClinicAwareDbContext` with `ApplyClinicIsolation`) and the migration
  `CD/Infrastructure/Migrations/…_Branches.cs`:
  - `clinicdata.Branch(Id, ClinicId FK tenancy.Clinic, NameAr nvarchar(200), NameEn
    nvarchar(200), Address nvarchar(300), IsMain bit, CreatedAtUtc)`;
  - `UX_Branch_OneMain` unique on `(ClinicId) WHERE [IsMain] = 1`;
  - `CreateClinicIsolation("clinicdata","Branch")` in the same migration.
  Implement `CDC/IBranches.cs`, with `CreateMainAsync` (`CLINIC_HAS_MAIN_BRANCH`) and
  `CountAsync`, in `CD/Infrastructure/Branches.cs`. T006's sample now passes.
- [X] T021 Write `Subs/Infrastructure/Migrations/…_Trial.cs` (`ClinicPlan.TrialEndsAtUtc
  datetimeoffset NULL`) and grow `Subs/Domain/ClinicPlan.cs`. Add `Subs/Domain/ClinicStatus.cs`
  (`Of(storedState, trialEndsAtUtc, now)` → `{code, ar, en, tone}`: `trial` «تجربة» info, `active`
  «نشطة» success) with `UT/Subscriptions/ClinicStatusTests.cs`, covering the second before and
  after the end.
- [X] T022 Grow `SubsC/IClinicPlans.cs` and `Subs/Infrastructure/ClinicPlans.cs`:
  - `StartAsync(planId, trialEndsAtUtc)`: refuses a plan not in `IPlanBook.AssignableAsync` with
    `PLAN_DISABLED`, under 014's lock, and writes `subscription.plan-set` with the fact
    `trial-ends`;
  - `StateAsync()`.
  Add `Subs/Infrastructure/OperatorClinicPlans.cs` (crossing `read-clinic-plans-as-operator`, not
  recorded). Extend `IT/Subscriptions/ClinicPlanTests.cs`.
- [X] T023 [P] Add `ActiveAsync()` to `OpsC/OperatorContracts.cs` (`IOperatorDirectory`) and
  `Ops/Infrastructure/OperatorProfile.cs`: active operators only, ordered by full name. Test in
  `IT/Operators/OperatorDirectoryTests.cs`.
- [X] T024 Create `Subs/Api/OpsClinicScope.cs`, an endpoint filter for `/v1/ops/clinics/{id}…`:
  1. `Establish(id, ClinicSource.Operator)`;
  2. `IClinicProfile.GetOwnAsync()`;
  3. no row, or `setting-up` or `discarded`: 404 `NOT_FOUND`.
  Create `Subs/Api/OpsClinicsEndpoints.cs` with the eight routes mapped, guarded and declared,
  each answering 501 for now, and map it from `Subs/SubscriptionsModule.cs`. T005 now passes: its
  reading checks assert only "not 403" until the routes are built.

### Ops console groundwork

- [X] T025 [P] Add `OpsCan.clinicsView` and `OpsCan.clinicsEdit` to
  `ops-app/access/ops-access.service.ts`. Add the section `{ key: 'clinics', path: '/clinics',
  icon: <T002's icon>, permission: 'clinics.view' }` after `home` in `ops-app/shell/sections.ts`.
  Add the four routes (`clinics/new` before `clinics/:id`; `:id` with child tabs `overview` and
  `settings`) through `guarded()` in `ops-app/app.routes.ts`, loading placeholder pages. Update
  `ops-app/ops.spec.ts`'s pinned section list.
- [X] T026 [P] Create `ops-app/clinics/clinics.service.ts`, typed per
  `contracts/ops-clinics-api.md`, with `ClinicCodes`. Create `ops-app/clinics/clinic-words.ts`
  (status words, count forms for days, branches and doctors, initials) with
  `clinic-words.spec.ts`.
- [X] T027 [P] Add `opsCairoDate` and `opsCairoDateTime` (`Africa/Cairo`, Western digits) to
  `ops-app/i18n/ops-format.ts`, with cases in `ops-format.spec.ts` at 21:59 and 22:00 UTC.
- [X] T028 [P] Add `app.shell.nav.clinics` and the `app.clinics.*` tree to
  `src/web/projects/ops/public/i18n/ar.json` and `en.json`, with every word of
  `contracts/clinics-screens.md` (English from T002).
- [X] T029 [P] Create `e2e/ops-clinics-platform.ts`, the fake API for `**/v1/ops/clinics**` in the
  pattern of `e2e/ops-plans-platform.ts`:
  - in-memory clinics, owners and operators;
  - `failing` and `delay` per route;
  - 403 `PERMISSION_REQUIRED` per method;
  - the refusals of the contract.

**Checkpoint**: migrations apply on a fresh database, every earlier test passes, and T005–T007
pass where their code now exists.

---

## Phase 3: User Story 1 — An operator adds a clinic and its owner is invited (P1) 🎯 MVP

**Goal**: one «إضافة العيادة» creates the clinic, its plan and trial, main branch and invited
owner, then emails the invitation, and lands on the detail.

**Independent Test**: spec US1. Add with every field and read back every row and the email. Try
each refusal: nothing remains.

### Tests

- [X] T030 [P] [US1] Write `IT/Clinics/AddClinicTests.cs`, each read through the crossing login:
  - **success**:
    - the clinic is `active` with every field;
    - the English name falls back to the Arabic;
    - the slug and `-2`;
    - the plan link with `TrialEndsAtUtc = now + 14 days`;
    - one main branch with the names and address;
    - the owner `invited` with name and mobile, holding «مالك العيادة», with no password;
    - `clinic.created` and `subscription.plan-set` in both trails, actor `operator`;
    - one email in the fake sender, in Arabic, naming `app.scs-heta.com`, with a 72-hour link;
  - **a failed email**: the clinic stays and the invitation is `none` (scenario 6).
- [X] T031 [P] [US1] Write `IT/Clinics/AddClinicRefusalTests.cs`. Each of these leaves no row in
  the five tables and no trail entry (SC-003):
  - each field rule of data-model §7;
  - an email of an existing account in another clinic (`email: TAKEN`);
  - a disabled plan (`PLAN_DISABLED`);
  - an unknown specialty.
- [X] T032 [P] [US1] Write `IT/Clinics/AddClinicFaultTests.cs`. With a test double that throws
  after each stage of research R2 (after register, after owner, after branch, after plan, before
  activate), the add answers 500, and then:
  - the clinic is in no list, count or figure, and its id answers `NOT_FOUND`;
  - the locator refuses its slug;
  - the email is free again, and an add with it succeeds.
- [X] T033 [P] [US1] Write `IT/Clinics/AddClinicRaceTests.cs`:
  - 10 adds at once with one English name all succeed with 10 distinct slugs;
  - 2 adds at once with one email: exactly one succeeds; the other answers `email: TAKEN`, and
    nothing of it remains (SC-002).

### Implementation

- [X] T034 [US1] Implement in `Id/Infrastructure/ClinicOwners.cs`:
  - `EmailIsFreeAsync`, through `identity.usp_FindAccountForSignIn`, answering true or false
    only;
  - `CreateAsync(email, fullName, mobile)`:
    - creates the `invited` account through `StaffAccounts.CreateAsync`, with the seat check;
    - sets the name and mobile;
    - sets the account's language preference to the clinic's `DefaultLanguage`;
    - assigns the owner role (`ReadyKey = owner` from `IClinicRoles.ListAsync`, then
      `AssignAsync`);
    - all in one Identity transaction, sending nothing;
  - `DiscardAsync(accountId)`: only an invited account with no link; removes the account and its
    role assignment.
- [X] T035 [US1] Create `Subs/Infrastructure/ClinicOnboarding.cs` per research R2. Under
  `TakeAddTurnAsync`:
  1. **The checks**:
     - the fields (data-model §7, collected into one `VALIDATION_FAILED`);
     - `Specialties.IsKnown`;
     - `EmailIsFreeAsync`;
     - the plan in `AssignableAsync`, else `PLAN_DISABLED`;
     - the plan's `max_users` not 0, read from `IPlanBook.FindAsync(planId).Features`, else
       `PLAN_LIMIT_REACHED` with 014's words.
  2. **The writes**:
     - `RegisterAsync`;
     - `Establish(id, Operator)`;
     - `IClinicOwners.CreateAsync` first: the one write another writer can refuse, before any
       record of the add (research R2, the owner's answer "reorder");
     - `IBranches.CreateMainAsync`;
     - `IClinicPlans.StartAsync(plan, now + TrialDays)`, with `subscription.plan-set` in its
       transaction.
  3. **Activation**: `ActivateAsync` with `clinic.created` (facts `slug`, `plan`, `specialty`, and
     each field from empty, the email as a change value only).
  4. **The email**: `IAccountHandover.ResendInvitationAsync(ownerId)`.

  On any exception in stage 2 or 3, undo in reverse order: `IClinicOwners.DiscardAsync`, then
  `IClinicRegistry.DiscardAsync`. Log the clinic id and stage only, then rethrow as 500. T008 and
  T030–T033 now pass.
- [X] T036 [US1] Implement `GET /v1/ops/clinics/form` and `POST /v1/ops/clinics` in
  `Subs/Api/OpsClinicsEndpoints.cs`:
  - the form answers the assignable plans (cheapest first) and the specialties;
  - the add answers 201 `{ id, slug }` with `Location`.
- [X] T037 [P] [US1] Create `ops-app/clinics/clinic-form.page.ts` in add mode, per `contracts/clinics-screens.md` "The add
  and edit form":
  - the fields in the drawn order, two to a row;
  - the plan and specialty selects;
  - field checks before sending, and server field errors mapped through `fieldErrors()`;
  - focus on the first invalid field;
  - the banner for `PLAN_DISABLED`, `PLAN_LIMIT_REACHED`, `LIMIT_BUSY` and faults;
  - «جارٍ الإضافة…»;
  - the toast «تم إنشاء العيادة!», then navigate to the detail.
  Write `clinic-form.page.spec.ts` for add mode.
- [X] T038 [US1] Write the add part of `e2e/ops-clinics.spec.ts`, in AR and EN at 1440 and 390:
  - every field error, and the invalid tax number states beside frames `2263:2467` / `2263:2373`;
  - the email taken;
  - the plan disabled;
  - success and the landing;
  - no «إضافة عيادة +» and no form for a `clinics.view`-only operator.

**Checkpoint**: an operator can add a clinic. US1's scenarios pass.

---

## Phase 4: User Story 2 — An operator finds a clinic in the list (P1)

**Goal**: «العيادات» lists every clinic newest first, 8 a page, with the four cards, search,
filters, the sortable «ينتهي في» and its states.

**Independent Test**: spec US2.

- [X] T039 [P] [US2] Write `IT/Clinics/ClinicsListTests.cs`:
  - the cards and their sum, against the badges (SC-004);
  - «+n هذا الشهر» in the Cairo month;
  - the search on each of the five fields, in any case, with the mobile's spaces and dashes;
  - each filter, and their combination;
  - the facets after the search, with a chosen plan kept;
  - the sort both ways, with no date last and ties newest first;
  - the pages and the size cap of 50;
  - `can`;
  - «شهري» and «سنوي» never appear among the facets before 018;
  - `setting-up` and `discarded` clinics never appear.
- [X] T040 [P] [US2] Write `IT/Clinics/ClinicsListTimingTests.cs`: 300 clinics seeded through the
  crossing login; 50 searches and filters, each answering in under 1 s (SC-007). Record the figure
  in `validation.md`.
- [X] T041 [US2] Create `Subs/Infrastructure/ClinicsReading.cs`:
  - it joins `IClinicRegistry.ListAsync`, `OperatorClinicPlans`,
    `IClinicOwners.ListForOperatorsAsync`, the plan names from `IPlanBook`, and the operators'
    names;
  - it works out the status through `ClinicStatus.Of`;
  - then it searches, filters, sorts, counts the cards and facets, and pages, per the contract.
  Implement `GET /v1/ops/clinics`. T039 now passes.
- [X] T042 [P] [US2] Create `ui/table/sort-header.directive.ts` (`button[scsSortHeader]`, with
  `aria-sort` on its `th` and the frame's ▾/▴), exported from `ui`'s `public-api.ts`. Add its
  gallery section, `catalogue.ts` entry and `design-inventory.json` line. Write its spec.
- [X] T043 [US2] Create `ops-app/clinics/clinics-list.page.ts` per the screens contract:
  - the header;
  - four `scs-kpi-card`;
  - `scs-list-toolbar` with a 300 ms search;
  - `scs-filter-popover` with four facets, staged until «تطبيق الفلاتر»;
  - `scs-applied-chips` with «مسح الكل»;
  - the table with initials, «أضيفت …», the badge, «مدة الاشتراك», «ينتهي في» with
    `scsSortHeader`, and the «⋯» menu only with `clinics.edit`;
  - a row that opens on click and on Enter;
  - `scs-table-footer` at 8 a page;
  - loading, empty, no-results and error states;
  - `TABLE_NARROW` for 390.
  Write `clinics-list.page.spec.ts`.
- [X] T044 [US2] Write the list part of `e2e/ops-clinics.spec.ts`, in AR and EN at 1440 and 390:
  - the cards;
  - the search;
  - each facet and the chips;
  - the sort;
  - the pages;
  - empty, no-results, error and loading beside their frames;
  - the row menu with and without `clinics.edit`;
  - no section and no access without `clinics.view`.

**Checkpoint**: US1 and US2 together are the MVP.

---

## Phase 5: User Story 3 — An operator reads a clinic and keeps its account manager (P2)

**Goal**: the detail header, the «معلومات العيادة» card, the account manager, the copy button and
the owner's invitation with resend.

**Independent Test**: spec US3.

- [X] T045 [P] [US3] Write `IT/Clinics/ClinicDetailTests.cs`:
  - every field of `ClinicDetail`;
  - a seeded clinic: its seeded owner with no name or mobile, and no address or plan;
  - the invitation `live`, `lapsed` and `none`;
  - an active owner with its last sign-in;
  - «0 أطباء · 1 فرع»;
  - an unknown id, a `setting-up` id and a malformed id all answer the same 404.
- [X] T046 [P] [US3] Write `IT/Clinics/AccountManagerTests.cs`:
  - assign, then none;
  - a suspended or unknown operator gives `OPERATOR_NOT_ACTIVE` and nothing changes;
  - a stale version gives `CLINIC_CHANGED_ELSEWHERE`;
  - `clinic.account-manager-assigned` in both trails with the names before and after;
  - a manager suspended later still shows by name.
- [X] T047 [P] [US3] Write `IT/Clinics/InvitationResendTests.cs`:
  - success replaces the earlier link, answers the new expiry, sends one email and records
    `clinic.invitation-resent`;
  - a second resend within 60 s gives `RESET_EMAIL_COOLDOWN`;
  - a failing sender gives `ACCOUNT_LINK_NOT_SENT`, and the earlier link still works;
  - an accepted owner gives `INVITATION_ALREADY_ACCEPTED`;
  - the resend after a lapse goes through the seat check.
- [X] T048 [US3] Implement in `Id/Infrastructure/ClinicOwners.cs`:
  - `FindAsync`, with the invitation state from `PasswordLink` rows of kind `new-account`
    (data-model §2);
  - `ResendInvitationAsync`, with the order of refusals of the contract, through
    `AccountHandover.ResendInvitationAsync`. `EmailSent = false` maps to `ACCOUNT_LINK_NOT_SENT`.
- [X] T049 [US3] Implement `GET /v1/ops/clinics/{id}`, `PUT …/account-manager` and `POST
  …/invitation` in `Subs/Api/OpsClinicsEndpoints.cs`. The account manager is checked with
  `IOperatorDirectory.FindAsync` (state `active`). Doctors count 0 until 029. T045–T047 now pass.
- [X] T050 [P] [US3] Create `ui/copy/copy-button.component.ts` (`scs-copy-button`:
  `navigator.clipboard.writeText`, a toast on success, the refusal words when it throws), exported
  from `public-api.ts`, with its gallery section, catalogue and inventory entries, and its spec.
- [X] T051 [US3] Create `ops-app/clinics/clinic-detail.page.ts` and `clinic-overview.tab.ts` per
  the screens contract:
  - the breadcrumb;
  - the head with initials, name, badge, the contact line and «تعديل العيادة» with
    `clinics.edit`;
  - `scs-tabs` with «نظرة عامة» and «الإعدادات»;
  - the «معلومات العيادة» card with every row;
  - the sign-in address with `scs-copy-button`, and the invitation line in its three states with
    «إعادة إرسال الدعوة» and its toasts;
  - the inline account manager: select, «حفظ» and «إلغاء», with its toasts;
  - loading, error and not found as one, and view only.
  Write the page specs.
- [X] T052 [US3] Write the detail part of `e2e/ops-clinics.spec.ts`, in AR and EN at 1440 and 390:
  - every row;
  - the three invitation lines beside `2330:3095` / `2330:3050`, and the active owner;
  - resend success and its three refusals;
  - copy success and refusal (clipboard permission denied);
  - assign, «غير معيّن», cancel and refusal;
  - view only beside `2786:26469` / `2786:26117`;
  - loading and error beside their frames;
  - not found equal to error.

- [X] T072 [US3] Add «عيادات تم إدارتها» to the operator page (FR-030, US3 scenario 11):
  - `IManagedClinicCounts.CountAsync(operatorId)` in `OpsC/OperatorContracts.cs`, with a default
    answering null (`TryAddScoped`) so the card shows only what exists;
  - `Subs/Infrastructure/ManagedClinicCounts.cs` implements it through
    `IClinicRegistry.ListAsync` (visible clinics whose `AccountManagerId` is the operator), and is
    registered after Operators;
  - `Ops/Infrastructure/OperatorsDirectory.cs` adds `PerformanceCard("clinicsManaged", n)` when
    the answer is not null;
  - the word `app.operators.detail.clinicsManaged` («عيادات تم إدارتها», English from the operator
    frame of 011) in both i18n files;
  - tests in `IT/Operators/` and the operator e2e.

---

## Phase 6: User Story 4 — An operator corrects a clinic's data (P2)

**Goal**: «تعديل العيادة» changes the clinic and its owner, without the plan.

**Independent Test**: spec US4.

- [X] T053 [P] [US4] Write `IT/Clinics/EditClinicTests.cs`:
  - each clinic field changed, with `clinic.updated` before and after and the note;
  - the owner's name and mobile, with `clinic.owner-updated`;
  - both together write two entries with one note;
  - nothing changed records nothing;
  - the tax number kept when missing, removed when empty, and an old stored value sent back
    unchanged accepted;
  - the specialty set to «غير مسجّل»;
  - a stale version changes nothing, the owner included;
  - every field error and `email: TAKEN`;
  - the plan cannot be changed through this route.
- [X] T054 [P] [US4] Write `IT/Clinics/OwnerEmailChangeTests.cs`:
  - an invited owner: the email changes and the earlier link stops working;
  - an active owner:
    - the new email signs in;
    - the old one gets the same answer as an unknown email;
    - every session is ended with `email-changed`;
    - one notice reaches the old address, with the new one masked;
    - the trail keeps both addresses;
  - a failing sender for the notice leaves the change in place.
- [X] T055 [US4] Implement `IClinicOwners.UpdateAsync` in `Id/Infrastructure/ClinicOwners.cs`:
  - one transaction with `clinic.owner-updated`;
  - for an invited owner, replace the links;
  - for an active owner, `EndForAccountCoreAsync(reason: email-changed)`;
  - after the commit, the notice.
  Add `AccountEmails.EmailChanged` per `contracts/emails.md`, with its Arabic and English samples
  in `IdentityEmailSamples`.
- [X] T056 [US4] Implement `PUT /v1/ops/clinics/{id}` in `Subs/Api/OpsClinicsEndpoints.cs`:
  1. check the version;
  2. run the owner's part (`IClinicOwners.UpdateAsync`);
  3. run the clinic's part (`IClinicRegistry.UpdateProfileAsync`).
  Answer 204. T053 and T054 now pass.
- [X] T057 [US4] Add edit mode to `ops-app/clinics/clinic-form.page.ts`:
  - the form opens filled;
  - the plan is a read-only line;
  - add «اللغة الافتراضية», «المنطقة الزمنية» (read only) and the note (≤ 500);
  - «غير مسجّل» among the specialties;
  - the tax number sent only when touched, `""` when cleared;
  - «جارٍ الحفظ…»;
  - the banner for «تعذر تحديث بيانات العيادة.» and for `CLINIC_CHANGED_ELSEWHERE` with reload;
  - the toast, then the detail.
  Extend `clinic-form.page.spec.ts`.
- [X] T058 [US4] Write the edit part of `e2e/ops-clinics.spec.ts`, in AR and EN at 1440 and 390,
  beside `655:1230` / `649:1124` and `2263:2661` / `2263:2561`:
  - each field;
  - the tax number cases;
  - changed elsewhere;
  - `/clinics/<id>/edit` refused without `clinics.edit`.
- [X] T059 [P] [US4] Add the notice to the Playwright `emails` tests and run `npm run emails`.
  Open both pictures.

---

## Phase 7: User Story 5 — An operator reads the clinic's settings (P3)

**Goal**: the «الإعدادات» tab with its strip and the «الإعدادات العامة» card.

**Independent Test**: spec US5.

- [X] T060 [P] [US5] Implement `GET /v1/ops/clinics/{id}/settings` in
  `Subs/Api/OpsClinicsEndpoints.cs`, with `IT/Clinics/ClinicSettingsTests.cs`: the language, the
  time zone, the staff address, and `can.edit` by permission.
- [X] T061 [US5] Create `ops-app/clinics/clinic-settings.tab.ts`:
  - the strip in its two forms by `clinics.edit` (the editor's without "the time zone");
  - the card's three rows;
  - «تعديل العيادة ←» with `clinics.edit`;
  - the error state with «إعادة المحاولة».
  Write its spec.
- [X] T062 [US5] Write the settings part of `e2e/ops-clinics.spec.ts`, in AR and EN at 1440 and
  390, beside `2589:4185` / `2589:4007` and `2790:8210` / `2790:7655`: both strips, the card, the
  link and the error.

---

## Phase 8: User Story 6 — The plans count their clinics (P3)

**Goal**: 013's plans list and detail show real clinic counts; revenue 0 until 018.

**Independent Test**: spec US6.

- [X] T063 [P] [US6] Write `IT/Clinics/PlanSubscribersTests.cs`. With clinics on two plans, some
  in trial, a `setting-up` one and a seeded one with no plan:
  - `CountsAsync`: clinics, `NewThisMonth` (Cairo) and `OnTrial`;
  - `RevenueAsync` gives 0;
  - `ClinicsAsync` lists newest first with «تجربة» or «نشطة» and the join date;
  - `GET /v1/ops/plans` and the plan's detail show them;
  - disabling a plan with clinics warns with the real number.
- [X] T064 [US6] Create `Subs/Infrastructure/PlanSubscribers.cs` (`IPlanSubscribers`) through
  `OperatorClinicPlans` and `IClinicRegistry.ListAsync`, and register it with `AddScoped` in
  `Subs/SubscriptionsModule.cs` (after Plans, so it replaces `NoSubscribers`). T063 now passes.
  `e2e/ops-plans.spec.ts` stays green; extend it only where its fake now carries counts.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T065 [P] Write the docs:
  - `docs/setup.md`: a "Clinics" section covering the trial setting, Mailpit for the invitation,
    and walk operators;
  - `docs/database-changes.md`: the `setting-up` and `discarded` states, `clinicdata`, and the
    new columns;
  - `docs/adding-a-module.md`: "Acting in a clinic from the operators console
    (`ClinicSource.Operator`, `OpsClinicScope`)" and "Reading across clinics for operators".
- [X] T066 [P] Run `T005`–`T008` against the final code and record in `validation.md` the earlier
  failing run of each (test-first evidence).
- [X] T067 Run every gate with nothing else running:
  - `dotnet test src/server/tests/Scs.UnitTests -c Debug`, `…/Scs.ArchitectureTests`,
    `…/Scs.IntegrationTests`;
  - `npm --prefix src/web run ci`;
  - `npx --prefix src/web playwright test`.
  Fix what fails.
- [X] T068 Walk the real stack per `quickstart.md`, all fourteen steps, recording the time from
  «إضافة العيادة» to the invitation in Mailpit (SC-001), in both languages, with
  walk accounts only, never the seeded operator. Read the API's log of the walk.
- [X] T069 Lay every screen and state of `contracts/clinics-screens.md` beside its frame, AR and
  EN, at 1440 and phone width. Open each picture. Fix the differences, or record them with whose
  decision they are.
- [X] T070 Make `specs/015-clinic-onboarding/design-vs-real.png`: the list, the add form, the
  detail and the settings tab beside their frames. Open it.
- [X] T071 Finish `specs/015-clinic-onboarding/validation.md` with:
  - what was run, with counts against T001;
  - what was walked and seen;
  - which pictures were opened;
  - every difference from the design and whose decision it is;
  - what was built differently from the plan;
  - the timing figure of T040;
  - the owner and designer items:
    - the per-clinic address;
    - «رقم الهاتف»;
    - the two name fields;
    - the any-country mobile;
    - the proposed words and codes;
    - the BRD's two imperatives;
    - the email-changed notice;
  - what was **not** checked.

---

## Dependencies & Execution Order

- **Setup (Phase 1)**: T003 comes before every ClinicData task. T002 comes before the screens'
  words (T028) and every page.
- **Foundational (Phase 2)** blocks every story.
  - The test-first tasks T005–T008 come before T013–T024.
  - T014 → T016 → T017.
  - T018 → T019.
  - T021 → T022.
  - T024 needs T017 and T019.
- **US1 (Phase 3)** needs Phase 2. T034 → T035 → T036. T037 (web, against the fake) can run
  beside them; T038 comes after T037.
- **US2 (Phase 4)** needs Phase 2 (T019's `ListForOperatorsAsync`, T022's `OperatorClinicPlans`).
  Its tests seed clinics through the US1 flow.
- **US3 (Phase 5)** needs Phase 2. Its tests seed clinics through the US1 flow.
- **US4 (Phase 6)** needs US3's detail page (it lands on it) and T048's `FindAsync`.
- **US5 (Phase 7)** needs US3's detail page.
- **US6 (Phase 8)** needs Phase 2 only.
- **Polish (Phase 9)** comes after all the stories.

### Parallel opportunities

- **Phase 2**: T005–T008 together; then T009, T010, T011 and T015 together; T023 and T025–T029
  alongside the server work.
- **Within each story**: the `IT/` test files marked [P] together, and the `ui` pieces (T042,
  T050) beside the server.
- **Between stories**: US2, US3 and US6 can proceed in parallel once US1's flow exists.

### Example: User Story 3

```text
T045, T046, T047 together (three test files)
T050 (ui copy button) beside T048
T048 → T049 → T051 → T052
```

## Implementation Strategy

1. **MVP**: Phases 1–4. An operator adds a clinic, the owner is invited, and the list finds it.
2. **Then**: US3 (detail, manager, invitation), US4 (edit, owner email), US5 (settings) and US6
   (plans' figures).
3. **Stop at each checkpoint** and run the suites.
4. **Close with Phase 9**: the walk, the frames and `validation.md`.

## Notes

- Commit only when asked. Keep `docs/reference/brd/SCS-SpecKit-Phases.md` and
  `specs/093-operator-notifications/` out of every commit.
- Never touch the user's containers or the seeded operator. Walks use walk accounts in separate
  Playwright browsers.
- Words: «الباقة», never «الخطة». No gendered address in proposed words. A catalogue name goes in
  «…».

---

## Phase 10: Convergence

- [X] T073 Check the clinic's part of an edit (fields and version) before the owner's part commits in `Subs/Api/OpsClinicsEndpoints.cs` `EditAsync`, so a refused or stale save writes nothing, owner included, and both parts' field errors come back together, with an integration test per FR-021 (partial)
- [X] T074 Carry each plan clinic's join date through `Plans/Infrastructure/PlanBook.cs` (`PlanClinicRow`), `ops/plans/plans.service.ts` and the plan detail's clinic rows (Cairo date), with tests, per FR-029 (partial)
- [X] T075 Make an owner-only edit move the clinic's version (or make the version cover the owner's account) so a stale form cannot write old owner values back, with a test, per FR-021 (partial)
- [X] T076 Make the tax-number and mobile checks in `SK/Clinics/ClinicFields.cs` ASCII-digit only, and retry `ClinicRegistry.RegisterAsync` only on a slug unique-key clash, with tests for Eastern-Arabic digits, per FR-003 (partial)
- [X] T077 Leave no branch and no role-change entry when the plan is disabled after the add's check: undo the main branch in `ClinicOnboarding.UndoAsync` (or re-check the plan before the first write), with a test, per SC-003 (partial)
- [X] T078 Release the add lock before the invitation is sent in `ClinicOnboarding.AddAsync`, with a test using a slow mailer, per SC-002 (partial)
- [X] T079 Send the email-changed notice in the clinic's default language (after the save) instead of the owner's preference in `Identity/Infrastructure/ClinicOwners.cs`, per US4/AC6 (contradicts)
- [X] T080 Write `clinic.invitation-resent` in the unit that stores the new link, or record the separate unit as a deviation in validation.md, per FR-018 and FR-027 (partial)
- [X] T081 Hide the owner's name, email and mobile in the edit form for a clinic with no owner account, per the spec's edge case (missing)
- [X] T082 Show «آخر تسجيل دخول» as a Cairo date and time, hide the manager select's label visually, word a stale-version assign refusal as changed-elsewhere with a reload, and not preselect a manager who is no longer offered, per contracts/clinics-screens.md (partial)
- [X] T083 Offer only the «ينتهي خلال» windows some searched clinic falls into, and reset the filter panel's draft to the applied filters when it opens, per FR-010 (partial)
- [X] T084 Decide and record (or build) the owner's `account.role-changed` entry in the platform trail and how a `suspended`/`cancelled` clinic counts in the cards before spec 016, per SC-006 and SC-004 (partial)
- [X] T085 Open the narrow and English pictures of the list, detail, edit and settings and record what is seen, and correct validation.md's "names, then contact · email, in the list's rows" to the add form's rows, per SC-005 (partial)

---

## Phase 11: Convergence

- [X] T086 Catch any non-cancellation fault of the invitation step in `ClinicOnboarding.AddAsync` after the clinic is active, log it and still answer 201, so the operator lands on the detail with «إعادة إرسال الدعوة» instead of «تعذر إنشاء العيادة.» for a clinic that exists, with a test, per FR-004 and US1/AC6 (partial)
- [X] T087 Show «غير مسجّل» once in the edit form's specialty list: drop the extra `''` option in `clinic-form.page.ts` `specialtyOptions` (the select's nothing-chosen row already is it), with a unit test, per FR-020 and US4/AC5 (partial)
- [X] T088 Leave replaced links out of the invitation's one-minute wait in `Identity/Infrastructure/ClinicOwners.cs` `ResendInvitationAsync`, so a resend right after an invited owner's email changed is not refused with «أُرسلت دعوة لهذا البريد قبل أقل من دقيقة», with a test, per FR-018 and FR-021a (contradicts)
- [X] T089 Check a changed owner email is free in the edit's pre-check (`IClinicOwners.CheckDetails` or beside it), so a taken email comes back with the other refused fields in one answer, with a test, per FR-021 and T073 (partial)
- [X] T090 Undo the plan link when an add fails after the plan's stage (activation), or record the leftover link and `subscription.plan-set` entries of the given-up clinic as an accepted window in validation.md, and assert it in the activation fault test, per SC-003 (partial)
- [X] T091 Write «مدة الاشتراك» with the Arabic count forms («تجربة 7 أيام», «تجربة 14 يومًا») or record the owner's choice of the frame's fixed «تجربة 14 يوم», per contracts/clinics-screens.md and FR-007 (partial)
- [X] T092 Open the English clinics list at 390 (with the filter panel and the chips row) and record what is seen in validation.md, per SC-005 (partial)

---

## Phase 12: Convergence

- [X] T093 Run the add's undo also when the request is cancelled after the clinic was registered (`ClinicOnboarding.AddAsync`'s catch skips `UndoAsync` for `OperationCanceledException`, leaving the owner account, branch and plan link and the clinic in `setting-up`), then rethrow, with a fault test that cancels after the owner's stage, per the edge case "the add fails part-way" and research R2 (contradicts)
- [X] T094 Set aside an active owner's open sign-in challenges and reset links when the owner's email changes in `Identity/Infrastructure/ClinicOwners.cs` `UpdateAsync`, in the same transaction, so a code already sent to the old address no longer opens a session, with a test, per FR-021a and US4/AC6 (partial)
- [X] T095 Join the chosen statuses of the status chip with the page language's separator (`, ` in English, `، ` in Arabic) in `clinics-list.page.ts`, with a two-status English check, per FR-010 and SC-005 (partial)
- [X] T096 Disable «تعديل العيادة» on the detail while offline (`[scsWrite]="online()"` on `clinic-edit` in `clinic-detail.page.ts`), with a unit test, per contracts/clinics-screens.md (offline state) (missing)

---

## Phase 13: Convergence

- [X] T097 Make `ClinicRegistry.DiscardAsync` read the clinic's stored state (not the copy `ActivateAsync` already set to active in memory), so a fault inside the real activation still discards the clinic and retires its slug, with a fault test that fails inside the real `ActivateAsync`, per research R2 and SC-003 (contradicts)
- [X] T098 Run the list's timing test over 50 searches as T040 says (it runs 20) and record the figure again, per T040 and SC-007 (partial)
