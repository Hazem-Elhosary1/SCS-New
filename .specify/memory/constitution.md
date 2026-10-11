# Smart Clinic OS Constitution

Smart Clinic OS (SCS) is a multi-clinic cloud platform that Heta Systems sells by subscription to
clinics in Egypt. It consists of the clinic console, the patient-facing surfaces, the operators
console, and one platform (one API, one database isolated per clinic).

Requirement IDs below (`SH-nnn`) refer to BRD Volume 0, "Shared Foundation"
(`docs/brd/SCS-BRD-Shared`, source `docs/brd/source/BRD-0-shared.md`).

## Core Principles

### I. The BRD and the Design File Are the Source of Truth

- There are two sources and both are required. The BRD says what the product does and what it
  says: behaviour, rules and wording. The design file (Figma) says what every screen looks like:
  layout, components, icons, sizes, colours and the look of each state. Neither is enough
  without the other.
- Every spec MUST name the BRD chapter and the requirement IDs (`SH-`, `CL-`, `PT-`, `OP-`) it
  implements. A behaviour with no BRD requirement behind it MUST NOT be built until the BRD
  carries it.
- Every spec that delivers or changes a screen MUST also name the design file, the page and the
  frames it implements, and its plan and tasks MUST be written from those frames. A screen MUST
  NOT be built from the BRD's wording alone. A screen or state the design file does not draw is
  listed in the spec as undrawn, built from the drawn components, and raised with the owner.
- When sources conflict, precedence is: written owner decisions, then the BRD for behaviour and
  wording and the design file for appearance, then code. A conflict between the BRD and the
  design file, or an owner decision that differs from either, MUST be stated in the spec and
  recorded as an item for the owner to bring the BRD or the design file into line. Within
  `docs/`, the BRD in `brd/` prevails over everything in `plans/` and `archive/`.
- The BRD describes the product required, not the code that exists. The earlier implementation
  (NestJS, React, PostgreSQL) is a reference for behaviour and rules only: no code is ported from
  it and no requirement counts as done because that version has it (SH-103).
- Requirement status governs scope: **required** ships at launch, **later** MUST NOT be built
  into a launch spec, **conditional** MUST NOT be built before the named external party has
  lifted its condition (BRD §12).
- Items the BRD lists as out of scope (BRD §2) MUST NOT be introduced through a spec; they need
  an owner decision and a BRD change first.

Rationale: 92 specs are cut from one document and one design file. Traceability to both is what
keeps them consistent with each other, keeps the old codebase from silently becoming the
requirement, and keeps a screen that behaves correctly from shipping looking nothing like its
design.

### II. Tenant Isolation Enforced in the Database (NON-NEGOTIABLE)

- Each clinic's data MUST be isolated from every other clinic's at row level (SH-004), and the
  isolation MUST be enforced by the database itself, not by application code alone (SH-104).
- Isolation MUST fail closed: a request that reaches the database without a clinic context
  returns no rows and writes nothing. It MUST never fall back to all clinics.
- Every table that holds clinic-owned data MUST carry the isolation policy from the migration
  that creates it. An automated check MUST fail the build when such a table lacks it.
- An automated test MUST prove, from the application's own database identity, that one clinic
  cannot read or write another clinic's rows.
- Any identity or code path that bypasses isolation (pre-sign-in lookup, operators console) MUST
  be the narrowest that works, limited to named call sites, and justified in the plan that
  introduces it.

Rationale: the product holds medical records for many clinics in one database. A single
forgotten filter in application code must not be able to become a cross-clinic leak.

### III. The Server Decides: Permissions and Plan Entitlements

- A permission is not a role. Every control is guarded by one permission from the catalogue, and
  a role is only a named set of permissions (SH-050).
- The server MUST enforce permissions on every route by default; a route is open only when it is
  explicitly declared so (SH-056). Hiding a control in the UI is never the enforcement.
- A write permission requires the view permission that opens its destination (SH-054). Nobody
  changes their own role, assigns or copies a permission they do not hold, or removes the last
  owner (SH-053).
- Plans are sets of features from a database catalogue managed by operators. Adding or pricing a
  feature MUST NOT require a code change (SH-010), and no limit value or price is hard-coded
  (SH-020).
