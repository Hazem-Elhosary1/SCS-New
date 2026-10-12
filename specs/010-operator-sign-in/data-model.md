# Data Model: Operator Sign-in (spec 010)

Schema `ops`. Every table is platform-wide (research R2) and is added to `PlatformWideTables`
with the reason given here. Times are UTC. Nothing here holds a clinic's or a patient's data.

## 1. `ops.Operator`

One person who runs the platform.

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid (v7) | key |
| `FullName` | text | required, 1–200 |
| `Email` | text | required, at most 254, kept trimmed and in lower case, **unique**, never changed after creation |
| `Mobile` | text | optional, at most 30 |
| `Language` | `ar` · `en` | required, default `ar` |
| `State` | `invited` · `active` · `suspended` | required |
| `PasswordHash` | text | empty while invited; never returned by any read |
| `PasswordChangedAtUtc` | time | optional |
| `FailedAttempts` | number | default 0 |
| `LastFailedAtUtc` | time | optional; wrong tries are forgotten after the attempt window with no new one |
| `SpentCodes` | number | default 0; codes spent in a row (research R7) |
| `LockedUntilUtc` | time | optional |
| `LastSignInAtUtc` | time | optional |
| `CreatedAtUtc` | time | required |
| `RowVersion` | version | checked on every change |

Checks: `State` in its three values; `Language` in its two; `FailedAttempts` and `SpentCodes`
not negative; an `active` or `suspended` account has a password hash.

Platform-wide because: the platform's own staff; names, emails and password hashes; no clinic
data.

The role, job title, department, "invited by" and two-factor flag of the operators' chapter
(§3-9) are added by spec 011. Two-factor is not stored here: it is always on (FR-008).

### States

| From | To | By | In this spec |
|---|---|---|---|
| — | `invited` | the deployment command | yes |
| `invited` | `active` | choosing the first password from the link | yes |
| `active` | `suspended`, and back | another operator | 011; this spec honours the state |

A lock is not a state: it is `LockedUntilUtc` in the future on an active account.

## 2. `ops.SignInChallenge`

A sign-in between the password and the code — and, with the purpose `password-change`, a
change of password between the current password and the code (FR-045a).

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | key; carried in the sign-in cookie |
| `OperatorId` | uuid | required |
| `Purpose` | `sign-in` · `password-change` | required |
| `SessionId` | uuid | for `password-change`: the session that asked; empty for `sign-in` |
| `PendingPasswordHash` | text | for `password-change`: the new password's hash, written to the account only when the code is right; never returned |
| `CodeHash` | bytes | keyed hash of the current code |
| `CodeSentAtUtc` · `CodeExpiresAtUtc` | time | required |
| `AttemptsLeft` | number | starts at 5 |
| `Resends` | number | at most 3 |
| `Remember` | yes/no | what «تذكرني» said |
| `DeviceMarkHash` | bytes | optional; the browser's mark when it had one |
| `Address` · `Browser` | text | where it was asked from |
| `ConsumedAtUtc` | time | set when the code opens a session, or when the challenge is ended |
| `ExpiresAtUtc` | time | the challenge itself ends with its last code |

A code is ended (`ReplacedAtUtc`) only by the code sent in its place — "send again" in the same
step — or by another challenge of the same session (a change of password begun again). A
sign-in begun in another browser does not end it: an account may have a standing sign-in code
in each of two browsers, each bound to its own browser, and whichever completes last holds the
session (spec, edge cases). A completed reset ends them all.

Index on `OperatorId` for what is still open. Platform-wide because: a sign-in in progress of
the platform's own staff; a hash, never the code.

## 3. `ops.Session`

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | key |
| `OperatorId` | uuid | required |
| `StartedAtUtc` | time | required |
| `ExpiresAtUtc` | time | start + the longest life (twelve hours) |
| `LastActivityAtUtc` | time | moved by what the operator does |
| `Remember` | yes/no | whether its cookies outlive the browser |
| `RenewalHash` · `PreviousRenewalHash` | bytes | hashes of the current secret and the one before |
| `RenewedAtUtc` | time | optional |
| `Address` | text | at most 45 |
| `Browser` · `System` | text | read from the request, at most 60 each; "unknown" when not recognised |
| `EndedAtUtc` | time | optional |
| `EndedFor` | `signed-out` · `replaced` · `idle` · `expired` · `password-reset` · `suspended` · `signed-out-everywhere` · `reuse` · `role-changed` | required when ended |

A filtered unique index on `OperatorId` where `EndedAtUtc` is empty: **one open session for an
operator** (FR-030). Index on (`OperatorId`, `StartedAtUtc` descending) for the profile's
list. `role-changed` is written by spec 011; its place is kept (FR-035).

Platform-wide because: sessions of the platform's own staff; hashes, never a session's value.

### What the profile reads from it

The last ten sessions of the account, newest first: browser and system, address, and either
"open, this one" or when it ended. The reason it ended is not shown on the profile.

