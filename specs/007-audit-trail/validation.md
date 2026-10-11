# Validation: Audit Trail (spec 007)

**Date**: 11 Oct 2026 · **Branch**: `007-audit-trail` (not committed) · **Constitution**: v1.2.0, Principle VII

What was run, what was walked on the real stack, what was measured, what was built differently
from the plan, and what was not done. Second writing, after the convergence tasks T036–T041
(section 7).

## 1. What was run

| Gate | Result |
|---|---|
| `dotnet test src/server/Scs.sln` (Debug) | unit **473** · architecture **38** · integration **557** — all passed. In the last full run one new test failed for a reason of its own (section 7, T036); it was corrected and its class re-run, 8 of 8 |
| `npm --prefix src/web run ci` (lint, translation check, tools, unit tests, build of the three apps) | passed — clinic **51**, core **128**, ui **68**, patient 1, ops 1; the clinic build prints **no size warning** |
| `npx playwright test` | **363 passed**, 4 skipped (the picture-only tests, which run when `SCS_SHOTS` is set), no retry needed, on a machine doing nothing else |

Against spec 006's last figures (401 · 30 · 445; 46 · 125 · 68; 363): 72 unit tests, 8
architecture tests and 112 integration tests are new on the server; 5 unit tests in clinic and 3
in core on the web. No browser test was added — the spec builds no screen.

New test files: `Audit/AuditIsolationTests`, `AuditWritingTests`, `AuditImmutabilityTests`,
`AuditFactsTests`, `RoleEntriesTests`, `SignInEntriesTests`, `AuditReadingAccessTests`,
`AuditReadingTests`, `AuditVolumeTests`, `Access/RouteAuditDeclarationTests`,
`Isolation/AuditSamples` (integration); `AuditActionsTests`, `SentencesTests`, `DeviceTextTests`
(unit); `AuditRulesTests` (architecture); `session-match.spec.ts` (core).

**Not re-run before starting (T001)**: the starting point was taken from spec 006's record, not
run again. The first full server run after the writer was added failed in four places, all
expected and all fixed: the isolation suite asked for a sample of the new table (two tests),
the count of declared crossings went from five to six, and the roles roll-out test now migrates
the trail before Identity.

**One browser test was made steadier, not looser.** `shell.spec.ts` "an owner sees every
destination…" failed 2 of 8 times when run alone: it decided whether to open a parent by asking
whether the child could be seen, and a child is still on screen for a moment while its parent
folds. It now asks the parent itself (`aria-expanded`); 20 of 20 alone, and the suite passes.
What it asserts is unchanged. Whether it could also fail before this spec was not checked.

## 2. What was measured

| What | Figure | Limit |
|---|---|---|
| One million entries of one clinic over two years: the first page of the last ninety days, with its counts (120,960 entries the clinic reads) | **254 ms** | 1 s (SC-007) |
| the last page of the same (page 4,839), with its counts | **444 ms** | 1 s |
| a page narrowed by category and action | **47 ms** | 1 s |
| A role edit with its entry, median of 20 — in a trail of a million entries | **16.4 ms** (slowest 53.3) | recording adds under 100 ms (SC-010) |
| the same in an ordinary trail | **16.0 ms** (slowest 36.7) | |
| The clinic app's first download | **445.95 kB** (was 585.52 kB) | warning at 500 kB (FR-041) |
| "Not in your role" on screen after opening the address, alone, ten times | 313, 276, 294, 285, 274, 276, 289, 306, 289, 290 ms | 1 s (FR-040, spec 005 SC-008) |

The whole edit is timed, lock and save included, so the entry alone costs less than the figure.
The million entries were filled in 28 s in a database of their own, so they burden no other
test. One unmeasured reading comes first in the volume test: the first call of a process pays
for building the query.

**Where the first download went** (`ng build clinic --stats-json`, by source): before, one file
held the frame (38 kB), the access checks (20 kB), the password pages (19 kB) and the code step
beside the sign-in form. Now the first download is the framework and the sign-in form; the
frame with everything inside it is one download of 72 kB + 26 kB for a signed-in person, and the
code step, the three password pages and the roles list are 3–11 kB each, fetched when opened.
The frame's own pages ("not in your role", "not in your plan", "not available yet", "not found")
are inside the frame's download, so opening one fetches nothing. The limit was not raised.

During one run of the browser suite made while the server suite and a build were also running,
the timed test passed on its first retry and `clinic.spec.ts` "under half a second" failed; both
pass on a quiet machine. The timed test now keeps its figure with its result.

## 3. What was walked on the real stack

Compose stack `heta-scs` (SQL Server, Mailpit), the API restarted on this branch — it applied
`InitialAuditTrail` and `SignInEventsIntoTrail` to a database that already held weeks of sign-in
events — and the clinic app. Real sign-ins with the seed password and the code from Mailpit, in
browsers of their own so nobody's session was touched (`walk7.mjs`, kept out of the repo). The
six steps of `quickstart.md`:

