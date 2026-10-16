# Implementation Plan: Operators and Their Roles

**Branch**: `011-operators-roles` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/011-operators-roles/spec.md`

## Summary

The operators console gets its people and its rules. A closed catalogue of 28 permissions and a
role per operator are put under every request of the console: a route answers only when it
names a permission the caller's role holds at that moment. On top of that the page «المشغلون»
is built with its two tabs — the team (list, one operator, invite, edit, suspend, reactivate,
send a link, unlock) and the roles (three system roles that cannot change; custom roles made
from the catalogue) — with the guards that keep anyone from granting what they do not hold and
the platform from losing its last «مسؤول عام».

It is built inside the `Operators` module of spec 010 and reuses what that spec made: the
invitation and its job, the links and their lives, ending sessions with a reason, the console's
frame. Three things are new to the platform: the operators' gate (the clinic's gate of spec
005, on the operators' scheme), the platform's own trail `audit.PlatformEntry` that spec 007
left for the first operator action, and a handful of form controls in the shared `ui` library.
The console also changes to Western digits everywhere, spec 010's screens included.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new as packages. EF Core, the platform's gates, mail template
(spec 094), job scheduler and audit writer; in the console the `ui` and `core` libraries,
Transloco and Tailwind.

**Storage**: SQL Server 2022 — `ops.Role` and `ops.RolePermission` (new), eight columns on
`ops.Operator`, `audit.PlatformEntry` (new, append-only); all platform-wide and listed with
their reasons. No clinic-owned table.

**Testing**: xUnit on the SQL Server fixture with the fake clock and captured mailbox. Access
tests first for every address; the grant rule's unit tests; the last-super-admin test with two
simultaneous requests. Architecture tests: every `/v1/ops/` route declares one of three; every
code of the operators group guards a route; the platform-wide list. Vitest for the console's
services, guards and components; Playwright for every screen and state in both languages at
1440 and 390 with pictures; a real-stack walk per `quickstart.md`.

**Target Platform**: the platform API and its worker; the operators console (`ops`, 4203
locally); the deployment tool.

**Project Type**: web application — an existing server module extended, a gate in the shared
project, a table in the Audit module, controls in the `ui` library, a feature in the `ops`
application.

**Performance Goals**: the list answers in under a second at 200 operators (SC-008); the gate
adds one indexed read to a request; a role change or suspension is on the other browser within
30 seconds (SC-004, by spec 010's polling).

**Constraints**: deny by default (FR-003); permissions read per request, never from the session
(FR-005); no link in any answer, record or log (FR-033); never zero active super admins, under
concurrency (FR-038); operators' and clinics' permissions never cross.

**Scale/Scope**: 53 functional requirements, 6 user stories; tens of operators, a dozen roles;
15 new addresses and 2 changed; 7 screens and 3 dialogs in two languages; 10 recorded actions;
1 email changed.

**Open items carried from research**: the unlock button's name and the drafted words marked
**(owner)** in `contracts/operator-screens.md`; differences for the designer and for the BRD
listed in `research.md`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, differences recorded | Requirements cite chapter 17 (§2, §3-9, §3-10, §4-3, §4-4, §5-هـ, §6-هـ, §7-هـ, §8-هـ, `OP-B-148`…`OP-B-438`) and SH-041…056. Every screen names its frames on page "Template — Operators"; `contracts/operator-screens.md` quotes their words in both languages. Eight owner decisions are in the spec's Clarifications; where the frames and the BRD differ, the plan says which is built and who updates the other |
| II. Tenant isolation in the database (non-negotiable) | Pass | No clinic-owned table is added or touched. Three platform-wide tables are added and listed one by one with a reason; they hold the platform's own staff, roles and actions and no clinic's data. The module establishes no clinic and has no crossing |
| III. The server decides; deny by default | Pass — this spec builds it for the console | Every `/v1/ops/` route declares open, own-account or a permission, or is refused; the gate runs before the handler and reads the role at each request. Everything the console draws disabled or hidden is refused by the server too, and each is held by a test (grants, own role, own account, last super admin, system roles, held roles) |
| IV. Financial records are immutable; changes are audited | Pass | No money moves. Ten actions are written to an append-only trail with actor, target, time and note |
| V. Secrets and patient data | Pass | Links exist only as hashes and in the email; no address returns one; entries and logs hold identifiers, never emails, names, links or reasons in logs. No patient data is involved |
| VI. Bilingual, complete screens | Pass | Every screen, dialog, state, toast and the changed email in Arabic and English, both directions, with loading, empty, nothing-matching, failed and no-access states; keyboard and screen reader covered; Western digits in both languages across the console (owner's decision, the BRD's rule for this console) |
| VII. Tested before merge, verified after implementation | Pass | Access and grant tests first. Full gates, the real-stack walk of `quickstart.md`, every screen beside its frame in both languages, every picture opened, `design-vs-real.png` and `validation.md` close the work |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged, with four things stated plainly. The
renaming of spec 010's `.OperatorsOnly()` to `.OperatorOwnAccount()` touches every route spec
010 declared; behaviour must not change and spec 010's tests are the proof. `audit.PlatformEntry`
is a table in another module (Audit) created by this spec, as spec 007 planned. Spec 094's
`FirstPassword` email gains two optional values and its samples one variant. And spec 010's
screens change in one respect only — their digits.

## Project Structure

### Documentation (this feature)

```text
specs/011-operators-roles/
├── plan.md
├── research.md                      # R1–R17
├── data-model.md                    # the catalogue, two tables, eight columns, the platform's trail
├── quickstart.md
├── contracts/
│   ├── operators-roles-api.md       # fifteen new addresses, two changed, the refusals
│   ├── operator-access.md           # for every module: declaring, asking, recording, the console's pieces
│   └── operator-screens.md          # each screen, its frames, words in both languages, states
├── checklists/requirements.md
└── tasks.md                         # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/
│   ├── Access/OperatorPermissions.cs        # NEW: the 28 codes, groups, names, what each needs
│   ├── Audit/…                              # + ten action codes, two target kinds
│   └── ErrorCodes.cs                        # + the OPS_* codes of the contract
├── shared/Scs.BuildingBlocks/
│   ├── Security/
│   │   ├── OperatorSessionAuthentication.cs # OperatorsOnly → OperatorOwnAccount
│   │   └── OperatorAccessControl.cs         # NEW: RequiresOperator, the gate, IOperatorAccess, start-up check
│   ├── Audit/AuditWriter.cs, AuditRecord.cs # + entries with no clinic; the operator as actor
│   ├── Tenancy/PlatformWideTables.cs        # + three lines
│   ├── Errors/ErrorMessages.cs              # + the words of the new codes
│   └── Mail/…                               # unchanged
├── modules/Audit/Scs.Audit/
│   ├── Infrastructure/Migrations/…_PlatformEntry.cs    # NEW table, append-only trigger
│   ├── Infrastructure/AuditReading.cs       # + an operator's last entries (IOperatorAuditReading)
│   └── Infrastructure/Sentences.cs          # unchanged (the console words its own sentences)
├── modules/Operators/
│   ├── Scs.Operators.Contracts/OperatorContracts.cs    # + role in the summary
│   └── Scs.Operators/
│       ├── Domain/Operator.cs               # + role, department, job title, inviter; Suspend, Reactivate, Unlock, ChangeRole
│       ├── Domain/Role.cs                   # NEW: Role, RolePermission, system keys
│       ├── Domain/RoleGrants.cs             # NEW: the one rule of what may be granted
│       ├── Infrastructure/
│       │   ├── Persistence/OperatorsDbContext.cs
│       │   ├── Migrations/…_OperatorsRoles.cs          # tables, columns, system roles, existing operators → super admin
│       │   ├── OperatorAccess.cs            # NEW: IOperatorAccess over the role, per request
│       │   ├── OperatorsDirectory.cs        # NEW: the list with cards and filters; one operator; activity
│       │   ├── OperatorManagement.cs        # NEW: invite, edit, suspend, reactivate, send link, unlock
│       │   ├── SuperAdminGuard.cs           # NEW: the lock and the count
│       │   ├── Roles.cs                     # NEW: list, read, options, create, edit, delete
│       │   ├── OperatorInvite.cs            # + role, fields, inviter, "send now"; the command gives super admin
│       │   ├── OperatorPasswords.cs         # + a link sent by a manager, one a minute
│       │   ├── OperatorEmails.cs            # FirstPassword + inviter and role
│       │   ├── OperatorEmailSamples.cs      # + the variant with an inviter
│       │   └── OperatorProfile.cs, OperatorSessions.cs   # + the role in the session's read
│       └── Api/
│           ├── OperatorEndpoints.cs         # declarations renamed; session and profile carry the role
│           ├── OperatorsEndpoints.cs        # NEW: /operators…
│           └── RolesEndpoints.cs            # NEW: /roles…, /permissions
├── tools/Scs.Migrator/                      # `operator invite` gives super admin; local seed unchanged otherwise
└── tests/
    ├── Scs.UnitTests/Operators/             # RoleGrants, the catalogue, the account's new rules, the email
    ├── Scs.ArchitectureTests/OperatorRulesTests.cs      # three declarations; every code of the group guards a route
    └── Scs.IntegrationTests/Operators/      # access first; management; roles; the last super admin; records
        + Fixtures/OperatorFixtures.cs       # operators with a given role or codes