- Plan entitlement is checked at one enforcement point on the server. A feature outside the plan
  is refused with one consistent response (SH-014), and anything that raises a plan-limited count
  is refused at the limit whatever its entry point, clinic side or operator side (SH-017).
- Before sign-in the server MUST NOT reveal whether a clinic or an account exists (SH-043).
- Disabling an account or changing its role MUST end its sessions (SH-045).

Rationale: three front ends share one API. Rules enforced only in a client are rules a second
client, or a direct API call, does not have.

### IV. Financial Records Are Immutable and Audited

- An issued financial document (invoice, receipt, refund, credit note) MUST NOT be edited or
  deleted. A correction is a new entry carrying the name of whoever made it (SH-062).
- Subscription invoices freeze the issuer identity and tax rate of the day of issue and are
  corrected only by credit note (SH-035).
- Every write that moves money, or changes a permission or a plan, MUST write an audit record
  with the value before and after, the actor and the time (SH-092). The audit write is part of
  the same unit of work as the change.
- An amount is counted as collected only when it has been received. An insurer's share stays
  "awaiting collection" until the insurer pays (SH-063), and a doctor's share is computed on what
  was actually collected.
- The platform does not receive patient money and takes no percentage of it. Patient payments go
  through the clinic's own gateway (BRD §8).
- Reports read financial data and MUST NOT change it.

Rationale: clinics close shifts, pay doctors and file taxes from these numbers. A figure that
can be changed after the fact, or changed without a trace, cannot be relied on for any of them.

### V. Patient Data, Secrets and AI Stay Contained

- No patient data leaves to a model hosted outside Egypt without a written owner decision and
  the lawyer's opinion (SH-080).
- AI output is a suggestion. It MUST NOT be written to the patient record until a doctor has
  reviewed and signed it (SH-082). AI features for operators MUST NOT carry patient data
  (SH-083).
- Secrets MUST NOT appear in any UI, log or API response; payment gateway keys are write-only
  once entered (SH-091, SH-060). All traffic is HTTPS (SH-091).
- Operational logs MUST NOT contain personal data (SH-094).
- Lab results MUST NOT be sent over messaging channels (SH-070).
- Retention periods are platform settings per data category, not constants in code (SH-095).

Rationale: this is health data under Egyptian data-protection law, with several legal questions
still open (BRD §12). The defaults must be the safe ones until those are answered in writing.

### VI. Every Screen Is Bilingual and Complete

- Every user-facing surface MUST work in Arabic and English, with the language chosen per user
  and Arabic laid out right to left (SH-002). No user-facing string is hard-coded in one
  language.
- Every screen MUST define its states: loading, empty, error, offline, view-only, and not
  included in the plan (SH-097). A spec that omits one of them for a screen is incomplete.
- Wording follows the BRD glossary (BRD §13) and its copy rules: formal Arabic, no gendered
  address of the user, controls labelled with the verbal noun (SH-098). A term the glossary marks
  as "not said" MUST NOT appear in the UI.
- Money is in Egyptian pounds and time is in the Cairo time zone (SH-001).
- No module ships in the clinic console without its counterpart in the operators console or a
  complete flow that ends (SH-005). A feature that is not built is not shown in pricing (SH-021).

Rationale: the clinic team works in Arabic all day. A screen that is correct in English but
broken right to left, or that has no answer for an expired subscription, is not finished.

### VII. Tested Before Merge, Test-First Where It Hurts, Verified After Implementation

- Every acceptance criterion in a spec MUST be covered by an automated test before the spec is
  merged. A criterion with no test is not done.
- Test-first is mandatory for four risk areas: tenant isolation, permission enforcement, plan
  entitlements and limits, and anything that moves or reports money. For these, the tests are
  written and seen to fail before the implementation is written.
- Outside those four areas the order of tests and code is the developer's choice; the coverage
  of acceptance criteria is not.
- Refusal paths are tested, not only success paths: no permission, view-only, not included in
  the plan, read-only clinic, and limit reached.
- Tests that touch isolation run against a real SQL Server instance with the application's own
  database identity, never against an in-memory substitute.