| Step | Seen |
|---|---|
| before anything | the sign-in form is on screen after ten script files, none of them the frame's |
| 1 | `owner@shifa.example` signs in |
| 2 | in a second browser, two wrong passwords for `doctor@shifa.example` (401, 401), then the right one and its code |
| 3 | the owner asks `GET /v1/audit/entries`: 200, three entries since the walk began, newest first, `counts` `{all 3, security 3, changes 0, records 0, users 0}` |
| | «تسجيل دخول — سُجِّل الدخول إلى حساب doctor@shifa.example», by `doctor@shifa.example`, «متصفح Chrome على Windows» |
| | «محاولة دخول فاشلة — محاولتان على حساب doctor@shifa.example», by «غير معروف», `attempts 2`, `locked false` |
| | «تسجيل دخول — سُجِّل الدخول إلى حساب owner@shifa.example», by `owner@shifa.example` |
| 4 | `?category=users`: total 0 and `counts.security` still 3 — the counts do not follow the open tab |
| | with `Accept-Language: en`: "Sign-in — doctor@shifa.example’s account was signed in to", "Failed sign-in — 2 attempts on doctor@shifa.example’s account" by "Unknown", "Chrome on Windows"; no Arabic letter anywhere in the answer; the same entries in the same order as in Arabic |
| | `?category=everything`: 400 `VALIDATION_FAILED`, `{"category":["INVALID"]}` |
| | without a period: the last ninety days, 13 Jul to 11 Oct 2026, 15 entries — the earlier ones carried in by the migration |
| | as `doctor@shifa.example`: 403 `PERMISSION_REQUIRED`, `reports.activity`, «هذه الصفحة ليست في دورك» |
| 5 | as `reception@shifa.example`: 403 `PERMISSION_REQUIRED`, `reports.activity`, «هذه الصفحة ليست في دورك» · "This page is not in your role" |
| 6 | in the database as the application's login: with no clinic established it sees 0 entries; for the clinic, 23. `UPDATE` and `DELETE` of one entry are both refused. The operator login's `UPDATE` is refused the same way. The entry is as it was |
| 6, again after T036 | the API restarted and applied `EntriesAreNeverRemoved`; as the operator login, `DELETE` of that entry: "Audit entries cannot be changed or removed." — 61 entries before, 61 after |

All three accounts were signed out afterwards (three more entries, as they should be).

**What the trail holds on this database after the walk**, seen with the operator login — all
but the walk's own three sign-ins, one run and three sign-outs carried in by the migration: 23
sign-ins, 20 sign-outs, 6 runs of failed attempts, 3 completed resets, 1 ending of sessions, and
8 roles as created by the platform — «مالك العيادة», «مدير العيادة», «طبيب» and the seeded «فريق العيادة» in
each of the two clinics, each ready role with its key.

The browser pane was also opened at <http://localhost:4201/> on the running app after the
loading change: the frame came up for the signed-in session, downloaded after the session was
answered.

## 4. What was laid beside the design

**Nothing, because nothing is drawn by this spec**: it builds no screen. The page that shows
these entries is spec 059's, and its frames are named in `spec.md` "Design source". What the
design did decide here is the wording — a noun for the action, one sentence in the passive, the
failed run with its count and its lock, «غير معروف», «متصفح Chrome على Windows» — read from
frames `5632:44831`, `5632:52027`, `6589:934565`, `6636:967915` and `5632:46851`, and checked
word for word by `SentencesTests` and `DeviceTextTests`. Of the twelve actions, one is drawn.

## 5. Built differently from the plan

Each of these was settled while building; the contracts and the data model were corrected to
say what is there.

| The plan said | What was built, and why |
|---|---|
| `IAuditTrail`, `ISignInTrail` and `AuditedSave` in `Scs.Audit.Contracts`, written by `Scs.Audit/Infrastructure/AuditTrail.cs` (T007, T008, T011, T013) | The writer is in `Scs.BuildingBlocks/Audit`. A contracts project may reference only the shared kernel, and writing on the caller's connection needs the database library. A module records on its own context — `db.Record(new AuditRecord(…))` — and the next `SaveChangesAsync` writes the entries in one transaction with the change, opening one when there is none. Nothing can save a recorded change without its entry; a synchronous save is refused while entries wait |
| `RecordAsSystemAsync` | `BySystem = true` on the record. And with nobody signed in and nobody named for the piece of work, the platform is the actor anyway |
| `DisplayName` on `ICurrentStaff` (T007) | Not added. The trail asks `IAuditActorNames`, which Identity implements: the email address of the account at that moment. One place to change when accounts get names (spec 026) |
| `Scs.Identity` references `Scs.Audit.Contracts` (T002) | Not needed and not added: Identity records through the shared writer. The audit module reads account names and failed runs through `IStaffDirectory`, in Identity's contracts |
| `POST /v1/auth/session/renew` declares that it records nothing | It declares `RecordedBySignIn`: a renewal that finds the session expired, or its owner disabled, ends it — and that is an entry. `/sign-in/code/resend` declares that it records nothing. The second scheduled job that exists, `AccountEmailJob`, is declared too |
| A run's attempts are counted "until the next successful sign-in or the lock" (T018) | Counted from the run's first request to the account's next run, stopping at a lock. A run is named by the trace of the request that began it, which the entry and the sign-in event share, so the count does not depend on two clocks agreeing. Attempts typed while the account is locked are not of the run |
| The backfill is idempotent by "adds only what is not there" | "Not there" is: no entry of that action about that account at the event's moment or within five seconds after it. Taking the migration back removes nothing — an entry is never removed — and applying it again adds nothing; tested |

