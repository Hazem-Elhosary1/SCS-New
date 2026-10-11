# Implementation Plan: Clinic Staff Sign-In and Sessions

**Branch**: `003-clinic-sign-in` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-clinic-sign-in/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Let a member of a clinic's team sign in with email and password, then a six-digit code sent by
email, and work for their clinic only. Before a correct password the server gives one identical
answer; after it, it says why entry is refused. A session is a row that is checked on every
request, so ending it takes effect at once; it is carried by cookies scripts cannot read, renewed
quietly, and shut down for the whole account if a replaced renewal secret is presented again.

Technical approach, one line each (detail in [research.md](research.md)):

- A new `Identity` module with schema `identity`: accounts, sign-in codes, sessions, events.
- One email, one account: a unique index over the whole account table.
- Before sign-in the account's clinic is found by one stored procedure that returns two
  identifiers and nothing else — the single declared database crossing of this spec.
- Passwords with PBKDF2-HMAC-SHA-512; codes and renewal secrets stored only as keyed hashes.
- Two protected cookies (access, 5 minutes; renewal, rotating) and a short-lived one between
  the two steps; the framework's data protection with its key ring in the database.
- The lock and the code check are each a single atomic `UPDATE`.
- Mail over SMTP inside the request; a mail catcher locally.
- Sign-in screens in the clinic app, built from new shared components the operators console
  will reuse.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: ASP.NET Core authentication, data protection (with its EF Core key
store) and rate limiting; EF Core (SQL Server); MailKit (new) for SMTP. Front end: Angular,
Angular CDK (dialog, focus trap), Transloco, Tailwind. No other new dependency.

**Storage**: SQL Server 2022. New schema `identity` (four clinic-owned tables, one stored
procedure) and `security` (one platform-wide, insert-only table); `platform.DataProtectionKey`.
One login-less database user for the sign-in lookup.

**Testing**: xUnit on the SQL Server Testcontainers fixture, as the application login; a fake
mail sender and a movable clock; NetArchTest; Angular unit tests; Playwright for every screen
state in both languages. Test-first for refusals, the lock, code limits and session ending.

**Target Platform**: as spec 001. Local additions: a Mailpit container.

**Project Type**: web application — a server module and the first real screens of the clinic app.

**Performance Goals**: step one answers in under 500 ms at the server, of which password
checking is about 100 ms by design; the per-request session check adds under 5 ms; the code
email leaves the server within 10 seconds or the request reports failure (SC-001); responses
for every "wrong before a correct password" case within 5% of each other (SC-002).

**Constraints**: one answer before a correct password, in body and timing; no session before
the code; every request re-checks the session; no password, code, cookie value or email address
in any log; cookies `HttpOnly`, `Secure`, `SameSite=Strict`; the application login cannot read
accounts without a clinic except through the one procedure.

**Scale/Scope**: 200 clinics, about 2,000 staff accounts, a few thousand sign-ins a day; every
staff request thereafter passes the session check. Scope here is 56 functional requirements, 7
endpoints, 6 tables, 1 module, 2 routes with 20 screen states, and about 10 shared components.

**Open items carried from research** (defaults chosen): the hashing function; access-cookie
life and grace; the resend countdown; a shared request limit across instances; the cloud mail
service. Settled in clarification on 10 Oct 2026: session life 12 hours; wrong attempts
forgotten after 15 minutes; a cancelled clinic refused like a suspended one; three resends per
sign-in.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD is the source of truth | Pass, one open item | Requirements cite SH-040 to SH-049 and CL-SH-065 to CL-SH-126; Arabic wording is quoted from the BRD. **Open**: SH-003, CL-SH-065 and B-R1 still describe a sign-in address per clinic, and B-3-1 says "unique within the clinic"; the spec follows constitution v1.1.0 and the owner's decision of 10 Oct 2026 (one email, one clinic). The BRD is to be updated before the spec is approved |
| II. Tenant isolation in the database (NON-NEGOTIABLE) | Pass | All four identity tables are clinic-owned with the policy of spec 002 in their creating migration and an isolation sample each. The pre-sign-in lookup is the bypass the constitution names: one procedure, two identifiers, a dedicated login-less user, declared in one list, and the guardrail fails the build for any other (R4). After the lookup the request works under ordinary isolation |
| III. The server decides | Pass | Every refusal is decided on the server in a fixed order (FR-009); the front end only shows it. The default stays "refuse unless the endpoint says otherwise"; the six endpoints reachable without a session are listed in the contract. The session is checked on every request (R8) |
| IV. Financial records immutable and audited | Not applicable | No money. Sign-in events are append-only by the same reasoning (R13) |
| V. Patient data, secrets and AI contained | Pass | Passwords, codes and renewal secrets are stored only as hashes; cookie values are protected; the hashing secret and mail password come from the environment; email addresses are classified `[PersonalData]` and never logged; the leak test is extended (R17). Before sign-in nothing reveals whether a clinic or an account exists (SH-043; R5, FR-010, FR-011) |
| VI. Bilingual and complete screens | Pass | Every state of FR-047 in Arabic and English, mirrored, with loading, error, offline and refusal states; checked by Playwright and the translation check (R16) |
| VII. Tested before merge, test-first where it hurts | Pass | Refusal rules, the lock, code limits and session ending are written as failing tests first, on a real SQL Server as the application login (R17). Each refusal path of FR-009 has a test |
| Technology constraints | Pass | New module with its two projects and the four folders; EF Core for writes; own components on Tailwind and Angular CDK; Transloco; no per-clinic subdomain; staff cookies on the staff host only. `Identity` reaches `Tenancy` only through its contracts (R14) |
| Workflow | Pass | The isolation step of the gates covers the new tables and the new guardrail rule automatically |

