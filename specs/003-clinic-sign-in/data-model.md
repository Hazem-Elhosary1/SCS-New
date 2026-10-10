# Data Model: Clinic Staff Sign-In and Sessions

Schema `identity` (module `Identity`) unless stated. Every table marked **clinic-owned** carries
`ClinicId uniqueidentifier NOT NULL`, the isolation policy of spec 002 in the migration that
creates it, and an isolation sample in the test project. Times are UTC `datetimeoffset`.
Identifiers are version-7 GUIDs unless stated.

## identity.StaffAccount — clinic-owned

A member of a clinic's team, as far as sign-in is concerned. Name, role and branch arrive with
specs 005 and 026.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key |
| ClinicId | uniqueidentifier | no | Stamped from the clinic context; never changes |
| Email | nvarchar(254) | no | Lower case, trimmed. **Unique over the whole table** (FR-003). Never changes (FR-002). `[PersonalData]` |
| PasswordHash | nvarchar(200) | yes | `v1.<iterations>.<salt>.<hash>` (research R5). Null while `invited`. `[Secret]` |
| State | varchar(16) | no | `invited`, `active`, `disabled`. Check constraint |
| FailedAttempts | tinyint | no | 0 to 4; default 0 |
| LastFailedAtUtc | datetimeoffset | yes | When the last wrong password was counted. A wrong attempt more than 15 minutes after it counts as the first (FR-015) |
| SpentCodes | tinyint | no | Codes spent in a row; default 0; zero after a successful sign-in; the third locks (FR-032) |
| LockedUntilUtc | datetimeoffset | yes | Null = not locked |
| CreatedAtUtc | datetimeoffset | no | |
| RowVersion | rowversion | no | Concurrency |

Rules:

- `State = 'active'` requires `PasswordHash` (check constraint). `invited` has none.
- Locked is not a state: an account is locked while `LockedUntilUtc > now`.
- Email validation at creation: required; at most 254 characters; one `@` with text on both
  sides and a dot in the domain; field codes `REQUIRED`, `TOO_LONG`, `INVALID_FORMAT`, `TAKEN`.
- Password rules (10 characters, upper, lower, digit, symbol — B-R16) are enforced where a
  password is set; that is spec 004. `IStaffAccounts.CreateAsync` with a password applies them
  here so seeds and tests cannot create weaker ones.

State transitions delivered here:

| From | To | By |
|---|---|---|
| — | invited / active | `IStaffAccounts.CreateAsync` |
| active | disabled | `IStaffAccounts.SetStateAsync` — ends the account's sessions |
| disabled | active | `IStaffAccounts.SetStateAsync` |
| active | locked (15 min) | The fifth consecutive wrong password, or the third code spent in a row — ends the account's sessions |
| locked | active | Time passing; `UnlockAsync`; `ClearLockAsync` (password reset, spec 004) |

`invited → active` (activation) is spec 004.

## identity.SignInChallenge — clinic-owned

One six-digit code issued after a correct password.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key; random (version 4) |
| ClinicId | uniqueidentifier | no | |
| AccountId | uniqueidentifier | no | FK to StaffAccount |
| CodeHash | binary(32) | no | HMAC-SHA-256 of the code, keyed by the server secret and `Id`. `[Secret]` |
| Attempts | tinyint | no | 0 to 5 |
| CreatedAtUtc | datetimeoffset | no | |
| ExpiresAtUtc | datetimeoffset | no | Created + 10 minutes. A resend does **not** extend the sign-in beyond its own 10 minutes |
| ResendAvailableAtUtc | datetimeoffset | no | Created + the resend countdown |
| ResendsUsed | tinyint | no | 0 to 3. Carried from the code it replaces, plus one. A code issued by a new correct password starts at 0 |
| ConsumedAtUtc | datetimeoffset | yes | Set by the one successful check |
| ReplacedAtUtc | datetimeoffset | yes | Set when a newer code is issued for the account |
| IpAddress | varchar(45) | yes | |

