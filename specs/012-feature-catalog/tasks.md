# Tasks: The Feature Catalogue

**Input**: Design documents from `/specs/012-feature-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (features-api.md, catalogue-screens.md), quickstart.md

**Tests**: required by the constitution (Principle VII). Access tests and the feature rules' tests are written before what they test and must fail first (permissions are a test-first area). No test of an earlier spec is loosened.

**Organization**: by user story. Server paths are under `src/server/`, console paths under `src/web/`. `Plans/` means `src/server/modules/Plans/Scs.Plans/`; `PlansC/` means `src/server/modules/Plans/Scs.Plans.Contracts/`; `ops-app/` means `src/web/projects/ops/src/app/`; `ui/` means `src/web/projects/ui/src/lib/`; test folders are `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/` and `Scs.ArchitectureTests/`; browser tests are `src/web/e2e/`. Frames are in the operators' design file `SetO9EhDivNcmurAMfcflY`, page «Template — Plans» (`1170:172`). The console's catalogue code stays in `ops-app/plans/`; only what a second console could use goes into `ui/`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Create `specs/012-feature-catalog/validation.md` and record the baseline as spec 095's last full run (from `specs/095-design-components/validation.md`), saying whether it was run again before this work
- [X] T002 Read every frame of this spec as a picture through the Figma connector, in Arabic and English — catalogue `2503:1262` / `2503:1114`, add `2509:1305` / `2509:1139`, where used `2783:30449` / `2783:30129`, retire `2783:31078` / `2783:30770` — and the component pages their parts come from (table, toolbar and filter, chips, badge, KPI card, menu, modal, form controls, breadcrumb); correct `contracts/catalogue-screens.md` where a word, a column or a part differs (the table's columns of §7-12, the hints and placeholders of §6-14, the English words, which row reads «يتبع التخصص»), take the English names of the seed from the English frame into `data-model.md`, and note each difference in `validation.md`; if Figma refuses, stop and tell the user
- [X] T003 [P] Add the error codes of `contracts/features-api.md` §2 to `shared/Scs.SharedKernel/ErrorCodes.cs` (`FEATURE_UNITS_REQUIRED`, `FEATURE_SWITCH_HAS_LIMIT`, `FEATURE_KEY_TAKEN`, `FEATURE_RETIRED_MUST_BE_SOLD`, `FEATURE_STATE_CHANGED`, `FEATURE_CHANGED_ELSEWHERE`) and their words in both languages to `shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs`; the existing test that every code has both passes
- [X] T004 [P] Add the icons the screens use from the design file's Icons page with `node src/web/tools/icons.mjs add` — the side bar's gem, the cards' tag and the "upcoming" barrier (research R10) — into `ui/icon/icon-registry.ts`; the icon tool's tests pass; no icon that no screen uses

---

## Phase 2: Foundational (blocks every story)

- [X] T005 Create the module per `docs/adding-a-module.md`: the projects `src/server/modules/Plans/Scs.Plans/Scs.Plans.csproj` (references `Scs.BuildingBlocks` and `Scs.Plans.Contracts`) and `src/server/modules/Plans/Scs.Plans.Contracts/Scs.Plans.Contracts.csproj` (references `Scs.SharedKernel` only), added to `src/server/Scs.sln`; `Plans/PlansModule.cs` implementing `IModule` and `IMigratableModule` as `OperatorsModule` does; registered in `src/server/host/Scs.Api/ModuleRegistration.cs` (with the project reference) and `src/server/tools/Scs.Migrator/Migrator.cs`; the architecture tests pick it up and pass
- [X] T006 [P] Write failing `tests/Scs.UnitTests/Plans/FeatureTests.cs` per `data-model.md`: key "2–64; `^[a-z][a-z0-9_]*$`"; names "1–120 each"; descriptions "≤ 500 each"; units "≤ 40 each; both required on a limit, both empty on a switch"; baseline "limit only; 0–1,000,000; null = no limit; empty on a switch"; group one of `core` · `channels` · `reports` · `money` · `clinical` · `ai` · `growth`; kind `switch` · `limit`; specialty one of the fifteen codes or null; status `built` · `upcoming`, default `upcoming`; `IsSold` default true; retire only "sold and not retired", sell again only "retired"; "sold" cannot be turned off a retired feature; `Version` "starts at 1; raised by every edit, not by retire or sell again"; the key never changes; editing a limit into a switch requires its units and baseline gone; the monthly list is exactly `message_credits`, `ai_xray_analysis`, `ai_voice_notes`, `teleconsult_minutes`; the specialty-decided key is `specialty_exam_modules`
- [X] T007 Create `Plans/Domain/Feature.cs`, `Plans/Domain/FeatureGroups.cs` (the seven in order), `Plans/Domain/Specialties.cs` (the fifteen of `data-model.md` in order, SH-013) and the fixed lists (monthly, specialty-decided) per `data-model.md`; T006 passes
- [X] T008 Create `Plans/Infrastructure/Persistence/PlansDbContext.cs` (default schema `plans`, history `plans.__EFMigrationsHistory`, as `PlatformDbContext` does) mapping `plans.Feature` per `data-model.md` — `Key` primary key, the lengths, the code columns, `Version` as a concurrency token — with the database checks: the key's pattern and length; `Kind = 'limit'` ⇒ both units present; `Kind = 'switch'` ⇒ no unit and no baseline; baseline in range; `IsRetired = 1` ⇒ `IsSold = 1`; an index on `(Group, SortOrder)`; add the migration `Plans/Infrastructure/Migrations/…_FeatureCatalog.cs`; add `("plans.Feature", "The catalogue of capabilities plans are made of (spec 012): names and rules of features; no clinic's data.")` to `shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs`; the isolation guardrail test passes
- [X] T009 [P] Add the four actions of `contracts/features-api.md` §3 to `shared/Scs.SharedKernel/Audit/AuditActions.cs` (`FeatureCreated` `feature.created`, `FeatureUpdated` `feature.updated`, `FeatureRetired` `feature.retired`, `FeatureResumed` `feature.resumed`; category `changes`; the sentences in both languages; facts `feature`, `group`, `kind` — never `key`) and append the four codes at the end of `shared/Scs.SharedKernel/Audit/audit-actions.lock`; the actions' catalogue tests pass
- [X] T010 [P] Create `PlansC/FeatureContracts.cs`: `IFeatureCatalogue` with `FindAsync(key)` → null or { key, kind, unitAr, unitEn, baseline, specialty, status, isSold, isRetired, monthly } and `SellableAsync()` → the sold, not retired features in the catalogue's order (`contracts/features-api.md` §4); implement it in `Plans/Infrastructure/FeatureCatalogue.cs` and register it in `PlansModule.cs`; an unknown key answers null, never a feature (FR-025, FR-026), tested in `tests/Scs.IntegrationTests/Plans/FeatureCatalogueContractTests.cs`
- [X] T011 Add `OperatorPermissions.BillingView` and `OperatorPermissions.BillingManagePlans` to the codes that must guard a route in `tests/Scs.IntegrationTests/Access/RouteDeclarationTests.cs` (it fails until T020 and T025 — leave it failing and say so)
- [X] T012 [P] Create `tests/Scs.IntegrationTests/Fixtures/FeatureFixtures.cs`: insert a feature with given fields straight into `plans.Feature`, read one back, and sign in operators holding `billing.view` alone or with `billing.managePlans` (through `CreateOperatorWithAsync` of `OperatorFixtures.cs`)
- [X] T013 [P] Extend `src/web/e2e/ops-team-platform.ts` to hold features in memory (the 35 of `data-model.md` by default) and answer every address of `contracts/features-api.md` with its refusals and `can`, to answer `/features/{key}/plans` with given plans for the dialogs' tests, and to sign in an operator holding the billing codes or not
- [X] T014 Create `ops-app/plans/features.service.ts` (the types of `contracts/features-api.md`, the reads and the writes) with `features.service.spec.ts`; add the routes of `contracts/catalogue-screens.md` §1 to `ops-app/app.routes.ts` behind `opsCan` (`billing.view`; `billing.managePlans` for `new` and `:key/edit`); add `{ key: 'plans', path: '/plans/features', icon: 'gem', permission: 'billing.view' }` to `ops-app/shell/sections.ts` with a comment that spec 013 points it at the plans page; its words «الباقات» · Plans in `src/web/projects/ops/public/i18n/{ar,en}.json` under `app.shell.nav.plans`

**Checkpoint**: the module, its table, its actions and its contract exist; the console has the section and its routes.

---

## Phase 3: User Story 5 - The catalogue starts with the features the design draws (P1)

**Goal**: the 35 seeded features. (Built before the screens because every screen, every walk and spec 013 read them.)

**Independent test**: start an empty platform and read the catalogue; run the migrations again.

- [X] T015 [P] [US5] Write failing `tests/Scs.UnitTests/Plans/FeatureSeedTests.cs`: exactly 35 rows with exactly the keys of `data-model.md` "The seed"; each row's group, kind, units, specialty (`dental_chart` and `dental_lab` `dental`, `treatment_plans` every specialty, right after `dental_chart`), baseline (`max_users` none, `max_doctors` and `max_branches` 1, the quotas 0) and sale (`core_clinic` and `specialty_exam_modules` not sold) as listed; every row `upcoming`; orders 10, 20, 30 … within each group in the listed order; every row passes `Feature`'s rules; both names present
- [X] T016 [US5] Create `Plans/Infrastructure/Seed/FeatureSeed.cs` with the 35 rows of `data-model.md` (English names as T002 took them) and the migration `Plans/Infrastructure/Migrations/…_FeatureSeed.cs` that inserts each row only when its key is absent and whose Down removes nothing (research R4); T015 passes
- [X] T017 [US5] Write `tests/Scs.IntegrationTests/Plans/FeatureSeedApplyTests.cs`: after the migrations, 35 rows equal to `FeatureSeed`; applying the seed's statements again adds nothing (SC-006); a seeded row edited (name, group, status) and one retired before re-applying are left as edited (FR-023); an operator's own feature is untouched

**Checkpoint**: a fresh platform has its catalogue.

---

## Phase 4: User Story 1 - An operator sees every feature a plan can be made of (P1) 🎯 MVP

**Goal**: the side bar's «الباقات» and the catalogue page — cards, search, filter, table, paging, states.

**Independent test**: as holders of «عرض الباقات والفواتير» with and without «إدارة الباقات والاشتراكات», and without either, open the catalogue in Arabic and English; search, filter, page.

- [X] T018 [P] [US1] Write failing `tests/Scs.IntegrationTests/Plans/FeaturesReadTests.cs`: for `GET /v1/ops/features`, `GET /v1/ops/features/{key}` and `GET /v1/ops/features/{key}/plans` — no session 401, a clinic's session refused, an operator without `billing.view` 403, with it 200; items by group (the seven in order) then `sortOrder`, retired ones included; the fields of `contracts/features-api.md` §1 with `monthly` from the fixed list and `plans` 0; `cards` { total 35, groups 7, built 0, upcoming 35, soldInPlans 0, plans 0 } on the seed and following added, built and retired rows; `can.manage` true only with `billing.managePlans`; 404 `NOT_FOUND` for an unknown key; `/plans` answers `items` [] and `total` 0; 300 features answer within a second
- [X] T019 [P] [US1] Create `ops-app/plans/feature-words.ts` with `feature-words.spec.ts`: the type's words («مفتاح» · «حدّ · <الوحدة>» · «مفتاح · للأسنان» and the other fourteen specialties · «يتبع التخصص»), «في الباقات» («<ن> باقات» · «باقتان» · «باقة واحدة» · «لا تُباع بعد»), the cards' lines and «عرض <من>–<إلى> من <المجموع> ميزة», in both languages, Western digits only, through `ops-app/i18n/ops-format.ts`
- [X] T020 [US1] Create `Plans/Api/FeaturesEndpoints.cs` with the three reads under `/ops`, each `.RequiresOperator(OperatorPermissions.BillingView)` and named (`OpsListFeatures`, `OpsGetFeature`, `OpsFeaturePlans`), and the reads in `Plans/Infrastructure/FeatureCatalogue.cs` (the plans' count and list empty until spec 013, through the same answer — research R7); map them in `PlansModule.cs`; T018 passes; `billing.view` leaves T011's list of owed codes
- [X] T021 [US1] Build `ops-app/plans/feature-catalogue.page.ts` per `contracts/catalogue-screens.md` §2 and frames `2503:1262` / `2503:1114` from `ui/` and spec 095's parts only: header and line, «+ إضافة ميزة» only with `billing.managePlans`, the four cards, the toolbar with search over both names and the key ignoring case, the filter with group, type and status offering only values that have rows, the count on the button, the chips and «مسح الكل», the table with its columns and words, 13 rows a page with the table footer, the row menu (only «أين تُستعمل» for a viewer; the other items wired by US2–US4), the retired mark, and the four states (loading, failed with «تعذّر تحميل الكتالوج.» and «أعد المحاولة», nothing matching, empty); every word in `app.plans.*` of the two i18n files; `feature-catalogue.page.spec.ts` for the states, the filter's values, and what a viewer does not see
- [X] T022 [US1] Write `src/web/e2e/ops-features.spec.ts` for the catalogue, in Arabic and English at 1440 and 390: «الباقات» shown with `billing.view` and not without, and the address refused as spec 011's pages are; the cards' numbers and lines; each row's words; search by part of an Arabic name, an English name and a key, case ignored (SC-005); each filter, the count, the chips and «مسح الكل»; paging by 13; the viewer's menu; loading, failed and retry, nothing matching; a 120-character name wraps and is not cut while the key does not wrap; no Arabic-Indic digit; `opsLooksWrong` on each; pictures when `SCS_SHOTS` is set
- [X] T023 [US1] Take the catalogue's pictures, **open every one**, lay them beside `2503:1262` and `2503:1114`, correct what differs, and record in `validation.md` what stays different by decision (the cards read 35 · 0 · 35 · 0 until features and plans are built; the retired mark is not drawn)

**Checkpoint**: the catalogue can be seen, searched and filtered by those whose role allows it, and by nobody else.

---

## Phase 5: User Story 2 - An operator adds a feature without a release (P1)

**Goal**: «+ إضافة ميزة» and its form; the feature is in the catalogue at once.

**Independent test**: add a switch, then a limit with its units, then try each refusal.

- [X] T024 [P] [US2] Write failing `tests/Scs.IntegrationTests/Plans/FeaturesAddTests.cs`: `POST /v1/ops/features` refused without a session, for a clinic's session, and for an operator holding `billing.view` alone; a valid switch 201 `{ key }` with `sortOrder` the group's highest + 10 (10 in an empty group); a valid limit with both units and a baseline; each refusal of `contracts/features-api.md` §2 — the key's pattern and length, names 1–120, descriptions ≤ 500, units ≤ 40, baseline 0–1,000,000, group, kind, specialty and status out of their lists, `FEATURE_UNITS_REQUIRED`, `FEATURE_SWITCH_HAS_LIMIT`, `FEATURE_KEY_TAKEN` (a retired feature's key too) — each saving nothing; two adds of one key at once: one 201, one `FEATURE_KEY_TAKEN`; two adds to one group at once take different orders; `feature.created` in the platform's trail with the operator as actor, the values after, and no fact named `key`
- [X] T025 [US2] Add the add to `Plans/Infrastructure/FeatureCatalogue.cs` (the order computed inside the save under a lock on the group's rows — research R5; a duplicate key from the database answered as `FEATURE_KEY_TAKEN`) and `POST /features` to `Plans/Api/FeaturesEndpoints.cs` with `.RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureCreated)` named `OpsCreateFeature`; T024 passes; T011 passes
- [X] T026 [US2] Build `ops-app/plans/feature-form.page.ts` in its add mode per `contracts/catalogue-screens.md` §3 and frames `2509:1305` / `2509:1139`: the breadcrumb, the title and its line, two columns with the fields in the drawn order and the hints and placeholders of §6-14 in both languages, the defaults (first group, «مفتاح», «قادمة», «تُباع في الباقات» on), the units and «بدون هذه القدرة» shown for a limit only, the checks run when the form is sent with the words of §3 beside each field and focus on the first in error, «إضافة الميزة» busy while saving, «أُضيفت الميزة.» and back to the catalogue, «تعذّر حفظ الميزة.» keeping what was typed; `feature-form.page.spec.ts`
- [X] T027 [US2] Extend `src/web/e2e/ops-features.spec.ts` with adding, in Arabic and English at 1440 and 390: switch to limit and back shows and hides the units and «بدون هذه القدرة»; each refusal of User Story 2 said beside its field in the page's language with nothing saved (SC-002); a key in use says «تعذّر حفظ الميزة.» and keeps the form; a saved feature is last in its group with its message; a viewer opening `/plans/features/new` gets the "not in your role" page; pictures
- [X] T028 [US2] Take the form's pictures, **open every one**, lay them beside `2509:1305` and `2509:1139`, correct what differs, and record what stays different by decision (both units and both descriptions where the frame draws one — §6-14; «بدون هذه القدرة» not drawn)

**Checkpoint**: a feature is added from the screen, without a release (SC-001).

---

## Phase 6: User Story 3 - An operator corrects a feature, never its key (P2)

**Goal**: «تعديل الميزة» with the key fixed.

**Independent test**: edit a feature's names and status; try to change its key by the form and by a direct request.

- [X] T029 [P] [US3] Write failing `tests/Scs.IntegrationTests/Plans/FeaturesEditTests.cs`: `PUT /v1/ops/features/{key}` refused as T024's add is without `billing.managePlans`; a change saved and `Version` raised; a `key` in the body ignored and the address's key kept; moved to another group it is last there; a limit made a switch with no unit and no baseline saved, with them `FEATURE_SWITCH_HAS_LIMIT`; a switch made a limit without both units `FEATURE_UNITS_REQUIRED`; an older `version` `FEATURE_CHANGED_ELSEWHERE` and nothing saved; an edit opened before a retire saves its fields and the feature stays retired; "sold" off on a retired feature `FEATURE_RETIRED_MUST_BE_SOLD`; 404 for an unknown key; `feature.updated` records only what changed, before and after
- [X] T030 [US3] Add the edit to `Plans/Infrastructure/FeatureCatalogue.cs` and `PUT /features/{key}` to `Plans/Api/FeaturesEndpoints.cs` with `.RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureUpdated)` named `OpsEditFeature`; T029 passes
- [X] T031 [US3] Add the edit mode to `ops-app/plans/feature-form.page.ts`: «تعديل ميزة» with its values, the key shown read-only with its hint, «حفظ التغييرات», «حُفظت الميزة.» and back, «تُباع في الباقات» disabled with its reason on a retired feature, the loading state while the feature is read and the failed state with its retry when it cannot be, «لم تعد هذه الميزة موجودة.» on a 404, «تعذّر حفظ الميزة.» on any other refusal; the row menu's «تعديل الميزة» opens it; specs extended
- [X] T032 [US3] Extend `src/web/e2e/ops-features.spec.ts` with editing, in Arabic and English at 1440 and 390: the key cannot be typed into; a saved change shows in its row; a group change moves the row; the refusals; the missing feature's sentence; the form's loading and failed states; pictures, **open every one**, beside the add frame (the edit form is not drawn apart) and record it

**Checkpoint**: features are corrected; keys never move.

---

## Phase 7: User Story 4 - An operator sees where a feature is used, and retires it (P2)

**Goal**: «أين تُستعمل», «إيقاف الميزة…» and «إعادة بيع الميزة».

**Independent test**: open «أين تُستعمل»; retire a sold feature and confirm; sell it again.

- [X] T033 [P] [US4] Write failing `tests/Scs.IntegrationTests/Plans/FeaturesRetireTests.cs`: `POST /v1/ops/features/{key}/retire` and `/resume` refused without `billing.managePlans`; retiring a sold feature sets it retired and changes nothing else of the row (`Version` too); retiring a not-sold or retired one and resuming a not-retired one `FEATURE_STATE_CHANGED`; resuming leaves it sold and not retired; retire, resume, retire again each allowed and each recorded as `feature.retired` / `feature.resumed`; `IFeatureCatalogue.SellableAsync` leaves a retired or not-sold feature out and takes it back after resuming, and `FindAsync` still finds a retired one (FR-021, SC-004 as far as this spec reaches)
- [X] T034 [US4] Add retire and sell again to `Plans/Infrastructure/FeatureCatalogue.cs` and their routes to `Plans/Api/FeaturesEndpoints.cs` with `.RequiresOperator(OperatorPermissions.BillingManagePlans)` and `.Records(AuditActions.FeatureRetired)` / `.Records(AuditActions.FeatureResumed)`, named `OpsRetireFeature` / `OpsResumeFeature`; T033 passes
- [X] T035 [P] [US4] Build `ops-app/plans/where-used.dialog.ts`, `ops-app/plans/retire-feature.dialog.ts` and `ops-app/plans/resume-feature.dialog.ts` per `contracts/catalogue-screens.md` §4 and frames `2783:30449` / `2783:30129` and `2783:31078` / `2783:30770` on spec 095's modal: the where-used sentence, plans with their state and clinics, the total row, the plain sentence when none; the retire sentence and, when plans include it, the warning naming them and their clinics; «إيقاف الميزة» in red; the sell-again dialog; their success and failure messages; wire them to the row menu of `feature-catalogue.page.ts` (retire for a sold, not retired feature after a divider; sell again for a retired one; neither for a not-sold one); focus back on the row's menu; specs for each, including the warning with given plans
- [X] T036 [US4] Extend `src/web/e2e/ops-features.spec.ts` with the three dialogs, in Arabic and English at 1440 and 390: where used with no plans and with intercepted plans (list, states, total); retiring with and without the warning, confirming, the row marked and its menu offering «إعادة بيع الميزة»; selling again; each failure's message; a not-sold row's menu; a viewer sees «أين تُستعمل» only; pictures
- [X] T037 [US4] Take the dialogs' pictures, **open every one**, lay them beside `2783:30449`, `2783:30129`, `2783:31078` and `2783:30770`, correct what differs, and record what is not drawn (the sell-again dialog, the messages, the empty where-used) for the designer

**Checkpoint**: every story of the spec works on its own and together.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T038 [P] Hold the offline state of FR-008a in `src/web/e2e/ops-features.spec.ts` (the bar, the catalogue kept, a save, a retire and a sell again refused offline with their messages, nothing changed), and the keyboard and screen-reader promises of `contracts/catalogue-screens.md` §5 in `src/web/e2e/ops-features.spec.ts`: the table's caption and headers, the row menu by Enter and Escape with focus returned, dialogs named and holding focus, the disabled switch reading its reason, messages announced, focus on the first field in error, controls at least 40 by 40 at 390
- [X] T039 [P] Write in `docs/database-changes.md` how a later spec marks a feature «مبنية» or adds seeded features (a migration of its own, by key, never overwriting an operator's edit — FR-024, research R4), and in `docs/setup.md` the catalogue and the walk accounts with the billing codes
- [X] T040 Run every gate with nothing else running: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test`; fix what fails; no size warning in the `ops` build
- [X] T041 Walk the real stack as `quickstart.md` "By hand", all nine steps, with walk accounts only — never the seeded operator; read the API's log of the walk
- [X] T042 Look at everything once more as a whole: with `SCS_SHOTS`, take every screen, dialog and state of this spec in Arabic and English at 1440 and 390, **open every picture**, and fix what is cut off, misaligned, in the wrong direction, in the wrong language, or different from its frame; re-take after any fix; also in Edge
- [X] T043 Make `specs/012-feature-catalog/design-vs-real.png`: every frame of this spec in both languages beside the screen as built, and open it
- [X] T044 Finish `specs/012-feature-catalog/validation.md`: what was run with its counts against T001's baseline; what was walked and seen; which pictures were opened; beside the design, with every difference and whose decision it is; what was built differently from the plan; the items for the designer and for the BRD (the joined «خريطة الأسنان وخطة العلاج», the undrawn sell-again, messages and retired mark); and what was **not** checked

