# Research: Clinic Staff Sign-In and Sessions

Decisions taken for the plan. Each has the choice, why, and what was set aside. Items the
product owner may want to confirm are listed at the end; none blocks the design.

## R1. A new module, `Identity`

**Decision**: a new module `Scs.Identity` with `Scs.Identity.Contracts`, schema `identity`,
migrated after `Tenancy` and before every later module. It owns staff accounts, sign-in codes,
sessions and sign-in events. Operator sign-in (spec 010) and patient sign-in (spec 070) add to
this module later; they do not get modules of their own.

**Why**: sign-in is used by every other module and owns its own tables; modules may not reach
into each other (constitution, technology constraints).

**Set aside**: putting accounts in `Tenancy` (it would mix "which clinic" with "which person"
and make `Tenancy` depend on mail and password rules).

## R2. Not ASP.NET Core Identity

**Decision**: use the framework's primitives (authentication handler, data protection,
cryptography, rate limiter) but not the ASP.NET Core Identity user system.

**Why**: its user store, lockout and two-factor model would have to be bent around four rules
of this product: accounts are isolated per clinic in the database (spec 002); the answer before
a correct password is one answer; the lock is reported only after a correct password; the email
code is compulsory and has its own attempt limit. Each is a few dozen lines written directly and
several overrides written against Identity.

**Set aside**: ASP.NET Core Identity with a custom store; an external identity provider (the
BRD specifies the screens and rules exactly, and patient data must stay inside the platform's
boundary).

## R3. One email, one account: uniqueness in the database

**Decision**: `identity.StaffAccount.Email` holds the address in lower case, trimmed, with a
unique index over the whole table. A unique index is enforced across clinics regardless of row
isolation, so the database itself refuses a second account for an email (FR-003). The refusal
shown to the creator is the ordinary "this email already has an account" for both the same and
another clinic.

**Why**: decided by the product owner on 10 Oct 2026; the database is the only place that can
guarantee it under concurrency.

**Set aside**: a separate platform-wide table of emails (a second copy of personal data,
readable without a clinic, to keep in step with the accounts).

## R4. Finding the account before sign-in: one narrow, declared database crossing

**Problem**: before sign-in there is no clinic, and with no clinic the account table returns
nothing (spec 002, FR-014).

**Decision**: one stored procedure, `identity.usp_FindAccountForSignIn(@Email)`, returns at most
one row with two values: the account's identifier and its clinic's identifier. It runs as a
database user without a login, `scs_signin_lookup`, which is a member of the cross-clinic role
and has `SELECT` on those two columns and the email column only. The application login may
execute the procedure and nothing else about that user. With the clinic known, the request
establishes it (source `account`) and reads the account row normally, under isolation.

The procedure is listed in a new `DeclaredDatabaseCrossings` beside `DeclaredCrossings`, and
the isolation guardrail is extended: any module in the database that runs as another user and is
not on that list fails the build.

**Why**: FR-056 and constitution II — the exception reads the least possible (two identifiers),
is declared in one reviewed place, and cannot be widened by the application login. Nothing
personal becomes readable without a clinic.

**Set aside**: using the operator connection of spec 002 (it can read everything, and the
application would hold it on every sign-in); a platform-wide directory of emails (see R3).

**To prove first**: that the isolation predicate honours role membership of the user the
procedure runs as. The first test of the implementation asserts it; if it fails, the fallback is
a platform-wide table keyed by a keyed hash of the email, written in the same transaction as the
account.

## R5. Passwords

**Decision**: PBKDF2 with HMAC-SHA-512, 210,000 iterations, a 16-byte random salt and a 32-byte
result, from the framework's own cryptography. Stored as one versioned text value
(`v1.210000.<salt>.<hash>`). When a correct password is checked against older parameters it is
re-stored with the current ones. Comparison is constant-time.

For an email that matches no account, and for an invited account with no password, the server
verifies the typed password against a fixed decoy value, so that the work done — and the time
taken — is the same (FR-011).

The single refusal also takes a single minimum time: 300 ms from the moment the request is
taken up. Checking a password costs about a tenth of a second and the three "wrong" paths differ
by a few database calls; waiting until one fixed moment hides both, and slows guessing (found in
implementation: measured spread 0.22% against the 5% of SC-002).

**Why**: the current OWASP figure for this function; no new dependency; FIPS-friendly.

**Set aside**: Argon2id (better against hardware attacks, but needs a third-party library to be
kept current; the versioned format lets it replace PBKDF2 later without forcing resets).

## R6. The lock

**Decision**: one `UPDATE` both counts a wrong attempt and, on the fifth, sets
`LockedUntilUtc = now + 15 minutes` and returns the count to zero, so two wrong attempts at the
same moment cannot both be "the fourth". Locking ends the account's sessions in the same
transaction. A wrong password while locked changes nothing. A correct password returns the count
to zero. The same `UPDATE` treats a wrong attempt as the first when the last counted one is more
than 15 minutes old (`LastFailedAtUtc`), so only attempts less than 15 minutes apart add up
(clarified 10 Oct 2026). The order of checks is exactly FR-009.

