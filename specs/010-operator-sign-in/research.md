# Research: Operator Sign-in (spec 010)

Decisions taken before design. Each says what was chosen, why, and what else was weighed. The
clinic's sign-in (specs 003 and 004) already solved most of these for a clinic's team; where
its answer holds, this spec uses the same one and says so.

## R1 — Where it lives

**Decision**: a new module, `Scs.Operators`, with its own schema `ops`, beside Identity. It
holds the operator's account, sign-in, sessions, password links and the profile's reads. It has
a small contracts project, `Scs.Operators.Contracts`, for what spec 011 and later will ask of
it (who is the signed-in operator; end an operator's sessions with a reason).

**Rationale**: Identity's tables are a clinic's — every row has a clinic and is isolated by the
database. An operator belongs to no clinic; putting operators in those tables would need a
clinic that is not one, or a hole in the isolation rule. A second module keeps the rule whole
(Constitution II) and keeps the two kinds of account from ever sharing a row, which is what
FR-002 and FR-037 ask.

**Alternatives**: operators as rows of `identity.StaffAccount` with a null clinic — refused:
the isolation guardrail forbids a nullable clinic, and one query without a filter would mix
them. Operators inside the Platform module — refused: Platform is the place for what has no
owner (version, heartbeat); accounts and sessions are a subject of their own.

## R2 — Platform-wide tables

**Decision**: every table of `ops` is platform-wide and is named in `PlatformWideTables` with
its reason. The module's context is a plain `DbContext`, not a `ClinicAwareDbContext`: no
clinic is established while an operator signs in. The application's database login is granted
only what each table needs: `SignInRecord` is insert-only for it, as
`security.AnonymousSignInAttempt` is.

**Rationale**: a platform-wide table is a reviewed exception (Constitution II). These hold no
clinic's and no patient's data: names, emails and hashes of the platform's own staff.

**Alternatives**: a second database for operators — heavier to run and to back up, for no
isolation the schema and grants do not already give.

## R3 — What is shared with Identity, and how

**Decision**: the pieces both modules need move out of Identity into the building blocks,
unchanged in behaviour: the password hasher (`PasswordHasher`), the keyed hash of a sign-in
code, the password rule (ten characters, the four classes, two hundred at most) and the
reading of a browser and system from a request. Identity is changed only to call them from
their new place; its tests must pass untouched. Nothing else of Identity is referenced: no
module reads another's tables or internals.

**Rationale**: two copies of a password hasher is how one of them falls behind. The
architecture tests already forbid a module from reaching into another.

**Alternatives**: a reference from Operators to Identity's internals — refused by the module
rule. Copying — refused above.

## R4 — The session

**Decision**: its own authentication scheme, `OperatorSession`, with its own cookies and its
own data-protection purposes; a staff cookie is not even read on an operators' address.

| Cookie | Holds | Life |
|---|---|---|
| `__Host-scs-ops-signin` | the sign-in in progress (after the password, before the code) | the code's ten minutes |
| `__Host-scs-ops-session` | the access ticket: session, account, expiry | five minutes, renewed in use |
| `__Secure-scs-ops-renew` | the renewal ticket: session and a secret that changes at every renewal | the session's life; path `/v1/ops/auth` only |
| `__Host-scs-ops-device` | a random mark that tells a known browser from a new one | 400 days |

Each renewal replaces the secret; the session row keeps the hash of the current one and of
the one before. A renewal that presents an older secret ends the session and writes
`session_token_reuse` (FR-034). The short access ticket is what makes an ended session refused
"at once": every request also checks the session row (one indexed read), so sign-out, a second
sign-in, a reset and a suspension take effect on the next request, not at the ticket's expiry.

With «تذكرني» unticked the cookies carry no expiry and die with the browser; ticked, they carry
the session's end (FR-033). The session row records which.

**One at a time**: completing a sign-in ends every open session of the account in the same
transaction that opens the new one, with the reason `replaced`; a filtered unique index allows
one open session per operator, so two sign-ins at the same moment cannot both win.

**Idle**: the session row keeps `LastActivityAtUtc`. A request that the operator caused moves
it; a renewal and a request the page makes by itself (marked by the console with a header) do
not. A renewal is refused when the last activity is more than thirty minutes old. The console
also runs its own clock on keyboard and pointer use and signs out when it runs down, so the
operator sees the sign-in screen at thirty minutes and not at their next click.

**Learning that a session ended elsewhere**: an open console asks `GET /v1/ops/auth/session`
every twenty seconds, marked as a background request so that it is not activity. When the
answer is "no session" with a reason, it shows the sign-in screen with that reason's line —
within thirty seconds of a sign-in on another device, a reset or a suspension, with nobody
touching it (FR-030, SC-005). It stops while the tab is hidden and asks at once when the tab
is shown again.

**Rationale**: the same design the clinic's sessions use (spec 003, `contracts/sessions.md`),
which the security review already accepted, with three differences the spec asks for: one
session, an idle limit, a remembered-or-not flag.

**Alternatives**: one cookie scheme for both kinds of account with a "kind" claim — refused: a
single mistake in a check would let a clinic's session through. Server-pushed sign-out — more
moving parts than the next-request check plus the tab message the console already has.

## R5 — Addresses and the deny-by-default rule

**Decision**: every route of this module is under `/v1/ops/`. The fallback rule of the
platform stays "refused unless declared". Two declarations are added: `OperatorsOnly()` — any
signed-in operator — and the existing idea of "own account" for the three things an operator
does to their own account. A route under `/v1/ops/` that declares neither, and is not
anonymous, is refused for everyone; a staff session presented there is no session; an operator
session presented to a clinic's route is no session (FR-037, FR-039). An architecture test
holds each half.

Spec 011 replaces `OperatorsOnly()` on its own routes with the operators' permissions; this
spec's routes keep it.

**Rationale**: Constitution III, and ق-١ of the operators' chapter.

## R6 — The order of checks and the single answer

**Decision**: as spec 003: blocked address (the place is kept, spec 025 fills it) → the request
limit, ten a minute for an address on the sign-in routes → the account and password → the
account's state. An unknown email still costs a full password verification against a fixed
dummy hash, and every refusal before a correct password returns the same body and status
after the same work, so content, form and time match (FR-010, FR-011, SC-003). An unknown
email is recorded as a keyed hash in the existing `security.AnonymousSignInAttempt`, never as
the address.

## R7 — The code

**Decision**: as spec 003 — six digits from the system's random source, stored as a keyed
hash, ten minutes, five tries, used once, bound to the sign-in in progress by the challenge
cookie; a new one after sixty seconds, at most three in one sign-in, each replacing the last.
The figures are the existing `SCS_AUTH__*` configuration.

Three codes spent in a row (fifteen wrong tries) lock the account, as for a clinic's team
(FR-020a, written into the spec after analysis on 12 Oct 2026). Without it someone who knows
the password can try codes without end, five at a time.

## R8 — Password links

**Decision**: one table, `ops.PasswordLink`, for both kinds — `reset` (thirty minutes) and
`first` (seventy-two hours). The address holds an opaque protected token; the row keeps the
hash of its secret. Opening the link only reads; saving the password consumes it. A newer link
of a kind ends the older ones. The existing `PasswordLinkTokens` is used with two new kinds,
`ops-reset` and `ops-first`, and no clinic.

Asking for a reset always answers the same and does the same amount of work; an email is sent
only for an active account, at most one a minute for an account (FR-025, FR-026).

## R8a — Changing the password while signed in

**Decision**: two steps (FR-045a). The first address checks the current password — a wrong one
counts as a wrong try — and the new password's rule, then keeps the new password's hash in a
challenge of purpose `password-change` bound to the session, and sends a code. The second
address takes the code and, when right, writes the hash and ends the challenge. A resend
follows the sign-in's rule. Five wrong codes end the challenge; they do not add to the count
of spent codes, because the person is already signed in and the current password was right.
Closing the dialog cancels the challenge.

**Rationale**: the owner's decision for an unattended, unlocked console. Reusing the
challenge table keeps one implementation of a code's life.

## R9 — The first operator

**Decision**: a command of the deployment tool that already migrates and seeds:
`operator invite --name "<name>" --email <address>`. It creates the account as invited (or
finds it), ends earlier first-password links, makes a new one and sends its email through the
platform's mail sender, then prints one line with the account's state and no secret. For an
active account it changes nothing and says so. Each run writes a `SignInRecord` of kind
`operator-invited-by-deployment` with the machine user that ran it.

A second command, `operator withdraw --email <address>`, removes an invited account that has
not chosen a password and ends its links; it refuses for any other state and writes
`operator_invite_withdrawn` (FR-003a).

It needs the mail settings; with none configured it refuses to run rather than create an
account nobody can reach. Locally, the development seed runs it for the made-up operator and
the link arrives in the local mailbox.

**Rationale**: the answer the owner chose — no password ever passes through the deployer's
hands. Putting it in the tool, not in the API, is what makes it unreachable from an address
(FR-003).

**Alternatives**: an address guarded by a deployment secret — refused: it is an address.

## R10 — The emails

**Decision**: four, built in the module as the clinic's code email is: the code, the reset
link, the first-password link, the new-browser notice. Each is written in the account's
language only (operators have one; FR-055), plain text and simple HTML, the platform's name in
the subject, the code or link once, no other link. They are sent directly through
`IEmailSender`: a code or a link that waits in a queue is a sign-in that waits.

The new-browser notice is sent after the session opens and its failure is logged and recorded,
never returned (FR-033a). It is not a row of the notifications engine: that engine's rows
belong to a clinic.

**Rationale**: the design's "Email Template" page gives the look; its exact wording for these
four is not drawn and is listed for the owner.

## R11 — Known browsers

**Decision**: `ops.KnownBrowser` — account, the hash of the device mark, first and last seen.
A sign-in that completes with no mark, or a mark this account has not used, is "new": the mark
is set, the row written, the email sent. Clearing cookies makes a browser new again; that is
the honest limit of the method and the email says "a browser we have not seen".

## R12 — Records

**Decision**: `ops.SignInRecord`, insert-only: one row for each event of FR-052 — kind,
account when known, when, address, browser and system, and a small set of facts by kind (the
reason a session ended; the try number). Kinds use the names the operators' chapter already
gives its security events (`login_failed`, `otp_failed`, `account_locked`,
`session_token_reuse`) so that specs 023 and 025 read them without translating.

It is not the clinic's audit trail (spec 007): that trail is a clinic's, by construction.
Whether spec 023 reads this table or folds it into an operators' audit log of its own is that
spec's decision; the rows are kept in a form either can use.

Nothing secret is ever a fact: no password, code, token or link (FR-053); the email of an
unknown account is not kept at all.

## R13 — Cleaning up

**Decision**: a daily job removes spent or expired codes and links after a day, ended
sessions after ninety days (the profile shows the last ten), and known browsers not seen for
four hundred days, and `SignInRecord` rows older than twelve months (FR-052a; the one delete the
application's login is granted on that table is by age, through this job's own statement).
Specs 023 and 025 may change the period.

## R14 — The console's side

**Decision**:

- **Where**: the `ops` application, which replaces its placeholder. Shared pieces stay shared:
  the `ui` library's auth frame, fields, code input, password rules, dialog and states; the
  `core` library's HTTP, language, online and error handling.
- **Its own auth service**: `OpsAuthService` in the `ops` application — the session of an
  operator, the idle clock, the reason a session ended, the message between tabs. It does not
  extend the clinic's `AuthService`: the two must not be able to be confused, and the clinic's
  carries clinic, permissions and branches that have no meaning here.
- **Colour**: the operators' design uses its own brand colour. The `ops` application sets the
  brand tokens of the `ui` library at its root; no component is forked.
- **The auth frame**: the design's split screen — the form on one side, a brand panel with a
  title and a line on the other. The `ui` auth frame is given what it lacks for this (a panel
  title and line that change by screen) rather than a second frame.
- **The frame after sign-in**: an `OpsShell` in the `ops` application — top bar, account menu,
  side bar. The side bar reads a list of sections that later specs add to by declaring a
  route's entry, as the clinic's does. It is not the clinic's shell: that one is built around
  a clinic, its branches and its permissions.
- **Language**: before sign-in, a small control on the screen (not drawn; listed for the
  owner); after sign-in, the account's language.
- **Routes**: `/sign-in`, `/sign-in/code`, `/forgot-password`, `/password/reset`,
  `/password/first`, then inside the frame `/` (home), `/profile`, and a not-found page.

## R15 — Tests

**Decision**: written first where the constitution asks — everything about who may reach
what: no session, a staff session on an operators' route and the reverse, an ended session,
the single answer and its timing, the lock, one session at a time. Integration tests run on
the SQL Server fixture with a fake clock and the captured mailbox the clinic's sign-in tests
use. Architecture tests: every `ops` table is in the platform-wide list with a reason; the
operators' module references no other module's internals; every `/v1/ops/` route declares who
may call it; the operators' cookies are read only by the operators' scheme. Browser tests
cover each screen in both languages and take the pictures laid beside the design. A real-stack
walk follows `quickstart.md`.

## Carried forward, not decided here

- The wording of states the design does not draw (the spec's Assumptions list them) and of the
  four emails: the owner's to confirm.
- How the operators' audit log presents `SignInRecord` rows, and whether twelve months stays
  their period: 023 and 025.
- The lock threshold, lock length, idle period and longest life as settings on a screen: 024.
- Blocking an address: 025. The place in the order of checks is kept.
- The uploaded logo on the sign-in screen: 024.
