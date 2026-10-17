# Research: Plans and Their Prices

Read on 16 Oct 2026: Ops BRD module 16 (`16-ops-clinics-plans-billing.md`) §2-2, §2-4, §3-5,
§3-7, §4-2, §5-43 … §5-46, §6-12, §6-13, §7-9 … §7-11, §7-19, §8-ج (ق-٣٥ … ق-٥٣), §10, §11-ي
(OP-A-187 … OP-A-208, OP-A-262), §12 «الباقات والكتالوج», §13-ب, §14; BRD-0 SH-010 … SH-021; the
frames on «Template — Plans» (`1170:172`), Arabic and English; the code of spec 012 (`Plans`
module, `ops/src/app/plans/`); the `Tenancy` module's clinic (no plan on it yet).

## R1 — Where plans live on the server

**Decision**: in the `Plans` module that 012 made, beside the catalogue: two new tables in schema
`plans`, a `PlanBook` service beside `FeatureCatalogue`, `PlansEndpoints` under `/v1/ops/plans`,
and new published reads in `Scs.Plans.Contracts`.

**Rationale**: 012's research R1 put the catalogue and plans in one area (ق-٣٦: one read answers
what a plan holds and what a clinic may use). A plan's grid refers to the catalogue's rows; one
module keeps the reference inside one database context and one transaction.

**Alternatives considered**: a `Billing` module now (invoices are 018–020; prices on a plan are
not billing yet); the `Tenancy` module (a plan is the product's, not a clinic's).

## R2 — The tables

**Decision**:

- `plans.Plan`: `Id` (GUID, made by the server), `Code` (varchar 110, unique), `NameAr`,
  `NameEn` (nvarchar 100), `Description` (nvarchar 500), `MonthlyEgp`, `AnnualEgp`, `MonthlyUsd`,
  `AnnualUsd` (`decimal(12,2)`, check ≥ 0), `Badge` (varchar 16, null or one of `best`,
  `most_popular`, `best_selling`), `State` (`active` / `disabled`), `DisabledReason`
  (nvarchar 500), `DisabledAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`, `Version` (concurrency).
- `plans.PlanFeature`: `PlanId` + `FeatureKey` (primary key; foreign keys to `Plan` with cascade
  and to `Feature` with no action), `Limit` (int, null = no limit; check 1–1,000,000).
- Check constraints: `Badge IS NULL OR State = 'active'`; `State = 'disabled' OR (DisabledReason
  IS NULL AND DisabledAtUtc IS NULL)`; the code's pattern (`[a-z0-9-]`, not starting or ending
  with a hyphen).
- One badge on the platform: a unique index on `State` filtered `WHERE Badge IS NOT NULL`
  (`UX_Plan_Badge`). Every holder is active (`CK_Plan_Badge`), so two holders would repeat
  `active`. *(Built differently: the plan's computed `BadgeSlot` with a filter on it was refused by
  SQL Server — a filter cannot name a computed column.)*
