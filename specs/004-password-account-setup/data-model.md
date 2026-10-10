# Data Model: Passwords and Account Activation

Additions to schema `identity` (module `Identity`, spec 003). Conventions as in spec 003's data
model: clinic-owned tables carry `ClinicId`, the isolation policy of spec 002 in the migration
that creates them, and an isolation sample; times are UTC `datetimeoffset`.

## identity.StaffAccount — two new columns

| Column | Type | Null | Rule |
|---|---|---|---|
| MustChangePassword | bit | no | Default 0. Set when a password is given by someone else (FR-031, FR-044); cleared when the owner sets their own |
| TemporaryPasswordExpiresAtUtc | datetimeoffset | yes | Set with the mark: now + 72 hours. After it, the password is refused at sign-in as a wrong one. Null when not marked |

Check constraints: `MustChangePassword = 0` requires `TemporaryPasswordExpiresAtUtc IS NULL`, and
`MustChangePassword = 1` requires `State <> 'invited'` (an account holding a temporary password
can still be disabled).

State transitions added:

| From | To | By |
|---|---|---|
| — | invited, with a new-account link | `IAccountHandover.CreateWithLinkAsync` |
| — | active, must change | `CreateWithGeneratedPasswordAsync`, `CreateWithTypedPasswordAsync` |
| invited | active | Using a new-account link; re-issuing by password |
| active, must change | active | Changing the temporary password; completing a reset |
| active | active, must change | Re-issuing by password |
| locked | active | Completing a reset; re-issuing by password |

## identity.PasswordLink — clinic-owned (new)

One emailed link.

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key; random (version 4) |
| ClinicId | uniqueidentifier | no | |
| AccountId | uniqueidentifier | no | FK to StaffAccount |
| Kind | varchar(12) | no | `reset` or `new-account`. Check constraint |
| SecretHash | binary(32) | no | SHA-256 of the link's secret. `[Secret]` |
| CreatedAtUtc | datetimeoffset | no | When it was sent |
| ExpiresAtUtc | datetimeoffset | no | Created + 30 minutes (`reset`) or + 72 hours (`new-account`) |
| UsedAtUtc | datetimeoffset | yes | Set by the one successful use |
| ReplacedAtUtc | datetimeoffset | yes | Set when a newer link is issued, the password is set another way, or the account is disabled |

A link is **live** when not used, not replaced and unexpired. At most one live link per account:
a filtered unique index on `AccountId` where `UsedAtUtc` and `ReplacedAtUtc` are null. Index on
`(AccountId, Kind, CreatedAtUtc)` for the two caps. A link whose email could not be handed over
is deleted, so it counts toward nothing. Rows older than 21 days — longer than the longest life a link can be
configured to have — are removed by the daily job of spec 003.

## identity.AccountEmailRequest — clinic-owned (new)

Mail waiting to be sent in the background (research R6, R7).

| Column | Type | Null | Rule |
|---|---|---|---|
| Id | uniqueidentifier | no | Key |
| ClinicId | uniqueidentifier | no | |
| AccountId | uniqueidentifier | no | FK to StaffAccount |
| Kind | varchar(20) | no | `reset-link` or `password-changed`. Check constraint |
| RequestedAtUtc | datetimeoffset | no | |
| HandledAtUtc | datetimeoffset | yes | Null = waiting |
| Outcome | varchar(16) | yes | `sent`, `skipped-limit`, `skipped-state`, `gave-up` |

No email address, no secret. Index on `ClinicId` filtered to waiting rows. Handled rows older
than one day are removed by the daily job.

## identity.SignInEvent — one new column, five new kinds

| Column | Type | Null | Rule |
|---|---|---|---|
| ActorAccountId | uniqueidentifier | yes | Who did it, when it was not the account's owner (`access-reissued`) |

| Kind | Category | Detail |
|---|---|---|
| `reset-requested` | activity | — |
| `reset-completed` | security | — |
| `invitation-accepted` | activity | — |
| `temp-password-changed` | activity | — |
| `access-reissued` | security | `link`, `generated`, `typed` |

Still append-only, still no email and no free text.

## Not stored

- A link's token or secret, a password, in readable form — never. Not in the job server's tables
  either: jobs take a clinic id only.
- Earlier passwords: none are kept.
- The clinic's name: read through `IClinicProfile` when a new-account link is inspected.

## Database crossing

None new. Asking for a link uses `identity.usp_FindAccountForSignIn` (spec 003). Using a link
needs none: the token names the clinic (research R1). `DeclaredDatabaseCrossings` is unchanged.

## Settings (configuration until spec 024)

| Setting | Variable | Default | Limits |
|---|---|---|---|
| Reset link life, minutes | `SCS_AUTH__RESET_LINK_MINUTES` | 30 | 5 to 1440 |
| New-account link life, hours | `SCS_AUTH__INVITE_LINK_HOURS` | 72 | 1 to 336 |
| Temporary password life, hours | `SCS_AUTH__TEMP_PASSWORD_HOURS` | 72 | 1 to 336 |
| Seconds between reset emails to one account | `SCS_AUTH__RESET_EMAIL_GAP_SECONDS` | 60 | 15 to 3600 |
| Reset emails to one account per hour | `SCS_AUTH__RESET_EMAILS_PER_HOUR` | 5 | 1 to 60 |
| Generated password length | `SCS_AUTH__GENERATED_PASSWORD_LENGTH` | 14 | 12 to 64 |
| The staff app's address, for links in emails | `SCS_AUTH__STAFF_APP_URL` | `http://localhost:4201` locally | Absolute `http(s)` address, no path. Required |

Validated at start-up like the rest; `https` is required outside local and test.

## Seed

One more made-up account, and one fact changed:

| Clinic | Email | State | Purpose |
|---|---|---|---|
| noor | `temp@noor.example` | active, must change | The temporary-password screen. Its expiry is pushed 72 hours ahead each time the seed runs |
| shifa | `new@shifa.example` | invited (as before) | Activation: `POST /v1/dev/staff-invitations` with its email sends a new-account link to the local inbox. The address exists only when the environment is `local` |
