# Data Model: Clinic Onboarding

**Feature**: [spec.md](spec.md) · **Research**: [research.md](research.md)

There are four migrations, one per module that changes. Each migration only adds (expand only,
`docs/database-changes.md`):

- **Tenancy**: `ClinicProfile`.
- **Identity**: `OwnerDetails`.
- **Subscriptions**: `Trial`.
- **ClinicData** (new schema): `Branches`. Its table is clinic-owned, so the row level security
  policy is created in the same migration.

## 1. `tenancy.Clinic` grows (Tenancy, clinic-owned, written only on the crossing login)

| Column | Type | Null | Rule |
|---|---|---|---|
| `NameAr` | nvarchar(200) | no | exists; required |
| `NameEn` | nvarchar(200) | no | exists; the Arabic when left empty at creation (clarified) |
| `State` | nvarchar(20) | no | exists; new values `setting-up`, `discarded`; check `CK_Clinic_State IN ('setting-up','active','suspended','cancelled','discarded')` |
| `Specialty` | varchar(40) | yes | exists; new check `CK_Clinic_Specialty`: NULL or one of the fifteen keys (`Specialties.All`); required at creation |
| `Address` | nvarchar(300) | yes | **new**; required at creation; trimmed; ≤ 300 |
| `TaxNumber` | char(11) | yes | **new**; check `CK_Clinic_TaxNumber`: `[TaxNumber] LIKE '[0-9][0-9][0-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9]'`. Stored from 9 digits typed with or without dashes. A stored value that predates the rule (FR-022) cannot exist today: the column is new and checked |
| `DefaultLanguage` | varchar(2) | no | **new**; default `'ar'`; check `IN ('ar','en')` |
| `AccountManagerId` | uniqueidentifier | yes | **new**; an operator id; no foreign key (other module) |
| `CreatedBy` | nvarchar(100) | yes | **new**; the operator id that added it (`tool:seed` for the seed) |
| `RowVersion` | rowversion | no | **new**; optimistic concurrency for the edit and the manager |

- **Seeded clinics**: the migration fills nothing. `noor` and `shifa` keep NULL address, tax
  number and specialty, `DefaultLanguage` `ar` and NULL `CreatedBy`. The detail shows «—» or
  «غير مسجّل» for what they lack.
- **The time zone** is not stored. It is always «القاهرة (GMT+2)» (SH-001), and the edit form
  shows it without letting it be chosen.

### Domain (`Scs.Tenancy/Domain/Clinic.cs`)

- `Create(id, ClinicRegistration, now)` checks:
  - names: `REQUIRED`, `TOO_LONG`;
  - address: `REQUIRED`, `TOO_LONG`;
  - specialty: `REQUIRED`, `INVALID`;
  - tax number: `TAX_NUMBER`.
  It starts in `setting-up`.
- `UpdateProfile(ClinicProfileChange)` returns the `AuditChange` list of the fields that changed.
  - Tax number: "keep" when the field was not sent; "remove" when it was sent empty. A stored value
    sent back unchanged is accepted even if it predates the rule.
  - Specialty: may be set to NULL («غير مسجّل») in editing only.
- `AssignAccountManager(Guid?)` returns the change, or nothing when the manager is the same.
- `Activate()` moves `setting-up` to `active`; any other state is refused.
- `Discard()` moves `setting-up` to `discarded`.

### `ClinicDirectory` (exists, platform-wide)

A derived slug is a new `ClinicDirectoryEntry`, current. The rules are `ShortName.Derive`
(research R9) and the existing `ShortName` checks. A discarded clinic's entry is retired, so its
slug is never reused.

## 2. `identity.StaffAccount` grows (Identity, clinic-owned)

| Column | Type | Null | Rule |
|---|---|---|---|
| `Email` | nvarchar(254) | no | exists; unique platform-wide (`UX_StaffAccount_Email`); **now changeable** through `IClinicOwners.UpdateAsync` |
| `FullName` | nvarchar(200) | yes | **new**; required for the owner (FR-025); 026 makes it every account's |
| `Mobile` | varchar(20) | yes | **new**; the code's rule is an optional `+`, then 8 to 15 digits, after removing spaces and dashes (clarified: any country). The database check `CK_StaffAccount_Mobile` (SQL Server has no regular expressions): `LEN([Mobile]) BETWEEN 8 AND 16 AND [Mobile] NOT LIKE '%[^0-9+]%' AND CHARINDEX('+', [Mobile], 2) = 0` |
| `LastSignInAtUtc` | datetimeoffset | yes | **new**; set when a session opens (`SessionService.Open`), never cleared (BRD §3-2). The migration fills it for existing accounts from their latest `identity.Session.CreatedAtUtc` (the migrator crosses clinics) |

`SessionEndings` gains `EmailChanged = "email-changed"`. This is a value of the reason column, not
a schema change.

### The owner and the invitation (read model, not stored)

- **The owner** is the clinic's account holding the ready role `owner`, the earliest
  `CreatedAtUtc` first.
- **The invitation** of an `invited` owner comes from `identity.PasswordLink` rows of kind
  `new-account`:

| State | When | Shown |
|---|---|---|
| `live` | a link with no use, no replacement and `ExpiresAtUtc > now` | «تنتهي صلاحية الدعوة في <date> — <relative>.» |
| `lapsed` | no live link; the latest one expired | «انتهت صلاحيتها في <date>. أرسل دعوة جديدة صالحة 72 ساعة.» |
| `none` | no new-account link at all | «لا توجد دعوة سارية. أرسل واحدة.» |

  An `active` owner has no invitation state. The row shows the address and its copy button only.
