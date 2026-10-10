# Contract: handing over an account, and what later specs must know

For specs 015 (clinic creation), 026 (team accounts) and any module that adds endpoints.

## `IAccountHandover` (in `Scs.Identity.Contracts`)

Every operation runs for the established clinic, under isolation: an account of another clinic
is "not found". None has an endpoint in this spec; the caller's permission is the caller's
business (spec 005).

| Operation | Does | Returns |
|---|---|---|
| `CreateWithLinkAsync(email)` | Creates the account `invited` and emails a new-account link (72 hours) | account id, `emailSent` |
| `CreateWithGeneratedPasswordAsync(email)` | Creates it `active` with a generated password, marked "must change", 72 hours; emails "an account was created" | account id, the password — **this once**, `emailSent` |
| `CreateWithTypedPasswordAsync(email, password)` | The same with the caller's password, which must meet the rules | account id, `emailSent` |
| `ResendInvitationAsync(accountId)` | For an `invited` account: a new link, the earlier one stops working | `emailSent` |
| `ReissueAsync(accountId, method, password?)` | For an existing account. `link`: emails a reset link (an invitation if still `invited`); the password keeps working until the link is used. `generated` / `typed`: replaces the password now, marks "must change", ends the account's sessions, cancels its links, lifts a lock, emails "your access was reset" | the generated password once (if any), `emailSent` |

Rules the caller can rely on:

- Refusals use field codes: `email` — `REQUIRED`, `TOO_LONG`, `INVALID_FORMAT`, `TAKEN`;
  `password` — `REQUIRED`, `TOO_SHORT`, `TOO_LONG`, `NO_UPPER_LOWER`, `NO_DIGIT`, `NO_SYMBOL`.
- A disabled account cannot be re-issued (`ACCOUNT_DISABLED`).
- `emailSent: false` means the account is as the operation left it and the email did not go:
  show it, and offer to send again. For a link, nothing usable was left behind.
- No email ever contains a password.
- The generated password cannot be read again by anyone. Show it once; do not store it, log it
  or put it in an audit entry.
- `access-reissued` is recorded with the signed-in person as actor.

`IStaffAccounts` (spec 003) is unchanged. Disabling an account now also cancels its live links.

## What every module gets for free

A session whose account holds a temporary password is refused on **every** endpoint that needs a
session, with `403 PASSWORD_CHANGE_REQUIRED` — unless the endpoint is marked
`AllowsTemporaryPassword()`. Do not mark your endpoints. A test enumerates all mapped endpoints
and fails if any other than the two of this spec carries the mark.

## For developers only

`POST /v1/dev/staff-invitations` `{ "email": … }` sends the invitation of an account that was
never activated. It is mapped only when the environment is `local` and always answers 204.
Specs 015 and 026 replace it with real screens.

## Error codes added (27 in total)

| Code | Status | Meaning |
|---|---|---|
| `LINK_INVALID` | 410 | The link cannot be used; `kind` says which screen |
| `PASSWORD_CHANGE_REQUIRED` | 403 | The session holds a temporary password; only changing it is allowed |
| `PASSWORD_CHANGE_NOT_REQUIRED` | 409 | The change endpoint was called for an account that is not marked |

Field code added: `SAME_AS_CURRENT` on `password`.
