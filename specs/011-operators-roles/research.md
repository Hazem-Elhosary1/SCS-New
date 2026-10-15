# Research: Operators and Their Roles

**Feature**: 011-operators-roles | **Date**: 2026-10-13

Decisions taken before design. No unknown is left open; what belongs to the owner is listed
at the end.

## R1 — Where the catalogue lives, and what a code looks like

**Decision**: one static catalogue, `OperatorPermissions`, in the shared kernel beside the
clinic's `Permissions`: 28 entries, each with its code, its group, its Arabic and English names
as the role editor draws them, and the code it needs. Codes are the BRD's
(`dashboard.view` … `settings.edit`); the one it leaves unnamed is `notices.send`. The
catalogue is closed: a role is saved only with codes from it, and the application refuses to
start if a route names a code that is not in it.

**Rationale**: the same shape the clinic's engine proved in spec 005; the screens of 22
permissions are not built yet, but roles must be whole from the start (spec, Assumptions).

**Alternatives considered**: a table of permissions edited in the database — rejected: the
catalogue is code that guards routes, not data; reusing the clinic's catalogue — rejected: two
audiences, two closed lists, and an operator's permission must never satisfy a clinic's route.

## R2 — How a route of the console says who may call it

**Decision**: every route under `/v1/ops/` declares exactly one of three things:

| Declaration | Who |
|---|---|
| `.Open()` (spec 010) | anyone: sign-in, the code, forgot password, reading a link |
| `.OperatorOwnAccount()` | any signed-in operator, on their own account only — a fixed list |
| `.RequiresOperator(OperatorPermissions.X)` | operators whose role holds `X` at that moment |

Spec 010's `.OperatorsOnly()` is renamed `.OperatorOwnAccount()`: every route that used it is
one an operator performs on their own account (session, profile, change password, sign out
everywhere). The gate runs before the handler, reads the role's permissions once per request
(`IOperatorAccess`, scoped, never cached between requests) and answers 403
`PERMISSION_REQUIRED` with the code, as the clinic's gate does. A route that declares nothing
is refused for everyone, and an architecture test names it.

**Rationale**: FR-003, FR-004, FR-005; the clinic's contract (spec 005 §1–§3) with the
operators' scheme. Reading per request is what makes a change to a role reach its holders at
their next request without ending sessions.

**Alternatives considered**: permissions as claims in the session — rejected: a changed role
would wait for the next renewal, against ق-53; one shared gate for both audiences with a
"kind" switch — rejected for the reason spec 010 kept the schemes apart.

## R3 — «مسؤول عام» as an identity

**Decision**: a role row carries `SystemKey` (`super-admin`, `billing-admin`, `viewer`, or
none). "Is a super admin" is `SystemKey == super-admin` on the operator's role, asked through
`IOperatorAccess.IsSuperAdmin`, and never by comparing names or counting permissions. The
three system roles are seeded by the migration with fixed identifiers; their names come from
the console's translations, not from the row.

**Rationale**: FR-010, FR-011. A custom role holding all 28 permissions is still not «مسؤول
عام» and cannot grant it.

## R4 — What may be granted, said once

**Decision**: one pure rule, `RoleGrants`, used by the server for every path and mirrored by
the console only to draw:

- a role may be given to an operator when the giver is a super admin, or when the role is not
  the super admin's and every permission in it is held by the giver;
- a permission may be added to a role when the giver is a super admin or holds it; what the
  role already had stays;
- nobody changes their own role; nobody but a super admin edits the role they hold;
- copying a role takes the intersection with what the creator holds.

The roles read for the pickers (`GET /v1/ops/roles/options`) answers, for each role, whether
this caller may grant it and why not; the catalogue read answers which boxes this caller may
tick. The console draws from those answers and does not compute the rule itself.

**Rationale**: FR-009, FR-016, FR-017, FR-030, SC-006 — one place to test "nobody ends up with
what the granter did not hold".

## R5 — The last active super admin, under simultaneous requests

**Decision**: every action that can reduce the number of active super admins — changing an
operator's role away from it, suspending one — runs in a transaction that first takes an
application lock named for that purpose (`sp_getapplock`, exclusive, transaction-owned), then
counts the active super admins other than the target, and refuses when none would remain.
Other actions do not take the lock.