- After implementation, and before a spec is reported as finished, it MUST be verified for real,
  beyond its automated tests: every automated suite is run in full; the feature's flows are
  walked against the running local stack; and every screen and state the spec delivers is laid
  beside its frame in the design file, in Arabic and in English, and the differences are fixed.
  Automated tests check behaviour and wording; they do not show that a screen matches its
  design.
- The result of that verification MUST be written in the spec's `validation.md`: what was run,
  what was walked, which frames were compared, and what was not checked. A verification step
  that was skipped is reported as skipped, never left as an open task and passed over.

Rationale: strict test-first on all 92 specs costs more than it returns, but the four risk areas
are where a mistake leaks data, grants access, gives away paid features or loses money. And a
suite that passes proves only what it asserts: the first sign-in screens passed every test with
their icons missing and their states unlike the design.

## Technology & Platform Constraints

These are owner decisions of 9 Oct 2026 and are constraints, not preferences.

| Area | Constraint | Source |
|---|---|---|
| Server | .NET 10 | SH-100 |
| Front ends (clinic, patient, operators) | Angular | SH-101 |
| Database | SQL Server | SH-102 |
| Build approach | Both systems built from scratch on the three technologies above | SH-103 |
| Hosting | AWS Middle East, designed so it can be moved to Egypt if the lawyer requires it | SH-090 |
| Topology | One API and one database, isolated per clinic | BRD §2 |
| Operators address | `hetasystems-scsadmin.app` | Project decision, 10 Oct 2026 |
| Staff address | `app.scs-heta.com`, one address for every clinic | Project decision, 10 Oct 2026; replaces SH-003 |
| Patient address | `scs-heta.com/<clinic>`, one path per clinic | Project decision, 10 Oct 2026; replaces SH-003 |
| API address | `/api` on each of the three addresses above, routed to the one API | Project decision, 10 Oct 2026 |
| Operations | Daily backup with a tested restore; fault and availability monitoring | SH-096 |
| Server architecture | Modular monolith: one deployable API, one module per BRD area | Project decision, 10 Oct 2026 |
| Repository | One monorepo: the API and one Angular workspace holding the three apps | Project decision, 10 Oct 2026 |
| Data access | EF Core with migrations; Dapper only for read-only report queries | Project decision, 10 Oct 2026 |
| Isolation mechanism | SQL Server Row-Level Security keyed on `SESSION_CONTEXT`, set per request | Project decision, 10 Oct 2026 |
| UI components | Shared component library on Tailwind CSS and Angular CDK, built to the Figma kit | Project decision, 10 Oct 2026 |
| Design source | The Figma files "Smart Clinic OS — Design System & Screens" (clinic and patient) and "SCS Ops Console — Admin Design System" (operators), read through the Figma connector | Owner decision, 11 Oct 2026 |
| Background work | Hangfire with SQL Server storage | Project decision, 10 Oct 2026 |
| Packaging and hosting | Docker containers on ECS Fargate; SQL Server on RDS; front ends as static files | Project decision, 10 Oct 2026 |

- All writes go through EF Core. A Dapper query MUST be read-only, MUST run on a connection that
  carries the clinic's `SESSION_CONTEXT`, and MUST NOT be used to get around Principle II.
- Schema changes are EF Core migrations checked into the repository. The Row-Level Security
  predicate and policy for a clinic-owned table are written in the migration that creates it.
- A background job that works on clinic data MUST set that clinic's context before it touches
  the database, exactly as a request does. A job that spans clinics processes them one at a time.
- A third-party UI component library MUST NOT be added. A component missing from the shared
  library is added to the shared library.
- Application code MUST NOT depend on an AWS-only service where a portable equivalent is in this
  table (jobs, database, containers), so that the move to Egypt stays possible (SH-090).

- There are no per-clinic subdomains. On the staff address the clinic is taken from the signed-in
  account, never from the address. On the patient address the clinic is taken from the first
  path segment. This replaces SH-003; BRD Volume 0 MUST be updated to match before specs 002,
  003 and 071 are written.
- Staff screens and patient screens MUST be served from different browser origins, so that a
  flaw in a public patient page cannot reach a signed-in staff session.
