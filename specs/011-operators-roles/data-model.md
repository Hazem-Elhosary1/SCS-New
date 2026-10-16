# Data Model: Operators and Their Roles

**Feature**: 011-operators-roles | **Date**: 2026-10-13

Schema `ops` (platform-wide, spec 010) gains two tables and eight columns; schema `audit` gains
one table. No clinic-owned table is added or touched.

## 1. The catalogue (code, not a table)

`OperatorPermissions` — 28 entries, closed.

| Group | Code | Arabic · English | Needs |
|---|---|---|---|
| لوحة التحكم | `dashboard.view` | عرض لوحة التحكم · View Dashboard | — |
| العيادات | `clinics.view` | عرض العيادات · View Clinics | — |
| | `clinics.edit` | تعديل بيانات العيادات · Edit Clinic Data | `clinics.view` |
| | `clinics.lifecycle` | إيقاف العيادات وإنهاء اشتراكها · Suspend Clinics & End Subscriptions | `clinics.view` |
| | `clinics.payments` | إدارة بيانات الدفع والفواتير · Manage Payments & Invoices | `clinics.view` |
| | `clinics.integrations` | إدارة تكاملات العيادة · Manage Clinic Integrations | `clinics.view` |
| | `clinics.users` | إدارة مستخدمي العيادة · Manage Clinic Users | `clinics.view` |
| | `clinics.topups` | إضافة رصيد إضافي للعيادات · Add Extra Quota to Clinics | `clinics.view` |
| التذاكر | `tickets.view` | عرض التذاكر المُسندة إليّ · View Assigned Tickets | — |
| | `tickets.viewAll` | عرض كل التذاكر · View All Tickets | `tickets.view` |
| | `tickets.reply` | الرد على التذاكر · Reply to Tickets | `tickets.view` |
| | `tickets.close` | إغلاق وإلغاء التذاكر · Close & Cancel Tickets | `tickets.view` |
| | `tickets.assign` | إسناد التذاكر للمشغلين · Assign Tickets to Operators | `tickets.view` |
| | `tickets.create` | فتح التذاكر · Open Tickets | `tickets.view` |
| التنبيهات | `notices.send` | إرسال إشعارات للعيادات · Send Notices to Clinics | `clinics.view` |
| الباقات والاشتراكات | `billing.view` | عرض الباقات والفواتير · View Plans & Billing | — |
| | `billing.managePlans` | إدارة الباقات والاشتراكات · Manage Plans & Subscriptions | `billing.view` |
| المشغلون | `operators.view` | عرض المشغلين · View Operators | — |
| | `operators.manage` | إضافة وتعديل المشغلين · Add & Edit Operators | `operators.view` |
| | `operators.manageRoles` | إدارة الأدوار والصلاحيات · Manage Roles & Permissions | `operators.view` |
| التقارير | `reports.financial` | عرض التقارير المالية · View Financial Reports | — |
| | `reports.security` | عرض تقارير الأمان · View Security Reports | — |
| | `reports.operators` | عرض تقارير أداء المشغلين · View Operator Performance | — |
| | `reports.activity` | عرض تقارير نشاط العيادات · View Clinic Activity | — |
| | `reports.export` | تصدير التقارير · Export Reports | — |
| الأمان | `security.manage` | مراجعة الأحداث الأمنية وحظر عناوين IP · Review Security Events & Block IP Addresses | — (its routes that start from the events log also ask for `reports.security`: spec 025) |
| الإعدادات | `settings.view` | عرض الإعدادات · View Settings | — |
| | `settings.edit` | تعديل إعدادات المنصة · Edit Platform Settings | `settings.view` |

`billing.managePlans` needing `billing.view` is not in the BRD's table (it lists "—" for the
group in chapter 17 because chapter 16 owns it); it is the same view-before-write rule, written
into FR-002 and confirmed when 013 is specified.

