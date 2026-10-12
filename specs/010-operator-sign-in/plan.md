# Implementation Plan: Operator Sign-in

**Branch**: `010-operator-sign-in` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/010-operator-sign-in/spec.md`

## Summary

Operators get their way into their own console and the room they arrive in. A new module,
`Scs.Operators`, holds the operator's account and everything about signing in: email and
password, then a code by email at every sign-in; the lock after five wrong tries; one answer
for anyone who does not know the password; a forgotten password replaced through a link; one
session at a time that ends at once when it should; and the detection of a stolen session.
The first operator is made by a command of the deployment tool, which emails a link to choose
a password and never sees one.

It reuses what the clinic's sign-in already proved (specs 003 and 004) — the hashing, the
code, the rotating session, the order of checks — by moving those pieces into the shared
project, and keeps everything that is an operator's apart from everything that is a clinic's:
its own tables, its own authentication scheme and cookies, its own addresses under
`/v1/ops/`. An operator's session opens nothing of a clinic's, and the reverse.

In the `ops` application the placeholder is replaced by the screens of the design: sign-in,
the code, forgot password, choosing a password, and — inside — the console's frame (top bar,
account menu, a side bar that lists only what exists) with the profile page: account details,
recent sessions, sign out of all devices and change password.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new as packages. EF Core, the platform's data protection, mail
sender, job scheduler and request guards; in the console the `ui` and `core` libraries,
Transloco and Tailwind.

**Storage**: SQL Server 2022 — new schema `ops` with six platform-wide tables (`Operator`,
`SignInChallenge`, `Session`, `PasswordLink`, `KnownBrowser`, `SignInRecord`), each named in
the platform-wide list with its reason. No clinic-owned table; no change to another module's
tables.

**Testing**: xUnit on the SQL Server fixture with the fake clock and the captured mailbox;
access tests written first (no session, the wrong kind of session, an ended session, the
single answer, the lock, one session). Architecture tests for the module rule, the
platform-wide list and the declarations of `/v1/ops/` routes. Vitest for the console's
services and components; Playwright for every screen in both languages with pictures; a
real-stack walk per `quickstart.md`.

**Target Platform**: the platform API and its worker; the operators console (`ops`
application, port 4203 locally, its own address in production); the deployment tool.

**Project Type**: web application — a server module with its contracts, pieces moved into the
shared project, one tool command, and a console application with its frame.

**Performance Goals**: sign-in end to end under a minute with the mailbox open (SC-001); the
code's email within thirty seconds (SC-002); the session check adds one indexed read to a
request.

**Constraints**: nothing is revealed before a correct password, in content, form or time
(FR-010, FR-011); an ended session is refused on the very next request (SC-006); no secret in
a log or a record (FR-053); the two kinds of session never cross (FR-037); the email code
cannot be switched off (FR-008).

**Scale/Scope**: 62 functional requirements, 11 user stories; a handful of operators, tens of
sign-ins a day; 16 addresses; 7 screens and one dialog in two languages; 4 emails.

**Open items carried from research**: the wording the design does not draw and the four
emails' text (owner); retention of sign-in records and their screens (023, 025); the lock and timeout
as settings (024).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite SH-040…049 and the operators' chapter (§3-9, §4-3, §9-3, ق-١, ق-٥٨, ق-٨٨, ق-١٢٥). Every screen names its frame in the operators' design file; `contracts/operator-screens.md` quotes its words and marks what is not drawn for the owner. Three deliberate differences from the sources are the owner's recorded decisions: the code is always required, one session, a thirty-minute idle limit |
| II. Tenant isolation in the database (non-negotiable) | Pass | No clinic-owned table is added or touched. The six `ops` tables hold only the platform's own staff and are entered in the platform-wide list one by one with a reason; the guardrail test fails the build for any table that is neither isolated nor listed. Nothing here can read a clinic's rows: the module establishes no clinic and has no crossing |
| III. The server decides; deny by default | Pass | Every route under `/v1/ops/` declares anonymous, operators-only or own-account, or is refused for everyone; a staff session is no session there and an operator's is none on a clinic's route; each is held by a test. All limits — tries, lock, code, idle, one session — are enforced by the platform whatever the console did |
| IV. Financial records are immutable; changes are audited | Pass | No money moves. Every sign-in event is written to an insert-only table the application cannot change or delete |
| V. Secrets and patient data | Pass | Passwords, codes, tokens and renewal secrets are stored as hashes only and never logged; the email of an unknown account is kept only as a keyed hash; cookies are HttpOnly, Secure, host-bound; no patient data is involved |
| VI. Bilingual, complete screens | Pass | Every screen and email in Arabic and English, both directions, with loading, error, no-connection and not-found states; keyboard and screen reader covered; digits follow the language |
| VII. Tested before merge, verified after implementation | Pass, two paths by state | Access tests first. Suspended and role-changed cannot be reached through the product until spec 011; they are tested by setting the state directly, and recorded. Full gates, a real-stack walk, each screen beside its frame, and `validation.md` close the work |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged, with three things stated plainly. Six
platform-wide tables are added at once — the largest such addition so far — because an
operator has no clinic; each is listed with its reason and holds no clinic data. Code moves
out of Identity into the shared project (research R3): behaviour must not change and
Identity's tests are the proof. And one rule first carried by the plan alone — three spent
codes lock the account — was written into the spec as FR-020a after analysis.

## Project Structure

### Documentation (this feature)

```text
specs/010-operator-sign-in/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── operators-api.openapi.yaml   # the sixteen addresses under /v1/ops/
│   ├── operator-sessions.md         # who is asking, cookies, what ends a session, for other modules
│   └── operator-screens.md          # each screen, its frame, words and states
├── checklists/requirements.md
└── tasks.md                         # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/
│   └── ErrorCodes.cs                        # + eight OPS_* codes
├── shared/Scs.BuildingBlocks/
│   ├── Security/
│   │   ├── PasswordHasher.cs                # moved from Identity, unchanged
│   │   ├── PasswordRules.cs                 # moved: ten characters, four classes, two hundred
│   │   ├── CodeHashing.cs                   # moved: the keyed hash of a code
│   │   ├── BrowserNames.cs                  # moved: browser and system from a request
│   │   ├── OperatorSessionAuthentication.cs # the scheme, its claims, OperatorsOnly()
│   │   ├── OperatorCookies.cs               # the four cookies
│   │   └── PasswordLinkTokens.cs            # + kinds ops-reset, ops-first
│   ├── Tenancy/PlatformWideTables.cs        # + six lines
│   ├── Errors/ErrorMessages.cs              # + the words of the eight codes
│   └── Configuration/ScsOptions.cs          # + Ops: idle minutes, console address, link lives; validator rules
├── modules/Operators/
│   ├── Scs.Operators.Contracts/             # IOperatorDirectory, IOperatorSessions, IOperatorAccess
│   └── Scs.Operators/
│       ├── OperatorsModule.cs
│       ├── Domain/Operator.cs, SignInChallenge.cs, OperatorSession.cs, PasswordLink.cs,
│       │          KnownBrowser.cs, SignInRecord.cs
│       ├── Infrastructure/
│       │   ├── Persistence/OperatorsDbContext.cs
│       │   ├── Migrations/…_InitialOperators.cs
│       │   ├── OperatorSignIn.cs            # step one and two, the order of checks, the single answer, the lock
│       │   ├── OperatorSessions.cs          # open, renew, end, one at a time, idle, reuse
│       │   ├── OperatorPasswords.cs         # forgot, read a link, set, change
│       │   ├── OperatorInvite.cs            # what the deployment command calls
│       │   ├── KnownBrowsers.cs
│       │   ├── SignInRecords.cs
│       │   ├── OperatorEmails.cs            # the four emails, each in one language
│       │   ├── OperatorProfile.cs           # the profile's read
│       │   └── OperatorsCleanupJob.cs
│       └── Api/OperatorAuthEndpoints.cs, OperatorPasswordEndpoints.cs, OperatorProfileEndpoints.cs
├── modules/Identity/Scs.Identity/           # calls the moved pieces from their new place; nothing else changes
├── host/Scs.Api/ModuleRegistration.cs, Program.cs   # + module; + the scheme
├── tools/Scs.Migrator/                      # + module's migration; + `operator invite`; the local seed invites the made-up operator
└── tests/
    ├── Scs.UnitTests/Operators/             # the account's rules, the emails, the options
    ├── Scs.ArchitectureTests/OperatorRulesTests.cs
    └── Scs.IntegrationTests/Operators/, Fixtures/OperatorFixtures.cs

