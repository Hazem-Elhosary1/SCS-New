# Feature Specification: Platform Foundation

**Feature Branch**: `001-platform-foundation`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: none typed. Taken from row `001-platform-foundation` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "the platform skeleton — the server,
front-end and database projects, the environments, logs and monitoring, backup."

**BRD source**: Volume 0 (Shared Foundation) §11 and §11-b — SH-001, SH-002, SH-090, SH-091,
SH-094, SH-096, SH-098, SH-100, SH-101, SH-102, SH-103.

**Depends on**: nothing. Every other spec depends on this one.

**Design sources** (for FR-031 to FR-034 only; this spec draws no business screen):

- Clinic console and patient surfaces — Figma "Smart Clinic OS — Design System & Screens":
  <https://www.figma.com/design/4HLWEqlna9sbn68276LHYt/?node-id=39-2>
- Operators console — Figma "SCS Ops Console — Admin Design System":
  <https://www.figma.com/design/SetO9EhDivNcmurAMfcflY/?node-id=65-2>

## Overview

Smart Clinic OS is built from scratch (SH-103). Before any business screen exists, the team needs
a skeleton that all 91 later specs stand on: the three front ends and the one platform running
together, a path that carries a change safely from a developer to production, and the operational
safety net the BRD requires from day one — encrypted traffic, protected secrets, logs free of
personal data, monitoring, and a daily backup whose restore has actually been tried.

This spec delivers no working screen for a clinic, a patient or an operator. Its users are the
development team and the Heta operations team. Its value to the business is that every feature
built afterwards inherits the same safe behaviour without rebuilding it.

**Out of scope** (each has its own spec): clinic data isolation and clinic addresses (002),
sign-in and sessions (003, 004, 010), permissions (005), the console shell and navigation (006),
the audit trail (007), notifications (008), file storage (009).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the whole system from a clean copy (Priority: P1)

A developer who has just joined gets a copy of the project, follows one written guide, and has
the platform, its database and the three front ends (clinic console, patient surfaces, operators
console) running together on their own machine. Each front end opens to a placeholder page that
confirms it reached the platform.

**Why this priority**: nothing else can be built or demonstrated until the skeleton runs. It is
also the proof that the three applications and the platform are wired to each other correctly.

**Independent Test**: on a machine that has never held the project, follow the guide only, and
confirm all three front ends open and each shows the platform as reachable.

**Acceptance Scenarios**:

1. **Given** a clean machine with the documented prerequisites, **When** the developer follows
   the setup guide, **Then** the platform, its database and all three front ends are running
   without any step that is not in the guide.
2. **Given** the system is running locally, **When** the developer opens each of the three front
   ends, **Then** each shows a placeholder page stating that the platform is reachable.
3. **Given** the system is running locally, **When** the database is stopped, **Then** the
   platform reports itself as alive but not ready, and each front end shows its error state
   instead of a blank page.
4. **Given** a developer has no production credentials, **When** they set up locally, **Then**
   setup still succeeds, using only sample values that ship with the project.

---

### User Story 2 - A change reaches users only through the gates (Priority: P1)

A developer proposes a change. It is checked automatically; if any check fails the change cannot
be merged. Once merged it is released to a test environment, and the same tested package is later
promoted to production — never rebuilt, never edited by hand on the way.

**Why this priority**: the constitution makes the quality gates a condition of every merge. They
must exist before the first business spec is written, or the first 91 merges go unchecked.

**Independent Test**: propose one change that breaks a check and one that passes; confirm the
first is blocked, the second reaches the test environment, and the identical package can be
promoted to production and rolled back.

**Acceptance Scenarios**:

1. **Given** a proposed change that fails any automated check, **When** someone tries to merge
   it, **Then** the merge is refused and the failing check is named.
2. **Given** a proposed change that passes every check, **When** it is merged to the integration
   line, **Then** it is released to the test environment without manual steps.
3. **Given** a release that has run in the test environment, **When** it is promoted, **Then**
   production receives the same package that was tested, and the promotion requires an explicit
   human approval.
4. **Given** a release in production that misbehaves, **When** the operations engineer rolls
   back, **Then** the previous release is serving users again within the agreed time.
5. **Given** any environment, **When** someone asks which version is running, **Then** the
   platform and each front end report their version and the time they were released.

---

### User Story 3 - See trouble and trace it (Priority: P2)

An operations engineer is told that a clinic saw an error. They look up the reference shown on the
error, find every log entry for that one request across the system, and see what failed — without
ever reading a patient's name, a phone number or a secret in the logs. Separately, when the
platform goes down or errors spike, the engineer is alerted without anyone having to report it.

**Why this priority**: required by SH-094 and SH-096, and it has to be in the skeleton: logging
that leaks personal data cannot be cleaned up after ten modules have copied the pattern.

