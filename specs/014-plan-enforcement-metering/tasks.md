# Tasks: Plan Enforcement and Limit Metering

**Input**: Design documents from `/specs/014-plan-enforcement-metering/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (enforcement-api.md,
not-in-plan-screen.md), quickstart.md

**Tests**: required by the constitution (Principle VII).

- Plan entitlements and limits are a test-first area. The entitlement rules, the plan gate, the
  seat checks and their race, and the meter's refusals and race are written before what they test
  and are seen to fail.
- No test of an earlier spec is loosened. Where this spec adds to what an earlier spec's test pins
  (routes, actions, codes, the guardrail, the shell's not-in-plan tests), the pin is extended or
  moved to the real source, never relaxed.

**Organization**: by user story.

- **Path shorthands**:
  - `Subs/` means `src/server/modules/Subscriptions/Scs.Subscriptions/`;
  - `SubsC/` means `src/server/modules/Subscriptions/Scs.Subscriptions.Contracts/`;
  - `Identity/` means `src/server/modules/Identity/Scs.Identity/`;
  - `BB/` means `src/server/shared/Scs.BuildingBlocks/`;
  - `SK/` means `src/server/shared/Scs.SharedKernel/`;
  - test folders are `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/` and
    `Scs.ArchitectureTests/`;
  - `clinic-app/` means `src/web/projects/clinic/src/app/`;
  - browser tests are in `src/web/e2e/`.
- **Frames** are in the clinic design file `4HLWEqlna9sbn68276LHYt`.
- **Built specs' folders** (`specs/001-…` to `specs/013-…`, `094`, `095`) are never edited
  (constitution 1.3.0). Their code may be, as research.md lists.
- **Shared `ui/` files** are edited by hand only, never by a formatter.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Create `specs/014-plan-enforcement-metering/validation.md` and record the baseline. It
  is spec 013's last full run, from `specs/013-plans-pricing/validation.md` including its
  convergence passes:
  - server unit 941, architecture 68, integration 907;
  - `npm run ci` passed (unit tests clinic 68, ops 100, core 164, ui 179);
  - Playwright 1200 passed, 4 skipped.
  Say whether it was run again before this work.
- [X] T002 Read the four frames as pictures through the Figma connector:
  - «Inbox / Not In Plan» `6701:1002017` / `6701:1002375`;
  - «Financial / Not In Plan» `6614:945146` / `6614:945529`;
  - the Empty State component (`4252:60047`, page Feedback `4228:4`) their card is built from.
  Correct `contracts/not-in-plan-screen.md` where a word, a size, a colour or a part differs, and
  note each difference in `validation.md`. If Figma refuses, stop and tell the user.
- [X] T003 Create the module per `docs/adding-a-module.md` steps 1–4, and build:
  - the projects `src/server/modules/Subscriptions/Scs.Subscriptions/Scs.Subscriptions.csproj`
    and `…/Scs.Subscriptions.Contracts/Scs.Subscriptions.Contracts.csproj`, added to
    `src/server/Scs.sln`;
  - references to `Scs.BuildingBlocks`, `Scs.Plans.Contracts` and `Scs.Tenancy.Contracts`;
  - `Subs/SubscriptionsModule.cs` implementing `IModule`;
  - registration in the host's two places;
  - `Subs/Infrastructure/Persistence/SubscriptionsDbContext.cs`, a `ClinicAwareDbContext` with
    default schema `subscriptions`;
  - a reference from `Scs.IntegrationTests` and `Scs.UnitTests`.
  The architecture tests pick the module up and pass.

---

## Phase 2: Foundational (blocks every story)

- [X] T004 [P] Add the codes of `contracts/enforcement-api.md` §8 to `SK/ErrorCodes.cs`:
  - `PLAN_FEATURE_NOT_INCLUDED`, `PLAN_LIMIT_REACHED`, `LIMIT_BUSY`,
    `METER_GIVE_BACK_TOO_MUCH`;
  - their words in both languages in `BB/Errors/ErrorMessages.cs`, exactly as `data-model.md`
    "Refusals" gives them: «غير مشمولة في باقتك» / "Not included in your plan"; «بلغت العيادة حدّ
    باقتها من «{الميزة}»: {الحدّ}. تُرفع الباقة من «الملف والاشتراك».» / "The clinic has reached its
    plan's limit of {feature}: {limit}. The plan is raised in Profile & subscription.";
  - the count pinned in `Scs.UnitTests/Errors/ErrorCatalogueTests.cs`, extended 69 → 73 (an unknown plan is the existing `NOT_FOUND`).
- [X] T005 [P] Create `SK/Plans/FeatureKeys.cs`: the keys code refers to (`CoreClinic`,
  `MaxUsers`, `MaxDoctors`, `MaxBranches`, `QueueScreen`, `MessageCredits`, `TeleconsultMinutes`,
  `AiXrayAnalysis`, `AiVoiceNotes`, `AllowWhatsapp`, `AllowTelegram`, `AllowAnalytics`,
  `SpecialtyExamModules`). It also holds `CountedLimits`, the BRD's eight in SH-015's order (users,
  doctors, branches, message credits, waiting screens, teleconsult minutes, x-ray analyses,
  voice-dictation minutes), and `Monthly`, the four. A unit test checks every constant is a key of
  `FeatureSeed`.
- [X] T006 [P] Add the recorded action and its pins:
  - `SubscriptionPlanSet` (`subscription.plan-set`, category `changes`, before and after, both
    languages' sentence, fact `plan`; never `key` or `code`) to `SK/Audit/AuditActions.cs`;
  - the code appended at the end of `SK/Audit/audit-actions.lock`;
  - `AuditTarget.Clinic = "clinic"` in `BB/Audit/AuditRecord.cs`;
  - the pins extended in `Scs.UnitTests/Audit/AuditActionsTests.cs` and
    `Scs.IntegrationTests/Audit/AuditFactsTests.cs`.
- [X] T007 Map `subscriptions.ClinicPlan` per `data-model.md`, in `Subs/Domain/ClinicPlan.cs` and
  the DbContext. The entity is `IClinicOwned`. Its columns:
  - `ClinicId` `uniqueidentifier` primary key;
  - `PlanId` `uniqueidentifier` NULL, "no foreign key across schemas";
  - `SetAtUtc` `datetimeoffset`;
  - `Version` rowversion.
  Then map `subscriptions.MeterEntry`, in `Subs/Domain/MeterEntry.cs`. Its columns:
  - `Id`, `ClinicId`, `FeatureKey` `nvarchar(60)`, `Month` `char(7)`, `Amount` `int`;
  - `AtUtc`, `Purpose` `nvarchar(100)`, `Reference` `nvarchar(200)` NULL, `By` `nvarchar(100)`;
  - `BeyondAllowance` bit, `GivesBack` `uniqueidentifier` NULL;
  - the index `IX_MeterEntry_Month` on `(ClinicId, FeatureKey, Month)` including `Amount`;
  - the checks `CK_MeterEntry_Amount` (`Amount <> 0`) and `CK_MeterEntry_GivesBack` (`(Amount <
    0) = (GivesBack IS NOT NULL)`).
  Add the migration `Subs/Infrastructure/Migrations/…_Subscriptions.cs`. It calls the isolation
  helper for both tables (`docs/adding-a-module.md` "Adding a clinic-owned table" step 2). Add an
  `IIsolationSample` for each in `Scs.IntegrationTests`. The isolation guardrail and the isolation
  test pass.
- [X] T008 [P] Write failing `Scs.IntegrationTests/Subscriptions/ClinicPlansTests.cs`, covering:
  - set, then read it back;
  - the same plan again records nothing;
  - an unknown plan id is refused `NOT_FOUND` (404);
  - null is "no plan";
  - `subscription.plan-set` lands in the clinic's trail and the platform's, with the plan before
    and after, in the same transaction (a failure in between leaves neither);
  - clinic B cannot read or write clinic A's row.
- [X] T009 Create `SubsC/ClinicPlanContracts.cs` (`IClinicPlans`) and
  `Subs/Infrastructure/ClinicPlans.cs`, with the audit in the same transaction (research R2), and
  register it. T008 passes.
- [X] T010 [P] Create `Scs.IntegrationTests/Fixtures/SubscriptionFixtures.cs` with helpers to:
  - give a clinic a plan made with 013's `PlanFixtures` (a grid of keys and limits);
  - mark a catalogue feature built or upcoming in the test database, restored in `finally`.
    Every test that flips a status belongs to the xUnit collection `CatalogueState`, which does
    not run in parallel with 012's and 013's catalogue tests (it shares their collection);
  - set a clinic's specialty;
  - move a fake `TimeProvider` across a Cairo month.

**Checkpoint**: the module, its two tables under isolation, the link and its trail, the codes and
the keys exist.

---

## Phase 3: User Story 1 - The platform knows what a clinic has (P1) 🎯 MVP

**Goal**: one answer, worked out by the BRD's rules, for every caller.

**Independent Test**: give a clinic a plan, change the plan and the catalogue, and check that
`IClinicEntitlements` follows every row of `data-model.md`'s rules table.

- [X] T011 [P] [US1] Write failing `Scs.UnitTests/Subscriptions/EntitlementsTests.cs`, one case per
  row of the rules table and each scenario of US1:
  - no plan: switches off, core on, the limits at their baselines (users null, doctors 1,
    branches 1, waiting screens 0, the four monthly 0);
  - in the plan with no number: null;
  - in the plan with n: n;
  - left out: the baseline;
  - an unknown key: not included, 0;
  - `dental_chart` for a dermatology clinic, and for no specialty: off; for a dental clinic: on;
  - `specialty_exam_modules` with no specialty: off;
  - upcoming: as the plan says, with `Enforced` false;
  - a limit made a switch, and a switch made a limit: as 013 left them;
  - `Withheld`: only built switches not included.
- [X] T012 [P] [US1] Write failing `Scs.IntegrationTests/Subscriptions/ClinicEntitlementsTests.cs`
  (collection `CatalogueState`):
  - the answer for a clinic through the real plan and catalogue;
  - a disabled plan still applies (ق-٤٧);
  - a retired feature still in the plan still applies (ق-٤٨);
  - a plan saved through 013's `PUT /v1/ops/plans/{id}` applies to the very next call (FR-007);
  - a plan id that no longer resolves reads as no plan;
  - one request asking three times reads once (the memo);
  - no clinic established is refused `CLINIC_REQUIRED`.
- [X] T013 [P] [US1] Add `Specialty nvarchar(40) NULL` to `tenancy.Clinic`
  (`src/server/modules/Tenancy/Scs.Tenancy/Domain/Clinic.cs`, `TenancyDbContext`, a migration
  `…_ClinicSpecialty`) and `string? Specialty` to `ClinicProfile` in
  `src/server/modules/Tenancy/Scs.Tenancy.Contracts/`. Nothing sets it yet (research R5). The
  Tenancy tests still pass.
- [X] T014 [P] [US1] Add `AllAsync()` to `IFeatureCatalogue`
  (`src/server/modules/Plans/Scs.Plans.Contracts/FeatureContracts.cs`), returning every row,
  retired and unsold included, with its kind, units, baseline, specialty, status and monthly.
  Implement it in `src/server/modules/Plans/Scs.Plans/Infrastructure/FeatureCatalogue.cs`, with a
  test in the existing catalogue tests.
- [X] T015 [US1] Create `Subs/Domain/Entitlements.cs` (the pure `Work(plan?, catalogue,
  specialty?)` of research R4). T011 passes.
- [X] T016 [US1] Create `SubsC/EntitlementContracts.cs` (`IClinicEntitlements`,
  `ClinicEntitlements`, `Entitlement`, `PlanFeatureNotIncludedException`) and
  `Subs/Infrastructure/ClinicEntitlements.cs`. It reads the link, `IPlanBook.FindAsync`,
  `IFeatureCatalogue.AllAsync` and `IClinicProfile.GetOwnAsync().Specialty`, and is memoised per
  request (scoped). Register it. T012 passes.

**Checkpoint**: every later check has its one answer.

---

## Phase 4: User Story 2 - A feature the plan does not include is refused by the server (P1)

**Goal**: a route that names a feature is refused with one message for a clinic whose plan
withholds it. The permission is decided first.

**Independent Test**: a probe route carrying `RequiresFeature(allow_analytics)`, with the feature
marked built in the test database, called for a clinic with and without it in its plan.

- [X] T017 [P] [US2] Write failing `Scs.IntegrationTests/Subscriptions/PlanGateTests.cs` (collection
  `CatalogueState`) with a
  probe route in `Scs.IntegrationTests/Fixtures/ProbeEndpoints.cs` (`.Requires(<a permission>)
  .RequiresFeature(FeatureKeys.AllowAnalytics)`). It covers:
  - withheld: 403 `PLAN_FEATURE_NOT_INCLUDED`, `feature` = `allow_analytics`, «غير مشمولة في
    باقتك» in Arabic and "Not included in your plan" in English, and the handler did not run;
  - included: the handler runs;
  - without the permission: 403 `PERMISSION_REQUIRED`, never the plan's;
  - upcoming: never refused;
  - `RequiresAnyFeature(allow_whatsapp, allow_telegram)`: passes with either one, refused with
    neither.
- [X] T018 [P] [US2] Write failing `Scs.IntegrationTests/Subscriptions/PlanFeatureDeclarationTests.cs`:
  - every endpoint's `PlanFeatureMetadata` key is a key of `FeatureSeed` (FR-010);
  - an operators' route carrying it fails at start-up;
  - a route declaring it twice fails.
- [X] T019 [US2] Create `BB/Plans/PlanGate.cs`:
  - `RequiresFeature`, `RequiresAnyFeature`, `PlanFeatureMetadata`;
  - `IPlanAnswers` (`IsWithheldAsync(keys)`, `LimitOfAsync(key)`);
  - `UseScsPlanGate()`, which writes the problem through the existing problem writer with the
    request's language words.
  Implement `IPlanAnswers` in `Subs/Infrastructure/ClinicEntitlements.cs`. Place
  `app.UseScsPlanGate()` right after `app.UseScsAccessGate()` in
  `src/server/host/Scs.Api/Program.cs`. T017 and T018 pass.

**Checkpoint**: any later spec refuses a feature with one line on its route.

---

## Phase 5: User Story 3 - A clinic cannot add a user past its plan's limit (P1)

**Goal**: «المستخدمون» is enforced at every way an account comes to count, from the clinic or an
operator, and never twice for the last seat.

**Independent Test**: a plan with users 3. Fill it, try each way, let an invitation expire, and
send 20 requests at once for one free seat.

- [X] T020 [P] [US3] Write failing `Scs.IntegrationTests/Subscriptions/SeatTests.cs`, through
  `IStaffAccounts` and `IAccountHandover` and the existing `POST /v1/dev/staff-invitations`. With
  a plan of users 3 and 3 counted accounts:
  - each of `CreateWithLinkAsync`, `CreateWithGeneratedPasswordAsync` and
    `CreateWithTypedPasswordAsync` is refused 409 `PLAN_LIMIT_REACHED` (`feature` `max_users`,
    `limit` 3), with no account and no email in the mail stand-in;
  - re-enabling a disabled account is refused, and it stays disabled;
  - re-sending an expired invitation is refused (ق-١٠٣);
  - `ReissueAsync` on an expired invited account is refused, by link and by password;
  - a role change and activation by link are not refused (FR-017);
  - disabling frees a seat and the next add succeeds;
  - a live invitation takes a seat; once expired it does not;
  - a just-created invited account with no link yet takes a seat;
  - lowering the plan to 2 keeps all three, and the next add is refused (ق-٤١);
  - no plan, and a plan including users with no number, refuse nothing;
  - the operator's path: `IStaffAccounts.CreateAsync` and `SetStateAsync` called with the clinic
    established by `ClinicSource.Job` and no staff session, as 015 and 017 will call them: the
    same refusal (SH-017).
- [X] T021 [P] [US3] Write failing `Scs.IntegrationTests/Subscriptions/SeatRaceTests.cs`. With one
  free seat, 20 creations together give exactly 1 success and 19 `PLAN_LIMIT_REACHED`. The same
  holds for 20 re-enables (SC-002, ق-أ-١٩).
- [X] T022 [US3] Create `BB/Plans/PlanLimits.cs` (`EnsureRoomAsync(db, key, count, adding)`,
  `PlanLimitReachedException`, the `LIMIT_BUSY` timeout) per research R7 and
  `contracts/enforcement-api.md` §4. It takes `sp_getapplock 'plan-limit:{clinicId}:{key}'`,
  exclusive, owned by the transaction, with a 10 s timeout, following
  `Identity/Infrastructure/ClinicRoles.cs`. Map `PlanLimitReachedException` to the problem with
  `feature` and `limit`, the words filled with the catalogue name in the request's language and
  the limit in Latin digits.
- [X] T023 [US3] Create `Identity/Infrastructure/Seats.cs`:
  - the count of research R8: `active`, plus `invited` with a live `NewAccount` link or no link
    yet;
  - `EnsureSeatAsync(accountId?)`, which checks whether the account already counts, else calls
    `PlanLimits.EnsureRoomAsync`;
  - an `ILimitCounter` for `max_users`.
  Register both, and reference `Scs.Subscriptions.Contracts` from Identity (contracts only).
- [X] T024 [US3] Call `Seats.EnsureSeatAsync` at the five places of research R8, each inside the
  transaction that writes:
  - `Identity/Infrastructure/StaffAccounts.cs`: `CreateAsync` for invited and active (begin a
    transaction when none is open); `SetStateAsync` when disabled → active;
  - `Identity/Infrastructure/AccountHandover.cs`: `CreateWithLinkAsync` (through `CreateAsync`: the lock holds the account's creation only;
    the link and the email come after it, outside the lock, and a just-created invited account
    with no link yet already counts, research R8); `ResendInvitationAsync` and `ReissueAsync` for an account that does not count.
  T020 and T021 pass, and the Identity tests of 003 and 004 still pass.
- [X] T025 [US3] Add the data migration
  `src/server/modules/Plans/Scs.Plans/Infrastructure/Migrations/…_MaxUsersBuilt.cs`. It sets
  `plans.Feature.Status = 'built'` where `Key = 'max_users'` and runs after the seed's migration
  (research R9). Update the expectations of 012's catalogue tests that count built and upcoming
  features (one more built, one less upcoming) and its browser stand-in's catalogue copy, if it
  pins `max_users` as upcoming.

**Checkpoint**: the users limit holds today, and 026, 015 and 017 inherit it.

---

## Phase 6: User Story 4 - The four monthly limits are metered and run down with use (P2)

**Goal**: an append-only meter per clinic and month, Cairo time, refusing what the allowance
cannot cover.

**Independent Test**: plan with 1,000 message credits: use 400, try 700, use beyond with a
promise, give back, change the plan mid-month, cross the 1st in Cairo.

- [X] T026 [P] [US4] Write failing `Scs.UnitTests/Subscriptions/CairoMonthTests.cs`:
  - `yyyy-MM` at 21:59:59 and at 22:00:00 UTC on 31 October (Cairo +02:00 after summer time);
  - the same around a summer-time date;
  - the renewal date is the 1st of next month.
- [X] T027 [P] [US4] Write failing `Scs.IntegrationTests/Subscriptions/MeterTests.cs`, covering:
  - 400 of 1,000 leaves 600, recorded with time, purpose, reference and `By`;
  - 700 refused `PLAN_LIMIT_REACHED` (`feature` `message_credits`, `limit` 1000), nothing taken;
  - a promised use beyond is taken with `BeyondAllowance`; left reads 0 and overrun reads the
    excess;
  - a give-back raises what is left in the use's month; beyond the use it is refused
    `METER_GIVE_BACK_TOO_MUCH`;
  - a plan change mid-month: the new allowance, use kept;
  - no plan: refused;
  - an included limit with no number never refuses;
  - a non-monthly key and an amount ≤ 0 are refused;
  - the fake clock crosses 00:00 Cairo on the 1st: the allowance restarts and last month reads
    as before (SC-006);
  - 20 uses together that only one fits: 1 succeeds;
  - another clinic's entries are invisible.
- [X] T028 [US4] Create `Subs/Domain/CairoMonth.cs` (`Africa/Cairo`, as `SK/Wording/Words.cs`
  finds it). T026 passes.
- [X] T029 [US4] Create `SubsC/MeterContracts.cs` (`IMonthlyMeter`, `MeterUse`, `MeterReading`)
  and `Subs/Infrastructure/MonthlyMeter.cs`, using the same lock as T022 on
  `meter:{clinicId}:{key}`, per research R10 and `contracts/enforcement-api.md` §5. Register it.
  T027 passes.

**Checkpoint**: 021, 061 and the AI and teleconsult specs have their meter.

---

## Phase 7: User Story 5 - The clinic console shows a page the plan does not include as such (P2)

**Goal**: the frames' card on a feature page the plan withholds, the link only for «عرض
الاشتراك», the role's answer first, and a plan change seen on the next page.

**Independent Test**: with the platform intercepted, a clinic whose session withholds
`allow_analytics` and the channels. Open «التقارير المالية» and «الصندوق الوارد» with and without
«عرض الاشتراك», without the view permission, after the plan changes, and with the feature
upcoming.

- [X] T030 [P] [US5] Write failing `Scs.IntegrationTests/Subscriptions/SessionPlanTests.cs`:
  - `GET /v1/auth/session` carries `plan`: names, `withheld` (only built switches not included)
    and `names` for those keys;
  - no plan: names null, the withheld built switches listed;
  - everything upcoming: `withheld` empty;
  - per `contracts/enforcement-api.md` §6.
- [X] T031 [US5] Add `plan` to the session answer in `Identity/Api/AuthEndpoints.cs` (and its
  response record), from `IClinicEntitlements.CurrentAsync`. T030 passes, and 003's session tests
  still pass.
- [X] T032 [P] [US5] Add `plan: { nameAr, nameEn, withheld, names }` to `StaffSession` in
  `src/web/projects/core/src/lib/auth/auth.service.ts`. Make `PlanSource` in
  `src/web/projects/core/src/lib/shell/sources.ts` real:
  - `excludes(keys)` is true when every key is withheld;
  - `name(key)` returns the catalogue name in the console's language;
  - add unit tests beside it.
  The dev sample sources (`clinic-app/dev/sample-sources.ts`) keep overriding it for the gallery
  page only.
- [X] T033 [P] [US5] Add `feature` (one key or an any-of list) to the destinations of research R13
  in `clinic-app/shell/destinations.ts`. Pin the table in `clinic-app/shell/destinations.spec.ts`.
- [X] T034 [US5] In `clinic-app/shell/shell.routes.ts`, make `outsidePlan` read the destination's
  feature. It re-reads the session (`auth.refreshSession()`) before deciding, on every opening of a
  destination that names a feature, and only those (research R12).
- [X] T035 [US5] Redraw `NotInPlanPage` in `clinic-app/shell/pages.ts` per
  `contracts/not-in-plan-screen.md` §2:
  - the page header unchanged;
  - `scs-empty-state context="not-in-plan"` with the title «الباقة الحاليّة لا تشمل {title noun}»;
  - the line ending without or with «، ويُطلب ذلك من مالك العيادة.» by `subscription.view`;
  - «فتح الملف والاشتراك» (`plan-link`) to `/account/subscription` only for a holder of
    `subscription.view`;
  - test ids `page-not-in-plan`, `not-in-plan-title`, `not-in-plan-text`.
  Add the keys of §3 to `src/web/projects/clinic/public/i18n/{ar,en}.json`:
  `app.plan.notIn.*`, and `app.plan.places.inbox|reports.title|subject` with the frames' words.
  Remove `app.shell.notInPlanText` once nothing reads it. Unit-test the words with and without
  the permission.
- [X] T036 [P] [US5] Add `plan` to the session answers of `src/web/e2e/staff-platform.ts` (empty
  by default; a helper to withhold keys). Move 006's not-in-plan cases in
  `src/web/e2e/placeholder.spec.ts`, `shell-banners.spec.ts` and `shell-states.spec.ts` from the
  sample sources to the session's `plan.withheld`. They keep asserting what they asserted, with
  the new card's words.
- [X] T037 [US5] Write `src/web/e2e/not-in-plan.spec.ts`, each case in both languages at 1440 and
  390:
  - the inbox card and the financial card for a holder of «عرض الاشتراك», with the drawn words
    and the link opening «الملف والاشتراك»;
  - the card without the link and with the owner line;
  - the page still in the side bar;
  - a view-only holder sees the same card;
  - no view permission: the role's page, never the card;
  - the feature removed from `withheld` (the plan changed): the next page opened shows the page;
  - an upcoming feature never shows the card;
  - nothing cut or overlapping, by the shell's layout checks of `shell-widths.spec.ts`.
- [X] T038 [US5] With `SCS_SHOTS`, take the two cards and the no-link card in Arabic and English at
  1440 and 390. **Open every picture** and lay each beside its frame (`6701:1002017`,
  `6701:1002375`, `6614:945146`, `6614:945529`). Fix what differs. Note in `validation.md` what
  stays different and whose decision it is (the frame's «الخطة», the undrawn no-link card, the
  undrawn words of other destinations).

**Checkpoint**: the console mirrors the server's refusal as drawn.

---

## Phase 8: User Story 6 - One answer for the eight limits (P3)

**Goal**: one read of the eight, the same names and numbers for 024, 078 and 015.

**Independent Test**: set a plan, accounts and meter use, read the eight, and compare.

- [X] T039 [P] [US6] Write failing `Scs.IntegrationTests/Subscriptions/ClinicLimitsTests.cs`:
  - the eight in SH-015's order, with the catalogue's names and units in both languages;
  - users 4 of 10 with four counted accounts;
  - a monthly limit 400 of 1,000, renewing on the 1st of next month;
  - doctors, branches and waiting screens with their number and `Used` null;
  - no plan: the baselines;
  - another clinic's counts never mixed in.
- [X] T040 [US6] Create `SubsC/LimitContracts.cs` (`IClinicLimits`, `LimitReading`,
  `ILimitCounter`) and `Subs/Infrastructure/ClinicLimits.cs`. It reads the entitlements, the
  registered `ILimitCounter`s and the meter, per research R11. Register it. T039 passes.

**Checkpoint**: SH-016's three places have one source.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T041 [P] Write `Scs.IntegrationTests/Subscriptions/EntitlementTimingTests.cs`. 200 checks
  (`EnsureIncludedAsync` and a seat count) on the local SQL Server, with the 95th percentile ≤ 50
  ms (SC-008). Record the measured figure in `validation.md`.
- [X] T042 [P] Add the architecture rules to `Scs.ArchitectureTests`:
  - nothing references `Scs.Subscriptions` but the host;
  - other modules reach it only through `Scs.Subscriptions.Contracts`;
  - `Scs.BuildingBlocks` references no module;
  - `Plans` references no `Tenancy` or `Subscriptions`.
- [X] T043 [P] Create `Subs/Api/DevClinicPlanEndpoints.cs` (`POST /v1/dev/clinic-plan`, local
  only) per research R15 and `contracts/enforcement-api.md` §7. Add it to the development list of
  `Scs.IntegrationTests/Access/RouteAuditDeclarationTests.cs` beside `/v1/dev/staff-invitations`.
  Add a test that it is not mapped outside local.
- [X] T044 [P] Write the docs:
  - `docs/setup.md`: a "Plans on a clinic" section (the dev route, plans empty, the users limit);
  - `docs/database-changes.md`: the `subscriptions` schema, the specialty column, `max_users`
    built;
  - `docs/adding-a-module.md`: a "Plan features and limits (spec 014)" section — `RequiresFeature`
    on a route, `PlanLimits.EnsureRoomAsync` with your own count, `ILimitCounter`,
    `IMonthlyMeter.UseAsync`, and marking your feature built in your own migration.
- [X] T045 Run every gate with nothing else running:
  - `dotnet test src/server/Scs.sln -c Debug`;
  - `npm --prefix src/web run ci`;
  - `npx playwright test`.
  Fix what fails.
- [X] T046 Walk the real stack as `quickstart.md` §2, all eight steps, with walk accounts only,
  never the seeded operator. Read the API's log of the walk.
- [X] T047 Make `specs/014-plan-enforcement-metering/design-vs-real.png`: the four frames beside
  the cards as built, plus the no-link card. Open it.
- [X] T048 Finish `specs/014-plan-enforcement-metering/validation.md` with:
  - what was run, with its counts against T001's baseline;
  - what was walked and seen;
  - which pictures were opened;
  - beside the design, every difference and whose decision it is;
  - what was built differently from the plan;
  - the items for the designer (the frame's «الخطة», the no-link card, other destinations'
    words) and for the BRD (the limit refusal's words);
  - what was **not** checked.

---

## Dependencies & Execution Order

- **Setup (T001–T003)**, then **Foundational (T004–T010)**: these block everything.
  - T003 before T007.
  - T007 before T008 and T009.
  - T008 before T009.
  - T004–T006 beside T007.
  - T010 after T009.
- **US1 (T011–T016)**: after the foundation.
  - T011 and T012 first, failing.
  - T013 and T014 beside them.
  - T015 before T016.
- **US2 (T017–T019)**: after US1 (the gate asks the answer).
- **US3 (T020–T025)**: after US1.
  - T020 and T021 first.
  - T022 before T023 before T024.
  - T025 is needed before T020 can pass (`max_users` must be built).
  - Independent of US2.
- **US4 (T026–T029)**: after US1 and T022 (the same lock helper). Independent of US2, US3 and US5.
- **US5 (T030–T038)**: after US1. Its server task T031 needs T016. Its web tasks need only T031's
  shape.
- **US6 (T039–T040)**: after US3 (the users counter) and US4 (the meter).
- **Polish (T041–T048)**: last. T045 → T046 → T047 → T048, in order.

### Parallel opportunities

- T004, T005, T006 together, beside T007.
- T011, T012, T013, T014 together.
- US2 (T017–T019), US3's tests (T020–T021) and US4's tests (T026–T027) together once US1 is done.
- US5's web tasks (T032, T033, T036) beside its server task T031.
- T041, T042, T043, T044 together.

### Example: User Story 3

```text
T020 SeatTests.cs       ┐ together, failing
T021 SeatRaceTests.cs   ┘
T022 PlanLimits.cs → T023 Seats.cs → T024 the five places → T025 MaxUsersBuilt → T020, T021 pass
```

---

## Implementation Strategy

- **MVP**: Setup, Foundational, and US1. That is the one answer, proven against every rule.
- **Then the three P1 refusals land together**: US2 (the gate) and US3 (users). After them, the
  platform enforces plans for real.
- **US4** (the meter), **US5** (the console's card) and **US6** (the eight-limit read) follow, in
  any order once their dependencies are met.
- **Polish closes the work** with the timing figure, the architecture rules, the walk and the
  pictures.

## Notes

- **Nothing in this spec makes a page of the console show the card for real.** Every feature with
  a page is upcoming. The card is proven with the platform intercepted, and a later spec that
  marks its feature built turns it on without touching this code.
- **The users limit is live from this spec.** A clinic with a plan of users n can no longer pass n
  through any path.
- **Commit only when the user asks.** The phases file and the 093 draft stay out of every commit.

## Phase 10: Convergence

- [X] T049 CRITICAL: Break each test-first area on purpose and see its tests fail, then restore it and record each in `validation.md`. The four areas:
  - the entitlement rules (`Subs/Domain/Entitlements.cs`, e.g. the baseline for a limit left out);
  - the plan gate (`BB/Plans/PlanGate.cs`, e.g. letting a withheld feature through);
  - the seat count (`Identity/Infrastructure/Seats.cs`, e.g. counting a lapsed invitation);
  - the meter's refusal (`Subs/Infrastructure/MonthlyMeter.cs`, e.g. taking a use past the allowance).
  Do this beside the lock already shown, since the code came before its tests in this spec, per Constitution VII (contradicts).
- [X] T050 Rewrite `specs/014-plan-enforcement-metering/quickstart.md` §2 with what exists today:
  - the plan made in the operators console with a walk account;
  - `POST /v1/dev/clinic-plan` with the `X-Scs-Request: 1` header;
  - `subscription.plan-set` in the clinic's and the platform's trail;
  - the session's `plan` for `owner@noor.example`;
  - a plain statement that no route creates or re-enables an account before spec 026, so the users limit is shown by `SeatTests`.
  Per plan: quickstart.md, T046 (partial).
- [X] T051 Add a test that `POST /v1/dev/clinic-plan` does not exist outside local development, in `src/server/tests/Scs.IntegrationTests/Subscriptions/`, per FR-031 and T043 (partial).
- [X] T052 Add a case to `src/server/tests/Scs.IntegrationTests/Subscriptions/SeatTests.cs`: an invited account with a live invitation, at the limit, activates by its link and is not refused, per FR-017 and US3 (partial).
- [X] T053 Make the `LIMIT_BUSY` case match `contracts/enforcement-api.md` §4. Either retry the check once in `Identity/Infrastructure/Seats.cs` and `Subs/Infrastructure/MonthlyMeter.cs`, or change the contract to "refused, and the person tries again", per contracts §4 (partial).
- [X] T054 Add an architecture rule in `src/server/tests/Scs.ArchitectureTests/` that `Scs.Plans` references neither `Scs.Tenancy.Contracts` nor `Scs.Subscriptions.Contracts`, per plan: «Plans never reads a clinic» and T042 (partial).
- [X] T055 With `SCS_SHOTS`, retake the inbox and financial cards and the no-link card in both languages after the 600 title weight, and open every picture before T047 uses them, per T038 and SC-007 (partial).

## Phase 11: Convergence

- [X] T056 In `src/server/modules/Subscriptions/Scs.Subscriptions/Infrastructure/ClinicPlans.cs`, turn two sets of one clinic's plan at once into a refusal instead of a 500, and add a test in `src/server/tests/Scs.IntegrationTests/Subscriptions/ClinicPlansTests.cs`. The two clashes:
  - the key clash, when the clinic had no row;
  - the `Version` clash, when it had one.
  Make each a 409 with a code of the catalogue (or one retry inside the operation), with one success and one refusal or retry, per data-model.md `ClinicPlan.Version` and FR-001 (partial).
- [X] T057 Update the test table of `specs/014-plan-enforcement-metering/quickstart.md` §1 to the test files that exist:
  - the catalogue-key and operators'-route cases are in `PlanGateTests`;
  - add `DevClinicPlanTests`, `EntitlementTimingTests` and `SeatTests`' activation case.
  Per plan: quickstart.md (partial).

## Phase 12: Convergence

- [X] T058 Add an offline case to `src/web/e2e/not-in-plan.spec.ts`, in both languages:
  - load «التقارير المالية» with the plan withholding `allow_analytics`;
  - go to the home page, set the browser offline, and open «التقارير المالية» again;
  - expect the card from the last session copy, with no error and no sign-in page.
  Per contracts/not-in-plan-screen.md §4, SH-097 and Constitution VII (partial).