## 2. `ops.Role` — new

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier | no | key; the three system roles have fixed ids |
| `Name` | nvarchar(100) | no | "1–100 characters after trimming"; empty for a system role (its name is the console's) |
| `NameKey` | nvarchar(100) | no | the name trimmed and lower-cased; unique among rows with `DeletedAtUtc` null |
| `Description` | nvarchar(500) | yes | "up to 500" |
| `SystemKey` | varchar(20) | yes | `super-admin`, `billing-admin`, `viewer`, or null for a custom role; unique when not null |
| `CreatedAtUtc` | datetimeoffset | no | orders the custom roles, oldest first |
| `CreatedById` | uniqueidentifier | yes | the operator who made it; null for a system role |
| `DeletedAtUtc` | datetimeoffset | yes | set by deleting; a system role's is always null (check) |
| `RowVersion` | rowversion | no | for concurrent edits |

## 3. `ops.RolePermission` — new

| Column | Type | Rule |
|---|---|---|
| `RoleId` | uniqueidentifier | key part; cascade with the role |
| `Code` | varchar(40) | key part; a code of the catalogue (held by the application and a check on save) |

A custom role has at least one row. The super admin's role has all 28, written by the
migration and by any later migration that adds a code.

**System roles' sets** (clarified): `viewer`, eleven — `dashboard.view`, `clinics.view`,
`tickets.view`, `tickets.viewAll`, `billing.view`, `operators.view`, `reports.financial`,
`reports.security`, `reports.operators`, `reports.activity`, `settings.view`. `billing-admin`,
eleven — `dashboard.view`,
`clinics.view`, `clinics.payments`, `clinics.topups`, `billing.view`, `billing.managePlans`,
`tickets.view`, `tickets.reply`, `reports.financial`, `reports.export`, `settings.view`.

## 4. `ops.Operator` — eight columns added

| Column | Type | Null | Rule |
|---|---|---|---|
| `RoleId` | uniqueidentifier | no | references `ops.Role`, restrict; "one role for an operator" |
| `Department` | varchar(20) | yes | one of `support`, `billing`, `success`, `sales`, `management` (check) |
| `JobTitle` | nvarchar(150) | yes | "up to 150" |
| `TwoStepRequired` | bit | no | default 1; always 1 while the platform requires the code of everyone (FR-040) |
| `InvitedById` | uniqueidentifier | yes | the inviting operator; null = the deployment tool («النظام (أول مشغل)» for the earliest-created such account, «النظام» for others) |
| `InvitedByName` | nvarchar(200) | yes | the inviter's name when they invited |
| `ActivatedAtUtc` | datetimeoffset | yes | when the first password was set |
| `SuspendedAtUtc` | datetimeoffset | yes | set while suspended |

Unchanged from spec 010 and used here: `FullName` (1–200), `Email` (unique, lower case, never
changed), `Mobile` (up to 30), `Language` (`ar`/`en`, default `ar`), `State`,
`FailedAttempts`, `SpentCodes`, `LockedUntilUtc`, `LastSignInAtUtc`, `CreatedAtUtc`,
`InvitationOwedSinceUtc`, `RowVersion`.

Index added: `(CreatedAtUtc DESC)` for the list; `(RoleId)` for a role's count of holders.

### States (unchanged values; transitions now reachable from the console)

| From | To | By | Guard |
|---|---|---|---|
| — | `invited` | add operator; the deployment command | the role is one the inviter may grant |
| `invited` | `active` | the holder sets a first password | link live (72 h) |
| `active` | `suspended` | suspend | not one's own account; not the last active super admin |
| `suspended` | `active` | reactivate | clears wrong tries, spent codes, lock |
| `active` + locked | `active` | unlock | locked now |
| `invited` · `active` | same | send a link | none sent to the account in the last minute |

No transition deletes an operator.

## 5. `audit.PlatformEntry` — new (the table spec 007 left for the first operator action)

The columns of `audit.Entry` without `ClinicId`: `Id`, `OccurredAtUtc`, `ActorKind`
(`operator` · `system`), `ActorAccountId`, `ActorName`, `ActionCode`, `Category`,
`TargetKind` (`operator` · `operator-role`), `TargetId`, `TargetName`, `Facts`, `Changes`,
`IpAddress`, `UserAgent`, `TraceId`. Append-only by the same kind of trigger; platform-wide and
listed. Index `(ActorAccountId, OccurredAtUtc DESC)` for an operator's last actions.

Actions written by this spec, with what `Changes` or `Facts` holds:

| Action | Target | Holds |
|---|---|---|
| `operator.invited` | operator | role's name; whether the email was sent now; `by: deployment` for the command |
| `operator.edited` | operator | each changed field, before and after; the note |
| `operator.role-changed` | operator | role's name before and after; the note |
| `operator.suspended` | operator | the reason |
| `operator.reactivated` | operator | — |
| `operator.link-sent` | operator | which kind (`reset` · `invitation`); never the link |
| `operator.unlocked` | operator | — |
| `opsrole.created` | role | name; the codes; the role it was based on |
| `opsrole.edited` | role | name and description before and after; codes added and removed; the note |
| `opsrole.deleted` | role | name; the note |

An edit that also changes the role writes both `operator.edited` (when anything else changed)
and `operator.role-changed`.

## 6. Platform-wide list — three lines added

`ops.Role` — "the roles of the platform's own staff: names and descriptions"; 
`ops.RolePermission` — "which codes of the operators' catalogue a role holds";
`audit.PlatformEntry` — "what operators and the platform did to the platform itself; no
clinic's data".

## 7. Relationships

```text
ops.Role 1 ──< ops.RolePermission          (code ∈ OperatorPermissions)
ops.Role 1 ──< ops.Operator (RoleId, restrict)
ops.Operator ··> ops.Operator (InvitedById, no constraint: a name is kept beside it)
ops.Operator ──> audit.PlatformEntry (as actor or target, by id; no constraint)
```