---

## Dependencies & Execution Order

- **Setup (T001–T004)** then **Foundational (T005–T014)**: block everything. T005 before T006–T010; T006 before T007; T007 before T008 and T010; T008 before T012; T013 before any browser test; T014 after T004.
- **US5 (T015–T017)**: after the foundation; T015 before T016 before T017.
- **US1 (T018–T023)**: after the foundation (its tests use fixtures; its walk uses the seed, so after US5 in practice). T018 before T020; T019 before T021; T021 before T022 before T023.
- **US2 (T024–T028)**: after US1 (the button and the catalogue it returns to). T024 before T025; T026 after T025's contract is stable.
- **US3 (T029–T032)**: after US2 (the same form).
- **US4 (T033–T037)**: after US1; independent of US2 and US3 on the server; T035 uses the row menu of T021.
- **Polish (T038–T044)**: last; T040 → T041 → T042 → T043 → T044 in order.

### Parallel opportunities

- T003 and T004 beside T002.
- T006, T009, T013 together once T005 is done; T010 after T007; T012 after T008.
- The test tasks that open each story — T015; T018 and T019; T024; T029; T033 — beside the console work of the story before.
- US4's server work (T033–T034) beside US2 and US3.
- T038 and T039 together.

### Example: User Story 1

```text
T018 FeaturesReadTests.cs   ┐ together
T019 feature-words.ts       ┘
T020 FeaturesEndpoints.cs (reads)  → T018 passes
T021 feature-catalogue.page.ts     → T022 ops-features.spec.ts → T023 pictures
```

