# Feature Specification: Clinic Tenancy and Data Isolation

**Feature Branch**: `002-clinic-tenancy-isolation`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: none typed. Taken from row `002-clinic-tenancy-isolation` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "the clinic as the root of the data,
its two addresses, and isolation enforced in the database."

**BRD source**: Volume 0 (Shared Foundation) §2 and §11-b — SH-003, SH-004, SH-104; also SH-094
(no personal data in logs) and SH-083 (operator features carry no patient data) where they
touch cross-clinic access.

**Depends on**: 001-platform-foundation.

**Open dependency on the BRD**: SH-003 as written gives every clinic its own subdomain. The
constitution (v1.1.0, owner decision of 10 Oct 2026) replaces that with one staff address for
all clinics and one patient path per clinic. This spec follows the constitution. SH-003 must be
updated to match before this spec is approved (spec 001, task T110).

## Overview

Smart Clinic OS keeps the records of many clinics in one database. The single most damaging
thing the product could do is show one clinic another clinic's patients. This spec makes the
clinic the owner of everything recorded for it, and makes the separation between clinics a
property of the database itself, so that a mistake in any later feature fails safe: it shows
nothing, rather than showing someone else's data.

It delivers three things every later spec stands on:

1. **The clinic** as a record: who it is, and the short name that identifies it in the patient
   address.
2. **Knowing which clinic a piece of work is for**: from the patient address, and ready to be
   taken from the signed-in account when sign-in arrives.
3. **Isolation that cannot be forgotten**: every record that belongs to a clinic is protected
   where it is stored, and the build refuses a change that adds such a record without the
   protection.

No screen for clinic staff, patients or operators is delivered here. The users are the clinics
and patients whose data is being kept apart, the Heta operators who must sometimes work across
clinics, and the developers of the 90 specs that follow.

**Out of scope** (each has its own spec): signing in and taking the clinic from the account
(003, 010); creating a clinic from the operators console and inviting its owner (015); the
clinic's subscription states and what each allows (016); branches (028); the public booking page
itself (071); the audit trail of business actions (007).

## Clarifications

### Session 2026-10-10

- Q: Should a clinic's short name be allowed in Arabic letters, or in English letters only? → A: English only — lowercase letters, digits and hyphens, 3 to 40 characters.
- Q: When the support team changes a clinic's short name, what should happen to the old address? → A: It keeps working forever and moves the patient to the new one; the old name is never given to another clinic.
- Q: How long should the record of operators' cross-clinic access be kept? → A: A platform setting, starting at 5 years; changeable without a code change.
- Q: When a patient opens the address of a clinic that exists but is not currently allowed to operate, what should be shown? → A: The same "not available" page as for an unknown address; nothing reveals that the clinic exists.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One clinic can never see another's data (Priority: P1)

Two clinics use the system on the same day. Whatever a member of Clinic A does — list, search,
open by a guessed identifier, edit, delete — only Clinic A's records are ever returned or
changed. If, through a defect, a piece of work reaches the stored data without the system
knowing which clinic it is for, it gets nothing back and can change nothing.

**Why this priority**: it is the non-negotiable principle of the project (constitution II) and
the reason the product can be trusted with medical records at all.

**Independent Test**: create two clinics with records; act as Clinic A and try every way of
reaching Clinic B's records; then act with no clinic at all. Nothing of B is returned or
changed, and nothing at all is returned with no clinic.

**Acceptance Scenarios**:

1. **Given** Clinic A and Clinic B each hold records, **When** work is done on behalf of
   Clinic A, **Then** only Clinic A's records are returned, however the request is phrased.
2. **Given** work on behalf of Clinic A, **When** it asks for a Clinic B record by its exact
   identifier, **Then** the answer is the same as for a record that does not exist.
3. **Given** work on behalf of Clinic A, **When** it tries to change or delete a Clinic B
   record, **Then** nothing is changed and no error reveals that the record exists.
