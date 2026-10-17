# Tasks: Plans and Their Prices

**Input**: Design documents from `/specs/013-plans-pricing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (plans-api.md, plans-screens.md), quickstart.md

**Tests**: required by the constitution (Principle VII). Access tests and the grid's rules are written before what they test and must fail first (permissions and plan entitlements are test-first areas). No test of an earlier spec is loosened; where this spec adds to what an earlier spec's test pins (routes, actions, codes), the pin is extended, never relaxed.

**Organization**: by user story. Server paths are under `src/server/`, console paths under `src/web/`. `Plans/` means `src/server/modules/Plans/Scs.Plans/`; `PlansC/` means `src/server/modules/Plans/Scs.Plans.Contracts/`; `ops-app/` means `src/web/projects/ops/src/app/`; `ui/` means `src/web/projects/ui/src/lib/`; test folders are `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/` and `Scs.ArchitectureTests/`; browser tests are `src/web/e2e/`. Frames are in the operators' design file `SetO9EhDivNcmurAMfcflY`, page «Template — Plans» (`1170:172`). Built specs' folders (`specs/001-…` to `specs/012-…`, `094`, `095`) are never edited (constitution 1.3.0); their code may be. Shared `ui/` files are edited by hand only, never by a formatter.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Create `specs/013-plans-pricing/validation.md` and record the baseline as spec 012's last full run (from `specs/012-feature-catalog/validation.md`: server unit 886, architecture 68, integration 867; `npm run ci`; Playwright 1143 passed, 4 skipped), saying whether it was run again before this work
- [X] T002 Read every frame of this spec as a picture through the Figma connector, in Arabic and English — list `1180:70` / `1180:291`, empty `1250:70` / `1253:70`, filter open `1253:5666` / `1261:756`, add `1202:70` / `1202:116`, edit `1214:70` / `1214:159`, disable `1214:601` / `1214:761`, detail `2016:963` / `2019:1004` — and the component pages their parts come from (table, toolbar and filter, chips, badge, KPI card, avatar, menu, modal, form controls, switch, select, breadcrumb); correct `contracts/plans-screens.md` where a word, a column, a size or a part differs, and note each difference in `validation.md`; if Figma refuses, stop and tell the user
- [X] T003 [P] Add the refusal codes of `contracts/plans-api.md` §2 to `shared/Scs.SharedKernel/ErrorCodes.cs` (`PLAN_FEATURE_UNKNOWN`, `PLAN_SWITCH_HAS_LIMIT`, `PLAN_FEATURE_NOT_SOLD`, `PLAN_BADGE_ON_DISABLED`, `PLAN_CHANGED_ELSEWHERE`, `PLAN_CODE_TAKEN`) and their words in both languages to `shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs`; extend the count pinned in `tests/Scs.UnitTests/Errors/ErrorCatalogueTests.cs` (63 → 69); the test that every code has both words passes
- [X] T004 [P] Add the cards' two icons from the design file's Icons page with `node src/web/tools/icons.mjs add Interface-Essential/trophy --as trophy` and `node src/web/tools/icons.mjs add Money-Shopping/coins-stack --as coins` into `ui/icon/icon-registry.ts` (research R13); the icon tool's tests pass once T016 uses them; no icon that no screen uses

---

## Phase 2: Foundational (blocks every story)

- [X] T005 [P] Write failing `tests/Scs.UnitTests/Plans/PlanTests.cs` per `data-model.md`: the code from the English name — lower-cased, "each run of characters other than `a–z` and `0–9` becomes one hyphen", "hyphens trimmed from both ends", "`plan` when nothing is left" (`Pro Plan` → `pro-plan`, `  Pro  ++ Plan 2 ` → `pro-plan-2`, `!!!` → `plan`, an Arabic-only name → `plan`), and the next free code (`pro-plan`, `pro-plan-2`, `pro-plan-3`); names "1–100 after trimming"; description "1–500 after trimming"; each of the four amounts "0 – 9,999,999,999.99; two decimals at most" (499.999 refused, −1 refused); badge null or `best` · `most_popular` · `best_selling`; state `active` when added; disable sets reason (trimmed, "≤ 500", empty → null) and time and clears the badge; a second disable keeps the first reason and time; enable clears reason and time and leaves the badge null; a badge on a disabled plan refused; `Version` "starts at 1; raised by a save and when the badge is taken away", not by disable or enable; the code never changes on edit
- [X] T006 Create `Plans/Domain/Plan.cs` (the plan, its rules, code derivation, `Create`, `Edit`, `TakeBadge`, `LoseBadge`, `Disable`, `Enable`) and `Plans/Domain/PlanLists.cs` (badges with their names in both languages, states) per `data-model.md`; T005 passes
- [X] T007 Map `plans.Plan` and `plans.PlanFeature` in `Plans/Infrastructure/Persistence/PlansDbContext.cs` per `data-model.md` — `Code` varchar(110) unique (`UX_Plan_Code`); names nvarchar(100); description nvarchar(500); the four amounts `decimal(12,2)`; `Badge` varchar(16) null; `State` varchar(10); `DisabledReason` nvarchar(500) null; `DisabledAtUtc`; `CreatedAtUtc`, `UpdatedAtUtc`; `Version` concurrency token; the persisted computed `BadgeSlot` with the filtered unique index `UX_Plan_Badge`; checks `CK_Plan_Code`, `CK_Plan_Prices`, `CK_Plan_State`, `CK_Plan_Badge` (`Badge IS NULL OR State = 'active'`), `CK_Plan_Disabled`; `PlanFeature` keyed `(PlanId, FeatureKey)`, `PlanId` → `Plan` cascade, `FeatureKey` → `Feature` no action, `CK_PlanFeature_Limit` (`Limit IS NULL OR Limit BETWEEN 1 AND 1000000`); add the migration `Plans/Infrastructure/Migrations/…_Plans.cs`; add both tables to `shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs` with "The platform's plans and what each includes from the catalogue (spec 013): the product's offer; no clinic's data."; the isolation guardrail test passes
- [X] T008 [P] Add the four actions of `contracts/plans-api.md` §4 to `shared/Scs.SharedKernel/Audit/AuditActions.cs` (`PlanCreated` `plan.created`, `PlanUpdated` `plan.updated`, `PlanDisabled` `plan.disabled`, `PlanEnabled` `plan.enabled`; category `changes`; before and after; the sentences in both languages; facts `plan` and `note` — never `key`), append the four codes at the end of `shared/Scs.SharedKernel/Audit/audit-actions.lock`, add `AuditTarget.Plan = "plan"` to `shared/Scs.BuildingBlocks/Audit/AuditRecord.cs`, and the `plan.*` kinds to `ops-app/operators/activity-sentences.ts` with their words in `src/web/projects/ops/public/i18n/{ar,en}.json`; extend the pins in `tests/Scs.UnitTests/Audit/AuditActionsTests.cs` and `tests/Scs.IntegrationTests/Audit/AuditFactsTests.cs`
- [X] T009 [P] Create `PlansC/PlanContracts.cs`: `IPlanBook` (`FindAsync(id)` → null or the plan with `features[]` of `{ key, kind, limit }`; `AssignableAsync()` → the active plans by monthly price then name) and `IPlanSubscribers` (`CountsAsync(planIds)`, `RevenueAsync()`, `ClinicsAsync(planId)`) with their records per `data-model.md` "Published"; create `Plans/Infrastructure/NoSubscribers.cs` answering no clinics, zero revenue, no list; register both in `Plans/PlansModule.cs` so that a later module's `IPlanSubscribers` replaces the default (research R7)
- [X] T010 [P] Create `tests/Scs.IntegrationTests/Fixtures/PlanFixtures.cs`: insert a plan with given fields and grid straight into `plans.Plan` / `plans.PlanFeature`, read one back with its grid, and a fake `IPlanSubscribers` a test can fill with clinic figures (registered over the default for that test's host)
- [X] T011 [P] Create `src/web/e2e/ops-plans-platform.ts`: plans in memory (none by default; a helper adds the design's four examples), every address of `contracts/plans-api.md` with its refusals, `can`, the grid reads from the catalogue stand-in of `src/web/e2e/ops-features-platform.ts`, the badge's move, disable and enable, given clinic figures for the cards and the detail; make `ops-features-platform.ts` count plans from it
- [X] T012 Create `ops-app/plans/plans.service.ts` (the types of `contracts/plans-api.md`, the reads and the writes) with `plans.service.spec.ts`; add the routes of `contracts/plans-screens.md` §1 to `ops-app/app.routes.ts` behind `opsCan` (`billing.view` for `plans` and `plans/:id`; `billing.managePlans` for `plans/new` and `plans/:id/edit`), keeping 012's `plans/features…` routes; point the `plans` section of `ops-app/shell/sections.ts` at `/plans` and drop 012's comment that said it would; update `ops-app/ops.spec.ts` and `src/web/e2e/ops-roles.spec.ts` where they pin the section's path

**Checkpoint**: the tables, the rules, the actions, the published reads and the console's routes exist.

---

## Phase 3: User Story 1 - An operator sees every plan on the platform (P1) 🎯 MVP

**Goal**: «الباقات» opens the plans list — cards, search, filter, table, paging, row menu, states.

**Independent test**: as holders of «عرض الباقات والفواتير» with and without «إدارة الباقات والاشتراكات», and without either, open the list in Arabic and English with no plans, with plans of both states, with a search, a filter, and more than 8 plans.

- [X] T013 [P] [US1] Write failing `tests/Scs.IntegrationTests/Plans/PlansReadTests.cs`: for `GET /v1/ops/plans` and `GET /v1/ops/plans/{id}` — no session 401, a clinic's session refused, an operator without `billing.view` 403, with it 200; items "active first, each by monthlyEgp then the name"; the fields of `contracts/plans-api.md` §1; `users.limit` from the `max_users` row (included with 1, with n, with none; not included → the feature's baseline, none → unlimited); `cards` { total, disabled } counted, and with no subscribers clinics 0, mostWanted null, revenue null; with the fake `IPlanSubscribers` filled, clinics, clinicsNewThisMonth, mostWanted (the plan with the most clinics, a tie broken by the lower monthly price then the newer plan) and revenue { now, lastMonthEnd } as given; `can.manage` true only with `billing.managePlans`; 404 `NOT_FOUND` for an unknown id; no address deletes a plan — `DELETE /v1/ops/plans/{id}` is refused (an undeclared route, SH-056) and the plan is still there (FR-026); 50 plans answer within a second
- [X] T014 [P] [US1] Create `ops-app/plans/plan-words.ts` with `plan-words.spec.ts`: «الحد الأقصى» («مستخدم واحد» · «حتى <ن> مستخدم» · «مستخدمون غير محدودين» and English, clarified), «<المبلغ> ج.م / شهريًا» · "EGP <amount> / mo", «عيادة واحدة» · «<ن> عيادة», the cards' notes («<ن> باقة معطلة», «+<ن> هذا الشهر», «+<ن>٪ هذا الشهر» and − when down, «لا توجد بيانات»), «عرض <من>–<إلى> من <المجموع> باقات», amounts with thousands separators and up to two decimals, the initials of a name, in both languages, Latin digits only, through `ops-app/i18n/ops-format.ts`
- [X] T015 [US1] Create `Plans/Infrastructure/PlanBook.cs` with the list and one-plan reads (the cards and figures from `IPlanSubscribers`), and `Plans/Api/PlansEndpoints.cs` with `GET /ops/plans` and `GET /ops/plans/{id:guid}`, each `.RequiresOperator(OperatorPermissions.BillingView)` and named (`OpsListPlans`, `OpsGetPlan`); map them in `PlansModule.cs`; T013 passes
- [X] T016 [US1] Build `ops-app/plans/plans-list.page.ts` per `contracts/plans-screens.md` §2 and frames `1180:70` / `1180:291`, `1250:70` / `1253:70`, `1253:5666` / `1261:756` from `ui/` and spec 095's parts only: header and line, «كتالوج الميزات» (→ `/plans/features`), «إضافة باقة +» only with `billing.managePlans`, the four cards with their icons, tones and notes, the toolbar with search over both names ignoring case, the filter «تصفية الباقات» with «الحالة» offering only states that have plans, the count on the button, chips and «مسح الكل», the table with the avatar, «أضيفت <التاريخ>», the columns' words, the order of the clarification, 8 a page with the table footer, a click on a row to the detail, the row menu (view, edit; disable or enable wired by US5) and no «⋯» for a viewer, and the states (loading, empty «لا توجد باقات» with its line and «إضافة باقة» for managers, no results with «مسح الفلاتر», failed «حدث خطأ ما» with «حاول مرة أخرى»); every word in `app.plans.*`; `plans-list.page.spec.ts` for the states, the filter's values, the order, and what a viewer does not see
- [X] T017 [US1] Write `src/web/e2e/ops-plans.spec.ts` for the list, in Arabic and English at 1440 and 390: «الباقات» opens `/plans` with `billing.view` and is not shown without, the addresses refused as spec 011's pages are; «كتالوج الميزات» opens the catalogue; the cards with no clinics and with given figures; each row's words; search by part of an Arabic and of an English name; the filter, its count, chips and «مسح الكل»; the order; paging by 8; the manager's and the viewer's row; empty, no results, loading, failed and retry; a 100-character name wraps and is not cut; no Arabic-Indic digit; `opsLooksWrong` on each; pictures when `SCS_SHOTS` is set
- [X] T018 [US1] Take the list's pictures, **open every one**, lay them beside `1180:70`, `1180:291`, `1250:70`, `1253:70`, `1253:5666`, `1261:756`, correct what differs, and record in `validation.md` what stays different by decision (no «تصدير إلى إكسل» until 023; «دورة الفوترة» dropped; the BRD's empty words and users' words; the cards' «لا توجد بيانات» until clinics exist)

**Checkpoint**: the plans can be seen, searched and filtered by those whose role allows it, and by nobody else.

---

## Phase 4: User Story 2 - An operator makes a plan out of the catalogue (P1)

**Goal**: «إضافة باقة +» — the form, its grid, the badge, the save checked whole.

**Independent test**: add a plan with switches, limits with and without numbers and an upcoming feature; one whose English name is taken; try every refusal; read the list, the detail and the trail.

- [X] T019 [P] [US2] Write failing `tests/Scs.IntegrationTests/Plans/PlansAddTests.cs`: `GET /v1/ops/plans/grid` and `POST /v1/ops/plans` refused without `billing.view` / `billing.managePlans`; the grid offers "the catalogue's sold, not retired features by group and order" with `badgeHolder`; a valid add answers 201 `{ id, code }`, active, the grid as sent, an empty limit stored as no limit; codes `pro-plan` then `pro-plan-2`, also when two adds of "Pro Plan" run at once (both succeed, two codes); each refusal writes nothing at all (`plans.Plan` and `plans.PlanFeature` unchanged): an unknown key `PLAN_FEATURE_UNKNOWN`, a limit on a switch `PLAN_SWITCH_HAS_LIMIT`, a limit 0 or 1,000,001 and a key twice `VALIDATION_FAILED` naming `features[i]`, a not-sold or a retired feature `PLAN_FEATURE_NOT_SOLD`, a missing name, a description over 500, an amount with three decimals or below 0 `VALIDATION_FAILED` naming the field; a badge asked for moves it from its holder in the same change (the holder's badge null and its `Version` raised), also when two adds ask for a badge at once (both succeed, the later one holds it, exactly one holder after, the first has its own `plan.updated`); `plan.created` in the platform's trail with every field and every included feature, the note as a fact, and `plan.updated` on the former holder (`badge` before → none)
- [X] T020 [US2] Add the grid read and the add to `Plans/Infrastructure/PlanBook.cs` (checks of research R4 before any write; the code of research R3 under `UPDLOCK, HOLDLOCK` with one retry, then `PLAN_CODE_TAKEN`; the badge move, the holder locked first — research R2; the audit records, all in one transaction) and their routes to `Plans/Api/PlansEndpoints.cs` — `GET /ops/plans/grid` (`BillingView`, `OpsPlanGrid`) and `POST /ops/plans` (`.RequiresOperator(OperatorPermissions.BillingManagePlans)`, `.Records(AuditActions.PlanCreated)`, `OpsCreatePlan`); extend the declarations pinned in `tests/Scs.IntegrationTests/Access/RouteAuditDeclarationTests.cs`; T019 passes
- [X] T021 [US2] Build `ops-app/plans/plan-form.page.ts` for adding per `contracts/plans-screens.md` §3–§4 and frames `1202:70` / `1202:116`: the breadcrumb, title and line; the fields in the clarified order — the names (Arabic right to left, English left to right in both languages, through the text field's `rtl` / `ltr`), the tax line, «العملة» with its one value, the four prices with their examples and hints, the description, the badge select with its hint and «تحملها الآن «<الباقة>»…» when another plan holds one, the grid, the change note; the grid by group with the shared switch (off by default), the name, the limit field with «بلا حدّ» and the unit only while on, «قادمة»; the field checks with their words before sending (amounts `^\d{1,10}(\.\d{1,2})?$`, limits 1–1,000,000); «جارٍ الحفظ…»; a server refusal at the top with «تعذر إضافة الباقة.» and what was typed kept, field errors placed from `errors`; after success the list with «تمت إضافة الباقة.»; «إلغاء»; `plan-form.page.spec.ts` for the order, the checks, the badge line, the grid's switch and limit, and the refusals
- [X] T022 [US2] Extend `src/web/e2e/ops-plans.spec.ts` with adding, in Arabic and English at 1440 and 390: the form and its grid by group; an upcoming feature tagged; a limit on and empty → «بلا حدّ» in the detail; each field check; each server refusal's message with nothing lost; the badge holder line and the move; the code shown in the detail (`pro-plan`, `pro-plan-2`); a viewer's `/plans/new` refused; pictures
- [X] T023 [US2] Take the form's pictures, **open every one**, lay them beside `1202:70` and `1202:116`, correct what differs, and record what is not drawn (the tax line, the USD prices, the badge field, the change note, the limit's error) for the designer

**Checkpoint**: a plan can be made from the catalogue, whole or not at all.

---

## Phase 5: User Story 3 - An operator changes a plan, never its code (P2)

**Goal**: «تعديل الباقة» — the same form, filled; the grid replaced; the code kept.

**Independent test**: edit a plan's name, prices, badge and grid; check the code, the grid and the trail; edit in two tabs.

- [X] T024 [P] [US3] Write failing `tests/Scs.IntegrationTests/Plans/PlansEditTests.cs`: `GET /v1/ops/plans/{id}/grid` and `PUT /v1/ops/plans/{id}` refused without their permissions; the edit grid adds each retired or unsold feature the plan already includes, in its place, flagged; a save replaces the grid exactly (a feature not sent is gone); the code unchanged after the English name changes; a retired and an unsold feature the plan had are kept when sent, refused (`PLAN_FEATURE_NOT_SOLD`) when another plan tries to add them; a number on a feature made a switch since is dropped on read and refused when sent; an older `version` `PLAN_CHANGED_ELSEWHERE` with nothing written; a badge taken by another plan's save raises this plan's `Version`; a badge on a plan disabled since `PLAN_BADGE_ON_DISABLED`; a code in the body ignored; `plan.updated` with only what changed, before and after, one `feature:<key>` change per grid change (`on` · `limit <n>` · `no limit` · absent) and the note; 404 for an unknown id
- [X] T025 [US3] Add the edit grid and the edit to `Plans/Infrastructure/PlanBook.cs` and their routes to `Plans/Api/PlansEndpoints.cs` — `GET /ops/plans/{id:guid}/grid` (`BillingView`, `OpsPlanEditGrid`) and `PUT /ops/plans/{id:guid}` (`BillingManagePlans`, `.Records(AuditActions.PlanUpdated)`, `OpsEditPlan`); extend `RouteAuditDeclarationTests.cs`; T024 passes
- [X] T026 [US3] Extend `ops-app/plans/plan-form.page.ts` for editing per `contracts/plans-screens.md` §3–§4 and frames `1214:70` / `1214:159`: «تعديل <الاسم>» and «حدّث تفاصيل الباقة أدناه.», the fields filled, the kept retired / unsold rows marked «موقوفة» / «لا تُباع» with their numbers, the badge removed with its line for a disabled plan (clarified), «حفظ التغييرات», the stale refusal with «تغيّرت هذه البيانات منذ فتحتها.» and «إعادة التحميل», loading and «تعذر تحميل هذه الباقة» with «حاول مرة أخرى», after success the detail with «تم تحديث الباقة.»; specs for each
- [X] T027 [US3] Extend `src/web/e2e/ops-plans.spec.ts` with editing, in Arabic and English at 1440 and 390: the form filled; the code unchanged after a rename; a feature switched off gone from the detail; a kept retired row; the disabled plan's badge line; the stale save in two pages; a missing plan; pictures
- [X] T028 [US3] Take the edit form's pictures, **open every one**, lay them beside `1214:70` and `1214:159` (without the unit-price sub-rows, 021's), correct what differs, record what is not drawn (the kept rows' marks, the disabled plan's badge line)

**Checkpoint**: plans are corrected from the screen; the code never moves.

---

## Phase 6: User Story 4 - An operator reads a plan's detail (P2)

**Goal**: `/plans/:id` — information, features, subscriptions, clinics.

**Independent test**: open an active and a disabled plan's detail with switches, limits with and without numbers and excluded features, as viewer and manager, with no clinics and with given clinics.

- [X] T029 [P] [US4] Extend `tests/Scs.IntegrationTests/Plans/PlansReadTests.cs` for the detail: `description`, `disabledReason`, `disabledAt`, `features[]` of what it includes with limits, `excluded` = the catalogue features offered to it that it does not include, `subscriptions` and `clinics[]` from `IPlanSubscribers` (empty with the default; the fake's newest first)
- [X] T030 [US4] Build `ops-app/plans/plan-detail.page.ts` per `contracts/plans-screens.md` §6 and frames `2016:963` / `2019:1004`: the breadcrumb, the avatar, name, «<الرمز> · أضيفت <التاريخ>», «تعديل» and «تعطيل الباقة» / «تفعيل الباقة» for managers only; «معلومات الباقة» in two columns with «<المبلغ> ج.م · <المبلغ> دولار», «بلا سعر سنوي — السنة 12 × الشهري» for 0, the dates through `opsDate` / `opsDateTime` / `opsRelative`, «سبب التعطيل» for a disabled plan; «ميزات الباقة» by group in two columns with «✓», «حتى <ن> <الوحدة>», «<ن> <الوحدة>» for the four monthly features, «بلا حدّ», the closing sentence (managers' and viewers' forms) or «لا توجد ميزات مفعّلة»; «الاشتراكات»'s four figures; «العيادات المشتركة في الباقة» or its empty sentence; no extra-units card; loading and failed with «حاول مرة أخرى»; `plan-detail.page.spec.ts`
- [X] T031 [US4] Extend `src/web/e2e/ops-plans.spec.ts` with the detail, in Arabic and English at 1440 and 390: each block's words for an active and a disabled plan; a viewer without «تعديل»; no clinics and given clinics; failed and retry; one column at 390; pictures
- [X] T032 [US4] Take the detail's pictures, **open every one**, lay them beside `2016:963` and `2019:1004`, correct what differs, record what stays different by decision (the code derived, not `pro`; USD beside EGP; no extra-units card until 021; the clinic figures 0 until 015/016)

**Checkpoint**: a plan can be read in full by anyone who may view plans.

---

## Phase 7: User Story 5 - An operator disables a plan, and enables it again (P2)

**Goal**: «تعطيل الباقة…» with its dialog, «تفعيل الباقة» without one, and the published read of assignable plans.

**Independent test**: disable a plan with and without a reason, one holding the badge; disable twice; enable; read the list, the detail, `AssignableAsync` and the trail.

- [X] T033 [P] [US5] Write failing `tests/Scs.IntegrationTests/Plans/PlansDisableTests.cs`: `POST /v1/ops/plans/{id}/disable` and `/enable` refused without `billing.managePlans`; disabling sets the state, the reason ("trimmed, ≤ 500, empty → none") and the time and removes the badge, `Version` unchanged; a reason of 501 `VALIDATION_FAILED`; disabling again answers 204 with the first reason and time kept and nothing recorded; enabling clears reason and time, the badge stays none, `Version` unchanged; enabling an active plan 204 with nothing recorded; `plan.disabled` and `plan.enabled` with before and after; and write failing `tests/Scs.IntegrationTests/Plans/PlanBookContractTests.cs`: `IPlanBook.FindAsync` gives what a plan includes with each limit (null for no limit, none for a switch) and null for an unknown id; `AssignableAsync` leaves disabled plans out and lists the active ones by monthly price then name; the default `IPlanSubscribers` answers no clinics
- [X] T034 [US5] Add disable and enable to `Plans/Infrastructure/PlanBook.cs` and their routes to `Plans/Api/PlansEndpoints.cs` (`BillingManagePlans`, `.Records(AuditActions.PlanDisabled)` / `.Records(AuditActions.PlanEnabled)`, `OpsDisablePlan` / `OpsEnablePlan`); implement `IPlanBook` in `PlanBook.cs` and register it; extend `RouteAuditDeclarationTests.cs`; T033 passes
- [X] T035 [P] [US5] Build `ops-app/plans/disable-plan.dialog.ts` per `contracts/plans-screens.md` §5 and frames `1214:601` / `1214:761` on spec 095's modal: the sentence, the warning banner only when the plan has active clinics, the optional reason (≤ 500), «إلغاء» and «تأكيد التعطيل» in red with «جارٍ التعطيل…», «تم تعطيل الباقة.» / «تعذر تعطيل الباقة.», focus back to what opened it; wire it and «تفعيل الباقة» («تم تفعيل الباقة.» / «تعذر تفعيل الباقة.») to the row menu of `plans-list.page.ts` (after a divider, red, for an active plan; enable for a disabled one) and to the head of `plan-detail.page.ts`; specs for each, the warning with given clinics
- [X] T036 [US5] Extend `src/web/e2e/ops-plans.spec.ts` with disabling and enabling, in Arabic and English at 1440 and 390: the dialog without and with the warning; with a reason; the row «معطلة», the card's «باقة واحدة معطلة», the detail's reason, the badge gone; enabling from the menu and from the detail; each failure's message; a viewer sees neither; pictures
- [X] T037 [US5] Take the dialog's pictures, **open every one**, lay them beside `1214:601` and `1214:761`, correct what differs, record what is not drawn (the enable item, the messages)

**Checkpoint**: every plan story works on its own and together.

---

## Phase 8: User Story 6 - The catalogue counts its plans (P3)

**Goal**: 012's catalogue counts real plans.

**Independent test**: include a feature in two plans; read its row, the cards, «أين تُستعمل» and the retire dialog.

- [X] T038 [P] [US6] Write failing `tests/Scs.IntegrationTests/Plans/FeaturePlansCountTests.cs`: with a feature in two plans and another in one, `GET /v1/ops/features` answers each row's `plans`, `cards.soldInPlans` (features in at least one plan) and `cards.plans` (the plans they are in); `GET /v1/ops/features/{key}/plans` lists each plan `{ id, nameAr, nameEn, state, clinics }` (clinics from the fake `IPlanSubscribers`) and `total`; a disabled plan is listed with its state
- [X] T039 [US6] Read `plans.PlanFeature` in `Plans/Infrastructure/FeatureCatalogue.cs` for `ListAsync` and `PlansOfAsync` (research R11), replacing 012's fixed zeros; T038 passes and 012's `FeaturesReadTests.cs` still passes unchanged
- [X] T040 [US6] Point the breadcrumb «الباقات» of `ops-app/plans/feature-form.page.ts` and of the catalogue at `/plans`; extend `src/web/e2e/ops-features.spec.ts` with a feature in plans (its row, the cards, «أين تُستعمل», the retire warning) through the plans stand-in, and the breadcrumb back to `/plans`; 012's catalogue tests still pass unchanged

**Checkpoint**: the catalogue and the plans agree.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T041 [P] Hold the offline state of FR-009a in `src/web/e2e/ops-plans.spec.ts` (the bar, the list and detail kept, a save, a disable and an enable refused offline with their messages, nothing changed), and the keyboard and screen-reader promises: the table's caption and headers, the row menu by Enter and Escape with focus returned, the dialog named and holding focus, the grid's switches labelled by their feature, messages announced, focus on the first field in error, controls at least 40 × 40 at 390
- [X] T042 [P] Write in `docs/setup.md` the plans section (the empty start, the walk accounts with the billing codes) and in `docs/database-changes.md` that plans ship empty and how 015/016 replace `IPlanSubscribers` (research R7)
- [X] T043 Run every gate with nothing else running: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test`; fix what fails; no size warning in the `ops` build
- [X] T044 Walk the real stack as `quickstart.md` "By hand", all twelve steps, with walk accounts only — never the seeded operator; read the API's log of the walk
- [X] T045 Look at everything once more as a whole: with `SCS_SHOTS`, take every screen, dialog and state of this spec in Arabic and English at 1440 and 390, **open every picture**, and fix what is cut off, misaligned, in the wrong direction, in the wrong language, or different from its frame; re-take after any fix; also in Edge
- [X] T046 Make `specs/013-plans-pricing/design-vs-real.png`: every frame of this spec in both languages beside the screen as built, and open it
- [X] T047 Finish `specs/013-plans-pricing/validation.md`: what was run with its counts against T001's baseline; what was walked and seen; which pictures were opened; beside the design, with every difference and whose decision it is; what was built differently from the plan; the items for the designer and for the BRD (the undrawn USD prices, change note, badge field, enable item, tax line, kept rows' marks; «دورة الفوترة»; the code `pro`; the BRD's users' words); and what was **not** checked