- **The cooldown** of a resend: refused when the latest `new-account` link was created less than
  60 s ago.

## 3. `subscriptions.ClinicPlan` grows (Subscriptions, clinic-owned)

| Column | Type | Null | Rule |
|---|---|---|---|
| `PlanId` | uniqueidentifier | yes | exists |
| `SetAtUtc` | datetimeoffset | no | exists; the clinic's join date to its plan |
| `TrialEndsAtUtc` | datetimeoffset | yes | **new**; `now + TrialDays` at creation; never moved by a change of the setting |

### Status (derived, `ClinicStatus.Of`)

| Stored clinic state | Trial end | Status | Arabic | English | Tone |
|---|---|---|---|---|---|
| `active` | in the future | `trial` | «تجربة» | Trial | info (blue) |
| `active` | past, or none | `active` | «نشطة» | Active | success (green) |
| `suspended` / `cancelled` | — | 016 | — | — | — |

The list's cards count from this status:

- **total**: every visible clinic;
- **«العيادات النشطة»**: status `active`;
- **«عيادات التجربة»**: status `trial`;
- **«تحتاج متابعة»**: 0 until 016.

So total = active + trial + 0 + 0 + 0 (SC-004).

## 4. `clinicdata.Branch` (new module ClinicData, clinic-owned)

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier | no | primary key, v7 |
| `ClinicId` | uniqueidentifier | no | FK `tenancy.Clinic(Id)`; leads every index |
| `NameAr` | nvarchar(200) | no | the clinic's Arabic name at creation |
| `NameEn` | nvarchar(200) | no | the clinic's English name at creation |
| `Address` | nvarchar(300) | no | the clinic's address at creation |
| `IsMain` | bit | no | — |
| `CreatedAtUtc` | datetimeoffset | no | — |

- Index `UX_Branch_OneMain` unique on `(ClinicId)` `WHERE [IsMain] = 1` (FR-026, §3-4).
- `CreateClinicIsolation("clinicdata", "Branch")` in the same migration. The isolation sample is
  `BranchSample`.
- The schema is granted to `scs_app` like the other module schemas, and the migrator login owns
  its history table `clinicdata.__EFMigrationsHistory`.

## 5. Configuration

| Setting | Environment name | Default | Rule |
|---|---|---|---|
| `Scs:Subscriptions:TrialDays` | `SCS_SUBSCRIPTIONS__TRIAL_DAYS` | 14 | 1–90, checked at start |

## 6. Audit actions (appended to `AuditActions.cs` and `audit-actions.lock`)

| Code | Category | Facts | Changes |
|---|---|---|---|
| `clinic.created` | changes | `slug`, `plan`, `specialty` | names, address, tax number, specialty, language, plan, trial end, owner name/email/mobile (each from empty) |
| `clinic.updated` | changes | `note` (≤ 500) | names, address, tax number, specialty, language |
| `clinic.owner-updated` | users | `note` (≤ 500) | owner name, email, mobile |
| `clinic.account-manager-assigned` | changes | — | `account-manager`: the operator's name before and after («غير معيّن» as empty) |
| `clinic.invitation-resent` | users | — | — |

- **Where each goes**: every entry goes to the clinic's trail and to the platform's trail, with
  target `clinic`. The actor is the operator (research R5).
- **What it holds**: the email appears only as a change value, never as a fact (`NeverKeys`).
- **The plan's own `subscription.plan-set`**: 014 still writes it, now with the fact `trial-ends`.

## 7. Validation summary (server; the console checks the same before sending)

| Field | Rule | Code |
|---|---|---|
| Arabic name | required, ≤ 200 | `REQUIRED`, `TOO_LONG` |
| English name | optional, ≤ 200 | `TOO_LONG` |
| Contact (owner name) | required, ≤ 200 | `REQUIRED`, `TOO_LONG` |
| Email | required, format, ≤ 254, no account anywhere | `REQUIRED`, `INVALID`, `TAKEN` |
| Mobile | required, `+`? and 8–15 digits | `REQUIRED`, `MOBILE` |
| Address | required, ≤ 300 | `REQUIRED`, `TOO_LONG` |
| Plan | required at creation, assignable | `REQUIRED`; `PLAN_DISABLED` (409) |
| Tax number | optional, 9 digits | `TAX_NUMBER` |
| Specialty | required at creation, one of 15; NULL allowed in editing | `REQUIRED`, `INVALID` |
| Default language | `ar` or `en` (edit only) | `INVALID` |
| Note | optional, ≤ 500 (edit only) | `TOO_LONG` |
| Version | required in an edit or an assign | `REQUIRED`; `CLINIC_CHANGED_ELSEWHERE` (409) |

## 8. State transitions

```text
              add: prechecks pass
   (nothing) ─────────────────────► setting-up ──activate──► active ── (016: suspended, cancelled …)
                                         │
                                         └──fault + undo──► discarded   (never visible)
```

The owner account:

```text
invited (link live) ──72 h──► invited (lapsed) ──resend──► invited (link live)
       │                                                    │
       └──────────── sets a password ───────────────────────┴──► active
```

The account starts `invited` with no link. The first link is sent after activation; if that email
fails, the invitation stays `none`.