4. **Given** work on behalf of Clinic A, **When** it tries to create a record marked as
   belonging to Clinic B, or to move one of its own records to Clinic B, **Then** it is refused.
5. **Given** work that reaches the stored data with no clinic established, **When** it reads,
   **Then** it receives no records; **When** it writes, **Then** nothing is written.
6. **Given** the isolation is in force, **When** someone connects to the stored data with the
   application's own identity, outside the application, **Then** the same rules apply: no clinic
   established means no records.

---

### User Story 2 - A new kind of record cannot ship unprotected (Priority: P1)

A developer building a later feature adds a new kind of record that belongs to a clinic —
appointments, say. If they forget to protect it, the automated checks fail and the change cannot
be merged. They do not have to remember; the project remembers for them.

**Why this priority**: isolation that depends on 90 specs each remembering a step will
eventually be forgotten once. The guardrail is what makes User Story 1 stay true.

**Independent Test**: add a clinic-owned record type without its protection and confirm the
checks fail naming it; add the protection and confirm they pass; confirm the isolation test
covers the new type without being edited.

**Acceptance Scenarios**:

1. **Given** a change that adds a clinic-owned record type with no isolation rule, **When** the
   automated checks run, **Then** they fail and name the unprotected record type.
2. **Given** a change that adds the record type and its isolation rule together, **When** the
   checks run, **Then** they pass.
3. **Given** a change that adds the record type in one step and its isolation rule in a later
   step, **When** the checks run on the first step alone, **Then** they fail.
4. **Given** any clinic-owned record type that exists, **When** the isolation test runs,
   **Then** that type is included automatically, with no list to keep up to date by hand.
5. **Given** a record type that deliberately belongs to no clinic (platform-wide data), **When**
   it is added, **Then** it must be declared as such by name, and the declaration is visible in
   review.

---

### User Story 3 - A patient address leads to the right clinic (Priority: P2)

A patient opens their clinic's address. The system recognises the clinic from the address and
everything the patient then sees and does belongs to that clinic alone. An address that matches
no clinic shows a plain "not available" page that gives nothing away.

**Why this priority**: it is the only way a clinic is known before anyone has signed in, and
spec 071 (public booking page) is built directly on it.

**Independent Test**: open the addresses of two clinics and confirm each resolves to its own
clinic; open a misspelt address, a reserved word and a retired name and confirm the same
"not available" answer each time.

**Acceptance Scenarios**:

1. **Given** a clinic whose short name is `noor`, **When** a patient opens the patient address
   with that name, **Then** the work that follows is done on behalf of that clinic.
2. **Given** an address whose name matches no clinic, **When** it is opened, **Then** a "not
   available" page is shown, in both languages' conventions, identical for every unknown name.
3. **Given** a name written with different letter case or surrounding spaces, **When** the
   address is opened, **Then** it resolves to the same clinic as the canonical form.
4. **Given** the staff address, **When** it is opened, **Then** no clinic is taken from the
   address; the clinic will come from the signed-in account (spec 003).
5. **Given** a patient is using Clinic A's address, **When** a request arrives that names
   Clinic B's records, **Then** User Story 1 applies: nothing of Clinic B is reachable.
6. **Given** a clinic that is suspended or cancelled, **When** a patient opens its address,
   **Then** the page and the answer behind it are identical to those for an unknown address.

---

### User Story 4 - The clinic and its short name (Priority: P2)

Heta's support team gives each clinic a short name that appears in its patient address. The
name is unique, readable, and cannot collide with a word the system itself uses. If a clinic's
name is changed, the old address keeps leading to the same clinic and is never given to another.

**Why this priority**: the short name is the public face of a clinic and the key that User
Story 3 resolves; getting its rules wrong later means breaking printed addresses and QR codes.

**Independent Test**: register clinics with valid, invalid, duplicate and reserved names;
change one clinic's name and open both the old and the new address.

**Acceptance Scenarios**:

