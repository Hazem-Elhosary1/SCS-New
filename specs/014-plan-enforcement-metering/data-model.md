# Data Model: Plan Enforcement and Limit Metering

Schema `subscriptions` (new module, research R1). Both tables are clinic-owned: they carry
`ClinicId`, implement `IClinicOwned`, and get their row level security predicate and policy in
the migration that creates them (constitution II). Neither is platform-wide.

## subscriptions.ClinicPlan — which plan a clinic is on

| Column | Type | Rules |
|---|---|---|
| `ClinicId` | `uniqueidentifier` | Primary key. The clinic. Isolation key |
| `PlanId` | `uniqueidentifier` NULL | The plan (013's `plans.Plan.Id`). No foreign key across schemas. Null = no plan |
| `SetAtUtc` | `datetimeoffset` | When it was last set |
| `Version` | `rowversion` | Guards the row; sets of one clinic's plan take a lock per clinic, so a second set waits for the first and then applies (T056) |

- **At most one row per clinic.** No row is the same as `PlanId` null: no plan, the baseline only
  (FR-001, FR-004).
- **Written only by `IClinicPlans.SetAsync(Guid? planId)`**, for the established clinic. Each
  set records `subscription.plan-set` in the clinic's trail (`audit.Entry`) and the platform's
  (`audit.PlatformEntry`), with `before` / `after` = the plan ids and codes, in the same
  transaction (FR-030, constitution IV).
- **A `PlanId` that `IPlanBook.FindAsync` no longer answers** reads as no plan. It is not an
  error, and the row is left alone.
- **Callers**: 015 (provisioning a clinic), 020 (a plan change takes effect), and the local-only
  development route (R15).
- **Actor on the record**: the signed-in operator or staff member when there is one (015, 020);
  `system:dev` from the development route, which has no identity.

## subscriptions.MeterEntry — one use or give-back of a monthly limit

| Column | Type | Rules |
|---|---|---|
| `Id` | `uniqueidentifier` | Primary key |
| `ClinicId` | `uniqueidentifier` | Isolation key |
| `FeatureKey` | `nvarchar(60)` | One of the four monthly keys (`message_credits`, `teleconsult_minutes`, `ai_xray_analysis`, `ai_voice_notes`); any other key is refused by the operation |
| `Month` | `char(7)` | `yyyy-MM` of `AtUtc` in `Africa/Cairo` |
| `Amount` | `int` | > 0 for a use; < 0 for a give-back; never 0 |
| `AtUtc` | `datetimeoffset` | When it happened |
| `Purpose` | `nvarchar(100)` | What it was for, as a code (`message.sent`, `teleconsult.call`, …); no personal text |
| `Reference` | `nvarchar(200)` NULL | The consumer's id for the thing (a message id, a call id) |
| `By` | `nvarchar(100)` | `account:{id}`, `operator:{id}` or `job:{name}` |
| `BeyondAllowance` | `bit` | True when a promise was completed past the allowance (FR-020) |
| `GivesBack` | `uniqueidentifier` NULL | For a give-back: the use it returns |

- **Index** `IX_MeterEntry_Month` on `(ClinicId, FeatureKey, Month)`, which includes `Amount`.
- **Append-only**: rows are never updated or deleted. Old months stay (FR-022). Retention is a
  later platform setting (SH-095).
- **Used this month** = `SUM(Amount)` for the clinic, key and month.
  - **Left** = `max(0, allowance − used)`.
  - **Overrun** = `max(0, used − allowance)`.
- **A give-back**: the amount is ≤ the use's amount plus the give-backs already made against it
  (amounts are negative, so they reduce what is left to give back). Its `Month` is the use's
  month, whatever today is (FR-021).
- **Check `CK_MeterEntry_Amount`**: `Amount <> 0`. **Check `CK_MeterEntry_GivesBack`**:
  `(Amount < 0) = (GivesBack IS NOT NULL)`.

## tenancy.Clinic — gains the specialty (research R5)

| Column | Type | Rules |
|---|---|---|
| `Specialty` | `nvarchar(40)` NULL | One of the fifteen specialty keys (012's `FeatureLists`), set by 015; null until then |

`ClinicProfile` (Tenancy contracts) gains `string? Specialty`. 014 reads it and does not write it.

## plans.Feature — `max_users` becomes built (research R9)

The data migration `MaxUsersBuilt` in the `Plans` module sets `Status = 'built'` where
`Key = 'max_users'`. No column changes.

## What a clinic has — worked out, never stored (research R3, R4)

`ClinicEntitlements` for the established clinic:

| Field | Meaning |
|---|---|
| `PlanId`, `PlanNameAr`, `PlanNameEn` | The plan, or null for no plan |
| `Features` | One `Entitlement` per catalogue key |

`Entitlement`:

| Field | Meaning |
|---|---|
| `Key` | The feature |
| `Kind` | `switch` or `limit` |
| `Included` | Switch: in the plan, and not excluded by the specialty; `core_clinic` always. Limit: always true (the limit says how much) |
| `Limit` | `int?`. The plan's number; null = no limit when the plan includes it with no number; the feature's baseline when left out or no plan; switches null |
| `Enforced` | `Status == built`. False = nothing refuses it and the console never shows it as not in the plan |
| `Monthly` | One of the four monthly keys |
| `NameAr`, `NameEn`, `UnitAr`, `UnitEn` | From the catalogue, for the words |

- **Unknown key** (asked by a caller but not in the catalogue): `Included = false`, `Limit = 0`,
  `Enforced = true` (ق-٣٩).
- **Rules table** (each one unit-tested):

| Situation | Switch | Limit |
|---|---|---|
| No plan | not included (core: included) | baseline |
| In plan, no number | included | null (no limit) |
| In plan, number n | included | n |
| Left out of the plan | not included | baseline |
| Follows a specialty the clinic does not have (or has none) | not included | — |
| Upcoming | as the plan says, `Enforced = false` | as the plan says, `Enforced = false` |
| Plan disabled (013) | as the plan says | as the plan says |
| Feature retired, still in the plan (012) | as the plan says | as the plan says |
| Not in the catalogue | not included | 0 |

## The counted limits (research R7, R8, R11)

| Key | Kind | Counted by | In 014 |
|---|---|---|---|
| `max_users` | standing | Identity: active + invited with a live or not-yet-written invitation | counted and enforced |
| `max_doctors` | standing | 029 | read only; `Used` null |
| `max_branches` | standing | 028 | read only; `Used` null |
| `queue_screen` | standing | the waiting-screen spec | read only; `Used` null |
| `message_credits` | monthly | the meter (061 writes) | metered; no consumer yet |
| `teleconsult_minutes` | monthly | the meter | metered; no consumer yet |
| `ai_xray_analysis` | monthly | the meter | metered; no consumer yet |
| `ai_voice_notes` | monthly | the meter | metered; no consumer yet |

## Refusals

| Code | Status | Extensions | Arabic / English |
|---|---|---|---|
| `PLAN_FEATURE_NOT_INCLUDED` | 403 | `feature` | «غير مشمولة في باقتك» / "Not included in your plan" |
| `PLAN_LIMIT_REACHED` | 409 | `feature`, `limit` | «بلغت العيادة حدّ باقتها من «{الميزة}»: {الحدّ}. تُرفع الباقة من «الملف والاشتراك».» / "The clinic has reached its plan's limit of {feature}: {limit}. The plan is raised in Profile & subscription." |

The words are filled with the feature's catalogue name in the request's language and the limit in
Latin digits.

## Recorded action

| Code | Category | Target | Facts |
|---|---|---|---|
| `subscription.plan-set` | `changes` | `AuditTarget.Clinic` (new, "clinic") | `plan` (the code after, or empty), `before` / `after` plan ids |

It is appended to `audit-actions.lock` at the end.