---

## Dependencies & Execution Order

- **Setup (T001–T004)** then **Foundational (T005–T012)**: block everything. T005 before T006; T006 before T007; T007 before T010; T009 before T010; T011 before any browser test; T012 after T004. Between T004 and T016 the icon check of `npm run ci` fails by design (two icons registered, none used yet): run only the parts of `ci` a task needs until T016 is done.
- **US1 (T013–T018)**: after the foundation. T013 before T015; T014 before T016; T016 before T017 before T018.
- **US2 (T019–T023)**: after US1 (the button and the list it returns to). T019 before T020; T021 after T020's contract is stable.
- **US3 (T024–T028)**: after US2 (the same form).
- **US4 (T029–T032)**: after US1; the edit's return (US3) lands on it, so T030 before T026's success path is walked.
- **US5 (T033–T037)**: after US1 and US4 (the row menu and the detail's head); its server work is independent of US2–US4.
- **US6 (T038–T040)**: after US2 (plans exist to count).
- **Polish (T041–T047)**: last; T043 → T044 → T045 → T046 → T047 in order.

### Parallel opportunities

- T003 and T004 beside T002.
- T005, T008, T009, T011 together; T010 after T007 and T009.
- The test tasks that open each story — T013 and T014; T019; T024; T029; T033; T038 — beside the console work of the story before.
- US5's server work (T033–T034) and US6's (T038–T039) beside US2–US4's console work.
- T041 and T042 together.

### Example: User Story 1

```text
T013 PlansReadTests.cs    ┐ together
T014 plan-words.ts        ┘
T015 PlanBook.cs reads + PlansEndpoints.cs  → T013 passes
T016 plans-list.page.ts                     → T017 ops-plans.spec.ts → T018 pictures
```

## Implementation Strategy

1. **MVP**: Setup, Foundational and US1 — the plans list, seen by those whose role allows it (empty until US2). Stop and look: three operators (no billing code, `billing.view`, both), three answers.
2. Then US2 — plans made from the catalogue. Walk an add end to end on the real stack before going on.
3. Then US3 and US4 — corrected and read in full; then US5 — disabled and enabled; then US6 — the catalogue counts them.
4. Then the polish phase with the gates, the walk, every picture opened and `validation.md`.

## Notes

- Requirements → tasks: FR-001 T012, T016–T017, T040 · FR-002 T013, T015, T019–T020, T024–T025, T033–T034 · FR-003 T016, T030, T035 · FR-004 T013, T014, T016 · FR-005, FR-006, FR-007 T013, T016–T017 · FR-008 T016, T035 · FR-009 T016–T017 · FR-009a T041 · FR-010 T005–T007 · FR-011 T007, T019–T020 · FR-012 T005, T009, T030 · FR-013 T005, T007, T019, T024 · FR-014 T021 · FR-015 T019, T021, T024, T026 · FR-016 T021 · FR-017 T019–T020, T024 · FR-018 T019, T024 · FR-019 T024, T026 · FR-020 T021, T026 · FR-021, FR-022 T005, T033–T035 · FR-023 T029–T030 · FR-024 T008, T019, T024, T033 · FR-025 T009, T033–T034 · FR-026 T013 · FR-027 T038–T040 · FR-028 T014, T017, T022, T027, T031, T036 · FR-029 T018, T023, T028, T032, T037, T045–T046.
- Success criteria: SC-001 T022, T044 · SC-002 T019, T024 · SC-003 T013, T019, T024, T033 · SC-004 T005, T019, T024, T033 · SC-005 T005, T024 · SC-006 T033 (again in 015 and 016) · SC-007 T018, T023, T028, T032, T037, T045–T046.
- Not built by any task, and said so in T047: extra-unit prices and the extra-units card (021); export to Excel (023); giving a clinic a plan and changing it (015–018); the clinic figures' real source (015/016); enforcement (014); the plan cards a clinic sees with the badge (078–079).
- The implementation is not complete until T043 to T047 are done and written down (constitution VII), and every picture has been opened.

## Phase 10: Convergence

- [X] T048 Extend the US6 block of `src/web/e2e/ops-plans.spec.ts` (Arabic and English) so the catalogue, opened with `designPlans()` through the plans stand-in, shows the «مُباعة في الباقات» card counting the features in at least one plan with its note «عبر <ن> باقات» · "across <n> plans", and «إيقاف الميزة…» on a feature the plans include shows the warning naming those plans in the console's language and their clinics per US6/AC1, US6/AC3 (partial)
- [X] T049 Make the EGP annual price decide «بلا سعر سنوي — السنة 12 × الشهري» · "No annual price — a year is 12 × monthly" in `src/web/projects/ops/src/app/plans/plan-detail.page.ts` (an EGP annual of 0 says so whatever the reserved USD annual is), with a case in `plan-detail.page.spec.ts` per the spec's edge case "An annual price of 0" and US4/AC2 (partial)
- [X] T050 Add to `src/web/e2e/ops-plans.spec.ts` that a 100-character name wraps on the detail's head and is never cut, and that closing the disable dialog opened from the detail (Escape, «إلغاء», ×) gives the focus back to «تعطيل الباقة»; return the focus explicitly in `plan-detail.page.ts` if the test shows it is not, per the spec's edge case "A very long name" and contracts/plans-screens.md §5 (partial)

## Phase 11: Convergence

- [X] T051 Link each grid row's limit error to its field in `src/web/projects/ops/src/app/plans/plan-form.page.ts` (an id on `grid-limit-error`, `aria-describedby` on `grid-limit` while it is in error) so a screen reader says why the focused field is wrong, and check it in `plan-form.page.spec.ts` and the keyboard test of `src/web/e2e/ops-plans.spec.ts` (the invalid limit's accessible description is «أدخل حدًّا من 1 إلى 1,000,000، أو اتركه فارغًا لبلا حدّ.») per T041 and constitution VI (partial)
- [X] T052 Put the breadcrumb «الباقات / كتالوج الميزات» · "Plans / Feature Catalogue" above the catalogue's title in `src/web/projects/ops/src/app/plans/feature-catalogue.page.ts` (the shared `scs-breadcrumb`, as the plan form, the plan detail and the feature form have it), «الباقات» opening `/plans`, and check it in `src/web/e2e/ops-plans.spec.ts` in both languages, at 1440 and 390 — the owner's answer of 17 Oct 2026: from the catalogue there was no way back to the plans but the side bar (FR-001)
