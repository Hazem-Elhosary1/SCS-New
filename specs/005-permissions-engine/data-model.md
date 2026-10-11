# Data Model: Permissions Engine

**Date**: 2026-10-11 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## 1. The catalogue (code, not a table)

`Scs.SharedKernel/Access`. The rows are in
[contracts/permission-catalogue.md](contracts/permission-catalogue.md).

| Thing | Fields | Rules |
|---|---|---|
| `Permission` | `Number` 1–83 · `Code` · `NameAr` · `NameEn` · `Group` · `Kind` · `Requires` (a code, or none) | number and code unique; `Requires` exists and is a `view` or `read` |
| `PermissionKind` | `view`, `read`, `write`, `scope` | 20 · 5 · 56 · 2 |
| `PermissionGroup` | key, `NameAr`, `NameEn`, order 1–11 | `home` first, with no permissions; then `bookings` 7, `patients` 20, `money` 7, `inbox` 4, `tickets` 4, `reports` 7, `admin` 6, `data` 8, `settings` 13, `subscription` 7 |
| `Permissions` | one constant per code | every constant is in the table and the reverse |
| `PermissionSets.Check(codes)` | → problems: `REQUIRED` (empty), `UNKNOWN`, `DUPLICATE`, `MISSING_REQUIREMENT` with the pairs | one step: each code's direct requirement must be in the set (FR-016, FR-017) |
| `ReadySets` | `Manager` (59 codes), `Doctor` (40 codes) | both pass `Check`; counts are computed, never written |

The catalogue has a `Catalogue` name (`clinic`) so a second one can sit beside it (FR-007);
nothing else is built for that.

## 2. Tables (schema `identity`)

Both are clinic-owned: `ClinicId` filled by the context, the isolation policy of spec 002 in the
creating migration, one isolation sample each.

### `identity.Role`

| Column | Type | Rules |
|---|---|---|
| `Id` | uniqueidentifier, key | version 7 |
| `ClinicId` | uniqueidentifier | isolation |
| `Name` | nvarchar(40) | required after trimming; unique within the clinic (index on `ClinicId, Name`) |
| `Description` | nvarchar(200), null | one line |
| `Category` | varchar(8) | `ready` or `custom` (check) |
| `ReadyKey` | varchar(8), null | `owner`, `manager`, `doctor`; set only when `Category = 'ready'`; unique within the clinic when set |
| `IsRenamed` | bit | false while a ready role still has the name it was created with |
| `HoldsEverything` | bit | true only for `ReadyKey = 'owner'` (check); such a role has no `RolePermission` rows |
| `CreatedAtUtc`, `CreatedByAccountId` | datetimeoffset, uniqueidentifier null | null account = the system |
| `UpdatedAtUtc`, `UpdatedByAccountId` | datetimeoffset null, uniqueidentifier null | |
| `RowVersion` | rowversion | two people editing one role: the second is refused |

"Fixed" in the spec is `HoldsEverything`: «مالك العيادة» is the one fixed role.

### `identity.RolePermission`

| Column | Type | Rules |
|---|---|---|
| `RoleId` | uniqueidentifier | foreign key to `Role`, cascade on delete |
| `Code` | varchar(64) | a code of the catalogue (checked by the application; a test fails for a stored code that is not in it) |
| `ClinicId` | uniqueidentifier | isolation; equals the role's |

Key: `RoleId, Code`.

### `identity.StaffAccount` — one new column

| Column | Type | Rules |
|---|---|---|
| `RoleId` | uniqueidentifier, null | foreign key to `Role`, no cascade: a role in use is not removed (FR-013). Null = no role = no permission (FR-011) |

Index on `ClinicId, RoleId` for "who holds this role" and "is this the last owner".

## 3. Rules across rows

| Rule | Where it is enforced |
|---|---|
| A role holds at least one permission, all in the catalogue, forming a valid set | `PermissionSets.Check` in `IClinicRoles.CreateAsync` / `UpdateAsync` |
| «مالك العيادة» is never edited, renamed or removed | `IClinicRoles` → `ROLE_FIXED` |
| A role an account holds is not removed | `IClinicRoles.RemoveAsync` → `ROLE_IN_USE`; the foreign key behind it |
| A clinic that has an owner never loses its last one | `IClinicRoles.AssignAsync` under the clinic's `roles` lock → `LAST_OWNER` |
| Nobody changes the role of their own account | `IClinicRoles.AssignAsync` → `OWN_ROLE` |
| Giving an account a different role ends its sessions | same transaction, reason `role-changed` |
| A role belongs to the account's own clinic | isolation: another clinic's role is "not found" |

## 4. Effective permissions

For a request: the account's `RoleId` → `HoldsEverything` ? every code of the catalogue : the
role's `RolePermission` codes that are still in the catalogue. No role → none. Read once per
request, on first use; never cached between requests.

## 5. Migration (one, `RolesAndPermissions`)

1. Create the two tables with their policies, checks and indexes; add `StaffAccount.RoleId`.
2. For every clinic in `tenancy.Clinic`: insert the three ready roles and the 59 and 40
   permission rows (the lists as of this migration).
3. For every clinic: give its «مالك العيادة» to the oldest account without a role — active
   accounts first, then by `CreatedAtUtc`, then `Id` (FR-036).

Down: clear `RoleId`, drop the column and the two tables.

## 6. Ready roles as created

| Key | Name | Category | Permissions |
|---|---|---|---|
| `owner` | مالك العيادة | ready, fixed | all, by `HoldsEverything` |
| `manager` | مدير العيادة | ready | the 59 marked in the catalogue |
| `doctor` | طبيب | ready | the 40 marked in the catalogue |

Their one-line descriptions are taken from the Arabic frame "Roles / Roles List Read Only"
(`6675:19376`); the English ones, shown while a role is not renamed, from frame `6675:20097`:
"Every clinic permission", "Running the clinic day to day — not the exam room, the technical
settings or the subscription", "The exam room, patient files and their own appointments".

## 7. Seed (local and test only)

| Account | Role | Why |
|---|---|---|
| `owner@noor.example` | مالك العيادة | everything |
| `reception@noor.example` | **فريق العيادة** (custom) | holds no `admin.roles.view`: sees the no-permission state |
| `temp@noor.example` | مدير العيادة | |
| `former@noor.example` (disabled) | طبيب | |
| `owner@shifa.example` | مالك العيادة | |
| `doctor@shifa.example` | طبيب | holds `admin.roles.view` without `admin.roles.manage`: sees «للعرض فقط» |
| `new@shifa.example` (invited) | مدير العيادة | |

«فريق العيادة» is created in the Noor clinic by the seed with a small valid set: `bookings.view`,
`bookings.manage`, `bookings.attendance`, `patients.view`, `money.view`, `money.collect`. The
seed assigns a role only to an account that has none, so a developer's own changes are kept.

## 8. Error codes (5 new, 32 in all)

| Code | Status | Carries |
|---|---|---|
| `PERMISSION_REQUIRED` | 403 | `permission`: the code needed |
| `ROLE_FIXED` | 409 | — |
| `ROLE_IN_USE` | 409 | `holders`: how many accounts |
| `LAST_OWNER` | 409 | — |
| `OWN_ROLE` | 409 | — |

`FORBIDDEN` (403, exists) is the answer of a route that declares nothing.

## 9. Settings

None new.
