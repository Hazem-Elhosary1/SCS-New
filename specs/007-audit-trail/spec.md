# Feature Specification: Audit Trail

**Feature Branch**: `007-audit-trail`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: empty. Taken from row `007-audit-trail` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "writing every action that changes
money, a permission or a plan, with the value before and after."

**BRD source**: `BRD-0-shared.md` §11 — SH-092 (the audit trail), SH-093 (the activity log a
clinic reads about itself), SH-094 (no personal data in operational logs), SH-095 (retention is a
platform setting). The shape of an entry: Chapter 09 §3-5 (the clinic's activity log) and
Chapter 17 §3-11 (the operators' audit entry). What an entry never holds: Chapter 09 rules ق-٣٩
to ق-٤٢. What the units built so far must write: Chapter 10 §3-8 (users, roles, licences) and the
sign-in chapter (Chapter 01).

**Design source**: no screen is built in this spec. The audit trail has two screens — the
clinic's «سجل الأنشطة والأمان» and the operators' «سجل التدقيق» — and each belongs to a later
spec (059, 023 and 025). But the words an entry is read as are fixed here, so they were read from
the design: Figma file `4HLWEqlna9sbn68276LHYt`, page "Activity log" (`5450:4`), frames
"Activity Log / Log" (`5632:44831`, LTR `5632:52027`), "Filtered" (`6589:934565`),
"Activity Details" (`6636:967915`) and "Security Details" (`5632:46851`).

What the frames fix: an action is named by a noun («محاولة دخول فاشلة» · "Failed sign-in",
«دعوة مستخدم» · "User invited", «تعديل سجل طبي» · "Medical record edited") and described by one
sentence in the passive («دُعيت د. ياسمين علي» · "Dr. Yasmin Ali was invited"); a run of failed
attempts reads «٥ محاولات متتالية على حساب د. سارة يوسف، فقُفل ١٥ دقيقة» · "5 attempts in a row on
Dr. Sara Youssef's account; locked for 15 minutes", and without a lock «محاولتان على حساب نهى
عادل»; the actor of a failed sign-in is «غير معروف» · "Unknown"; the device is «متصفح Chrome على
Windows»; the four categories are «أمنية» · "Security", «تعديلات» · "Edits", «سجلات وفواتير» ·
"Records & invoices", «مستخدمون وصلاحيات» · "Users & permissions"; and the page's own note says
«السجل للقراءة فقط: لا يُعدَّل ولا يُحذف منه شيء، ولا يَعرض كلمة مرور ولا رمزًا ولا محتوًى طبيًّا.
ويُحفظ السجل بلا مدة محددة.»

**Undrawn** (written here in the frames' style, raised with the owner): the names and sentences
of the role actions, of a successful sign-in, a sign-out, ended sessions, the password events
and an accepted invitation — the frames show five rows and none of these.

**Carried over from spec 006** (owner decision, 11 Oct 2026): two items that touch every later
spec are settled with this work, although they are not part of the trail — the timed test of
spec 005, and the size of the clinic app's first download.

**Depends on**: 005-permissions-engine (the catalogue of permissions, roles and the changes to
them that must be recorded). Built on 002 (isolation), 003 and 004 (the sign-in and password
events those specs already keep).

## Overview

The platform must be able to answer, long after the fact and to someone who doubts it: who did
this, to what, when, from where, and what was the value before and after. Until now nothing
answers it. Role changes are made and forgotten; sign-in events are kept but nothing reads them
as a trail.

This spec delivers the trail itself, once, for every spec after it:

- **One kind of entry** for the whole platform — a clinic's person, an operator or the platform
  itself as actor; an action from a closed catalogue; what it was done to; the allowed facts
  about it; before and after where it moves money, a permission or a plan; where from; when.
- **One way to write it**, in which a change and its entry happen together or not at all.
- **A guarantee that it stays**: no one — not the clinic, not an operator, not the application —
  changes or removes an entry.
- **A guarantee about what it never holds**: no secret, no clinical content, nothing beyond the
  facts allowed for that action.
- **A check that nothing is forgotten**: every way of changing data says which action it
  records, or says why it records none.
- **Reading**, for the screens to come: a clinic's own view and the operators' view, by period,
  category, actor and action.

It also records what the specs already built do: role changes (005) and the sign-in and
password events of 003 and 004.

Out of scope: the clinic's activity and security page with its tabs, counts, search, export and
security alert (059); the operators' audit page, its cards, chart and export (023, 025); security
events as things to review, and blocking addresses (025); telling anyone that something happened
(008); retention periods and removal after them (SH-095, conditional on the lawyer); the
operators' console itself and its actions (phase 1) — the trail is ready for them, none exists
yet.

## Clarifications

### Session 2026-10-11

- Q: What is kept in an entry as the actor's name at the moment, while accounts have no names (until spec 026)? → A: The email address, as the display name the console shows today; from spec 026 the person's name. Entries already written keep the address.
- Q: Is it enough that the data store refuses any change or removal of entries, or must tampering from outside the application also be detectable? → A: Refusal is enough. The data store refuses change and removal from the application's account, and the daily backup is the reference in case of doubt. No chain of fingerprints is built.
- Q: Is a signed-in person's attempt to reach something their role does not open recorded in the clinic's trail? → A: No. The trail holds actions that took effect and sign-in events; a refusal stays in the operational logs, without personal data.
- Q: Is the clinic's trail readable in this spec through an address of the platform (with no screen), or does reading stay internal until spec 059? → A: Through an address now, guarded by «عرض سجل الأنشطة», returning a page of entries and the counts per category. No screen; spec 059 builds the page on it.
- Q: Where do a successful sign-in, a sign-out, a completed password reset and a replaced temporary password go among the clinic's four categories? → A: Security. Everything about access to accounts is in one place.
- Q: Where does the wording of each entry's name and sentence come from? → A: From the frames of the Activity log page of the design file, read now; only what the design does not draw is written here.
- Q: What is built for the operators' side, which has no console yet? → A: The contract of the operators' reading, with a test. The table for entries with no clinic is created with the first operator action.
- Q: How are the entries of role changes verified, when no screen or address can cause one before spec 027? → A: By integration tests against the real database. The real-stack walk checks sign-in entries, the reading address, the permission and immutability.
- Q: How long are entries kept? → A: Everything is kept; nothing is removed until the lawyer's answer on SH-095. Removal is a later spec.
- Q: May one migration of the sign-in module write old sign-in events and existing roles into the trail's table? → A: Yes, once, as a data migration recorded in the plan.
- Q: Which items left open by spec 006 are settled inside this spec's work? → A: Both — the timed test of spec 005 ("not in your role" within a second), and the clinic app's first download, brought under its warning limit by loading destinations on demand.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A change to who may do what is on the record, with before and after (Priority: P1)

A clinic's owner edits a role: adds two permissions and removes one. Months later a dispute
asks who gave reception the right to refund. The trail says who edited the role, when, from
where, which permissions were added and which were removed. The same holds for creating a role,
deleting one, and giving a person a different role.

**Why this priority**: it is the first thing SH-092 names, and the only such change the platform
can make today. Spec 005 left it unrecorded on purpose, for this spec.

**Independent Test**: change a role and a person's role as a clinic's owner; read the clinic's
trail and find one entry for each change with its before and after.

**Acceptance Scenarios**:

1. **Given** a role holding a set of permissions, **When** a person changes the set, **Then**
   one entry is recorded naming the actor, the role, the permissions added and the permissions
   removed, the address and device it came from, and the time.
2. **Given** a role is created, **When** the creation succeeds, **Then** one entry names the
   role and the permissions it was given.
3. **Given** a role is deleted, **When** the deletion succeeds, **Then** one entry names the
   role as it was called at that moment, and stays readable after the role is gone.
4. **Given** a person holds one role, **When** they are given another, **Then** one entry names
   the person whose role changed, the role before and the role after.
5. **Given** a change that is refused (the fixed owner role, a role in use, the last owner, an
   invalid set), **When** it is refused, **Then** no entry is recorded.
6. **Given** a role is saved with the set it already holds, **When** nothing changed, **Then**
   no entry is recorded.

---

### User Story 2 - A change and its entry stand or fall together, and the entry stays (Priority: P1)

Whoever relies on the trail must be able to trust two things: that nothing happened without an
entry, and that an entry is what was written at the time. So a change that cannot be recorded
does not happen, and an entry, once written, cannot be changed or removed by anyone.

**Why this priority**: a trail that can lose an entry or be edited is not evidence. Every other
story rests on this.

**Independent Test**: make recording fail and see the change did not happen; try to change and
to remove an entry as the application, and see both refused.

**Acceptance Scenarios**:

1. **Given** an action that must be recorded, **When** its entry cannot be written, **Then**
   the action does not take effect and the person is told it failed.
2. **Given** an action that fails after its entry was prepared, **When** it fails, **Then** no
   entry remains.
3. **Given** an entry exists, **When** anything the application can do tries to change it,
   **Then** it is refused and the entry is as it was.
4. **Given** an entry exists, **When** anything the application can do tries to remove it,
   **Then** it is refused.
5. **Given** the person who acted is later renamed, disabled or deleted, **When** the entry is
   read, **Then** it still shows who they were at the time.
6. **Given** the thing acted on is later renamed or deleted, **When** the entry is read,
   **Then** it still shows what it was called at the time.

---

### User Story 3 - Sign-in and account security are part of the same trail (Priority: P1)

A clinic's manager reads what happened to the clinic's accounts: who signed in, a run of wrong
passwords that locked an account, a password that was reset, an invitation accepted, sessions
ended. These events are already kept by the sign-in specs; the trail holds them in the same
order and shape as everything else, without anyone having to look in two places.

**Why this priority**: SH-093 asks for a log the clinic reads about itself, and security is the
first of its four categories. The events exist; without this they are unread.

**Independent Test**: sign in, fail to sign in until the account locks, reset a password; read
the clinic's trail and find each, in order, with the right category.

**Acceptance Scenarios**:

1. **Given** a person signs in, **When** the clinic's trail is read, **Then** one entry shows
   the sign-in, the account, the address, the device and the time.
2. **Given** five wrong passwords in a row lock an account, **When** the trail is read, **Then**
   the run is one entry that says how many attempts there were and that the account was locked —
   not five entries.
3. **Given** a password is reset, a temporary password is replaced or an invitation is accepted,
   **When** the trail is read, **Then** each is one entry naming the account.
4. **Given** a person's sessions are ended by someone else or by a change of role, **When** the
   trail is read, **Then** one entry says whose sessions, why and by whom.
5. **Given** an attempt on an address that matches no account, **When** any clinic's trail is
   read, **Then** it is in none of them: it belongs to no clinic.
6. **Given** these events were recorded before this spec, **When** the trail is read, **Then**
   they are there too.

---

### User Story 4 - The trail never holds what it must not (Priority: P1)

A person reading the trail — or anyone who obtains a copy of it — finds no password, no code, no
link, no key, no session proof, no clinical content, and no personal data beyond the facts each
action is allowed to carry. This is decided where an entry is written, not where it is shown.

**Why this priority**: the trail is kept for years and read by many. One secret in it is a
breach that cannot be undone, because entries cannot be removed.

**Independent Test**: write entries through every action that exists while passing secrets and
extra facts alongside; read the stored entries and find none of them.

**Acceptance Scenarios**:

1. **Given** an action whose allowed facts are named, **When** an entry is written with a fact
   that is not among them, **Then** the entry is refused and the action does not take effect.
2. **Given** any action, **When** its entry is stored, **Then** it holds no password, code,
   link, key or session proof, in any field.
3. **Given** an action on a patient's clinical record (in a later spec), **When** its entry is
   written, **Then** it says that the record was changed and whose, never what changed.
4. **Given** an action that adds a person (in a later spec), **When** its entry is written,
   **Then** it names the account by reference, not by email address.
5. **Given** a person changes how a screen is shown to them — language, a folded side bar, a
   sort order, a filter — **When** they do, **Then** no entry is recorded.

---

### User Story 5 - A later spec cannot forget to record (Priority: P2)

A developer adds a way to issue a refund. They name the action in the catalogue, say which facts
it may carry and that it carries before and after, and record it where the refund is made. If
they add the way of changing data and say nothing about recording, the checks fail before the
change can be merged.

**Why this priority**: SH-092 says "every". Eighty-five specs follow; one that forgets makes the
trail untrustworthy for all.

**Independent Test**: add a way of changing data with no declaration and see the checks fail;
declare its action and see them pass; record an action that is not in the catalogue and see it
refused.

**Acceptance Scenarios**:

1. **Given** a way of changing data that declares neither an action nor a reason for recording
   none, **When** the checks run, **Then** they fail and name it.
2. **Given** an action marked as moving money, changing a permission or changing a plan,
   **When** its entry is written without before and after, **Then** the entry is refused.
3. **Given** an action code already in use, **When** someone renames or reuses it, **Then** the
   checks fail: a code, once used, means the same thing forever.
4. **Given** an entry written with a code that is not in the catalogue, **When** it is written,
   **Then** it is refused.
5. **Given** an action that changes nothing a person would care to trace (a display
   preference), **When** it is declared as recording nothing with its reason, **Then** the
   checks pass.

---

### User Story 6 - Each side reads its own part of the trail (Priority: P2)

The screens come later, but what they will read is fixed now. A clinic reads the entries of its
own people and the sign-in attempts on its own accounts — never another clinic's, and never what
operators or the platform itself did. Operators read what operators did, across clinics. Both
read by period, newest first, and narrow by category, actor and action.

**Why this priority**: who may read what is part of what the trail is. Deciding it with the
screens would let each screen decide differently.

**Independent Test**: record entries by a clinic's person, by the platform and for a second
clinic; read as the first clinic and find only its people's entries; narrow by each facet.

**Acceptance Scenarios**:

1. **Given** entries of two clinics, **When** one clinic's trail is read, **Then** no entry of
   the other is returned, whatever is asked for.
2. **Given** entries whose actor is the platform itself or an operator, **When** a clinic's
   trail is read, **Then** they are not in it.
3. **Given** a clinic's trail, **When** it is read for a period, **Then** entries are returned
   newest first, with how many there are in each of the four categories and in all.
4. **Given** a clinic's trail, **When** it is narrowed by category, by actor or by action,
   **Then** only matching entries are returned and the counts follow.
5. **Given** a person without the permission to read the activity log, **When** they ask for
   the trail, **Then** they are refused as for any page their role does not open.
6. **Given** no period is asked for, **When** the trail is read, **Then** the last ninety days
   are returned, not the whole trail.

---

### User Story 7 - An entry reads in the language of whoever reads it (Priority: P3)

An entry is stored as facts, not as a sentence. When it is read, the name of its action and the
sentence that describes it are produced in the reader's language — Arabic or English — so a
person who switches language reads the same trail in the other one. Identifiers such as network
addresses are shown the same in both.

**Why this priority**: both languages are required of every screen (Constitution VI). It ranks
after the others because nothing is lost if it is delivered last.

**Independent Test**: read the same entries in Arabic and in English and compare.

**Acceptance Scenarios**:

1. **Given** an entry, **When** it is read in Arabic and in English, **Then** its action's name
   and its describing sentence are in that language, with nothing left in the other.
2. **Given** an entry about a ready role that still has its own name, **When** it is read,
   **Then** the role is named in the reader's language; a role the clinic named is shown as the
   clinic wrote it.
3. **Given** an entry with a network address, **When** it is read in Arabic, **Then** the
   address is in Latin digits.
4. **Given** every action in the catalogue, **When** the checks run, **Then** each has a name
   and a sentence in both languages.

### Edge Cases

- **One act, several things changed.** Editing a role's name and its permissions together is one
  entry holding both changes, not two.
- **One act on many things.** An action applied to several records at once (in a later spec)
  records one entry per record, so each record's history is whole.
- **The actor is the platform.** A scheduled job or an automatic rule that changes money, a
  permission or a plan records its entry with the platform as actor and no person.
- **No clinic.** An operator's action on the platform itself has no clinic. No such action
  exists yet; its entries, readable from the operators' side only, come with the first one.
- **Acting for someone.** When a person acts on another person's account (ending their
  sessions), the entry has both: who acted and whose account.
- **Clock.** The time of an entry is the platform's, not the browser's; two entries of one act
  share it.
- **Address and device unknown.** An action by the platform, or a request that carries neither,
  records without them rather than failing.
- **Very many entries.** A clinic with years of entries reads a period in the same time as a
  new clinic; reading never loads the whole trail.
- **A deleted account in the catalogue of actors.** Narrowing by actor still offers people who
  acted in the period and no longer exist.
- **A refused request.** A person who asks for what their role does not open, or whose change
  fails its checks, leaves no entry; five such refusals are not a security entry either.
- **Before and after are the same.** An action that sets a value to what it already was records
  nothing.
- **Environment.** Seeded accounts and clinics in local and test environments are created
  without entries; their later actions are recorded like anyone's.

## Requirements *(mandatory)*

### Functional Requirements

**The entry**

- **FR-001**: The platform MUST keep one kind of audit entry for every recorded action, holding:
  the kind of actor (a clinic's person, an operator, the platform itself); the actor, when it is
  a person; the actor's display name at that moment; the action, as a code from a closed
  catalogue; what was acted on — its kind, its identity and its name at that moment; the clinic;
  the facts allowed for that action; the network address and the device the
  request came from, when known; and the time (SH-092, Chapter 09 §3-5, Chapter 17 §3-11).
- **FR-002**: For an action that moves money, changes a permission or changes a plan, the entry
  MUST hold the value before and the value after (SH-092). For a set, that is what was added and
  what was removed.
- **FR-003**: Names kept at the moment — the actor's, the target's — MUST stay as they were
  written when the person or thing is later renamed or deleted. The actor's name at the moment
  is the display name the console shows for them: until accounts have names (spec 026) that is
  their email address; from then on their name. Entries written before keep what they hold.
- **FR-004**: The time of an entry MUST be the platform's clock at the moment of the action and
  MUST NOT be supplied by the browser.
- **FR-005**: Each entry MUST belong to exactly one of the clinic's four categories — security;
  changes; records and invoices; users and permissions — derived from its action, never chosen
  per entry (Chapter 09 §7-ج-٢). Every sign-in and password event is in security: a successful
  sign-in, a run of failed attempts, a sign-out, ended sessions, a completed reset, a replaced
  temporary password (clarified). An accepted invitation and a re-issued way in are in users and
  permissions, with the role changes.

**The catalogue of actions**

- **FR-006**: Actions MUST come from one closed catalogue kept with the product, each with: a
  code; its name in Arabic and English; its category; the facts it may carry; whether it must
  carry before and after; and the sentence that describes an entry of it, in both languages.
- **FR-007**: A code, once used, MUST NOT be renamed, reused or removed; the checks MUST fail
  when the catalogue changes a code that exists (Chapter 17 §3-11).
- **FR-008**: An entry whose action is not in the catalogue MUST be refused.

**Writing**

- **FR-009**: A recorded action and its entry MUST take effect together or not at all: if the
  entry cannot be written the action fails; if the action fails no entry remains.
- **FR-010**: An entry MUST be written only with the facts its action allows; an entry carrying
  any other fact MUST be refused, and the action with it (Chapter 09 §3-5 "a white list of key
  names").
- **FR-011**: An entry for an action that must carry before and after MUST be refused without
  them.
- **FR-012**: An action that leaves a value as it was MUST record nothing.
- **FR-013**: One act MUST produce one entry per thing acted on: several changes to one thing in
  one act are one entry; one act on several things is one entry for each.
- **FR-014**: The actor, the clinic, the network address and the device MUST be taken by the
  platform from the signed-in session and the request; whoever records an action MUST NOT be
  able to supply them.
- **FR-014a**: Where no one is signed in yet — a sign-in, a failed attempt, a password set by a
  link, an accepted invitation — the actor is the account the entry is about, and a failed
  attempt has no actor at all. Only the sign-in part of the platform can record this way.
- **FR-015**: The platform itself MUST be able to record as actor, for work it does on its own
  (scheduled jobs, automatic rules).

**What an entry never holds**

- **FR-016**: No entry MUST hold a secret in any form: a password, a sign-in code, a link, a
  key, a channel token or a session proof (ق-٤٠, SH-091).
- **FR-017**: No entry MUST hold clinical content: it may say that a clinical record was changed
  and whose, never what changed (ق-٤١).
- **FR-018**: An entry about an account MUST name it by reference; an email address MUST NOT be
  written into an entry by the action that records it (Chapter 10 §3-8).
- **FR-019a**: A request that is refused — for a missing permission, for failing its checks,
  for any reason — MUST NOT be recorded in the trail: the trail holds what took effect and the
  sign-in events of FR-028. Refusals stay in the operational logs, without personal data
  (SH-094).
- **FR-019**: Changes to how a screen is shown to a person — language, a folded side bar, a
  banner closed for the day, a sort order, a filter, the order of cards — MUST NOT be recorded
  (ق-٣٩).

**Staying**

- **FR-020**: An entry MUST NOT be changed after it is written — not by a clinic, not by an
  operator, not by the application; the data store itself MUST refuse it (ق-٤٢, Chapter 17
  §3-11 "the trail is not edited and not pruned").
- **FR-021a**: Protection is by refusal, not by detection: no fingerprint chain links entries,
  and nothing verifies the trail against tampering done outside the application. The daily
  backup (SH-096) is the reference when an entry is doubted.
- **FR-021**: An entry MUST NOT be removed by anything the application can do. Removal after a
  retention period is a platform setting decided later (SH-095) and is not built here; until it
  is, nothing is removed.
- **FR-022**: The sign-in and password events kept since specs 003 and 004 fall under FR-020 and
  FR-021 too, and their retention is decided together with the trail's.

**Nothing forgotten**

- **FR-023**: Every way of changing data that the platform offers — every address that is not a
  read, and every scheduled job — MUST declare the action it records, or declare that it records
  none with the reason; the checks MUST fail for one that declares neither, naming it.
- **FR-024**: What specs 003 to 006 already offer MUST be declared under FR-023 in this spec:
  the role changes of 005 record; a person's own display preferences of 006 record none;
  sign-in, sign-out, the password ways and activation record the entries of FR-028.

**What is recorded now**

- **FR-025**: Creating a role MUST record the role and the permissions it was given; editing a
  role MUST record what changed — its name and description before and after, the permissions
  added and removed; deleting a role MUST record the role as it was called (CL-AD-105).
- **FR-026**: Giving a person a different role MUST record the person, the role before and the
  role after (CL-AD-039).
- **FR-027**: The first roles a clinic is given when it is established, and the roles given to
  existing clinics when roles were introduced, MUST be recorded with the platform as actor.
- **FR-028**: Each of these MUST be written as one entry, beside the sign-in event it comes
  from: a successful
  sign-in; a run of failed sign-in attempts on an account, with their number and whether the
  account was locked; a completed password reset; a temporary password replaced; an invitation
  accepted; sessions ended, with the reason and by whom when it was not the owner; sign-out
  (Chapter 09 §3-5-ب, §3-6).
- **FR-029**: A sign-in attempt on an address that matches no account MUST NOT appear in any
  clinic's trail.

**Reading**

- **FR-030**: A clinic's reading MUST return only entries of that clinic whose actor is one of
  its people, together with the sign-in attempts on its accounts; it MUST NOT return entries
  whose actor is an operator or the platform (Chapter 09 §3-5, §7-ج-١).
- **FR-031**: The operators' reading MUST return entries whose actor is an operator, across
  clinics (Chapter 17 §5-و-٥). No operator exists yet; the reading is fixed here as a contract
  and used by specs 023 and 025. Entries with no clinic — an operator acting on the platform
  itself — are added with the first such action, not here (clarified).
- **FR-032**: Reading MUST be by period, newest first, and MUST be able to narrow by category,
  by actor and by action, and to search by the actor's name or the action's name. With no period
  given it MUST return the last ninety days. It MUST return the number of entries in the period
  in all and in each category.
- **FR-033**: Reading MUST return a page of entries at a time and MUST NOT need the whole trail
  to do so.
- **FR-033a**: The clinic's reading MUST be offered by the platform as an address a signed-in
  person's browser can ask, in this spec, with no screen: it answers a page of entries — each
  with its time, actor, action name, describing sentence, category, address and device — and
  the counts of FR-032. The operators' reading has no address until the operators' console
  exists.
- **FR-034**: Reading a clinic's trail MUST need the permission «عرض سجل الأنشطة» of the
  catalogue of spec 005; a person without it is refused as for any destination their role does
  not open.
- **FR-035**: An entry of one clinic MUST never be readable from another clinic, by any way of
  reading (Constitution II).

**Languages**

- **FR-035a**: An action MUST be named by a noun and described by one sentence in the passive,
  as the frames of the Activity log write them; the actor of a failed sign-in reads «غير معروف» ·
  "Unknown"; a run of failed attempts reads its number of attempts, the account, and the
  minutes of the lock when it ended in one.
- **FR-036**: An entry MUST be stored as facts and read as words: the action's name and the
  describing sentence are produced when it is read, in the reader's language, Arabic or English.
- **FR-037**: A ready role still under its own name MUST be named in the reader's language; what
  a clinic wrote MUST be shown as written.
- **FR-038**: Network addresses MUST be shown in Latin digits in both languages; the device MUST
  be described in words in the reader's language («متصفح Chrome على Windows»).
- **FR-039**: Every action of the catalogue MUST have its name and its sentence in both
  languages; the checks MUST fail for one that lacks either.

### Key Entities

- **Audit entry**: one recorded action. Who (kind of actor, the actor, their name then), what
  (action code), on what (kind, identity, name then), in which clinic if any, the allowed facts,
  before and after where required, from where (address, device), when. Never changed, never
  removed.
- **Action**: one row of the closed catalogue. Code, names, category, the facts it may carry,
  whether it needs before and after, its describing sentence in two languages.
- **Category**: the four the clinic's log is split by — security; changes; records and invoices;
  users and permissions. Each action is in exactly one.
- **Sign-in event**: what specs 003 and 004 already keep about sign-in and passwords. The ones
  that belong in the trail are also written as entries of the security and the users
  categories.
- **Declaration**: what each way of changing data says about recording — an action, or none with
  its reason.

### Carried over from spec 006

- **FR-040**: The page a person's role does not open MUST be on screen within a second of
  opening its address, measured alone on the build machine, inside the console's frame
  (spec 005, SC-008). Where the frame made this slower, what it loads before the first page is
  reduced; the limit is not raised.
- **FR-041**: The clinic app's first download MUST be under its warning limit of 500 kB:
  destinations' pages are loaded when opened, not with the frame.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the ways of changing data that the platform offers declare what they
  record; a new one that declares nothing cannot be merged.
- **SC-002**: 100% of role creations, edits, deletions and changes of a person's role made in a
  test run are found in the trail, each once, with the right before and after.
- **SC-003**: In a test that makes recording fail, 0 changes take effect; in a test that makes
  the change fail, 0 entries remain.
- **SC-004**: 0 of the attempts to change or remove an entry succeed, through every means the
  application has.
- **SC-005**: In entries written while secrets, email addresses and extra facts were passed
  alongside, 0 of those values are found anywhere in what was stored.
- **SC-006**: 0 entries of one clinic are returned to another across every way of reading and
  every facet.
- **SC-007**: A clinic with one million entries reads its last ninety days, and any page of
  them, in under one second.
- **SC-008**: 100% of the catalogue's actions have a name and a sentence in Arabic and in
  English, and every entry read in one language contains no text of the other except what a
  person wrote.
- **SC-009**: A developer adds a new recorded action — catalogue row, allowed facts, the place
  it is recorded — without changing anything in the trail itself.
- **SC-010**: Recording adds no more than a tenth of a second to an action a person waits for.
- **SC-011**: The timed test of spec 005 passes ten times out of ten when run alone, and the
  clinic app builds with no size warning.

## Assumptions

- **No screen is built here.** The clinic's page is spec 059 and the operators' pages are 023
  and 025; this spec fixes what they read. The clinic's reading is an address of the platform
  from now on (clarified, FR-033a), so isolation, the permission and paging are verified on the
  real path, and spec 059 adds the page alone.
- **The actor's own address is the one email in an entry** (clarified, FR-003). FR-018 is about
  the account an action is done *to*, which is named by reference and shown by whatever name it
  has when the entry is read.
- **Sign-in events that belong in the trail are also written as entries** (decided in the
  plan, research R2, so that the trail has one store to read). Specs 003 and 004 keep their
  detailed events as before, unchangeable and without email addresses, for the operators'
  security page; events recorded before this spec are carried into the trail once.
- **A run of failed attempts is one entry**, as Chapter 09 §3-6 says, with the number of
  attempts; when the run ends is decided by the lock rule of spec 003.
- **The four categories are the clinic's.** The operators' sections (clinics, tickets, plans,
  operators, reports, security, settings, account) are derived from the same action codes when
  operator actions exist; none exists now.
- **The security alert** — a standing alert a clinic reports or closes — and blocking addresses
  are spec 059 and 025. This spec records the events they are raised from.
- **Retention**: nothing is removed (confirmed). SH-095 makes the period a platform setting that
  waits for the lawyer; the daily clean-up of spec 003 continues to leave these events alone.
  The design's own note says the same: «ويُحفظ السجل بلا مدة محددة».
- **Exports** are recorded as actions by the specs that add them («تصدير تقرير»).
- **Volume**: up to one million entries a clinic in its first years; the trail is read by
  period and never whole.
- **The operators' reading** is defined but has no caller until the operators' console exists.