src/web/
├── projects/ui/src/lib/
│   ├── controls/                            # + checkbox, select, switch, textarea, status mark (where missing)
│   └── list/                                # + KPI card, breadcrumb (where missing)
├── projects/ops/
│   ├── public/i18n/{ar,en}.json             # ops.operators.*, ops.roles.*, ops.permissions.*, ops.access.*
│   └── src/app/
│       ├── access/ops-access.service.ts, ops-can.guard.ts, ops-can.directive.ts   # NEW
│       ├── i18n/ops-format.ts               # NEW: numbers, dates, relative times — Western digits
│       ├── auth/ops-auth.service.ts         # the session carries the role and codes; re-read on navigation and 403
│       ├── shell/sections.ts, ops-shell.component.ts    # «المشغلون»; the role's name in the top bar
│       ├── operators/
│       │   ├── operators.service.ts
│       │   ├── operators.page.ts            # the two tabs
│       │   ├── operators-list.component.ts, operator-filters.component.ts
│       │   ├── operator.page.ts             # one operator
│       │   ├── operator-form.page.ts        # add and edit
│       │   ├── suspend.dialog.ts, send-link.dialog.ts
│       │   ├── roles-list.component.ts, role-form.page.ts, delete-role.dialog.ts
│       │   └── activity-sentences.ts
│       ├── sign-in/, password/, profile/    # digits only
│       └── app.routes.ts
└── e2e/
    ├── ops-platform.ts                      # answers the new addresses; operators with roles
    ├── ops-access.spec.ts, ops-operators.spec.ts, ops-roles.spec.ts
    └── ops-look.spec.ts                     # + every new screen and state; no Arabic-Indic digit in ops