- Each front end calls the API on its own origin under `/api`. No front end calls the API
  across origins in test or production.
- Each server module follows Clean Architecture layering (domain, application, infrastructure,
  API). Dependencies point inward: the domain layer references no framework, database or web
  type.
- A module reaches another module only through that module's published contract. It MUST NOT
  read or write another module's tables or reference its internal types. Automated architecture
  tests MUST fail the build when a boundary or a layer rule is broken.
- The three front ends are applications in one Angular workspace. Code used by more than one of
  them lives in a shared library, not copied between apps.
- Splitting a module into a separately deployed service requires a constitution amendment.
- Deviating from a constraint in this table requires an owner decision and a constitution
  amendment before the plan that depends on it is approved.
- A plan MUST NOT bind the system to a hosting feature that would prevent the move to Egypt
  without recording that as a justified exception.

## Development Workflow & Quality Gates

- Work is delivered as the 92 specs in 10 phases listed in `docs/brd/SCS-SpecKit-Phases.md`.
  Each row is one spec created with `/speckit-specify`; its number and name are the name of its
  branch and of its folder (for example `specs/035-booking-calendar`).
- The "depends on" column sets the order. A spec MUST NOT be implemented before the specs it
  depends on. Phase 9 specs MUST NOT be built before their external condition is lifted.
- Branching: `main` holds production-ready code only and `develop` is the integration branch.
  Each spec branch is cut from `develop` and returns to it by pull request; `develop` reaches
  `main` by pull request at release. Nobody commits directly to `main` or `develop`.
- Language: the constitution, specs, plans, tasks, code, comments and commit messages are written
  in English. User-facing wording is quoted in Arabic exactly as the BRD and its glossary give it.
- Every plan produced by `/speckit-plan` MUST include a Constitution Check that states, for each
  of Principles I–VII, how the feature complies or why the principle does not apply. A violation
  is recorded with its justification and the simpler alternative that was rejected.
- A change is mergeable only when all of these pass in CI: build, lint, automated tests,
  architecture tests, and the tenant-isolation guardrail and isolation test required by
  Principle II.
- A spec is done only when, in addition, the verification after implementation required by
  Principle VII is recorded in its `validation.md`, including the comparison of each of its
  screens with the design file. `/speckit-implement` is not complete until that is written.
- Acceptance criteria in a spec MUST be testable and MUST cover the refusal paths: no permission,
  view-only, not included in the plan, and read-only clinic.
- A schema change that adds a clinic-owned table and the isolation policy for that table are one
  change. They are never split across merges.

## Governance

- This constitution supersedes other development practices for SCS. Where a plan, a task list or
  existing code conflicts with it, the constitution prevails, and the BRD prevails over the
  constitution on what the product must do.
- **Amendment**: an amendment is proposed in writing with its reason, approved by the product
  owner (Heta Systems), and applied through `/speckit-constitution`. A change to BRD Volume 0
  that alters a rule cited here MUST be accompanied by the matching amendment. An amendment that
  invalidates approved specs or plans MUST list them and say how each is brought back into line.
- **Versioning**: semantic versioning. MAJOR for removing or redefining a principle in a way that
  is not backward compatible; MINOR for a new principle or section, or materially expanded
  guidance; PATCH for clarification and wording.
- **Compliance review**: compliance is checked at `/speckit-plan` (Constitution Check), at
  `/speckit-analyze` before implementation, and in code review before merge. A reviewer MUST
  block a change that violates a principle without a recorded justification. Violations of
  Principle II are never justifiable.
- **Decisions outside the BRD**: the server architecture, repository layout, branching, testing
  discipline, artifact language and the stack choices marked "Project decision" in this document
  are project decisions rather than BRD requirements. They change by amendment here, without a BRD change.
- **Runtime guidance**: `docs/brd/SCS-BRD-Shared` for shared rules, then the volume for the
  system being built (`SCS-BRD-Clinic` or `SCS-BRD-Ops`), the design file for the look of its
  screens, and `docs/brd/SCS-SpecKit-Phases.md` for the order of work.

**Version**: 1.2.0 | **Ratified**: 2026-10-10 | **Last Amended**: 2026-10-11