## Implementation Strategy

1. **MVP**: Setup, Foundational, US5 and US1 — the seeded catalogue, seen by those whose role allows it. Stop and look: three operators (no billing code, `billing.view`, both), three answers.
2. Then US2 and US3 — features are added and corrected from the screen. Walk an add end to end on the real stack before going on.
3. Then US4, and the polish phase with the gates, the walk, every picture opened and `validation.md`.

## Notes

- Requirements → tasks: FR-001 T014, T022 · FR-002 T011, T018, T020, T024–T025, T029, T033 · FR-003 T018, T021 · FR-004 T018, T019, T021 · FR-005, FR-006 T021–T022 · FR-007 T021–T022 · FR-008 T021, T036 · FR-008a T021, T038 · FR-009 T006–T008, T024 · FR-010 T006, T008, T024, T029 · FR-011 T024–T025, T029 · FR-012 T006–T007, T018 · FR-013 T007, T019 · FR-014 T026, T031 · FR-015 T026–T027, T031–T032 · FR-016 T026, T031 · FR-017 T009, T024, T029, T033 · FR-018 T018, T035–T036 · FR-019, FR-020 T035–T036 · FR-021, FR-021a T033–T036 · FR-022 T015–T016 · FR-023 T017 · FR-024 T015, T039 · FR-025, FR-026 T010.
- Success criteria: SC-001 T027, T041 · SC-002 T024, T027, T029, T032 · SC-003 T011, T018, T024, T029, T033 · SC-004 T033 (again in 013 and 014) · SC-005 T022 · SC-006 T017 · SC-007 T023, T028, T032, T037, T042–T043.
- Not built by any task, and said so in T044: plans, their grid and the «كتالوج الميزات» button on the plans page (013); enforcement and counting (014); unit prices (021); the clinic's specialty (015).
- The implementation is not complete until T040 to T044 are done and written down (constitution VII), and every picture has been opened.