## 4. `ops.PasswordLink`

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | key; inside the protected token |
| `OperatorId` | uuid | required |
| `Kind` | `reset` · `first` | required |
| `SecretHash` | bytes | hash of the token's secret |
| `CreatedAtUtc` · `ExpiresAtUtc` | time | 30 minutes for `reset`, 72 hours for `first` |
| `UsedAtUtc` | time | set when the password is saved |
| `EndedAtUtc` | time | set when a newer link of the kind replaces it, or a reset completes |

Index on (`OperatorId`, `Kind`, `CreatedAtUtc`). Platform-wide because: a hash and times.

## 5. `ops.KnownBrowser`

| Field | Type | Rule |
|---|---|---|
| `OperatorId` · `MarkHash` | uuid · bytes | key together |
| `FirstSeenAtUtc` · `LastSeenAtUtc` | time | required |

Platform-wide because: a hash of a random mark and two times.

## 6. `ops.SignInRecord` — insert-only

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | key |
| `OccurredAtUtc` | time | the database's clock |
| `Kind` | code | one of the list below |
| `OperatorId` | uuid | optional: unknown for an email that matches nobody |
| `SessionId` | uuid | optional |
| `Address` | text | at most 45 |
| `Browser` · `System` | text | at most 60 each |
| `Facts` | small JSON | by kind; never a secret, never an email |
| `TraceId` | text | to find the request in the logs |

Kinds: `login_succeeded` · `login_failed` · `otp_failed` · `account_locked` ·
`signed_out` · `session_ended` (fact: the reason) · `session_token_reuse` ·
`password_reset_requested` · `password_reset_completed` · `password_changed` ·
`first_password_set` · `new_browser` (fact: whether its email was sent) ·
`operator_invited_by_deployment` and `operator_invite_withdrawn` (fact: the machine user) ·
`operator_command_changed_nothing` (fact: the outcome, then the machine user; never the email).

The application's database login may insert and read; it may not change a row, and may delete
only rows older than the retention period (twelve months, FR-052a). Index on
(`OperatorId`, `OccurredAtUtc`) and on (`Kind`, `OccurredAtUtc`).

Platform-wide because: security events of the platform's own staff; identifiers, addresses and
times, no clinic data.

## 7. Not stored

- A code, a password, a link's token, a session's value: only their hashes.
- The email typed for an account that does not exist: a keyed hash in the existing
  `security.AnonymousSignInAttempt`, as for the clinic's sign-in.
- Two-factor on or off: always on.

## 8. Configuration

Existing keys, shared with the clinic's sign-in: `SCS_AUTH__CODE_KEY`,
`MAX_FAILED_ATTEMPTS` (5), `ATTEMPT_WINDOW_MINUTES` (15), `LOCK_MINUTES` (15), `CODE_MINUTES`
(10), `CODE_ATTEMPTS` (5), `MAX_SPENT_CODES` (3), `RESEND_SECONDS` (60), `MAX_RESENDS` (3),
`SESSION_HOURS` (12), `REQUESTS_PER_MINUTE` (10).

New:

| Key | Default | What |
|---|---|---|
| `SCS_OPS__IDLE_MINUTES` | 30 | no activity for this long ends an operator's session; 15 to 10,080 |
| `SCS_OPS__APP_BASE` | required outside local | the operators console's address, for the links in emails |
| `SCS_OPS__RESET_LINK_MINUTES` | 30 | life of a reset link |
| `SCS_OPS__FIRST_LINK_HOURS` | 72 | life of a first-password link |
| `SCS_OPS__RECORD_MONTHS` | 12 | how long sign-in records are kept |

The validator refuses a start with an idle period outside its range or, outside local, with no
console address.

## 9. Declarations

- `PlatformWideTables`: six lines, `ops.Operator`, `ops.SignInChallenge`, `ops.Session`,
  `ops.PasswordLink`, `ops.KnownBrowser`, `ops.SignInRecord`, each with its reason.
- Error codes (new): `ACCOUNT_DISABLED`, `ACCOUNT_LOCKED` (with `until`),
  `CODE_INVALID`, `SIGN_IN_CODE_NOT_SENT`, `STEP_EXPIRED`, `LINK_INVALID` (with
  `kind`), `VALIDATION_FAILED` (password: `SAME_AS_CURRENT`), `VALIDATION_FAILED` (current: `WRONG`). The single answer, the password
  rule, the request limit and "no session" use the codes the platform already has.
- Jobs: `ops-cleanup`, daily (research R13). It crosses no clinic boundary: its tables have
  none.
- The deployment tool gains `operator invite` and `operator withdraw`.

## 10. Migrations

One, `InitialOperators`, in the module: the schema, the six tables with their checks and
indexes, and the grants of §6. The module is registered and migrated after Identity. No table
of another module changes; the shared pieces that move (research R3) are code, not data.