**Independent Test**: trigger a failing request that carries personal data and a secret; confirm
the user sees a reference, the reference finds the full trail, and neither the personal data nor
the secret appears anywhere in the trail. Then stop the platform and confirm an alert arrives.

**Acceptance Scenarios**:

1. **Given** a request fails, **When** the user sees the error, **Then** the error carries a
   reference that the operations engineer can use to find every log entry for that request.
2. **Given** a request contains personal data or a secret, **When** it is logged, **Then** the
   log entry shows neither, whether the request succeeded or failed.
3. **Given** the platform in production stops answering, **When** five minutes have passed,
   **Then** the operations team has been alerted.
4. **Given** the rate of failed requests rises above the agreed threshold, **When** it stays
   there, **Then** the operations team is alerted and the alert says which environment.
5. **Given** an unexpected failure inside the platform, **When** the response is returned,
   **Then** it contains no internal detail — no file paths, no query text, no stack trace.

---

### User Story 4 - Lose nothing, and prove it (Priority: P2)

Every day the production data is backed up without anyone starting it. On a regular schedule the
operations engineer restores the latest backup into a separate, empty environment, checks that
the data is complete, and records the result. If a backup does not run, the team is alerted.

**Why this priority**: required by SH-096, which asks for a restore that has been tried, not
only a backup that exists. It ranks below the first two stories because there is no clinic data
to lose until later specs ship, but it must be in place before the first clinic is created.

**Independent Test**: put known data in, wait for the daily backup, restore it into an empty
environment, and confirm the known data is there; then skip a backup and confirm an alert.

**Acceptance Scenarios**:

1. **Given** production is running, **When** a day passes, **Then** a backup of that day exists
   and nobody had to start it.
2. **Given** the latest backup, **When** the operations engineer runs the documented restore into
   a separate environment, **Then** the data present at backup time is there and the restore
   finishes within the agreed time.
3. **Given** a restore test has been run, **When** it finishes, **Then** its date, who ran it,
   how long it took and whether it passed are recorded.
4. **Given** a daily backup did not complete, **When** the next morning comes, **Then** the
   operations team has already been alerted.
5. **Given** a backup, **When** someone without operations access tries to read it, **Then**
   they cannot.

---

### User Story 5 - Every later feature inherits the same behaviour (Priority: P3)

A developer starting spec 006 or 035 does not decide how errors look, how Arabic is laid out, how
dates and money are shown, or what a screen does while loading or offline. The skeleton already
answers these once, and each placeholder page demonstrates the answers.

**Why this priority**: it prevents 91 specs from each inventing these conventions. It is P3
because the system runs and ships without it, but every week it is missing costs rework later.

**Independent Test**: on each placeholder page, switch language and confirm the layout flips;
force each screen state and confirm it appears; confirm a date and an amount are shown in the
agreed form.

**Acceptance Scenarios**:

1. **Given** a placeholder page in Arabic, **When** it is displayed, **Then** it is laid out
   right to left; **When** the language is switched to English, **Then** it is laid out left to
   right and every visible text has changed language.
2. **Given** any front end, **When** it shows a date, a time or an amount of money, **Then** the
   time is Cairo time and the amount is in Egyptian pounds.
3. **Given** a placeholder page, **When** each of the six screen states is forced (loading,
   empty, error, offline, view-only, not included in the plan), **Then** each has a defined
   appearance that later specs reuse.
4. **Given** any refused or failed request to the platform, **When** a front end receives the
   answer, **Then** the answer has the same shape every time: a stable code, a message in the
   user's language, and the reference from User Story 3.
5. **Given** a visible text on any front end, **When** the project is checked, **Then** the text
   exists in both languages and none is written directly into a page in one language.

---

### Edge Cases

- The platform is running but its database is not: it must report "alive, not ready" and must
  not be sent user traffic until it is ready again.
- A required configuration value or secret is missing at start-up: the platform refuses to start
  and names the missing value, without printing any secret it did have.
- A release fails part-way through: users keep being served by the previous release; no user is
  left on a half-released system.
- A change to the database structure is released and then the release is rolled back: the
  previous release must still work against the changed structure, or the rollback procedure must
  say exactly how the structure is taken back.
- A front end is open in a user's browser when a new release goes out: the user is not left with
  a broken page; they are moved to the new version at a safe moment.
- A user asks for a language that a text has not been translated into: the text falls back to
  the other language rather than showing a blank or a code, and the missing text is reported.
- A request arrives over an unencrypted connection: it is moved to an encrypted one and never
  served unencrypted.
- A failure happens before a reference could be assigned (for example, a malformed request):
  the user still receives the uniform error shape.
- The monitoring service itself is unavailable: the platform keeps serving users.
- The restore test finds the backup unusable: this is treated as a production incident, not as a
  failed test.