**Why**: B-R6, SH-041; the edge case "the fifth wrong attempt and a correct one at the same
moment".

## R7. The sign-in code

**Decision**: six digits from the cryptographic random generator. Stored as an HMAC-SHA-256 of
the code keyed by a server secret (`SCS_AUTH__CODE_KEY`) and the challenge's identifier, never as
the code. One row per code in `identity.SignInChallenge` with attempts, expiry (10 minutes) and
the time a resend becomes possible (60 seconds, a setting). Issuing a new code — by a new correct
password or a resend — marks the earlier one replaced. A resend carries the count of resends
forward; after three in one sign-in the answer is `RESEND_LIMIT_REACHED` and the standing code
stays usable (clarified 10 Oct 2026). Checking a code is one `UPDATE` that
counts the attempt and consumes the code only if it matches, is standing, unexpired and under
five attempts.

Between the two steps the browser holds a short-lived, HttpOnly cookie that carries the
challenge's identifier, the account and the clinic, protected against reading and tampering
(R9). That is how the second request knows its clinic without a session.

**Why**: SH-040, B-R3, B-R5, B-R28. A six-digit value hashed without a secret could be recovered
from a copy of the database by trying a million values.

**Set aside**: a time-based authenticator app (not what the BRD specifies); keeping the pending
sign-in in server memory (lost on restart, and wrong with more than one server).

## R8. Sessions: two cookies, a row per session, checked on every request

**Decision**:

- `identity.Session` holds one row per signed-in browser.
- **Access cookie** `__Host-scs-session`: protected (R9), carries session, account and clinic
  identifiers, valid 5 minutes. `HttpOnly`, `Secure`, `SameSite=Strict`, path `/`.
- **Renewal cookie** `__Secure-scs-renew`: protected, carries the session identifier, the clinic
  and a 256-bit random secret; path limited to the renewal and sign-out addresses. The row keeps
  only a hash of the current secret and of the previous one.
- **Every request**: the handler opens the access cookie, establishes the clinic from it
  (source `account`), and reads the session row by its key; ended or missing means refused
  (FR-034). One indexed read, under isolation.
- **Renewal**: when the access cookie has expired the front end calls the renewal address once
  and repeats the request. Renewal replaces the secret, re-checks the account's and the clinic's
  state, and refuses past the session's maximum life (12 hours, a setting).
- **Theft**: the previous secret is accepted for 30 seconds (two tabs renewing together).
  Presented later, every session of the account is ended and a security event is recorded
  (FR-038).

**Why**: the BRD's model (B-3-2, B-R26, CL-SH-124, CL-SH-125) and the constitution's reason for a
separate staff host: cookies that scripts cannot read, on a host no patient page shares.

**Set aside**: tokens in browser storage (readable by any script on the page); a single
long-lived cookie (no theft detection); signed tokens trusted without a database read (cannot be
ended at once).

## R9. Protecting cookies: ASP.NET Core Data Protection, keys in the database

**Decision**: the framework's data protection, with its key ring stored in a platform-wide
table `platform.DataProtectionKey` so every server instance shares it. Separate purposes for the
challenge, access and renewal cookies. In cloud environments the key ring is itself encrypted
with a key from the cloud's key service; locally it is not.

**Why**: authenticated encryption and key rotation that are maintained by the framework rather
than written here.

**Carried to the cloud tasks of spec 001**: wrapping the key ring with the cloud key service.
Until then the design is complete only for local and test.

## R10. Protection against forged requests

**Decision**: `SameSite=Strict` on all three cookies; and every request that changes something
that acts for a session must carry the header `X-Scs-Request: 1` and, when the browser sends
`Origin`, an origin from the configured list. Otherwise 403. The header is added by the existing
HTTP interceptor on the staff and operators sites.

"Acts for a session" means: every endpoint that needs a signed-in person, and the sign-in,
renewal and sign-out endpoints, which set or use the cookies. An endpoint open to everyone that
never touches a session (reporting a front-end failure, a public patient page) has nothing of
anyone's to misuse and is not checked (decided in implementation: a blanket rule refused the
front-end failure report of spec 001).

**Why**: a form on another site cannot add a custom header, and a script on another site is
stopped by the cross-origin rules of spec 001.

## R11. Request limit and blocked networks

**Decision**: the framework's rate limiter, a fixed window of ten per minute per network
address, on the three sign-in addresses together — sign-in, code, resend (FR-044). Renewal has
its own window of 120 per minute per address. The answer is 429 with `Retry-After`
and code `RATE_LIMITED`; the form shows a warning with the time. The limit is per server
instance for now.

Blocked networks: an interface `IBlockedNetworks` in the shared project, checked first, with a
default implementation that blocks nothing. Spec 025 supplies the real list. Refusal is 403
`NETWORK_BLOCKED` and touches no account (FR-045).

