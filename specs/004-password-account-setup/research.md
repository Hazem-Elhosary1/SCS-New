# Research: Passwords and Account Activation

Decisions taken for the plan, each with why and what was set aside. Everything builds on spec
003 (`Identity` module, sessions, mail, shared components); only what is new is decided here.

## R1 — How a link finds its account before anyone is signed in

**Decision**: the link carries one opaque token, made with the framework's data protection
(purpose `Scs.PasswordLink.v1`, the key ring spec 003 already keeps in the database; the class
lives in the shared project beside `SessionCookies`, the only other place that may create a
protector). Inside:
the link's kind, the clinic's id, the link's id and a 32-byte random secret. The server opens the
token, establishes the clinic from it, and reads the link row under ordinary isolation. **No new
database crossing.**

**Rationale**: before sign-in there is no clinic, and without one the link table is invisible
(spec 002). The token is authenticated, so the clinic id in it cannot be forged; a token that
does not open is answered like a dead reset link (FR-023). The kind is inside the token, so the
server can say which "no longer works" screen to show even after the row has been cleaned up.

**Alternatives considered**: a second stored procedure running as another user (a new crossing
to declare and guard, for something a token solves); a plain `clinicId.linkId.secret` token (the
clinic context would be set from input nobody vouched for); a link table outside isolation
(personal linkage where no clinic protects it).

## R2 — What is stored for a link

**Decision**: the row stores `SHA-256(secret)` only. The secret is random, 256 bits, so a plain
hash is enough; it is compared in constant time. The token itself is never stored and never
logged.

**Rationale**: FR-008. Even someone holding the key ring cannot use a link without the secret,
and someone reading the table cannot rebuild a token.

**Alternatives considered**: relying on the protected token alone (a leaked key ring would then
mint links); a keyed hash (adds nothing for a 256-bit random value).

## R3 — Where the secret travels

**Decision**: the email's address is `<staff app>/set-password#t=<token>` — the token is in the
fragment. The page reads it, removes it from the address at once (`history.replaceState`), keeps
it in memory only, and sends it to the server in the **body** of a `POST`.

**Rationale**: a fragment is never sent to a server, so it reaches no access log, no proxy and
no `Referer` (FR-012, SC-010). A `POST` body is not logged by spec 001's request logging.

**Alternatives considered**: a query parameter (lands in every log on the way and in browser
history); a path segment (the same).

## R4 — Opening a link changes nothing

**Decision**: two calls. `POST /v1/auth/password/link` inspects: it answers the kind, the
account's email and, for a new account, the clinic's names — or `410 LINK_INVALID` with the
kind. `POST /v1/auth/password/link/set` is the only call that uses the link up.

**Rationale**: FR-011. Mail scanners and previews fetch addresses; with the token in the
fragment they cannot even reach the server, and inspecting is harmless anyway.

## R5 — Using a link exactly once

**Decision**: one transaction: lock the account row; one `UPDATE … SET UsedAtUtc = now WHERE Id
= @id AND SecretHash = @hash AND UsedAtUtc IS NULL AND ReplacedAtUtc IS NULL AND ExpiresAtUtc >
now`; if exactly one row changed, set the password and do the rest (R9). Otherwise answer
`LINK_INVALID`. Checks before it, in this order: the token opens; the link is live and the
account is in the state the kind needs (`active` for reset, `invited` for new account); the
password meets the rules; it differs from the current one. A refused password leaves the link
live.

**Rationale**: the same single-statement pattern as the code check of spec 003; two submissions
at the same moment cannot both win (edge case "double press").

## R6 — Asking for a link: one answer, one timing

**Decision**: the request does the least possible and never sends mail itself:

1. Validate the email's shape (public rule; `400` when malformed).
2. Find the account with the existing lookup `identity.usp_FindAccountForSignIn` — the declared
   crossing of spec 003, used by the same module for the same purpose: turning an email into
   (account, clinic) before sign-in.
3. If an account was found: establish its clinic, record `reset-requested`, and, when the account
   is `active`, insert one row in `identity.AccountEmailRequest` and enqueue the clinic's mail job.
4. Answer `202` with an empty body, never sooner than a fixed floor (300 ms, the same constant
   that already covers wrong-credentials answers).

**Rationale**: FR-003, FR-006, SC-002. The mail server can take seconds; if the request waited
for it, an active account would answer measurably later than an unknown email. The floor hides
the few milliseconds of difference that remain.

**Alternatives considered**: sending inside the request under a longer floor (every request
would have to wait as long as the mail timeout); an in-process queue (lost on restart, no
retry).

## R7 — The mail job and the two caps

**Decision**: a per-clinic job, `AccountEmailJob`, handles the clinic's unhandled
`AccountEmailRequest` rows. For `reset-link`, per account, under a lock on the account row:

- skip unless the account is still `active`;
- skip if a reset link was sent to it in the last 60 seconds, or five in the last 60 minutes
  (FR-005) — counted over `PasswordLink` rows of kind `reset`;
- otherwise generate the secret and the token here, send the email, and only then store the
  link (replacing the earlier live one, R8) — the order spec 003 uses for the sign-in code. If
  the email cannot be handed over nothing has changed, and the job fails so the job server
  retries; a request older than 10 minutes is given up. Accounts are handled independently: one
  account's failure does not hold up another's.

The lock is an application lock named after the account, held for the transaction, so a slow
mail server never blocks that account's sign-in.

Several requests for one account are handled as one. The job is the only place the caps live.
The secret exists only in the job's memory and in the email: the job server's own tables never
hold it (its jobs take the clinic id and nothing else — spec 002).

