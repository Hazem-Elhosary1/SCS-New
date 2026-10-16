# Research: The Feature Catalogue

Read on 16 Oct 2026: Ops BRD module 16 (`16-ops-clinics-plans-billing.md`) §3-6, §4-3, §5-47 …
§5-49, §5-61, §6-14, §7-12, §7-15, §7-19, §8-ج, §11-ك; BRD-0 SH-010 … SH-013, SH-021; the
frames on «Template — Plans» (`1170:172`); the behaviour reference's migrations
`20260920100000_feature_catalog` and `20260920150000_feature_baseline` (keys and English names
only — the rebuild is not bound by its code).

## R1 — Where the catalogue lives on the server

**Decision**: a new module `Plans` (schema `plans`), with a contracts project. It will hold the
plans of spec 013 and the single entitlement read of spec 014.

**Rationale**: "one module per BRD area" (constitution); module 16 owns clinics, plans and
billing, and its clinics are already the `Tenancy` module's. The catalogue and plans are one
area (plans are made of features; ق-٣٦: one read answers what a plan holds and what a clinic
may use), so they share a module; billing (018–021) can be its own when it comes.

**Alternatives considered**: the `Operators` module (it is about the console's staff, not the
product's capabilities — and 014's enforcement runs on clinic requests); the `Platform` module
(infrastructure, not a BRD area).

## R2 — The table and its constraints

**Decision**: one table `plans.Feature`; the key is the primary key; the group, type, specialty
and status are stored as their codes (`core` … `growth`; `switch` / `limit`; one of the fifteen
specialty codes or null for every specialty; `built` / `upcoming`); `IsSold`, `IsRetired` and
`SortOrder`; both names (1–120), both descriptions (≤ 500), both units (≤ 40) and the baseline
(0–1,000,000 or null). Check constraints in the database mirror the rules: a limit has both
units, a switch has no unit and no baseline; the key's pattern. An edit counter (`Version`) guards concurrent
edits; retiring and selling again do not raise it, so an edit open across a retire still saves
its fields (spec edge case).

**Rationale**: the BRD names a feature by its key everywhere (plans, the server, the audit
trail), so the key is the identity; the rules are enforced twice, in the domain (for clear
refusals) and in the database (so nothing else can write a broken row).

**Alternatives considered**: a surrogate id with a unique key (adds nothing — the key never
changes, ق-٤٦); groups and specialties as tables (they are closed lists the operator cannot
edit; codes with names in the console's words are enough, and 015 reads the same specialty
codes).

## R3 — "Monthly" is not a column

**Decision**: whether a limit is counted monthly is a fixed set of four keys in the domain
(`message_credits`, `ai_xray_analysis`, `ai_voice_notes`, `teleconsult_minutes`), published in
the contracts; not stored.

**Rationale**: §3-6 says «محسوب من قائمة ثابتة»; only those four take extra units and a unit
price (021). Storing it would let an operator make a limit "monthly" that nothing counts.

## R4 — The seed

**Decision**: a migration of its own, right after the one that creates the table, inserts the 35
rows of `FeatureSeed`, each only when its key is absent, and its Down removes nothing (an
operator may have edited a row); every row «قادمة»; a later release that adds seed rows adds them the same way in its
own migration. The order within a group is the design's, spaced by 10.

**Rationale**: FR-022–FR-024: once, idempotent, never overwriting an edit; the seed is part of
the product's schema history, applied by the deployment tool like every migration.

**Alternatives considered**: a start-up seeder (runs on every start; needs its own "applied"
bookkeeping; the migration history already is that bookkeeping).

## R5 — Order within a group

**Decision**: computed on the server inside the save: the group's highest order plus 10, under a
lock on the group's rows so two adds to one group cannot take the same number. Moving to another
group does the same; reordering by hand is not offered (§3-6: «لا يُسأل عنه المشغّل»).

## R6 — Retire and sell again

**Decision**: `IsRetired` is a flag, separate from `IsSold`. Retire is offered when the feature
is sold and not retired; sell again when retired. Neither touches anything else; plans that hold
the feature keep it (ق-٤٨) — enforced by 013, which will refuse adding a retired feature to a
plan that does not hold it. A not-sold feature is never retired (it is not offered in plans in
the first place).

**Rationale**: §4-3 draws two separate attributes (built/upcoming, sold/not sold) and a third
transition (sold → retired); the clarification of 16 Oct adds retired → sold. Keeping "retired"
apart from "sold" keeps «لا تُباع» (a product decision: core capabilities, specialty-decided)
apart from «موقوفة» (a commercial one).

## R7 — Where it is used, before plans exist

**Decision**: the "where used" address returns, for a key, the plans that include it with their
state and clinics, and a total. Until spec 013 creates plans it returns none, through the same
contract; 013 fills it in place. The «في الباقات» column and the card «مُباعة في الباقات» read the
same numbers.

**Rationale**: the screens and their tests are built and checked now, with the empty answer the
real platform gives today; 013 changes the data, not the screens.

## R8 — The console's entry

**Decision**: a section «الباقات» (icon `gem`, as drawn) for holders of `billing.view`, routed to
`/plans/features` until spec 013 makes `/plans` the plans list and puts «كتالوج الميزات» beside
«إضافة باقة +» (§5-47). The form is `/plans/features/new` and `/plans/features/:key/edit`, with
the breadcrumb «الباقات / إضافة ميزة» as drawn.

## R9 — Filtering

**Decision**: the list toolbar and filter panel of spec 095 with three facets — group, type,
status (built, upcoming, retired) — each offering only values that have rows, the count on the
Filter button and the applied chips with «مسح الكل», as on the operators list. Search and filter
run in the console on the loaded catalogue (a few hundred rows at most).

## R10 — Icons

**Decision**: from the design file's Icons page with `tools/icons.mjs add`: the section's gem,
the cards' tag (sold in plans) and barrier (upcoming); the menus use the existing `menu-edit`,
`menu-view`, `menu-disable` and `menu-enable`.

## R11 — The recorded actions' codes

**Decision**: `feature.created`, `feature.updated`, `feature.retired`, `feature.resumed`, category
`changes`, appended to `audit-actions.lock`; the target is the feature's name and the feature is
named by the fact `feature`.

**Rationale**: every code in the trail is `x.y` and frozen in the lock file (spec 007); the spec's
`feature_created` … are the BRD's names for the same four. `key` is one of the trail's
`NeverKeys` (nothing secret is ever a fact), so a fact called `key` would be refused.

## For the owner and the designer

- Not drawn, built as stated: «إعادة بيع الميزة» and its dialog and messages; the retire
  messages; the filter panel's facets; the empty "where used" state.
- The frame joins «خريطة الأسنان وخطة العلاج»; the seed has them apart (OP-A-222, clarified).
- The form draws one unit and one description; both are in two languages (§6-14); «بدون هذه
  القدرة» is not drawn.
- The design's cards (26 built, 16 sold across 4 plans) are the replaced product's; the rebuild
  shows 0 built and 0 sold until features and plans are built.