**Result**: gate passed. One open item under Principle I blocks approval of the spec, not the
design.

**Post-design re-check** (after Phase 1): unchanged. The design adds one database crossing and
one platform-wide, insert-only table that holds no personal data in readable form; both are
declared where spec 002 requires. The key ring that protects cookies is stored unwrapped in
local and test; wrapping it with the cloud key service is carried to the cloud tasks of spec 001
and must be done before any production use (Principle V).

## Project Structure

### Documentation (this feature)

```text
specs/003-clinic-sign-in/
├── plan.md
├── research.md          # 17 decisions, 6 items still to confirm
├── data-model.md        # 6 tables, 1 procedure, settings, seed
├── quickstart.md        # 10 validation scenarios
├── contracts/
│   ├── identity-api.openapi.yaml   # 7 endpoints
│   ├── sessions.md                 # what later modules rely on and must do
│   └── sign-in-screens.md          # routes, states, which answer shows what
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.BuildingBlocks/
│   ├── Security/
│   │   ├── StaffSessionAuthentication.cs   # replaces NoIdentityAuthenticationHandler for staff
│   │   ├── RequestOriginCheck.cs           # X-Scs-Request and Origin on changing requests
│   │   ├── BlockedNetworks.cs              # IBlockedNetworks, default blocks nothing
│   │   └── CookieProtection.cs             # data protection purposes, key ring in the database
│   ├── Mail/                               # IEmailSender, SMTP implementation, MailOptions
│   ├── Tenancy/DeclaredDatabaseCrossings.cs
│   └── Configuration/                      # AuthOptions, MailOptions, SupportOptions + validation
├── shared/Scs.SharedKernel/ErrorCodes.cs   # 12 new codes
├── modules/Identity/
│   ├── Scs.Identity/
│   │   ├── Domain/            # StaffAccount, SignInChallenge, Session, SignInEvent, EmailAddress, PasswordRules
│   │   ├── Application/       # SignInService, CodeService, SessionService, StaffAccounts, StaffSessions
│   │   ├── Infrastructure/    # IdentityDbContext, Migrations/, PasswordHasher, CodeHasher,
│   │   │                      # SessionCookies, CodeEmail, CleanupJob
│   │   ├── Api/               # AuthEndpoints (sign-in, code, resend, session, renew, sign-out)
│   │   └── IdentityModule.cs
│   └── Scs.Identity.Contracts/             # ICurrentStaff, IStaffAccounts, IStaffSessions
├── modules/Tenancy/Scs.Tenancy.Contracts/  # + IClinicProfile
├── modules/Platform/                       # + GET /v1/meta/support; DataProtectionKey table
├── tools/Scs.Migrator/                     # Identity after Tenancy; seed accounts
└── tests/
    ├── Scs.UnitTests/Identity/             # email, password rules, hashers, options
    ├── Scs.IntegrationTests/Identity/      # flow, single answer, lock, code, resend, sessions,
    │                                       # renewal, rate limit, blocked network, isolation
    ├── Scs.IntegrationTests/Isolation/     # samples for 4 tables; database-crossing guardrail
    ├── Scs.IntegrationTests/Fixtures/      # FakeEmailSender, movable clock, SignInAsAsync
    └── Scs.ArchitectureTests/              # Identity boundaries; nothing logs an email

src/web/projects/
├── ui/src/lib/               # auth-frame, banner, text-field, password-field, code-input,
│                             # button, dialog, link — each with its spec
├── core/src/lib/auth/        # AuthService, session guard, renewal in the interceptor,
│                             # tab broadcast, support service
├── clinic/src/app/
│   ├── sign-in/              # sign-in.page, code.page, help-window, state mapping
│   ├── landing/              # signed-in landing page with sign-out (until spec 006)
│   └── app.routes.ts         # /sign-in, /sign-in/code, guarded /
├── clinic/public/i18n/       # ar.json, en.json — sign-in keys
└── e2e/                      # sign-in.spec.ts, sign-in-states.spec.ts

deploy/compose/docker-compose.yml     # mailpit
deploy/compose/sql/create-logins.sql  # scs_signin_lookup, schema security and its grants
deploy/compose/.env.example           # SCS_AUTH__*, SCS_MAIL__*, SCS_SUPPORT__*, SCS_SEED__*
scripts/dev.ps1, scripts/dev.sh       # start mailpit; print the inbox address
docs/setup.md, docs/adding-a-module.md
```

