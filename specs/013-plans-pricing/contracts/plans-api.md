# Contract: the plans' addresses

**Feature**: 013-plans-pricing

All under `/v1/ops/`, on the operators' session (spec 010), through the operators' gate (spec
011): each names its permission and the gate answers before the handler runs — no session 401 (a
clinic's session is no session here), an operator without the permission 403
`PERMISSION_REQUIRED`. Bodies are JSON; errors are the platform's problem shape with a `code`.
Names come back in both languages; the console picks by its language. Amounts are JSON numbers
with at most two decimals (research R12).

## 1. Reads — `billing.view` («عرض الباقات والفواتير»)

| Address | Answers |
|---|---|
| `GET /plans` | `items[]` — id, code, nameAr, nameEn, monthlyEgp, annualEgp, monthlyUsd, annualUsd, badge, state, users (`{ limit }`, null = unlimited — the users' limit of `data-model.md`), clinics, createdAt, updatedAt, version — ordered active first, each by monthlyEgp (the console orders equal prices by the name of its language, research R13); `cards` { total, disabled, clinics, clinicsNewThisMonth, mostWanted (`{ id, nameAr, nameEn, clinics }` or null), revenue (`{ now, lastMonthEnd }` or null) }; `can` { manage } for this caller |
| `GET /plans/{id}` | one plan: the fields above, `description`, `disabledReason`, `disabledAt`; `features[]` — `{ key, limit }` for what it includes; `excluded` — how many catalogue features offered to it it does not include; `subscriptions` { clinics, monthlyRevenue, newThisMonth, onTrial }; `clinics[]` — `{ id, nameAr, nameEn, stateAr, stateEn }`, newest first (empty until 015) |
| `GET /plans/grid` | what the add form offers: the catalogue's sold, not retired features by group and order — `{ key, nameAr, nameEn, group, kind, unitAr, unitEn, status }` — and `badgeHolder` (`{ id, nameAr, nameEn, badge }` or null) |
| `GET /plans/{id}/grid` | the same for editing this plan, plus each retired or unsold feature it already includes, in its place, with `retired` / `sold` flags |

`404 NOT_FOUND` for an id that does not exist. Search, filter and paging are the console's
(research R13).

## 2. Writes — `billing.managePlans` («إدارة الباقات والاشتراكات»)

| Address | Body | Success | Records |
|---|---|---|---|
| `POST /plans` | nameAr, nameEn, description, monthlyEgp, annualEgp, monthlyUsd, annualUsd, badge?, note?, `features[]` of `{ key, limit? }` (every feature switched on) | 201 `{ id, code }`; state active; code derived (research R3) | `plan.created` |
| `PUT /plans/{id}` | the same, and `version` | 204; the grid replaced by `features` | `plan.updated` (only what changed, before and after; the grid per feature); also `plan.updated` on the plan the badge was taken from |
| `POST /plans/{id}/disable` | `{ reason? }` (≤ 500) | 204; disabled, badge removed — or nothing for a disabled plan | `plan.disabled` (when it changed) |
| `POST /plans/{id}/enable` | — | 204; active, reason and time cleared, badge not back — or nothing for an active plan | `plan.enabled` (when it changed) |

The code is never in a body; one sent is ignored. None of them touches a clinic or an invoice.

### Refusals

Checked whole before anything is written (ق-٤٢). The console says «تعذر إضافة الباقة.» or «تعذر
حفظ الباقة.» for every refusal of a save (§5-44), «تعذر تعطيل الباقة.» · «تعذر تفعيل الباقة.» for
those of a disable or enable, and places field errors at their fields.

| Status · code | When |
|---|---|
| 400 `VALIDATION_FAILED` | a field missing, too long, an amount below 0, above 9,999,999,999.99 or with more than two decimals, a limit outside 1–1,000,000, a key twice in `features`, a reason over 500; `errors` names the field (`nameAr`, `monthlyEgp`, `features[3].limit` …) |
| 400 `PLAN_FEATURE_UNKNOWN` | a key the catalogue does not hold |
| 400 `PLAN_SWITCH_HAS_LIMIT` | a limit on a feature that is a switch |
| 409 `PLAN_FEATURE_NOT_SOLD` | a feature not sold or retired that the plan did not already include |
| 409 `PLAN_BADGE_ON_DISABLED` | a badge on a disabled plan |
| 409 `PLAN_CHANGED_ELSEWHERE` | `version` is older than the plan's (a save or a badge taken since) |
| 409 `PLAN_CODE_TAKEN` | two adds with the same code lost the race twice (research R3) |

## 3. The catalogue's addresses (spec 012) — what changes

| Address | Now answers |
|---|---|
| `GET /features` | each row's `plans` — how many plans include it; `cards.soldInPlans` — features in at least one plan; `cards.plans` — the plans those features are in |
| `GET /features/{key}/plans` | `items[]` — each plan including it `{ id, nameAr, nameEn, state, clinics }`, and `total` — the clinics across them |

## 4. The recorded actions

Codes follow the trail's convention (`x.y`) and are appended to `audit-actions.lock`; the spec's
`plan_created` … `plan_enabled` are these four. Category `changes`. Target `plan`, named by its
Arabic name. Facts: `plan` (the code), `note` (the change note, when written).

| Code | Arabic · English sentence | Facts | Before and after |
|---|---|---|---|
| `plan.created` | «أُضيفت الباقة «{target}»» · Plan “{target}” was added | plan, note? | yes (every field, every included feature) |
| `plan.updated` | «عُدِّلت الباقة «{target}»{changes}» · Plan “{target}” was edited{changes} | plan, note? | yes (what changed; `feature:<key>` per grid change) |
| `plan.disabled` | «عُطِّلت الباقة «{target}»» · Plan “{target}” was disabled | plan | yes (`state`, `badge`, `disabledReason`) |
| `plan.enabled` | «فُعِّلت الباقة «{target}»» · Plan “{target}” was enabled | plan | yes (`state`, `disabledReason`) |

## 5. Published to other modules — `Scs.Plans.Contracts`

`IPlanBook.FindAsync(id)` and `IPlanBook.AssignableAsync()`; `IPlanSubscribers` with its "no
clinics" default (data-model.md, research R7, R8). In-process only; no address.