1. **Given** a proposed short name that is valid and unused, **When** the clinic is registered,
   **Then** it is accepted and the patient address works at once.
2. **Given** a proposed name already used by another clinic, now or in the past, **When** it is
   submitted, **Then** it is refused.
3. **Given** a proposed name that is a reserved word or breaks the format rules, **When** it is
   submitted, **Then** it is refused with a reason that says which rule.
4. **Given** a clinic whose short name is changed, **When** a patient opens the old address,
   **Then** they are taken to the new one, and the old name remains that clinic's alone.
5. **Given** a clinic record, **When** it is read on behalf of that clinic, **Then** it is
   returned; **When** it is read on behalf of another clinic, **Then** it is not.

---

### User Story 5 - Operators work across clinics through one narrow, visible door (Priority: P2)

Heta operators must list clinics, register them and support them, which means working across
the boundary that User Story 1 puts up. They do so through a small number of named operations.
Each use is recorded with who, when and which clinic. Nothing else in the system can cross the
boundary, and the automated checks fail if a new crossing appears without being declared.

**Why this priority**: without a deliberate door, developers will make accidental ones. With
it, every cross-clinic access is countable and reviewable.

**Independent Test**: use a declared cross-clinic operation and confirm it works and is
recorded; attempt the same from a part of the system that is not declared and confirm it is
refused at build time.

**Acceptance Scenarios**:

1. **Given** a declared cross-clinic operation, **When** an operator uses it, **Then** it
   succeeds and a record is kept of the operator, the operation, the clinic and the time.
2. **Given** a part of the system that has not been declared, **When** it tries to read across
   clinics, **Then** the automated checks fail and name it.
3. **Given** the record of a cross-clinic access, **When** it is read, **Then** it contains no
   patient data, only identifiers.
4. **Given** the lookup that finds a clinic from the patient address before any clinic is
   established, **When** it runs, **Then** it can read the clinic's short name and state and
   nothing else.
5. **Given** an access record older than the retention period, **When** the daily clean-up
   runs, **Then** it is removed; a record inside the period is untouched.

---

### User Story 6 - Background work keeps to one clinic at a time (Priority: P3)

Work that runs on a schedule — reminders, renewals, summaries in later specs — is done for one
clinic at a time, under the same isolation as a person's request. A job that has to cover every
clinic takes them one by one; a failure in one clinic's turn does not stop or leak into the next.

**Why this priority**: no such job exists until later specs, but the first one written without
this rule would run with no clinic and either do nothing or need a door through the boundary.

**Independent Test**: run a job for Clinic A and confirm it sees only A; run a job across all
clinics with one clinic made to fail and confirm the others complete and none saw another's data.

**Acceptance Scenarios**:

1. **Given** a job started for Clinic A, **When** it reads and writes, **Then** it is confined
   to Clinic A exactly as a request would be.
2. **Given** a job that covers all clinics, **When** it runs, **Then** each clinic is handled in
   its own turn, isolated from the others.
3. **Given** one clinic's turn fails, **When** the job continues, **Then** the remaining clinics
   are still handled and the failure is recorded against the clinic it happened in.
4. **Given** a job started with no clinic, **When** it touches clinic-owned records, **Then** it
   sees none and writes none.

---

### Edge Cases

- A request carries two different signals of which clinic it is for (the address says one, the
  account says another): the work is refused; it is never done for either.
- The clinic is established, then the same work tries to switch to another clinic part-way: it
  is refused; one piece of work belongs to one clinic from start to finish.
- A record identifier is guessed correctly but belongs to another clinic: the answer is
  indistinguishable from "does not exist", including how long it takes to answer.
- Two clinics hold records with the same natural key (the same patient mobile number, the same
  invoice number): both are allowed; uniqueness is within a clinic unless a rule says otherwise.
- A report or export that adds up records: it adds up only the establishing clinic's records,
  even when written as a hand-built query.
- A stored-data connection is reused after serving Clinic A: nothing of Clinic A's context
  remains for the next piece of work.
