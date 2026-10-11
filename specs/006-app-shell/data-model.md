# Data Model: Console Frame (App Shell)

**Date**: 2026-10-11 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## 1. Table (schema `identity`)

### `identity.StaffPreference`

Clinic-owned: `ClinicId` filled by the context, the isolation policy of spec 002 in the creating
migration, one isolation sample.

| Column | Type | Rules |
|---|---|---|
| `AccountId` | uniqueidentifier, key | foreign key to `StaffAccount`; one row per account |
| `ClinicId` | uniqueidentifier | isolation |
| `Language` | varchar(2), null | `ar` or `en` (check). Null = the person never chose |
| `SidebarFolded` | bit, null | null = the person never chose (the width decides, FR-043) |
| `DismissedBanner` | varchar(8), null | `trial`, `expiring` or `grace` (check) — never `readonly` |
| `DismissedOnDay` | date, null | the Cairo day it was closed; set with `DismissedBanner`, and only then (check) |
| `UpdatedAtUtc` | datetimeoffset | |

A row is created by the first write. No row = nothing chosen.

**Rules**

| Rule | Where |
|---|---|
| A person reads and writes their own row only | the route is own-account; the row is found by the session's account |
| A closed banner counts only on the day it was closed, Cairo time | the answer carries the state only when `DismissedOnDay` is today |
| The read-only banner cannot be closed | `readonly` is refused (400, field `dismissedBanner`: `INVALID`) |
| Language is `ar` or `en` | 400, field `language`: `INVALID` |

Migration `StaffPreferences`: the table with its policy and checks. Down: drop it. No data to
bring over.

## 2. Answers that change

`GET /v1/auth/session` gains:

| Member | Type | Meaning |
|---|---|---|
| `preferences.language` | `ar`, `en` or null | the saved language |
| `preferences.sidebarFolded` | boolean or null | the saved choice |
| `preferences.dismissedBanner` | `trial`, `expiring`, `grace` or null | closed **today**, else null |

`GET /v1/roles` — each role gains `holders`: how many accounts of the clinic hold it.

## 3. Client-side shapes (no storage)

**Destination** — fixed in code (research R2):

| Field | Notes |
|---|---|
| `key` | stable name, e.g. `administration.roles` |
| `parent` | `reports`, `administration`, `data` or none |
| `group` | `main` («القائمة») or `management` («إدارة العيادة») |
| `path` | the address |
| `icon` | an icon of the shared set (top-level items only) |
| `labelKey` | translation key of its name |
| `permission` | a code of the catalogue, or none |
| `needs` | `dental` for «معمل الأسنان»; otherwise none |

**Sources** (research R5) — what each gives the frame:

| Source | Shape |
|---|---|
| Notifications | `state`: ready, loading, failed · `items[]`: id, kind (ticket, invoice, subscription, feedback, notice, warning, incident), title, text, time, read · `unread` · `markAllRead()` · `open(item)` · `retry()` |
| Branches | `branches[]`: id, name · `choice`: all or a branch id · `choose(choice)` |
| Subscription | `state`: trial, expiring, grace, readonly or none · `days` |
| Plan | `excludes(destinationKey)` |
| Inbox count | `unread` or none |
| Clinic traits | `dental`, `linkedToDoctor` |

## 4. Error codes

None new. Refused preferences use `VALIDATION_FAILED` with field codes.

## 5. Settings

None new. The environment's name already reaches the client in the runtime configuration; the
environment strip is shown when it is not `production`.

## 6. Dependencies added

`@angular/cdk`, the version matching Angular 22 (research R6).
