# Implementation Plan: Passwords and Account Activation

**Branch**: `004-password-account-setup` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-password-account-setup/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Let a member of a clinic's team get or replace a password without support: ask for a reset link
and use it; activate a new account from an invitation link; replace a temporary password at first
sign-in. Asking for a link gives one answer in one time whatever the address. A link works once,
for a limited time, and a dead one says what to do. Later specs get the operations to hand over
an account in three ways and to re-issue access.

Technical approach, one line each (detail in [research.md](research.md)):

- Everything is added to the `Identity` module of spec 003; no new module.
- A link is one opaque protected token naming its clinic, so using it needs **no new database
  crossing**; the row stores only a hash of the link's secret.
- The token travels in the address fragment and in request bodies, never in a path or query.
- Asking for a link only records a request and answers after a fixed floor; a per-clinic
  background job applies the caps, creates the link and sends the mail.
- One routine sets a password for all four routes, in one transaction.
- The per-request session check also says "must change password"; a gate refuses every endpoint
  not marked as allowed — deny by default.
- Three routes in the clinic app, one new shared component for the four rules.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new. ASP.NET Core data protection, rate limiting and
authentication; EF Core; Hangfire through `IClinicJobScheduler`; MailKit through `IEmailSender`;
Angular, Transloco, Tailwind and the shared components of spec 003.

**Storage**: SQL Server 2022, schema `identity`: two new clinic-owned tables (`PasswordLink`,
`AccountEmailRequest`), two new columns on `StaffAccount`, one on `SignInEvent`. One migration.

**Testing**: xUnit on the SQL Server Testcontainers fixture as the application login, with the
fake mail sender and movable clock of spec 003; NetArchTest; Angular unit tests; Playwright for
every screen state in both languages. Test-first for the single answer, the caps, link life and
one-time use, and the temporary-password gate.

**Target Platform**: as spec 001. Nothing new locally (Mailpit is already there).

**Project Type**: web application — additions to one server module and to the clinic app.

**Performance Goals**: asking for a link answers at the fixed floor (300 ms) in every case,
means within 5% of each other (SC-002); a queued email leaves within 10 seconds (job polling 5
seconds) so the wait is under 30 seconds in 95% of cases (SC-001); the session check stays under
5 ms with the extra fact.

**Constraints**: one answer when asking, in body and timing; a link's secret and every password
never stored readable and never logged — including by the job server; opening a link changes
nothing; no session is opened by a reset or an activation; a temporary-password session reaches
only the allowed endpoints; no email address in any log.

**Scale/Scope**: as spec 003 (about 2,000 staff accounts); a few dozen resets a day. Scope here
is 44 functional requirements, 4 new endpoints and 1 changed, 2 tables, 5 operations for later
specs, 5 emails, 3 routes with 18 screen states, 1 shared component.