- Many pieces of work for different clinics run at the same moment: each sees only its own.
- A short name differs only by case, by a trailing slash, or by look-alike characters: case and
  slashes are normalised; characters outside the allowed set are refused at registration.
- A short name equal to a word the system uses in addresses (`api`, `app`, and the like): it can
  never be registered.
- A clinic's short name is changed twice: every earlier name still leads to the clinic.
- The clinic a patient address names exists but is not currently allowed to operate (suspended
  or cancelled): the patient sees exactly the "not available" page of an unknown address, and
  nothing in the answer shows that the clinic exists. Which states count as "not allowed to
  operate", and what the other states permit, is decided by spec 016.
- An operator's cross-clinic record is itself read by a clinic: it is not clinic-owned data and
  is not reachable from any clinic.
- A developer needs data that genuinely belongs to no clinic (a list of specialties): it is
  declared platform-wide by name; it is not made visible by weakening a clinic-owned rule.
- The restore test of spec 001 restores a backup: isolation rules are part of what is restored
  and are checked by the verification.

## Requirements *(mandatory)*

### Functional Requirements

**The clinic**

- **FR-001**: The system MUST hold a record for each clinic, and every record that belongs to a
  clinic MUST name the clinic it belongs to (SH-004, BRD §2).
- **FR-002**: Each clinic MUST have a short name that is unique among all clinics, past and
  present, and that identifies it in the patient address (SH-003 as amended).
- **FR-003**: A short name MUST be 3 to 40 characters, made only of lowercase English letters,
  digits and hyphens, start with a letter, and not end with or repeat a hyphen. Arabic letters
  and any other script are refused.
- **FR-004**: A short name MUST NOT be a word reserved by the system. The reserved list MUST be
  kept in one place and include at least every word the system uses as the first part of an
  address.
- **FR-005**: Only Heta's support team sets or changes a clinic's short name (SH-003). This spec
  provides the rule and the operation; the operator screen is spec 015.
- **FR-006**: When a short name is changed, every earlier name MUST continue to lead to the same
  clinic with no time limit, MUST move the patient to the current address while keeping the rest
  of the address they opened, and MUST never be available to another clinic.
- **FR-007**: A clinic's own record MUST itself be isolated: readable on behalf of that clinic
  and through the declared operator operations, and by nothing else. It MUST be writable only
  through the operator operations: work done on behalf of a clinic can never change that
  clinic's own record, including its state.

**Knowing which clinic**

- **FR-008**: Every piece of work that touches clinic-owned records MUST be done on behalf of
  exactly one clinic, established before the first record is touched and fixed until the work
  ends.
- **FR-009**: On the patient address, the clinic MUST be taken from the first part of the path;
  on the staff address, the clinic MUST NOT be taken from the address at all (constitution
  v1.1.0).
- **FR-010**: Resolving a short name MUST ignore letter case and surrounding slashes, and MUST
  give the same "not available" answer for every name that matches no clinic.
- **FR-033**: On the patient address, a clinic whose state is one of the "not publicly
  available" states MUST get the identical "not available" answer as a name that matches no
  clinic. This spec provides the rule with `suspended` and `cancelled` as that set; spec 016 owns
  the list of states and may change the set.
- **FR-011**: Work that carries conflicting signals of its clinic MUST be refused.
- **FR-012**: This spec MUST provide the place where a signed-in account's clinic is established,
  ready for spec 003 to use, without itself implementing sign-in.

**Isolation**

- **FR-013**: Isolation between clinics MUST be enforced where the data is stored, at the level
  of the individual record, and MUST NOT rely on application code alone (SH-004, SH-104).
- **FR-014**: Isolation MUST fail closed: with no clinic established, reads return no
  clinic-owned records and writes change none. It MUST never fall back to all clinics.
- **FR-015**: Isolation MUST cover reading, creating, changing and deleting, and MUST prevent a
  record being created for, or moved to, a clinic other than the one established.