A code is **standing** when not consumed, not replaced, unexpired and `Attempts < 5`. At most one
standing code per account: a filtered unique index on `AccountId` where `ConsumedAtUtc` and
`ReplacedAtUtc` are null, and issuing replaces the earlier one in the same transaction.

Rows older than one day are removed by a daily per-clinic job.

## identity.Session — clinic-owned

One signed-in browser.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key; random (version 4) |
| ClinicId | uniqueidentifier | no | |
| AccountId | uniqueidentifier | no | FK to StaffAccount |
| RenewalHash | binary(32) | no | SHA-256 of the current renewal secret. `[Secret]` |
| PreviousRenewalHash | binary(32) | yes | The one before. `[Secret]` |
| PreviousValidUntilUtc | datetimeoffset | yes | Replaced-at + grace (30 seconds) |
| CreatedAtUtc | datetimeoffset | no | |
| LastUsedAtUtc | datetimeoffset | no | Updated at renewal, not on every request |
| ExpiresAtUtc | datetimeoffset | no | Created + maximum life (12 hours) |
| EndedAtUtc | datetimeoffset | yes | Null = alive |
| EndReason | varchar(24) | yes | `signed-out`, `expired`, `locked`, `disabled`, `ended-for-account`, `ended-for-clinic`, `clinic-suspended`, `renewal-reused`, `password-reset`, `role-changed` |
| IpAddress | varchar(45) | yes | Where it was opened from. `[PersonalData]` |
| UserAgent | nvarchar(300) | yes | Truncated. `[PersonalData]` |

A session is **alive** when `EndedAtUtc` is null and `ExpiresAtUtc > now`. Index on
`(ClinicId, AccountId)` filtered to alive rows. Sessions ended more than 90 days ago are removed
by the same daily job.

## identity.SignInEvent — clinic-owned, append-only

What happened to an account at sign-in. Read later by specs 025, 059 and 007.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key |
| ClinicId | uniqueidentifier | no | |
| AccountId | uniqueidentifier | no | |
| Kind | varchar(24) | no | `signed-in`, `wrong-password`, `account-locked`, `wrong-code`, `code-spent`, `renewal-reused`, `session-ended` |
| Category | varchar(8) | no | `activity` (`signed-in`) or `security` (the rest) |
| Detail | varchar(24) | yes | For `session-ended`: the end reason |
| SessionId | uniqueidentifier | yes | |
| IpAddress | varchar(45) | yes | `[PersonalData]` |
| UserAgent | nvarchar(300) | yes | `[PersonalData]` |
| TraceId | char(32) | no | The request reference |
| OccurredAtUtc | datetimeoffset | no | |

No email, no name, no free text. A trigger refuses `UPDATE` and `DELETE` unless the caller is in
the cross-clinic role. Retention is decided with spec 007.

## security.AnonymousSignInAttempt — platform-wide

Attempts on an email that matches no account. New schema `security`, created by the logins
script: the application login may `INSERT`; it may not read, change or delete. Declared in
`PlatformWideTables`.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | bigint identity | no | Key |
| EmailKey | binary(32) | no | HMAC-SHA-256 of the lower-cased text typed, keyed by the server secret. Never the text |
| IpAddress | varchar(45) | yes | |
| UserAgent | nvarchar(300) | yes | |
| TraceId | char(32) | no | |
| OccurredAtUtc | datetimeoffset | no | |

## platform.DataProtectionKey — platform-wide

The key ring that protects the three cookies (research R9). Shape dictated by the framework:
`Id int identity`, `FriendlyName nvarchar(max)`, `Xml nvarchar(max)`. Declared in
`PlatformWideTables`. The application login reads and inserts; `[Secret]` — never logged.

## Database crossing

`identity.usp_FindAccountForSignIn(@Email nvarchar(254))` returns `(AccountId, ClinicId)` for at
most one account, running as the login-less user `scs_signin_lookup` (member of the cross-clinic
role; `SELECT` on `Id`, `ClinicId`, `Email` of `StaffAccount` only). `scs_app` has `EXECUTE` on
it. Declared in `DeclaredDatabaseCrossings`; the guardrail fails the build for any other module
that runs as another user (research R4).