- A save that asks for a badge first locks the current holder's row (`SELECT … WITH (UPDLOCK,
  HOLDLOCK) WHERE Badge IS NOT NULL`) in its transaction, then clears it and gives the badge
  to its plan. A second save at the same moment waits for the first and then moves the badge
  again; both succeed. The filtered unique index is the last guard and is never expected to fire.

**Rationale**: a row only for what a plan includes — "not sent means not included" (ق-٤٣) —
so a new catalogue feature is "not included" in every plan with no back-fill. `decimal(12,2)`
holds 9,999,999,999.99 exactly. The database refuses a second badge even if two saves race.

**Alternatives considered**: a row per plan × feature with `Included` false (every new feature
would need a row in every plan, and §3-7's "«لا» جواب صريح" is met by the grid sending all rows
and the save replacing the set); the badge as its own one-row table (one more place to keep in
step).

## R3 — The plan's code

**Decision**: derived once, when the plan is added, from the English name: lower-cased; each run
of characters other than `a–z` and `0–9` becomes one hyphen; hyphens trimmed from both ends;
`plan` when nothing is left. If the code is taken, `-2`, `-3` … are tried. The search for a free
code and the insert run in one transaction that locks the codes beginning with the base
(`UPDLOCK, HOLDLOCK`), and the unique index is the last word: a lost race retries once.

**Rationale**: OP-A-194 and the acceptance criterion "Pro Plan" → `pro-plan` / `pro-plan-2`. The
same locking pattern as 012's order numbers (012 research R5).

**Alternatives considered**: the operator types the code (the BRD says derived); the frame's
`pro` (from the product being replaced — spec, Differences found).

## R4 — Saving a plan: checked whole, written whole

**Decision**: `PUT /plans/{id}` and `POST /plans` carry the fields and the grid as a list of
`{ feature, limit }` for every feature switched on. Before any write the server checks, against
the catalogue as it is in the same transaction:

1. every key is in the catalogue (`PLAN_FEATURE_UNKNOWN`);
2. no limit on a switch (`PLAN_SWITCH_HAS_LIMIT`), every limit in 1–1,000,000 (`VALIDATION_FAILED`);
3. every feature is sold and not retired — unless this plan already includes it (ق-٤٨,
   clarified) (`PLAN_FEATURE_NOT_SOLD`);
4. no badge on a disabled plan (`PLAN_BADGE_ON_DISABLED`);
5. no duplicate key in the grid (`VALIDATION_FAILED`).

Then, in the same transaction: the badge is taken from its holder if asked for, the plan's
fields are written, its `PlanFeature` rows are replaced by the grid sent, and the audit record is
written. A feature that is now a switch but carries a number in a plan (012's edge case) is sent
without one by the form, and the read drops the number (it is not shown).

**Rationale**: ق-٤٢ "refuse, do not correct" and "before any write"; ق-٤٣ replace; SC-002 all or
nothing.

**Alternatives considered**: a separate grid address (two saves, half a plan between them); a
patch of the grid (the BRD says replace).

## R5 — Concurrent edits

**Decision**: `Version` raised by every save of the plan's fields and grid, and on the holder
when another plan's save or a disable takes its badge; disabling and enabling do not raise it
(as 012's retire). An edit sent with an older version is refused `409 PLAN_CHANGED_ELSEWHERE`
and the form says «تعذر حفظ الباقة.» with spec 011's «تغيّرت هذه البيانات منذ فتحتها.» and its
«إعادة التحميل» button (the operators' form already has both words). An edit of a plan disabled since it
was opened saves its fields; a badge in it is refused (`PLAN_BADGE_ON_DISABLED`).

**Rationale**: FR-019; a badge taken away is a change the second editor must see, or their save
would silently take it back.

## R6 — Disabling and enabling

**Decision**: `POST /plans/{id}/disable` with `{ reason? }` and `POST /plans/{id}/enable`.
Disabling an active plan sets the state, the reason (trimmed, ≤ 500, empty → none), the time,
and removes the badge. Disabling a disabled plan answers 204 and changes nothing (§4-2: the first
reason and time stay) and records nothing. Enabling clears reason and time; the badge stays
off. Enabling an active plan answers 204 and records nothing.

**Rationale**: §4-2 and ق-٤٥/٤٧; idempotent answers make a double click harmless.

**Alternatives considered**: 012's `STATE_CHANGED` refusal (the BRD here says the second disable
keeps the first, not that it fails).

## R7 — Clinics on a plan, before clinics have plans

**Decision**: `Scs.Plans.Contracts` publishes `IPlanSubscribers` — for a set of plans: the
clinics on each (active, new this month, in trial), the clinics' list for one plan (name, state
line, joined), and the monthly revenue now and at the end of last month. The `Plans` module
registers a default that answers "no clinics" for every plan. Spec 015 (clinic and its plan)
and 016 (subscription) replace it with the real one from their modules.

**Rationale**: the clinic will point at the plan (015 depends on 013), so the `Plans` module must
not reference `Tenancy`; the screens and their tests are written once with the figures' rules
(FR-004, OP-A-189), and 015/016 only supply the numbers.

**Alternatives considered**: hard zeros in the screens (015 would have to reopen them); a plan
id on today's clinic row now (015's decision, with its migration and rules).

## R8 — What other modules read

**Decision**: `IPlanBook` in `Scs.Plans.Contracts`:

- `FindAsync(planId)` → null or `{ id, code, nameAr, nameEn, state, prices, features: [{ key, kind,
  limit }] }` — the included features with their limit (null = no limit; a switch none).
- `AssignableAsync()` → the active plans, by monthly price (the order of the list), for the
  clinic form (015).

In-process only; no address. 014's entitlement read builds on `FindAsync` and the catalogue's
`FindAsync` (baselines for what a plan leaves out).

**Rationale**: FR-025; the catalogue's published read (012 R10) is the pattern.

## R9 — Recorded actions

**Decision**: four codes appended to `audit-actions.lock` — `plan.created`, `plan.updated`,
`plan.disabled`, `plan.enabled` (the spec's `plan_created` … `plan_enabled` in the trail's `x.y`
convention, as 012 R11), category `changes`, before and after. Target `plan`, named by its
Arabic name. Facts: `plan` (the code), and `note` when the change note is written. Changes: each
field that changed, and for the grid one change per feature that changed, named `feature:<key>`,
with values `on` · `limit <n>` · `no limit` · absent. A badge taken from another plan writes one
`plan.updated` on that plan too (`badge` best → none), so its own history says why.
`plans_exported` is 023's.

**Rationale**: SH-092 and constitution IV: "changes a plan … before and after … in the same unit
of work"; the fact name `key` is forbidden (`NeverKeys`).

## R10 — Refusal codes

**Decision**: six new codes in `ErrorCodes` with Arabic and English words: `PLAN_FEATURE_UNKNOWN`,
`PLAN_SWITCH_HAS_LIMIT`, `PLAN_FEATURE_NOT_SOLD`, `PLAN_BADGE_ON_DISABLED`,
`PLAN_CHANGED_ELSEWHERE`, `PLAN_CODE_TAKEN` (only if the retry of R3 also loses). Field errors
use `VALIDATION_FAILED` with `errors` naming the field (`nameAr`, `monthlyEgp`, `features[3].limit`
…). The console shows «تعذر إضافة الباقة.» / «تعذر حفظ الباقة.» for every server refusal (§5-44),
and the field's own words for `errors` it can place.

## R11 — The catalogue's counts (User Story 6)

**Decision**: `FeatureCatalogue` reads `PlanFeature`: a feature's `plans` is the number of plans
including it; the cards' `soldInPlans` and `plans` count from it; `PlansOfAsync` returns each
plan (id, name, state) with its clinics from `IPlanSubscribers` and their total. 012's console
and its retire dialog already draw these answers; only the stand-in's fixed zeros change.

## R12 — Money on the wire and on screen

**Decision**: amounts travel as JSON numbers with at most two decimals and are `decimal` on the
server (the binder refuses a third decimal as `VALIDATION_FAILED`). The console checks the field
as text (`^\d{1,10}(\.\d{1,2})?$`), shows amounts with `Intl.NumberFormat` in Latin digits,
thousands separators and up to two decimals: «499 ج.م», «4,990 ج.م», "EGP 499"; USD «25 دولار» ·
"USD 25". An annual price of 0 reads «بلا سعر سنوي — السنة 12 × الشهري» · "No annual price — a
year is 12 × monthly".

**Rationale**: amounts up to 10¹⁰ with two decimals are exact enough in a JavaScript number for
display and entry; the server never computes on a float.

## R13 — The console

**Decision**: routes `/plans` (list, `billing.view`), `/plans/new` and `/plans/:id/edit`
(`billing.managePlans`), `/plans/:id` (detail, `billing.view`); the side bar's `plans` section
points at `/plans`; 012's catalogue breadcrumb links `/plans`. The list loads every plan once and
searches, filters and pages on the console, and orders plans of equal price by the name of its
language (a handful of plans; 012 R9). Pages are built from the
`ui` parts: cards, toolbar and filter with chips, table with avatars, row menu, form controls,
the shared switch, select, textarea, dialog, empty and error states. Two icons are added through
the icon tool: `Interface-Essential/trophy` (`trophy`) and `Money-Shopping/coins-stack`
(`coins`); `gem` and `building` exist.

## R14 — The filter panel

**Decision**: the console's search-and-filter pattern (spec 095, as 011 and 012): «فلترة» opens
«تصفية الباقات» with one facet «الحالة» (نشطة · معطلة, only the states that have plans); the
frame's «دورة الفوترة» is dropped (spec, Differences found); applied filters show as a count on
the button and chips with «مسح الكل».

## R15 — Dates

**Decision**: dates in the Cairo time zone: «24 أغسطس 2026» · "Aug 24, 2026"; with time «24 أغسطس
2026 · 3:15 م» · "Aug 24, 2026 · 3:15 PM"; «آخر تحديث» relative («منذ يومين» · "2 days ago") — all through
the console's existing helpers `opsDate`, `opsDateTime`, `opsRelative` and `opsNumber`
(`ops/src/app/i18n/ops-format.ts`), Latin digits in both languages.