- **FR-016**: Isolation MUST apply to every way the application reads stored data, including
  hand-written report queries, and such queries MUST be unable to change stored data.
- **FR-017**: A record of another clinic MUST be indistinguishable from a record that does not
  exist, to whoever asks for it.
- **FR-018**: The application's own identity for the stored data MUST be subject to isolation
  and MUST NOT be able to switch it off.
- **FR-019**: No trace of one piece of work's clinic may remain for the next piece of work that
  reuses the same resources.

**The guardrail**

- **FR-020**: An automated check MUST fail the build when any clinic-owned record type lacks its
  isolation rule, and MUST name the record type.
- **FR-021**: A clinic-owned record type and its isolation rule MUST be introduced in the same
  change; the check MUST fail on a change that introduces the type alone.
- **FR-022**: An automated test MUST prove, using the application's own identity for the stored
  data, that one clinic cannot read, create, change or delete another's records, and that no
  clinic established means no records. It MUST cover every clinic-owned record type without a
  hand-maintained list.
- **FR-023**: A record type that belongs to no clinic MUST be declared platform-wide by name in
  one reviewed place; anything not declared is treated as clinic-owned by the check.
- **FR-024**: These checks MUST be part of the gates every change passes before merge (spec 001,
  FR-009).

**Crossing the boundary**

- **FR-025**: Access across clinics MUST be possible only through operations declared by name in
  one reviewed place. An undeclared crossing MUST fail the automated checks.
- **FR-026**: The declared crossings in this spec are limited to five: the operator operations
  that register a clinic, change its short name and list clinics; listing clinic identifiers so
  that a per-clinic job can take them one by one; and removing access records older than the
  retention period. Finding a clinic by its short name is deliberately not a crossing: it reads
  only the directory of names, which holds nothing that is not already in a public address.
- **FR-027**: Each use of an operator crossing MUST be recorded with the operator, the
  operation, the clinic and the time, and the record MUST hold identifiers only, never patient
  data (SH-083, SH-094).
- **FR-028**: The lookup that finds a clinic by short name MUST be able to read only what is
  needed to establish the clinic: its identifier, its short names and its state.
- **FR-032**: Cross-clinic access records MUST be kept for a period that is a platform setting,
  starting at 5 years, changeable without a code change (SH-095). Records older than the period
  MUST be removed automatically, and nothing else may remove or alter a record.

**Background work and observability**

- **FR-029**: Background work that touches clinic-owned records MUST establish its clinic first,
  exactly as a request does; work that covers several clinics MUST handle one at a time.
- **FR-030**: A failure while handling one clinic in a multi-clinic job MUST NOT prevent the
  others from being handled, and MUST be recorded against that clinic.
- **FR-031**: Every operational log entry for work done on behalf of a clinic MUST carry the
  clinic's identifier, and MUST carry nothing else about the clinic or its data (SH-094).

### Key Entities

- **Clinic**: the owner of data. Has an identifier that never changes, a current short name, a
  display name in Arabic and English, and a state that later specs give meaning to. Everything
  recorded for a clinic refers to it.
- **Clinic short name**: a name that leads to a clinic in the patient address. A clinic has one
  current name and may have earlier ones; each name belongs to exactly one clinic forever.
- **Reserved word**: a name no clinic may take, because the system uses it in addresses.
- **Clinic context**: the fact, established once per piece of work, of which clinic the work is
  for. Not stored; it exists for the duration of the work.
- **Clinic-owned record type**: any kind of record that belongs to a clinic. Protected by an
  isolation rule from the moment it exists.
- **Platform-wide record type**: a kind of record that belongs to no clinic, declared by name.
- **Declared crossing**: a named operation allowed to read or write across clinics.
- **Cross-clinic access record**: who used which declared crossing, on which clinic, when. Kept
  for the retention period (5 years to start), then removed automatically.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an automated run that tries every read, create, change and delete on every
  clinic-owned record type as the wrong clinic, the number of another clinic's records returned
  or altered is zero.