**Open items carried from research** (defaults chosen): wording of the undrawn screen, message
and three emails (page review, B-O19); the staff app's public address per environment. Settled
on 10 Oct 2026: a new password must differ from the current one; a notice email after a reset;
a temporary password expires after 72 hours; five reset emails per account per hour; re-issuing
access is provided here.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD is the source of truth | Pass, open items | Requirements cite B-5-4 to B-5-7, B-5-12, B-R12 to B-R21, CL-SH-096 to CL-SH-114, SH-044, SH-048; Arabic wording is quoted from the BRD. **Open**: the address-per-clinic lines carried from spec 003, and five owner decisions of 10 Oct 2026 not yet in the BRD (different password, notice email, temporary-password expiry, hourly cap, re-issue). The BRD is to be updated before the spec is approved |
| II. Tenant isolation in the database (NON-NEGOTIABLE) | Pass | Both new tables are clinic-owned with the policy of spec 002 in their creating migration and an isolation sample each. No new bypass: asking for a link reuses the one declared lookup of spec 003; using a link establishes the clinic from an authenticated token and then works under ordinary isolation (R1). The job runs per clinic through the runner of spec 002 |
| III. The server decides | Pass | Rules, link life, caps and the "different password" check are decided on the server. The temporary-password restriction is deny-by-default: every session endpoint is refused unless marked, and a test enumerates them (R11). Nothing before sign-in reveals whether an account exists (SH-043; R6); a dead link never says why (FR-024) |
| IV. Financial records immutable and audited | Not applicable | No money. The five new events go to the existing append-only table (R13) |
| V. Patient data, secrets and AI contained | Pass | Passwords as PBKDF2 hashes (spec 003); link secrets as SHA-256 only; the token never stored, never in a path or query, never a job argument (R2, R3, R7); the generated password is returned once and not kept; no email contains a password; emails carry no clinic data; the leak test is extended (R16) |
| VI. Bilingual and complete screens | Pass | All 18 states in Arabic and English, mirrored, with loading, refusal, too-many-requests and unreachable states; checked by Playwright and the translation check (R15) |
| VII. Tested before merge, test-first where it hurts | Pass | The single answer and its timing, the caps, one-time use, expiry, and the gate are failing tests first, on a real SQL Server as the application login (R16). Every acceptance scenario maps to an automated test in the quickstart |
| Technology constraints | Pass | Inside the existing module and its four folders; services that use the database live in Infrastructure; mail and jobs only through the shared abstractions; own components; Transloco |
| Workflow | Pass | The isolation and guardrail steps of the gates cover the new tables automatically |

**Result**: gate passed. The open items under Principle I block approval of the spec, not the
design.

**Post-design re-check** (after Phase 1): unchanged. The design adds no crossing and no
platform-wide table. One thing to state plainly: links are protected by the key ring that
spec 003 stores unwrapped locally and in test; wrapping it with the cloud key service (spec 001,
task T127) must be done before production — a leaked key ring alone still cannot use a link,
because the row's secret hash is also needed (R2).

## Project Structure

### Documentation (this feature)

```text
specs/004-password-account-setup/
├── plan.md
├── research.md          # 16 decisions, 3 items still to confirm
├── data-model.md        # 2 tables, 3 columns, settings, seed
├── quickstart.md        # 10 validation scenarios
├── contracts/
│   ├── password-api.openapi.yaml   # 4 new endpoints, 1 changed
│   ├── account-handover.md         # IAccountHandover; what every module gets for free
│   └── password-screens.md         # routes, 18 states, behaviour
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.BuildingBlocks/
│   ├── Security/StaffSessionAuthentication.cs  # session check returns "must change"; the gate;
│   │                                           # AllowsTemporaryPassword() metadata
│   └── Configuration/ScsOptions.cs             # 7 new AuthOptions values + validation
├── shared/Scs.SharedKernel/ErrorCodes.cs       # 3 new codes
├── modules/Identity/
│   ├── Scs.Identity/
│   │   ├── Domain/            # PasswordLink, AccountEmailRequest; StaffAccount (+2 facts);
│   │   │                      # PasswordRules (+ differs), new event kinds
│   │   ├── Infrastructure/    # Migrations/ (one), PasswordLinkTokens, PasswordLinks,
│   │   │                      # PasswordSetter, PasswordGenerator, ForgotPasswordService,
│   │   │                      # AccountEmailJob, AccountEmails (5 messages), AccountHandover;
│   │   │                      # SignInService (expired temporary password), SessionService,
│   │   │                      # StaffAccounts (disable cancels links), CleanupJob
│   │   ├── Api/PasswordEndpoints.cs   # forgot, link, link/set, change
│   │   └── IdentityModule.cs          # registrations; seed temp@noor.example
│   └── Scs.Identity.Contracts/        # IAccountHandover
├── host/Scs.Api/Program.cs            # the gate after authentication
├── tools/Scs.Migrator/                # verb send-link (local and test only)
└── tests/
    ├── Scs.UnitTests/Identity/        # rules + differs, generator, token round trip, options
    ├── Scs.IntegrationTests/Identity/ # reset, single answer, caps, link life, activation,
    │                                  # temporary password, gate, hand-over, isolation, leak
    ├── Scs.IntegrationTests/Isolation/IdentitySamples.cs   # + 2 tables
    └── Scs.ArchitectureTests/         # only the two endpoints carry the mark

src/web/projects/
├── ui/src/lib/password-rules/         # scs-password-rules + spec
├── core/src/lib/auth/                 # password rules function; AuthService (forgot, inspect,
│                                      # set, change, mustChangePassword); guard and interceptor
├── clinic/src/app/
│   ├── password/                      # forgot-password.page, set-password.page,
│   │                                  # change-password.page, state mapping
│   ├── sign-in/sign-in.page.ts        # the two success banners
│   └── app.routes.ts                  # three routes
├── clinic/public/i18n/                # ar.json, en.json — password keys
└── e2e/                               # password-reset, activation, temporary-password,
                                       # password-states

deploy/compose/.env.example            # 7 new values
docs/setup.md, ops/runbooks/sign-in.md # the new settings; "a person cannot get in"
specs/001-platform-foundation/contracts/conventions.md   # the 3 codes
```