docs/setup.md, ops/runbooks/operators.md     # roles; the command as recovery; who may do what
```

**Structure Decision**: everything that knows about operators stays in the `Operators` module
and the `ops` application. The gate goes into the shared project because every later module of
the console declares with it; the platform's trail goes into the Audit module because that is
where the trail lives; form controls go into `ui` only where a second console could use them.

**Terms**: "the catalogue" is `OperatorPermissions`; "a code" is one permission's identifier;
"a system role" is one of the three seeded roles; "super admin" is the role with system key
`super-admin`; "the gate" is the check before a handler; "the platform's trail" is
`audit.PlatformEntry`; "the console" is the `ops` application.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| A second gate beside the clinic's | The two audiences have different schemes, catalogues and own-account lists, and a permission of one must never satisfy the other | One gate with a switch makes a single wrong branch a way across; spec 010 kept the schemes apart for the same reason |
| A table in the Audit module created by this spec | An operator's action on the platform has no clinic; spec 007 left `audit.PlatformEntry` to the first spec with such an action | Entries in `audit.Entry` with no clinic break the rule that every row there is a clinic's and isolated |
| Three more platform-wide tables | Roles and the platform's trail belong to no clinic | Same as spec 010: rows without a clinic in clinic-owned tables break isolation |
| Renaming a declaration across spec 010's routes | "Operators only" stops being true once roles exist; left as it is, it would read as a permission-free door for any new route | Keeping both names invites the wrong one on a new route |