## Phase 9: Convergence

- [X] T045 Assert the catalogue's empty state («لا توجد ميزات بعد.» · "No features yet.") in `src/web/projects/ops/src/app/plans/plans.spec.ts` and in `src/web/e2e/ops-features.spec.ts` with an empty stand-in catalogue, both languages, per FR-007 and Constitution VII (partial)
- [X] T046 Assert all four cards' numbers (35 · 0 · 35 · 0 and, with one feature marked built and plans intercepted, the built count and «عبر <ن> باقات») and a built feature's «مبنية» badge and filter value in Arabic and English in `src/web/e2e/ops-features.spec.ts` per US1/AC3 and FR-003 (partial)
- [X] T047 Assert in `src/web/e2e/ops-features.spec.ts` that the group and type facets offer only the values that have rows (remove every limit from the stand-in: no «حدّ»; leave one group empty: it is not offered) per FR-006 (partial)
- [X] T048 Assert in `src/web/e2e/ops-features.spec.ts` that a viewer opening `/plans/features/max_users/edit` gets «لا يمكنك فتح هذه الصفحة» per FR-002 (partial)
- [X] T049 Assert the `feature.resumed` entry's actor, target name and change `isRetired` true → false in `src/server/tests/Scs.IntegrationTests/Plans/FeaturesRetireTests.cs` per FR-017 (partial)
- [X] T050 Assert in `src/server/tests/Scs.IntegrationTests/Plans/FeaturesEditTests.cs` that after `FEATURE_SWITCH_HAS_LIMIT` and `FEATURE_UNITS_REQUIRED` the row is unchanged (kind, units, baseline, version) and no `feature.updated` entry was written per US3/AC4 (partial)
- [X] T051 Test the form's own checks in `src/web/projects/ops/src/app/plans/plans.spec.ts`: a key of 1 and of 65 characters, a baseline of -1, 1,000,001 and "abc", a 121-character name, a 41-character unit and a 501-character description — each said beside its field and nothing sent, per FR-015 (partial)
- [X] T052 Sweep the search in `src/web/e2e/ops-features.spec.ts` over every seeded feature: for each of the 35, a part of its Arabic name, of its English name and of its key finds it on the first try, per SC-005 (partial)
- [X] T053 Make `src/web/e2e/ops-features-platform.ts` refuse as `contracts/features-api.md` §2 does: descriptions over 500, a baseline outside 0–1,000,000, a group, kind, status or specialty outside its list (`VALIDATION_FAILED` naming the field), per plan: the browser tests' stand-in (partial)
- [X] T054 Time the 300-feature read in `src/server/tests/Scs.IntegrationTests/Plans/FeaturesReadTests.cs` as the best of three answers after a warm-up, so the one-second goal is checked and a busy machine does not fail it, per plan: Performance Goals (partial)
- [X] T055 Record in `specs/012-feature-catalog/validation.md` "Built differently from the plan" that the browser tests' stand-in is its own file `src/web/e2e/ops-features-platform.ts` built on `opsTeam` (T013 named `ops-team-platform.ts`), and add the counts after T045–T054 to the run table, per T013 and Constitution VII (partial)