**Rationale**: FR-038, ق-56. The same device spec 004 uses for one account's links. A count
without the lock lets two requests each see "one other remains".

**Alternatives considered**: a database constraint — a count across rows cannot be one;
serializable isolation for the whole request — heavier and deadlock-prone for no gain.

## R6 — Roles' tables, deletion and names

**Decision**: `ops.Role` and `ops.RolePermission`, platform-wide, added to the platform-wide
list with reasons. Deleting sets `DeletedAtUtc`; a deleted role is in no list and no picker,
and earlier entries keep its name because an entry stores the name it had. A name is unique
among roles not deleted, compared trimmed and case-insensitively (a filtered unique index on a
normalised column); the three system roles' English and Arabic names are reserved.
`ops.Operator.RoleId` is required and restricts deletion at the database too: a held role
cannot go even if the application's check were bypassed.

**Rationale**: FR-012, FR-019, FR-020.

## R7 — What the operator row gains, and what happens to rows that exist

**Decision**: `ops.Operator` gains `RoleId`, `Department`, `JobTitle`, `TwoStepRequired`,
`InvitedById`, `InvitedByName`, `ActivatedAtUtc` and `SuspendedAtUtc`. The migration creates
the three system roles, then gives every existing operator the super admin role, then makes
`RoleId` required. `ActivatedAtUtc` is filled from the first-password record for accounts
that are already active.

**Rationale**: FR-027b (clarified); §7-هـ-1's "+n this month" needs when an account became
active. `InvitedByName` is kept beside the id so the page can say who invited someone even if
that account is later suspended.

## R8 — Inviting from the console on top of what spec 010 built

**Decision**: the console's "add operator" calls the same `OperatorInvite` the deployment
command calls, extended with the role, the optional fields, the inviter and "send now". An
account whose invitation is to be sent is marked "owed" and the existing job sends it within a
minute, exactly as today; with the switch off nothing is owed. The email is spec 094's
`FirstPassword`, which gains two optional values — who invited and the role's name in the
email's language — and keeps today's wording when there is no inviter (the deployment
command). The command gives the super admin role (FR-027a) and records itself as today.

**Rationale**: FR-031 to FR-034; one way of making an invitation, one job, one email.

## R9 — Sending a link from an operator's page

**Decision**: one address, `POST /v1/ops/operators/{id}/password-link`. For an active account
it issues a reset link (30 minutes) through spec 010's `OperatorPasswords`; for an invited one
it marks the invitation owed again, which ends the earlier link when the new one is issued.
The one-a-minute rule is checked against the account's newest link of either kind and answers
409 `OPS_LINK_TOO_SOON` before anything is written or recorded. This path is separate from the
account holder's own "forgot password" and its caps; a link sent here counts toward them.

**Rationale**: FR-043, FR-044, ق-61.

## R10 — Suspending, reactivating, unlocking

**Decision**: methods on the account with their state guards — `Suspend` (active only),
`Reactivate` (suspended only; clears wrong tries, spent codes and the lock), `Unlock` (active
and locked only). Suspending and a change of role end the sessions through spec 010's
`OperatorSessions.EndAllAsync` with the reasons it already knows, inside the same transaction
as the change. A request for a state the account is not in answers 409 `OPS_STATE_CHANGED` and
changes nothing.

**Rationale**: FR-039, FR-041, FR-042, FR-046, SC-004. Spec 010 already shows the reason on the
ended browser within its polling time.

## R11 — Recording what was done: the platform's own trail

**Decision**: this is the first spec in which an operator acts on the platform itself, so it
creates `audit.PlatformEntry` — the table spec 007 left for it (its research R5): the columns
of `audit.Entry` without a clinic, append-only by the same trigger, platform-wide and listed.
Writing goes through the shared writer with the operator as the actor (a new
`AuditActorScope` path for operators, taken from the session and never from a caller).
`IOperatorAuditReading` gains "the last entries by this operator". Routes declare what they
record with the existing `.Records(...)` declaration; ten actions are added to the catalogue
of actions (`operator.invited`, `.edited`, `.role-changed`, `.suspended`, `.reactivated`,
`.link-sent`, `.unlocked`, `opsrole.created`, `.edited`, `.deleted`).