- Personal data arrives in a place nobody expected (an address line, a free-text note inside an
  error): it is still kept out of the logs.
- Test and local environments need realistic data: they use made-up data only, never a copy of
  production patient data.

## Requirements *(mandatory)*

### Functional Requirements

**The skeleton**

- **FR-001**: The project MUST contain one platform, one database and three front ends — clinic
  console, patient surfaces, operators console — as separate applications that are built, run
  and released from a single source repository (SH-100, SH-101, SH-102, BRD §2).
- **FR-002**: The system MUST be built new; no code is carried over from the earlier
  implementation, which serves only as a reference for behaviour (SH-103).
- **FR-003**: Each front end MUST have a placeholder page that shows whether it can reach the
  platform, and the platform MUST be able to report whether it can reach its database.
- **FR-004**: The platform MUST report two separate conditions: that it is alive, and that it is
  ready to serve requests. It is ready only when everything it depends on is reachable.
- **FR-005**: The platform and each front end MUST report the version that is running and when
  it was released.
- **FR-006**: A written setup guide MUST take a developer from a clean machine to the whole
  system running locally, using only sample configuration that ships with the project.

**Environments and release**

- **FR-007**: There MUST be three environments — local, test and production — each with its own
  data, its own configuration and its own secrets. No environment can read another's data.
- **FR-008**: Production patient data MUST NOT be copied into the local or test environments.
- **FR-009**: Every proposed change MUST pass automated checks before it can be merged: it
  builds, it meets the coding rules, its automated tests pass, and the structural rules set by
  the constitution hold. A failed check blocks the merge.
- **FR-010**: A merged change MUST be released to the test environment automatically. Release to
  production MUST use the same package that ran in test and MUST require explicit approval.
- **FR-011**: A release MUST NOT interrupt users who are in the middle of their work, and a
  failed release MUST leave the previous release serving users.
- **FR-012**: The operations team MUST be able to return production to the previous release by a
  documented procedure.
- **FR-013**: Changes to the database structure MUST be recorded in the repository, applied in
  order, applied the same way in every environment, and never applied by hand in production.
- **FR-014**: Production MUST be hosted in the AWS Middle East region, and nothing in the system
  may prevent it from being moved to hosting inside Egypt if the lawyer requires it (SH-090).

**Security baseline**

- **FR-015**: Every connection to the platform and to every front end MUST be encrypted; an
  unencrypted request is redirected and never served (SH-091).
- **FR-016**: Secrets MUST be kept outside the source repository and outside the released
  package, and MUST NOT appear in any screen, log or response (SH-091).
- **FR-017**: The platform MUST refuse to start when a required configuration value is missing,
  and MUST name the missing value without revealing any secret.
- **FR-018**: A response to an unexpected failure MUST NOT reveal internal detail.
- **FR-019**: Technical documentation of the platform's interface MUST NOT be reachable by the
  public in production.

**Logs and monitoring**

- **FR-020**: Every request MUST be given a reference that appears in every log entry it causes
  and is returned to the caller, so one request can be traced from end to end.
- **FR-021**: Operational logs MUST NOT contain personal data — names, mobile numbers, emails,
  national IDs, addresses, clinical content — or secrets. The protection MUST apply by default
  to everything logged, not rely on each developer remembering (SH-094).
- **FR-022**: Logs from the platform in test and production MUST be collected in one place where
  the operations team can search them by reference, time and severity.
- **FR-023**: The system MUST watch the availability of the platform and of each front end from
  outside, and MUST alert the operations team when one is unavailable (SH-096).
- **FR-024**: The system MUST alert the operations team when the rate of failed requests or the
  response time passes an agreed threshold, and every alert MUST name the environment (SH-096).
- **FR-025**: An unhandled failure in a front end MUST be reported to the operations team with
  the same reference scheme, and without personal data.

**Backup and restore**

- **FR-026**: Production data MUST be backed up at least once a day without manual action
  (SH-096).
- **FR-027**: Backups MUST be kept separately from the system they protect, encrypted, and
  readable only by the operations team.
- **FR-028**: A documented restore procedure MUST exist, and the restore MUST be tested on a
  schedule into a separate environment, with each test's date, operator, duration and result
  recorded (SH-096).
- **FR-029**: The operations team MUST be alerted when a daily backup does not complete.

**Shared conventions every later spec inherits**

- **FR-030**: Every refusal or failure returned by the platform MUST have one shape: a stable
  code, a message in the user's language, and the request reference.
- **FR-031**: Each front end MUST support Arabic and English from the first page, with Arabic
  laid out right to left, the language switchable, and no visible text written in one language
  only (SH-002).
- **FR-032**: Dates and times MUST be shown in Cairo time and money in Egyptian pounds, in one
  consistent form across the three front ends (SH-001).