**Structure Decision**: passwords belong to the module that owns accounts and sessions, so this
spec extends `Identity` rather than adding a module. The only change outside it on the server is
in the shared authentication handler, which is where "refuse unless allowed" has to live to
cover every later module. The screens are a second feature folder of the clinic app beside
`sign-in`.

**Terms**: the spec's "password link" is the `PasswordLink` row; its "link secret" is the random
part inside the token; "must change" is `MustChangePassword`.

## Delivery Order

For `/speckit-tasks`. Tests first throughout.

1. **Settings and schema** — the seven options and their validation; the migration (two tables
   with isolation and samples, three columns, constraints); error codes.
2. **Password building blocks** — the "differs" rule; the generator; the token; `PasswordSetter`
   with its tests (lock cleared, sessions ended, links cancelled, mark, event).
3. **Links** — failing tests for life, one-time use, replacement and the dead-link answer, then
   `PasswordLinks`, the inspect and set endpoints. Stories 1, 3, 4, 5.
4. **Asking for a link** — failing tests for the single answer, its timing and the caps, then
   the endpoint, `AccountEmailRequest`, the mail job and the two emails. Stories 1, 2.
5. **Hand-over** — `IAccountHandover` with the three emails; the migrator verb; the seed.
   Stories 3, 6.
6. **Temporary password** — failing tests for expiry at sign-in and for the gate over every
   endpoint, then the session-check change, the gate, the change endpoint. Story 6.
7. **Shared component and client rules** — `scs-password-rules`; the rules function; service,
   guard and interceptor changes. Story 5.
8. **Screens** — the three pages and the two banners; every state in both languages. Stories 1,
   3, 4, 6, 7.
9. **Events, cleanup and gates** — the five events; cleanup of old links and requests; leak
   test; runbook and setup notes; `validation.md`.

## Complexity Tracking

No constitution violations to justify.

Choices recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A request table and a background job for the reset email | The answer must take the same time whether or not mail is sent (FR-006), and the mail server can take seconds | Sending inside the request makes an active account answer later than an unknown one; a floor long enough to hide it would make every request wait as long as the mail timeout |
| The job creates the link, not the request | The job server stores its arguments readable; a token must never be stored (FR-008) | Passing the token to the job would put a usable link in the job tables |
| A protected token naming the clinic, plus a secret hashed in the row | Before sign-in there is no clinic to read the link under; and a leaked key ring must not be enough | A second stored procedure would be a new isolation bypass; a token alone would make the key ring a master key |
| The gate lives in the shared authentication code | Every later module must be covered without opting in (FR-036) | A check inside Identity covers only Identity's endpoints |
| One routine for all four ways of setting a password | Lock, sessions, links, mark and event must change together each time | Four separate code paths drift, and the forgotten step is a security hole |