## Not stored

- The code, the password and the renewal secret in readable form — never.
- The pending sign-in between the two steps: it is in the challenge row and the protected
  cookie; there is no server-side "half session".
- The clinic's state: read from `Tenancy` through `IClinicProfile`.

## Settings (configuration until spec 024)

| Setting | Variable | Default | Limits |
|---|---|---|---|
| Wrong attempts before the lock | `SCS_AUTH__MAX_FAILED_ATTEMPTS` | 5 | 3 to 10 |
| Lock length, minutes | `SCS_AUTH__LOCK_MINUTES` | 15 | 1 to 1440 |
| Code life, minutes | `SCS_AUTH__CODE_MINUTES` | 10 | 1 to 30 |
| Code attempts | `SCS_AUTH__CODE_ATTEMPTS` | 5 | 3 to 10 |
| Resend countdown, seconds | `SCS_AUTH__RESEND_SECONDS` | 60 | 15 to 600 |
| Resends per sign-in | `SCS_AUTH__MAX_RESENDS` | 3 | 0 to 10 |
| Spent codes in a row before the lock | `SCS_AUTH__MAX_SPENT_CODES` | 3 | 1 to 10 |
| Renewals per minute per address | `SCS_AUTH__RENEWALS_PER_MINUTE` | 120 | 30 to 1200 |
| Path the API is served under | `SCS_AUTH__COOKIE_PATH_BASE` | empty locally; `/api` when deployed | The renewal cookie's path is this plus `/v1/auth` |
| Wrong attempts are forgotten after, minutes | `SCS_AUTH__ATTEMPT_WINDOW_MINUTES` | 15 | 1 to 1440 |
| Session maximum life, hours | `SCS_AUTH__SESSION_HOURS` | 12 | 1 to 168 |
| Access cookie life, minutes | `SCS_AUTH__ACCESS_MINUTES` | 5 | 1 to 30 |
| Grace for a replaced renewal secret, seconds | `SCS_AUTH__RENEWAL_GRACE_SECONDS` | 30 | 5 to 120 |
| Sign-in requests per minute per address | `SCS_AUTH__REQUESTS_PER_MINUTE` | 10 | 5 to 120 |
| The email-code step is on | `SCS_AUTH__EMAIL_CODE_ENABLED` | true | Platform-wide switch only (FR-022) |
| Secret for code and email hashes | `SCS_AUTH__CODE_KEY` | — (required) | At least 32 bytes. `[Secret]` |
| Mail server | `SCS_MAIL__HOST`, `__PORT`, `__USER`, `__PASSWORD`, `__FROM`, `__USE_TLS` | Mailpit locally | `[Secret]` for the password |
| How long a request waits for the mail server, seconds | `SCS_MAIL__TIMEOUT_SECONDS` | 10 | 1 to 60 |
| Support hours and channels | `SCS_SUPPORT__HOURS_AR`, `__HOURS_EN`, `__HOTLINE`, `__WHATSAPP`, `__EMAIL` | The BRD's sample values | Shown before sign-in: nothing private |

All are validated at start-up, as in spec 001; a missing or out-of-range value stops the server
naming the variable.

## Seed

For each of the two made-up clinics of spec 002, three accounts with made-up people:

| Clinic | Email | State | Purpose |
|---|---|---|---|
| noor | `owner@noor.example` | active | Ordinary sign-in |
| noor | `reception@noor.example` | active | A second person; two sessions in one clinic |
| noor | `former@noor.example` | disabled | The disabled banner |
| shifa | `owner@shifa.example` | active | Isolation between two signed-in clinics |
| shifa | `doctor@shifa.example` | active | |
| shifa | `new@shifa.example` | invited | The single answer for a not-yet-activated account |

One shared password for the active and disabled ones, set in `.env.example` as
`SCS_SEED__STAFF_PASSWORD` and never used outside local and test.