- **SC-002**: With no clinic established, the number of clinic-owned records readable or
  writable is zero, for every clinic-owned record type.
- **SC-003**: 100% of changes that add a clinic-owned record type without its isolation rule are
  blocked before merge, with the record type named.
- **SC-004**: A developer adds a new clinic-owned record type, correctly protected and covered
  by the isolation test, by following the written guide, without editing the test or the check.
- **SC-005**: Asking for another clinic's record and asking for a record that does not exist
  give the same answer, and their response times differ by less than can be told apart by a
  caller (under 5% difference on average over 1,000 requests).
- **SC-006**: A patient address resolves to its clinic, and an unknown address to "not
  available", in under half a second as the patient sees it.
- **SC-007**: 100% of cross-clinic accesses by operators appear in the access record, and 0% of
  those records contain patient data.
- **SC-008**: The number of places in the system able to cross the clinic boundary equals the
  number declared; the check proves it on every change.
- **SC-009**: With 200 clinics and work running for 50 of them at the same moment, no piece of
  work receives a record of another clinic.
- **SC-010**: Isolation adds no delay a user would notice: a typical screen's data arrives
  within the same time, to within a tenth of a second, as it would without it.
- **SC-011**: Every earlier short name of a clinic still leads to that clinic after any number
  of changes, and none has ever been assigned to a second clinic.
- **SC-012**: Spec 003 can establish the clinic from a signed-in account, and spec 015 can
  register a clinic, without changing how isolation works.

## Assumptions

- Addresses follow constitution v1.1.0: operators on `hetasystems-scsadmin.app`, staff on
  `app.scs-heta.com`, patients on `scs-heta.com/<clinic>`. The BRD still states the earlier
  per-clinic subdomains in SH-003 and is to be updated by the product owner.
- The short-name format (3 to 40 characters, lowercase English letters, digits, hyphens; no
  Arabic letters) was confirmed by the product owner on 10 Oct 2026. The BRD says only that the
  support team sets the name; this rule is to be added to it with SH-003.
- Changing a short name keeps every earlier name leading to the clinic, with no time limit, and
  moves the patient to the current address (confirmed by the product owner on 10 Oct 2026). The
  BRD does not state this; it is to be added with SH-003.
- The reserved list starts with the words the system already uses as the first part of an
  address (`api`, `app`, `assets`, `i18n`, `fonts`, `media`, `health`, `v1`, `config.json`,
  `version.json`) and common words that would mislead (`admin`, `ops`, `support`, `login`,
  `www`). Later specs add to it; they never remove from it.
- A clinic's display name is held in Arabic and English because every surface is bilingual
  (SH-002). Other details of the clinic (specialty, tax data, branches) belong to specs 015, 028
  and 077.
- The clinic's state is stored here as a value; the states and what each permits are spec 016
  (SH-030). The one behaviour this spec gives it is on the patient address: `suspended` and
  `cancelled` are answered as "not available" (confirmed by the product owner on 10 Oct 2026).
  Staff and operator access in those states is spec 016.
- Uniqueness of business values (a mobile number, an invoice number) is within a clinic unless a
  later spec says otherwise.
- The record of cross-clinic access kept here is an operational record for security review. The
  business audit trail with before-and-after values (SH-092) is spec 007 and may absorb it.
- The retention period for those records is held as platform configuration until the operators'
  settings screen exists (spec 024), which then exposes it. The lawyer's answer on retention
  (BRD §12) may change the number.
- Until sign-in exists (specs 003 and 010), the operator operations of this spec are exercised
  by tests and by the migration and seed tooling, not by a person at a screen.
- Made-up clinics and records for local and test environments are added by this spec's seed
  (spec 001, task T112); no production data is used (spec 001, FR-008).
- The technology that enforces isolation is fixed by the constitution and applied at planning;
  it is deliberately not named here.
