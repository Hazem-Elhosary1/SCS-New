# Contract: what other modules may rely on, and must do

Spec 003. For the developers of specs 004 onwards. Complements
`specs/001-platform-foundation/contracts/conventions.md` and
`specs/002-clinic-tenancy-isolation/contracts/isolation.md`.

## 1. An endpoint needs a signed-in person unless it says otherwise

Nothing changes for a module: the default of spec 001 stays "refuse". From this spec on, the
refusal is lifted by a live staff session. An endpoint reachable without signing in is marked
`.AllowAnonymous()` and is listed in its spec.

With a session, the clinic context is already established (source `account`) before the
endpoint runs. A module never establishes it, and never reads the clinic from the request.

## 2. Who is asking

Inject `ICurrentStaff` (`Scs.Identity.Contracts`):

| Member | Meaning |
|---|---|
| `AccountId` | The signed-in account |
| `SessionId` | This browser's session |
| `ClinicId` | The same value as the clinic context |

It throws `UNAUTHENTICATED` when used on an anonymous endpoint. Roles and permissions are not
here; they arrive with spec 005.

## 3. Operations later specs call

`IStaffAccounts`:

| Operation | For | Behaviour |
|---|---|---|
| `CreateAsync(email, state, password?)` | 004, 015, 026 | Refuses a taken email with field code `TAKEN`, whichever clinic holds it. `active` needs a password that meets the password rules; `invited` takes none |
| `SetStateAsync(accountId, state)` | 026, 004 | Disabling ends the account's sessions in the same transaction (SH-045) |
| `UnlockAsync(accountId)` | 026 | Clears the lock and the count (SH-049) |
| `ClearLockAsync(accountId)` | 004 | The same, named for the password-reset path |

`IStaffSessions`:

| Operation | For | Behaviour |
|---|---|---|
| `EndForAccountAsync(accountId, reason)` | 004, 005, 026 | Ends every live session of the account. Reasons: `ended-for-account`, `password-reset`, `role-changed` |
| `EndForClinicAsync(exceptSessionId?, reason)` | 085, 016 | Ends every live session of the established clinic, except one if given. Reasons: `ended-for-clinic`, `clinic-suspended`. **Spec 016 must call it when it suspends or cancels a clinic**; without that call the clinic's sessions stop only at their next renewal, up to five minutes later |

All run for the established clinic, under isolation: an account of another clinic is "not
found". None has an endpoint in this spec; the spec that owns the screen adds it and its
permission.

## 4. Ending is immediate

A session ended by any of the operations above is refused on its very next request
(`SESSION_ENDED`). A module does not cache "is signed in" anywhere.

## 5. Codes

Added to the catalogue of `conventions.md` §3:

| Code | Status | Meaning |
|---|---|---|
| `INVALID_CREDENTIALS` | 401 | The single answer before a correct password |
| `CODE_INVALID` | 401 | The single answer for a wrong, expired, spent, used or replaced code |
| `ACCESS_EXPIRED` | 401 | Renew the session, then repeat |
| `SESSION_ENDED` | 401 | The session is over; sign in again |
| `ACCOUNT_LOCKED` | 403 | After a correct password; carries `lockedUntil` |
| `ACCOUNT_DISABLED` | 403 | After a correct password |
| `CLINIC_SUSPENDED` | 403 | After a correct password, and at renewal |
| `NETWORK_BLOCKED` | 403 | Before anything about the account |
| `RESEND_TOO_EARLY` | 409 | Carries `resendAvailableInSeconds` |
| `RESEND_LIMIT_REACHED` | 409 | Three codes were already resent in this sign-in; the last one is still valid |
| `SIGN_IN_CODE_NOT_SENT` | 503 | The password was correct; the email failed |
| `SIGN_IN_CODE_NOT_RESENT` | 503 | The earlier code is still valid |

## 6. Requests that change something for a signed-in person

Carry `X-Scs-Request: 1`. The shared HTTP interceptor adds it on the staff and operators sites;
a module does nothing. Without it, or with an `Origin` outside the configured list, the answer
is 403 `FORBIDDEN`.

The rule covers every endpoint that needs a signed-in person, and the sign-in, renewal and
sign-out endpoints. An endpoint marked `.AllowAnonymous()` that never touches a session is not
checked: there is no session for another site to misuse.

The single refusal `INVALID_CREDENTIALS` never arrives sooner than 300 ms after the request, so
its timing says nothing about what was wrong.

## 7. Email

`IEmailSender.SendAsync(to, subject, textBody, htmlBody)` in the shared project. It throws when
the message could not be handed to the mail server within its time limit. Until spec 008, the
only caller is sign-in. The recipient address is never logged.

## 8. What is never logged

Passwords, codes, cookie values, renewal secrets, email addresses. Log `accountId`, `sessionId`
and `clinicId`. The leak test of spec 001 sends each of the others through every sign-in
endpoint.

## 9. Tests

`ApiFactory` gains `SignInAsAsync(clinicId, accountId?)`, which gives an `HttpClient` carrying a
real session, so a module's tests exercise its endpoints as a signed-in person of a clinic.