Two sentences were added to the wording, in the same form as the rest, and are written in
`contracts/audit-trail.md` §7: a person given their first role is said to have had «بلا دور» ·
"no role" before; and an edit that renames a role and changes its permissions joins the parts.

A sixth declared crossing exists, `read-audit-as-operator`: the operators' reading uses the
operator connection, and every use of it is on the one list.

## 6. What was not done, and what waits for its owner

- **Role entries on the real stack**: verified by the integration tests only. No page and no
  address changes a role until spec 027; on the real stack they are seen as the eight roles the
  platform created.
- **Entries with no clinic** (an operator acting on the platform itself): no table, deferred to
  the first spec that has such an action. The operators' reading over clinics exists as a
  contract with a test and no address.
- **Retention**: nothing removes an entry; SH-095 waits for the legal decision.
- **`sessions.ended` by the platform** — a lock, an expiry — is in the table and is not part of
  what a clinic reads (FR-030). Whether a clinic should see "the account was locked and its
  sessions ended" as its own line is spec 059's question; today it reads the lock on the run's
  entry.

**Owner items** (from the spec; item 5 is new):

1. Eleven of the twelve sentences are not drawn. They are written in the form of the one that
   is; the design should draw or confirm them before spec 059.
2. The BRD's tab «أمنية» lists failed attempts, the lock and closing an alert and is silent on a
   successful sign-in, a sign-out and the password events; the owner decided all of them are
   under security, and the BRD should say so.
3. The design's row «دُعيت د. ياسمين علي» addresses a person in the feminine, which SH-098
   forbids; spec 026 writes the invitation's sentence.
4. The security details window of the design shows the account's email address; it is read from
   the account when the entry is read, never stored on the entry (FR-018).
5. The spec's edge case "Environment" says seeded accounts and clinics are created without
   entries. Accounts are. The roles a seeded clinic is given — the three ready ones and the
   seeded «فريق العيادة» — are recorded as the platform's doing, like any clinic's (FR-027), and
   the migration records the same for a database seeded before this spec; a test holds the two
   equal. The edge case should read: "seeded accounts are created without entries; the roles a
   seeded clinic is given are recorded as the platform's".

## 7. After convergence (T036–T041)

`/speckit-converge` found six gaps; none in the constitution. What each was, and what is there
now:

| Task | The gap | Now |
|---|---|---|
| T036 | **An entry could be removed.** The trigger let a member of the cross-clinic role delete, and the application holds such a login — the operator connection. FR-021 says nothing the application can do removes an entry. The first writing of this record said so too, having tried only that login's `UPDATE` | A second migration, `EntriesAreNeverRemoved`, replaces the trigger: any `UPDATE` or `DELETE` is refused, for the operator connection and the migrator too. Tested with both, and on the real stack. The administrator, being in no clinic, is shown no entry to act on — the new test first expected a refusal there and was corrected to expect nothing touched |
| T037 | **A module could name the actor** on a request with nobody signed in, through the scope meant for jobs and tests | On a request the scope is not read: the actor is the signed-in person or nobody. An architecture rule lets no module use the scope. Two tests through a route that tries: anonymous → the platform; signed in → the signed-in person |
| T038 | **Two scheduled jobs said nothing** about recording — the heartbeat and the purge of access records — and the check looked only at per-clinic jobs | Both declare that they record nothing, with the reason. The check now collects every job the modules register with either scheduler, and every per-clinic job there is |
| T039 | The search for secrets ran after five of the twelve actions | It runs after all twelve — role changes, an invitation, a temporary password, ended sessions added — and asserts the actions found are the whole catalogue |
| T040 | Two entries of one act had two moments: a change of role and the sessions it ended were stamped by separate saves | Entries written in one transaction carry one moment. Asserted for the change of role and for a completed reset |
| T041 | The seed records the seeded custom role, and the spec's edge case reads as if it should not | Kept, and made an owner item (5 above): the migration records the same role on older databases, so removing it from the seed would make a fresh database and a carried-over one disagree. A test builds both and finds the same four entries |

Nothing in the web apps changed in this pass, so the browser suite and the web checks were not
run again; their last results stand.