**Set aside**: a shared counter in the database (a write per sign-in request before any check;
revisit when more than one instance runs in production).

## R12. Sending the code by email

**Decision**: `IEmailSender` in the shared project with one implementation over SMTP (MailKit).
The code is sent inside the request with a 10-second limit (`SCS_MAIL__TIMEOUT_SECONDS`), because
the form must know whether it was sent (FR-014). The email leaves **before** the code is stored:
if it cannot be sent, nothing has changed — no code stands after step one, and the earlier code
stays valid after a failed resend — and no database transaction is held open while waiting for
the mail server. Locally a mail-catcher container (Mailpit) is added to the compose file;
its inbox is at `http://localhost:8025`. The message is plain text and HTML, Arabic then
English, and contains the code and its lifetime only (FR-055).

**Why**: SMTP is what the cloud's mail service and every alternative offer; the notifications
engine (spec 008) will take over templates and language and keep the sender.

**Set aside**: sending through a background job (the request could not report failure);
`System.Net.Mail` (the framework's own documentation advises against it for new work).

## R13. Events

**Decision**: one clinic-owned table `identity.SignInEvent` for what happened to an account:
signed in, wrong password, account locked, wrong code, code spent, renewal secret reused,
session ended (with its reason). Identifiers, network address and browser only. Rows cannot be
changed or removed by the application or report logins (a database trigger refuses).

Attempts on an email that matches no account belong to no clinic. They go to
`security.AnonymousSignInAttempt`, a platform-wide table in a new schema on which the
application login may insert and nothing else. It holds a keyed hash of the email typed, never
the email (people type passwords into the wrong box).

**Why**: FR-052, FR-053; specs 025 and 059 read these later. Neither table is the audit trail of
spec 007.

## R14. What other modules get

**Decision**: `Scs.Identity.Contracts` publishes

- `ICurrentStaff` — the signed-in account and session of this request.
- `IStaffAccounts` — create an account in a state (FR-004); unlock; clear lock and count (for
  password reset); set state (for specs 026 and 004), which ends sessions when disabling.
- `IStaffSessions` — end one account's sessions; end all sessions of the clinic except one.

The existing "no identity" authentication of spec 001 is replaced by the staff session handler;
the default remains "refuse unless an endpoint says otherwise". An endpoint that should be
reachable without signing in is marked so, as today.

`Scs.Tenancy.Contracts` gains `IClinicProfile.GetOwnAsync()` — the established clinic's names
and state, read under isolation — so `Identity` does not read `Tenancy`'s table.

## R15. Clinic states at sign-in

**Decision**: `suspended` and `cancelled` refuse entry and renewal with the suspended answer;
every other state lets the team in. A read-only state does not exist in the data yet (spec 016)
and will fall in "every other state" by default.

**Why**: B-R10, SH-034; spec 002 already treats those two states as "not available" for
patients.

## R16. The front end

**Decision**: the sign-in screens are a feature folder in the clinic app
(`src/web/projects/clinic/src/app/sign-in/`), built from new shared pieces in `ui`: the
two-panel frame, banner, text and password fields, the six-box code input, primary and secondary
buttons, and the dialog. `core` gains `AuthService` (session state, sign-in calls, renewal with
one request in flight at a time) and a route guard. The clinic app's placeholder becomes the
signed-in landing page with «تسجيل الخروج».

Layout and wording follow the design file "Smart Clinic OS — Design System & Screens" (sign-in
section, 29 drawn states, 18 of them in this spec) and are checked against it during
implementation.

**Why**: constitution — own components on Tailwind and Angular CDK; anything two apps need goes
into `core` or `ui` (the operators console reuses all of it in spec 010).

## R17. Testing

**Decision**: test-first for the refusal rules (single answer, order of checks, lock, code
limits, session ending), on a real SQL Server as the application login. A fake `IEmailSender`
captures messages; a fake clock (`TimeProvider`) moves time for expiry and lock. The timing
equality test follows the pattern of spec 002 (`IndistinguishableTests`). Playwright covers every
state of FR-047 in both languages with the platform's answers supplied by the test, and one run
against the real stack with Mailpit.

## Items to confirm

| # | Item | Default used | Effect of changing |
|---|---|---|---|
| R5 | Password hashing function | PBKDF2-HMAC-SHA-512, 210,000 | Versioned; can change without resets |
| R8 | Maximum life of a session | **Confirmed 10 Oct 2026**: 12 hours | — |
| R8 | Life of the access cookie / grace for a replaced secret | 5 minutes / 30 seconds | Two settings |
| R7 | Resend countdown | 60 seconds | One setting |
| R15 | A cancelled clinic at staff sign-in | **Confirmed 10 Oct 2026**: refused like a suspended one | — |
| R11 | Request limit shared between server instances | Per instance | Revisit before production scale-out |
| R11 | Ten sign-in requests per minute per address (the BRD's figure) when a whole team shares one address | 10 | One setting |
| R12 | Mail service in the cloud | Not chosen ("Nothing yet", 10 Oct 2026) | Configuration only |