**Rationale**: FR-048, SC-010; spec 007's contract. Sign-ins stay in `ops.SignInRecord` (spec
010).

**Alternatives considered**: writing these into `ops.SignInRecord` — rejected: that table is
security events removed by age, and spec 023's audit page reads the trail.

## R12 — "Recent activity" and "performance" on an operator's page

**Decision**: recent activity is the newest ten of two sources merged by time: the account's
successful sign-ins (`ops.SignInRecord`, with browser and system in words) and the platform
entries whose actor is the account. Each is turned into one sentence by a small table of
sentences in both languages. Performance shows one card, the account's active sessions
(0 or 1 under spec 010's one-session rule); the read returns the cards as a list so that
specs 015 and 082 add theirs without changing the page's contract.

**Rationale**: clarified at specify. The frame's three other cards are not drawn empty.

## R13 — The list: search, filters, cards, paging

**Decision**: one read answers the page of rows, the total, the four cards and the values the
filters may offer, so the cards and the table never disagree. Search is a case-insensitive
"contains" on name and email; filters are state and role; order is newest created first; page
size 8 as the frame draws, with a pager when there is more than one page. Roles are one read
with their cards; search filters on the client, since the list is one page.

**Rationale**: FR-013, FR-022 to FR-024; SC-008 at 200 operators needs no more than an index
on creation time.

## R14 — The console: who may see and open what

**Decision**: the session read (`GET /v1/ops/auth/session`) returns the role (id, name, system
key) and the codes it holds. `OpsAccess` in the console keeps them as signals; `sections.ts`
gives each section its permission; a route guard `opsCan(code)` sends someone who may not open
a page to the console's "you cannot open this page" state; a structural directive hides write
controls. After a 403 `PERMISSION_REQUIRED`, and on every navigation, the session is read
again, so a changed role shows without a reload. The server is the judge in every case.

**Rationale**: FR-006 to FR-009, US1.

## R15 — Western digits in the console

**Decision**: one formatter in the console, `opsNumber` / `opsDate` / `opsRelative`, always
`latn` numerals with the language's words; every number, date, count and countdown of the
`ops` application goes through it, including spec 010's screens (the resend countdown, the
session times, the code's minutes). A browser test fails on any Arabic-Indic digit on any
`ops` screen in Arabic, as the emails' test does.

**Rationale**: clarified at specify; the BRD's rule for the operators console and the frames.
The clinic console keeps Arabic-Indic digits (its own rule).

## R16 — What goes into the shared `ui` library

**Decision**: parts a second console could use go into `ui` when they are missing there: a
checkbox, a select, a switch, a textarea, a status mark (the green/grey pill), a KPI card and
a breadcrumb — each built from the operators' component pages of the design file (Checkbox
`35:2`, Dropdown `57:2`, Switch `35:4`, Textarea `41:3`, Badge `8:36`, KPI Card `8:39`,
Navigation `65:2`). The list's toolbar, tabs, table, footer, filter popover, confirm, menu,
toast and states are reused as they are. Anything that knows about operators stays in `ops`.

**Rationale**: the rule set in spec 010's plan and the owner's note to keep the two consoles'
code in their places.

## R17 — Concurrency of edits

**Decision**: operator and role edits carry the row version they were read with; a save
against a changed row answers 409 `OPS_CHANGED_ELSEWHERE` and the form says so and offers to
reload. Nothing is overwritten silently.

**Rationale**: spec, Edge Cases.

## Carried forward

- **Owner**: the unlock button's name («فك قفل الحساب»); the wording of the two refusals the
  BRD says have no text yet (email already used; the reason under a disabled permission or
  role); the sentences of the activity list. Drafted in `contracts/operator-screens.md`.
- **Designer**: the suspend dialog draws no reason field and the edit form no "note on the
  edit", both of which the BRD requires and are built; the operator's page draws no reset or
  unlock button; the roles tab draws «فلترة», a pager and export, not built; the phone field's
  name; the invitation email's "30 minutes".
- **BRD (owner to update)**: the role editor as drawn (`OP-B-185`, §5-هـ-7); the roles table's
  columns as drawn (§7-هـ-3).
- **Later specs**: export (023); the three other performance cards (015, 082); the bell (093);
  "enforce two-step" as a setting (024); every permission's routes other than the operators
  group's.
