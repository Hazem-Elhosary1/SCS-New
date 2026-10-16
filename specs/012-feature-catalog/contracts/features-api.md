# Contract: the catalogue's addresses

**Feature**: 012-feature-catalog

All under `/v1/ops/`, on the operators' session (spec 010), through the operators' gate (spec
011): each names its permission and the gate answers before the handler runs — no session 401 (a clinic's session is no
session here), an operator without the permission 403 `PERMISSION_REQUIRED`. Bodies are JSON;
errors are the platform's problem shape with a `code`. Names and units come back in both
languages; the console picks by its language.

## 1. Reads — `billing.view` («عرض الباقات والفواتير»)

| Address | Answers |
|---|---|
| `GET /features` | `items[]` — key, nameAr, nameEn, descriptionAr, descriptionEn, group, kind, unitAr, unitEn, baseline, specialty, status, isSold, isRetired, sortOrder, monthly (bool, from the fixed list), `plans` (how many plans include it; 0 until 013), `version` — ordered by group (the seven in order), then sortOrder; `cards` { total, groups, built, upcoming, soldInPlans, plans } where groups counts the groups that have features, soldInPlans counts features included in at least one plan and plans the plans they are in (both 0 until 013); `can` { manage } for this caller |
| `GET /features/{key}` | one row as above |
| `GET /features/{key}/plans` | `items[]` — plan id, name, state, clinics — and `total`, the clinics across those plans; empty until 013 builds plans |

`404 NOT_FOUND` for a key that does not exist. The whole catalogue is one answer: search, filter
and paging are the console's (research R9).

## 2. Writes — `billing.managePlans` («إدارة الباقات والاشتراكات»)

| Address | Body | Success | Records |
|---|---|---|---|
| `POST /features` | key, nameAr, nameEn, descriptionAr?, descriptionEn?, group, kind, unitAr?, unitEn?, baseline?, specialty?, status, isSold | 201 `{ key }`; sortOrder = the group's highest + 10 | `feature.created` |
| `PUT /features/{key}` | the same without key; `version` | 204; sortOrder recomputed when the group changes | `feature.updated` (only what changed, before and after) |
| `POST /features/{key}/retire` | — | 204; `isRetired = true` | `feature.retired` |
| `POST /features/{key}/resume` | — | 204; `isRetired = false`, still sold | `feature.resumed` |

None of them touches a plan, a subscription or a clinic.

### Refusals

The form checks the same rules before it sends (`catalogue-screens.md` §3); a refusal of a
save is said «تعذّر حفظ الميزة.» (FR-015), of retiring «تعذّر إيقاف الميزة.», of selling again
«تعذّر إعادة بيع الميزة.»; a 404 on a save «لم تعد هذه الميزة موجودة.».

| Status · code | When |
|---|---|
| 400 `VALIDATION_FAILED` | a field missing, too long, out of its list; the key's pattern; `errors` names the field |
| 400 `FEATURE_UNITS_REQUIRED` | a limit without both units |
| 400 `FEATURE_SWITCH_HAS_LIMIT` | a switch with a unit or a baseline |
| 409 `FEATURE_KEY_TAKEN` | the key exists — retired ones included; also the loser of two adds at once |
| 409 `FEATURE_RETIRED_MUST_BE_SOLD` | turning "sold" off a retired feature |
| 409 `FEATURE_STATE_CHANGED` | retire a not-sold or already retired feature; resume a not-retired one |
| 409 `FEATURE_CHANGED_ELSEWHERE` | `version` is older than the row's — except that a retire or sell again in between does not refuse an edit (the edit saves its fields; the flag stays) |

The key in the address is never changed by a body; a `key` field in `PUT` is ignored.

## 3. The recorded actions

Codes follow the trail's convention (`x.y`) and are appended to `audit-actions.lock`; the spec's
`feature_created` … `feature_resumed` are these four. Category `changes`. The target is the
feature's name; facts never use the word `key` (it is in `NeverKeys`) — the feature is named by
the fact `feature`.

| Code | Arabic · English sentence | Facts | Before and after |
|---|---|---|---|
| `feature.created` | «أُضيفت الميزة «{target}»» · Feature “{target}” was added | feature, group, kind | yes |
| `feature.updated` | «عُدِّلت الميزة «{target}»{changes}» · Feature “{target}” was edited{changes} | feature | yes |
| `feature.retired` | «أُوقف بيع الميزة «{target}»» · Feature “{target}” was retired | feature | yes (`isRetired` false → true) |
| `feature.resumed` | «أُعيدت الميزة «{target}» إلى البيع» · Feature “{target}” was put back on sale | feature | yes (`isRetired` true → false) |

## 4. Published to other modules — `Scs.Plans.Contracts`

`IFeatureCatalogue.FindAsync(key)` → null, or { key, nameAr, nameEn, kind, unitAr, unitEn, baseline, specialty,
status, isSold, isRetired, monthly }; `IFeatureCatalogue.SellableAsync()` → the sold, not retired
features in the catalogue's order. In-process only; no address.