**Rationale**: durable, retried, and the request path stays identical for every case. The job
server polls every 5 seconds (spec 001), inside SC-001's 30 seconds.

**Alternatives considered**: checking the caps in the request (two code paths to keep equal;
races between simultaneous requests); passing the token to the job as an argument (it would be
stored readable in the job tables — against FR-008).

## R8 — One live link per account

**Decision**: a filtered unique index on `AccountId` where `UsedAtUtc` and `ReplacedAtUtc` are
null; issuing sets `ReplacedAtUtc` on the earlier one in the same transaction. Links are also
replaced when: the password is set by any route; the account is disabled (`SetStateAsync`).

**Rationale**: FR-010, FR-013. The index makes "two live links" impossible rather than unlikely.

## R9 — What setting a password does

**Decision**: one internal routine, `PasswordSetter`, used by all four routes (reset link,
new-account link, temporary-password change, re-issue by password). In one transaction it:
writes the new hash; clears the lock and both counters; replaces every live link of the account;
sets or clears `MustChangePassword` and `TemporaryPasswordExpiresAtUtc`; makes an `invited`
account `active`; ends sessions (all of them, or all but the current one for the
temporary-password change); writes the event.

"Differs from the current password" is checked by verifying the new password against the
current hash. No history of earlier passwords is kept.

**Rationale**: FR-015, FR-019, FR-026, FR-037, FR-044. One routine means the four routes cannot
drift apart.

## R10 — The temporary password

**Decision**: two columns on the account: `MustChangePassword bit` and
`TemporaryPasswordExpiresAtUtc`. Step one of sign-in treats a correct but expired temporary
password exactly as a wrong one: same answer, same floor, counted toward the lock (FR-031). The
change endpoint does not look at the expiry (story 6, scenario 11).

The generated password: 14 characters (setting) from an alphabet without look-alikes
(`0 O o 1 l I` and similar are left out), drawn with the cryptographic generator, with at least
one of each class, shuffled.

## R11 — Holding a person on the set-password screen

**Decision**: the per-request session check of spec 003 returns one more fact — the account
must change its password. The authentication handler records it on the request, and a small
gate after authentication refuses every endpoint that needs a session with `403
PASSWORD_CHANGE_REQUIRED`, unless the endpoint is marked `AllowsTemporaryPassword()`. Marked:
`GET /v1/auth/session` and `POST /v1/auth/password/change`. Renewal and sign-out handle their
own session and are not affected.

The session answer gains `mustChangePassword`. In the browser the session guard sends such a
person to `/change-password`, and the interceptor does the same on that refusal code.

**Rationale**: FR-036 and constitution III: deny by default. A new endpoint written by a later
spec is refused for such a session without its author doing anything. The fact is read in the
same query that already checks the session, so the cost stays under 5 ms.

**Alternatives considered**: a flag in the session cookie (stale until renewal — the person
would stay held for up to five minutes after changing); checking in each module (opt-in, so
somebody forgets).

## R12 — Hand-over operations for later specs

**Decision**: a new contract `IAccountHandover` in `Scs.Identity.Contracts`, without endpoints:
create by link, by generated password, by typed password; send the invitation again; re-issue
access. Each returns whether the email was handed over (FR-034) instead of throwing, and the
generated password exactly once. These send their email inside the call — the caller is a
signed-in person waiting for the result, and there is nothing to hide from them.

`IStaffAccounts.CreateAsync` stays for seeds and tests.

## R13 — Events

**Decision**: the existing append-only `identity.SignInEvent` takes five new kinds —
`reset-requested`, `reset-completed`, `invitation-accepted`, `temp-password-changed`,
`access-reissued` — and one new nullable column, `ActorAccountId`, for who re-issued. `Detail`
carries the method (`link`, `generated`, `typed`). `reset-completed` and `access-reissued` are
`security`; the rest `activity`. The table keeps its name; renaming an append-only table that
later specs read is not worth it.

## R14 — Emails

**Decision**: five messages built like the code email of spec 003 (Arabic and English together,
text and HTML, no clinic data): reset link, password changed, new-account link, account created,
access reset. The link emails name the limit (30 minutes / 72 hours). "Password changed" is sent
by the mail job (its failure is logged and retried; the reset stands). One new setting gives the
staff app's address for links and for "where to sign in".

## R15 — Screens

**Decision**: three routes in the clinic app, all in the sign-in frame:

- `/forgot-password` — the request form and its result (replaces the placeholder of spec 003).
- `/set-password` — reads the token; shows the reset form, the new-account form, or one of the
  two "no longer works" screens.
- `/change-password` — for a signed-in person holding a temporary password.

One new shared component, `scs-password-rules` (the four lines, neutral / met / unmet), fed by a
pure function that mirrors the server's rules. The success banners on the sign-in form are handed
over in memory by the `SignInFlow` service the code step already used — never in the address,
and gone after a reload.

## R16 — Testing

**Decision**: test-first, on the real database as the application login, for: the single answer
and its timing (same harness as spec 003's `SingleAnswerTests`); the caps; one-time use and
expiry with the movable clock; the temporary-password gate (every mapped endpoint is enumerated
and must refuse unless marked); isolation samples for the two new tables. The mail job is run in
tests through the clinic job runner. Playwright covers every state of FR-039 in both languages;
the leak test is extended to these endpoints and to the page address after a link is opened.

## Still to confirm (defaults chosen)

1. The wording of the temporary-password screen and of the "different password" message (page
   review, B-O19).
2. The texts of the three undrawn emails.
3. The staff app's public address in each environment.