## Phase 10: Convergence

- [X] T056 Return the focus to the row's ⋯ menu whenever «أين تُستعمل», «إيقاف الميزة…» or «إعادة بيع الميزة» closes — by Escape, ×, «إغلاق», «إلغاء» or after a confirm — in `src/web/projects/ops/src/app/plans/feature-catalogue.page.ts` (remember the trigger when the dialog opens, focus it when it closes; after a confirm that reloads, the same row's menu), and hold it in `src/web/e2e/ops-features.spec.ts` for each way of closing, per contracts/catalogue-screens.md §6 and Constitution VI (partial)
- [X] T057 Give «بدون هذه القدرة» the placeholder and hint of BRD §6-14 — «بلا حدّ» and «ما تحصل عليه العيادة حين لا تشمل باقتها هذا الحدّ. اتركه فارغًا لبلا حدّ، أو 1 لواحد، أو 0 لِلا شيء.», in English "No limit" and "What a clinic gets when its plan leaves this limit out. Leave empty for no limit, 1 for one, or 0 for none." — in `src/web/projects/ops/public/i18n/{ar,en}.json` (`app.plans.form.baselineHint`, `baselineNote`), and check them in `src/web/projects/ops/src/app/plans/plans.spec.ts`, per FR-014 (partial)

## Phase 11: Convergence