- **FR-033**: The three front ends MUST share one definition of the six screen states — loading,
  empty, error, offline, view-only, not included in the plan — which later specs reuse rather
  than redefine (SH-097).
- **FR-034**: Visible text in the placeholder pages MUST follow the BRD copy rules: formal
  Arabic, no gendered address, controls labelled with the verbal noun (SH-098).
- **FR-035**: Work that runs on a schedule or in the background MUST have one common way to be
  started, retried on failure, and observed by the operations team, ready for later specs to use.

### Key Entities

- **Environment**: one complete, separate running copy of the system — local, test or
  production — with its own data, configuration and secrets.
- **Release**: one tested package of the platform and front ends, with a version and a release
  time, that moves unchanged from test to production and can be replaced by the one before it.
- **Health status**: what the platform reports about itself — alive, and ready or not ready —
  and which dependency is the reason when it is not ready.
- **Request reference**: the identifier given to one request, shown to the user on an error and
  carried by every log entry that request causes.
- **Operational log entry**: a record of something the system did, with time, severity,
  environment and request reference, and with no personal data or secret in it.
- **Alert**: a notice sent to the operations team when availability, failure rate, response time
  or a backup crosses its threshold; it names the environment and the condition.
- **Backup**: a daily copy of production data, held apart from production, encrypted, kept for
  the retention period.
- **Restore test record**: the date, operator, duration and outcome of one trial restore.
- **Structure change**: one ordered, recorded change to the shape of the database, applied the
  same way in every environment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer new to the project has the whole system running on a clean machine in
  under 30 minutes, following the written guide and asking nobody.
- **SC-002**: 100% of changes that fail an automated check are blocked from merging; none
  reaches the test environment.
- **SC-003**: A merged change is available in the test environment within 15 minutes, with no
  manual step.
- **SC-004**: A release reaches production with no interruption noticed by a user who is working
  at the time.
- **SC-005**: Production can be returned to the previous release in under 15 minutes.
- **SC-006**: An operations engineer given only the reference from a user's error finds the full
  trail of that request in under 5 minutes.
- **SC-007**: A scan of the logs produced by a test run that submits personal data and secrets
  in every input finds zero occurrences of either.
- **SC-008**: The operations team is alerted within 5 minutes of production becoming
  unavailable.
- **SC-009**: A backup exists for every day production has run; on any day, no more than 24
  hours of data could be lost.
- **SC-010**: A trial restore of the latest backup completes within 4 hours and is carried out
  at least once every three months, with every trial recorded.
- **SC-011**: Production is available at least 99.5% of each calendar month, as measured from
  outside.
- **SC-012**: Each of the three placeholder pages passes a check in both languages: correct
  direction, no untranslated text, and all six screen states shown.
- **SC-013**: The first business spec built on this foundation (002) adds no new decision about
  environments, release, logging, error shape or language handling.

## Assumptions

- The BRD does not fix numeric operational targets. The following are working defaults, to be
  confirmed by the product owner: availability 99.5% per month; at most 24 hours of data loss;
  restore within 4 hours; trial restore every three months; backups kept 30 days; operational
  logs kept 30 days; rollback within 15 minutes.
- Three environments are enough for launch: local, test and production. A separate
  pre-production environment can be added later without changing this spec.
- The technology and architecture are fixed by the constitution (Technology & Platform
  Constraints) and are deliberately not repeated here; `/speckit-plan` applies them.
- The retention of clinic and patient data categories (SH-095) is conditional on the lawyer and
  belongs to later specs. This spec covers only operational logs and backups.
- Clinic-specific web addresses (SH-003) and data isolation (SH-004, SH-104) are delivered by
  spec 002; this spec only has to leave room for them.
- Limiting the rate of sign-in attempts (SH-041) is delivered with sign-in in spec 003.
- The appearance of the six screen states and of shared components is taken from the two Figma
  files listed under Design sources. Both files carry Arabic variants of their components, so
  the right-to-left layout is designed, not derived. Where the clinic file and the operators
  file differ, each front end follows its own file.
- The placeholder pages are replaced by real screens in later specs and are never shown to a
  clinic, a patient or an operator.
- The Heta operations team is the recipient of alerts; who is on call and through which channel
  they are reached is an operating decision outside this spec.
- An AWS account in the Middle East region, a source-hosting service that can run automated
  checks, and the domains `scs-heta.com` and `hetasystems-scsadmin.app` are available to the
  team.
- Addresses follow the constitution (v1.1.0), which replaces SH-003: operators on
  `hetasystems-scsadmin.app`, staff on `app.scs-heta.com`, patients on `scs-heta.com/<clinic>`.
  This spec stands up the three addresses; resolving the clinic belongs to specs 002 and 003.