src/web/
├── projects/ui/src/lib/auth-frame/          # + a panel title and line that change by screen
├── projects/ops/
│   ├── public/i18n/{ar,en}.json             # ops.*
│   └── src/
│       ├── styles.css                       # the operators' brand tokens
│       └── app/
│           ├── app.config.ts, app.routes.ts
│           ├── auth/ops-auth.service.ts     # session, idle clock, reasons, tabs
│           ├── auth/ops-auth.guard.ts
│           ├── sign-in/sign-in.page.ts, code.page.ts
│           ├── password/forgot.page.ts, set-password.page.ts
│           ├── shell/ops-shell.component.ts, account-menu.component.ts, sections.ts
│           ├── home/home.page.ts, not-found.page.ts
│           └── profile/profile.page.ts, change-password.dialog.ts
└── e2e/
    ├── ops-platform.ts                      # answers the /v1/ops/ addresses
    └── ops-sign-in.spec.ts, ops-password.spec.ts, ops-shell.spec.ts, ops-profile.spec.ts

deploy/compose/.env.example                  # + SCS_OPS__*
docs/setup.md, docs/adding-a-module.md, ops/runbooks/   # the first operator; the operator with no mailbox
```

**Structure Decision**: one new module beside Identity, platform-wide by nature, sharing
Identity's proven mechanisms through the shared project and none of its tables. The console's
screens live in the `ops` application; only what a second console could use — the auth
frame's panel text — goes into `ui`.

**Terms**: an "operator" is a row of `ops.Operator`; a "sign-in in progress" is a challenge,
between the password and the code; a "session" is one signed-in browser; a "link" is a
first-password or reset link; "the frame" is the top bar and side bar around every page after
sign-in; "the console" is the `ops` application.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Six platform-wide tables in one spec | An operator belongs to no clinic; their account, sessions and records cannot be a clinic's rows | Rows of Identity's tables with no clinic would break the rule that every clinic-owned row has one, and one unfiltered query would mix operators with a clinic's team |
| A second authentication scheme and set of cookies | FR-037: the two kinds of session must never cross | One scheme with a "kind" claim makes a single wrong check a way from a clinic's session into the operators console |
| Code moved out of Identity into the shared project | Both modules need the same hasher, code hash, password rule and browser reading | Two copies drift; a reference to Identity's internals breaks the module rule |
| A second console frame (`OpsShell`) rather than the clinic's shell | The clinic's shell is built around a clinic, its branches and permissions, none of which an operator has | Bending it to both would put clinic concepts behind flags in every part of it |
