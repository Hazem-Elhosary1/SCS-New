# Data Model: Plans and Their Prices

**Feature**: 013-plans-pricing · schema `plans` (module `Plans`, spec 012) · both tables are
product data of the whole platform, no clinic's data, and are added to `PlatformWideTables` with
that reason (constitution II).

## Plan — `plans.Plan`

| Field | Type | Rule | Source |
|---|---|---|---|
| `Id` | GUID | made by the server; the address's `{id}` | §3-5 |
| `Code` | varchar(110) | unique; `[a-z0-9-]`, no hyphen at either end; derived once from `NameEn` (research R3); never changed | OP-A-194, ق-٤٦ |
| `NameAr` · `NameEn` | nvarchar(100) | required, 1–100 after trimming | §6-12 |
| `Description` | nvarchar(500) | required, 1–500 after trimming | §6-12 |
| `MonthlyEgp` · `AnnualEgp` | decimal(12,2) | required; 0 – 9,999,999,999.99; two decimals at most; `AnnualEgp` 0 = no annual price | §3-5, Q9 |
| `MonthlyUsd` · `AnnualUsd` | decimal(12,2) | as above; kept and shown, never billed (clarified) | ب١٥, OP-A-207 |
| `Badge` | varchar(16) null | null · `best` «الأفضل» · `most_popular` «الأكثر انتشارًا» · `best_selling` «الأكثر مبيعًا»; at most one plan on the platform (`UX_Plan_Badge`: `State` unique where `Badge IS NOT NULL`); null when disabled | ق-٤٥ |
| `State` | varchar(10) | `active` (when added) · `disabled` | §4-2 |
| `DisabledReason` | nvarchar(500) null | trimmed; empty → null; only when disabled | §5-45 |
| `DisabledAtUtc` | datetimeoffset null | only when disabled; kept on a second disable | §4-2 |
| `CreatedAtUtc` · `UpdatedAtUtc` | datetimeoffset | set by the server | §3-5 |
| `Version` | int | concurrency token; raised by a save and when the badge is taken away (research R5) | FR-019 |

Check constraints: `CK_Plan_Code`, `CK_Plan_Prices` (each ≥ 0), `CK_Plan_State`,
`CK_Plan_Badge` (`Badge IS NULL OR State = 'active'`), `CK_Plan_Disabled` (reason and time only
when disabled). Indexes: `UX_Plan_Code`, `UX_Plan_Badge` (`State`, filtered `WHERE Badge IS NOT NULL`).

**Counted, not stored**: the plan's clinics, those new this month and in trial, its revenue
(from `IPlanSubscribers`, research R7 — none until 015/016); «الحد الأقصى للمستخدمين» from its
`max_users` row (below).

## Plan feature — `plans.PlanFeature`

| Field | Type | Rule |
|---|---|---|
| `PlanId` | GUID | → `Plan.Id`, cascade |
| `FeatureKey` | varchar(64) | → `Feature.Key` (spec 012), no action |
| `Limit` | int null | for a limit only: 1–1,000,000, null = no limit; null for a switch |

Primary key `(PlanId, FeatureKey)`. A row means "included"; no row means "not included"
(ق-٤٣). Check `CK_PlanFeature_Limit` (`Limit IS NULL OR Limit BETWEEN 1 AND 1000000`). That a
switch has no limit is checked by the server against the feature's kind (the kind lives in the
other table), and a number left on a feature that became a switch is ignored on read.

## The users' limit of a plan

From the plan's `max_users` row, read in this order:

| The plan's row | «الحد الأقصى» · "Max Limit" | Detail «الحد الأقصى للمستخدمين» |
|---|---|---|
| included, limit 1 | «مستخدم واحد» · "1 user" | the same |
| included, limit n | «حتى <ن> مستخدم» · "Up to <n> users" | the same |
| included, no limit | «مستخدمون غير محدودين» · "Unlimited users" | the same |
| not included | the feature's baseline (spec 012; `max_users` has none → unlimited, ق-٣٨) | the same |

## States

| From | To | By | Guard / effect |
|---|---|---|---|
| — | active | `POST /plans` | the grid valid against the catalogue (research R4) |
| active | disabled | `POST /plans/{id}/disable` | badge removed; reason and time kept |
| disabled | disabled | the same | nothing changes (204), nothing recorded |
| disabled | active | `POST /plans/{id}/enable` | reason and time cleared; badge not back |
| active | active | `POST /plans/{id}/enable` | nothing changes (204), nothing recorded |

A plan is never deleted (FR-026).

## What the grid offers

For the add form: every catalogue feature with `IsSold` and not `IsRetired`, by group (the
seven in their order) then `SortOrder`. For the edit form: the same, plus each feature the plan
already includes that is retired or not sold, in its place, marked «موقوفة» or «لا تُباع»
(clarified). A plan's included feature that is retired or unsold stays in a save only if the
plan already had it.

## Published (`Scs.Plans.Contracts`)

- `IPlanBook.FindAsync(id)` → `PlanEntitlements?`: id, code, both names, state, the four prices,
  and `features[]` of `{ key, kind, limit }` for what it includes.
- `IPlanBook.AssignableAsync()` → the active plans (id, code, both names, monthly and annual EGP),
  by monthly price then name.
- `IPlanSubscribers` (implemented here as "no clinics"; 015/016 replace it):
  `CountsAsync(planIds)` → per plan `{ clinics, newThisMonth, onTrial, monthlyRevenue }`;
  `RevenueAsync()` → `{ now, lastMonthEnd }` across plans; `ClinicsAsync(planId)` → `[{ clinicId,
  nameAr, nameEn, stateAr, stateEn, joinedAtUtc }]`, newest first.