**Structure Decision**: sign-in is a new module, `Identity`, because every later module depends
on it and none may reach into another. What every module needs at run time — the authentication
handler, the request-origin check, the mail sender — lives in the shared project; what describes
the signed-in person is published in `Scs.Identity.Contracts`. The screens are the first feature
folder of the clinic app; the components they need are built in `ui` so the operators console
(spec 010) and password screens (spec 004) reuse them.

**Terms**: the spec's "sign-in code" is the `SignInChallenge` row here; its "session proof" is
the renewal cookie's secret.

## Delivery Order

For `/speckit-tasks`. Tests first throughout; refusals, the lock, code limits and session ending
are test-first areas.

1. **Configuration, logins and mail** — options and their validation; `scs_signin_lookup`,
   schema `security`; Mailpit in compose; `IEmailSender` and the fake; the movable clock.
2. **Module, tables and the crossing** — `Identity` module; four tables with isolation and
   samples; the procedure; `DeclaredDatabaseCrossings` and the guardrail rule; the proof of R4
   first. Data-protection key table.
3. **Accounts** — email and password rules, hasher, `IStaffAccounts.CreateAsync`, uniqueness
   across clinics; seed. Story 1 foundations.
4. **Step one** — failing tests for the order of checks, the single answer and its timing, then
   `SignInService`; the lock. Stories 1, 2, 3.
5. **Step two** — failing tests for the code's limits, resend and re-checks, then `CodeService`
   and the code email. Stories 1, 4.
6. **Sessions** — the authentication handler, the per-request check, `ICurrentStaff`, the clinic
   context from the account; renewal, rotation and reuse; sign-out; the ending operations.
   Stories 1, 5, 6.
7. **Outer defences** — request limit, blocked networks, request-origin check. Story 7.
8. **Shared components** — frame, banner, fields, code input, buttons, dialog, against the
   design file. Story 8.
9. **Screens** — `AuthService`, guard, renewal in the interceptor; sign-in page, code page, help
   window, landing page; every state in both languages. Stories 1 to 5, 8.
10. **Events, cleanup and gates** — sign-in events and anonymous attempts; the daily cleanup
    job; the leak test; guides; `validation.md`.

## Complexity Tracking

No constitution violations to justify.

Choices recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| A stored procedure running as a dedicated user to find an account's clinic | Before sign-in there is no clinic, and without one the account table is invisible (spec 002) | Giving the application the operator connection would let any sign-in request read every clinic; a table of emails outside isolation would copy personal data to where no clinic protects it |
| Two cookies and rotation instead of one session cookie | The BRD requires detecting a stolen session and ending the account's sessions (CL-SH-125) | One long-lived cookie cannot tell a copy from the original |
| A database read on every request | Ending a session must take effect on the very next request (CL-SH-124) | A signed token trusted until it expires keeps a disabled person inside for minutes |
| Own account and session code instead of ASP.NET Core Identity | Four product rules (isolation per clinic, one answer, lock reported after a correct password, compulsory code with its own limit) run against its model | Each would be an override of the framework rather than a direct rule |
| The mail is sent inside the request | The form must say when the code could not be sent (FR-014) | A background job cannot report failure to the person waiting |
| An insert-only table outside any clinic for attempts on unknown emails | The BRD requires recording them, and they belong to no clinic | Writing them to a clinic-owned table is impossible; writing them only to logs loses them for the security page of spec 025 |