- [X] T058 Word the catalogue's empty state as BRD §5-47 does — «لا ميزات بعد» / «أضف أول قدرة يمكن أن تشملها باقة.», in English "No features yet" / "Add the first capability a plan can include." — in `src/web/projects/ops/public/i18n/{ar,en}.json` (`app.plans.list.emptyTitle`, `emptyText`), in `specs/012-feature-catalog/contracts/catalogue-screens.md` §2, and in the assertions of `src/web/projects/ops/src/app/plans/plans.spec.ts` and `src/web/e2e/ops-features.spec.ts`; re-take and open the two empty pictures, per FR-007 (contradicts)

## Phase 12: Convergence

- [X] T059 Rebuild `specs/012-feature-catalog/design-vs-real.png` with `src/web/node_modules/.tmp/versus12.mjs` from the pictures taken after the last screen changes (the English where-used sentence, the retire dialog waiting for the plans, the form's specialty list, the empty catalogue's words), open it, and correct the "Pictures (T042, T043)" section of `validation.md`, per Constitution VII and T043 (partial)

## Phase 13: Convergence

- [X] T060 Run every gate again on the final code, one after another with nothing else running — `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test` (all three apps, from `src/web`) — since the shared `scs-text-field`/`scs-textarea` (18:26), `Feature.cs` and `FeatureCatalogue.cs` (18:34) and the console's dialogs and words changed after the runs recorded in `validation.md`; fix what fails, and replace the run table of "What was run (T040)" with these counts, per Constitution VII and T040 (partial)
