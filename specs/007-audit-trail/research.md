# Research: Audit Trail

**Spec**: [spec.md](spec.md) | **Date**: 2026-10-11

What was read before deciding: the sign-in events of specs 003 and 004
(`Scs.Identity/Domain/SignInRecords.cs` — `SignInEvent`, its kinds, its append-only trigger); the
role service of spec 005 (`Infrastructure/ClinicRoles.cs`, `RoleChange`); the route declarations
of spec 005 (`Scs.BuildingBlocks/Security/AccessControl.cs`, `RouteDeclarationTests`); the
isolation helper (`Tenancy/ClinicIsolationSql.cs`); BRD Chapter 09 §3-5 to §3-6 and §7-ج, Chapter
17 §3-11, Chapter 10 §3-8.

No unknown is left open; each decision below resolves one.

## R1 — Where the trail lives

**Decision**: a new module, `Scs.Audit` with `Scs.Audit.Contracts`, schema `audit`. The catalogue
of actions is code in `Scs.SharedKernel/Audit`, beside the permission catalogue.

**Rationale**: every module will write to it and two will read it; it belongs to none of them.
The catalogue is in the shared kernel because a module names its actions without referring to the
audit module's internals, exactly as it names permissions.

**Alternatives**: inside Identity (rejected: money, plans and bookings would then depend on
Identity to record); a shared table with no module (rejected: nothing would own its migration).

## R2 — One store that is read; sign-in events also become entries

**Decision**: the trail is read from one table, `audit.Entry`. From this spec on, Identity writes
an entry in the same transaction as each sign-in event that belongs in the trail. `SignInEvent`
stays as it is — the detailed record that spec 025's security page reads — and is not read by the
trail. Events recorded before this spec are carried into the trail once, in a migration.

This settles the item `/speckit-clarify` deferred, and it changes one assumption of the spec:
"sign-in events are not copied" becomes "sign-in events that belong in the trail are also
written as entries; the detailed events remain". The spec's assumption is reworded with this plan.

**Rationale**: reading must be newest-first across everything, paged, counted per category and
narrowed by actor and action, at a million rows in under a second (SC-007). A union of two
differently shaped tables cannot be indexed for that, and the "one entry for a run of failed
attempts" rule would need a window function over the whole period on every read.

**Which events become entries**:

| Sign-in event | Entry | Why |
|---|---|---|
| `signed-in` | `signin.succeeded` | BRD catalogue «تسجيل دخول» |
| first `wrong-password` of a run | `signin.failed-run` | one entry per run (Chapter 09 §3-6) |
| later `wrong-password`, `account-locked` | none | counted into the run's entry when it is read |
| `session-ended` by sign-out | `session.signed-out` | FR-028 |
| `session-ended` for another reason | `sessions.ended` | with the reason, and by whom |
| `reset-completed` | `password.reset-completed` | BRD catalogue |
| `temp-password-changed` | `password.temporary-replaced` | FR-028 |
| `invitation-accepted` | `invitation.accepted` | BRD catalogue «قبول دعوة» |
| `access-reissued` | `account.access-reissued` | Chapter 10 §3-8 «إرسال رابط / توليد كلمة مؤقّتة» |
| `reset-requested`, `wrong-code`, `code-spent`, `renewal-reused` | none | nothing took effect, or it is a security signal for spec 025 |

**A run of failed attempts**: a run starts with a wrong password on an account whose count of
failed attempts was zero; that attempt writes the run's entry. The number of attempts and whether
the run ended in a lock are not stored (an entry cannot be changed): they are counted from
`SignInEvent` through Identity's contract when the entry is read, for the entries of the page
only.

**Alternatives**: read `SignInEvent` in place through a view (rejected above); write the run's
entry when the run closes (rejected: a run of two wrong passwords followed by nothing would never
appear).

## R3 — A change and its entry in one transaction

**Decision**: `IAuditTrail.RecordAsync(DbContext db, AuditRecord record, ct)` adds the entry on
the caller's own connection and inside the caller's transaction. A helper,
`AuditedSave.SaveAsync(db, trail, records, ct)`, opens a transaction when none is open, saves the
caller's changes, records, and commits — so the usual call site is one line and cannot save
without recording.

The record a caller passes holds only what the caller knows: the action, the target, the facts,
before and after. The actor, the actor's display name, the clinic, the address, the device, the
trace and the time are taken inside the trail from the signed-in session, the request and the
platform's clock (FR-014, FR-004). Work the platform does by itself calls
`RecordAsSystemAsync`.

**As built**: the same decision, reached differently — the writer lives in the shared project
and a module calls `db.Record(…)` on its own context; the next `SaveChangesAsync` writes the
entries with the change. See validation.md §5 for why.

**Rationale**: each module has its own `DbContext`, but all share one database. Writing through
the caller's connection is the only way the two succeed or fail together (FR-009) without a
distributed transaction or an outbox, and an outbox would let a change stand whose entry is
written later — which FR-009 forbids.

**Alternatives**: an outbox table per module (rejected: eventual, not atomic); the audit module's
own `DbContext` with `TransactionScope` (rejected: escalates to a distributed transaction).

## R4 — Staying

**Decision**: `audit.Entry` gets the same protection as `identity.SignInEvent`: a trigger that
refuses `UPDATE` and `DELETE` for everyone, the application's login included. No fingerprint
chain (clarified). Nothing prunes the table; the daily clean-up of spec 003 is left as it is.

**Rationale**: FR-020, FR-021, FR-021a. One mechanism already proven in the product.

## R5 — Isolation, and entries with no clinic

**Decision**: `audit.Entry` is clinic-owned: `ClinicId` is required and the table has the
isolation policy of spec 002, created in the same migration. Entries with no clinic — an
operator acting on the platform itself — have no writer until the operators' console exists
(phase 1); the table for them, `audit.PlatformEntry`, is created by the first spec that has such
an action. The operators' reading over clinics' entries is built now as a contract
(`IOperatorAuditReading`) with an integration test and no address.

**Rationale**: Constitution II is non-negotiable and its helper needs a required clinic column. A
nullable clinic would need a second policy shape for one table, for rows nobody can write yet.

**Open item recorded**: FR-001's "the clinic, when there is one" is met for every entry that can
exist today; the clinic-less table is deferred with its first writer.

## R6 — The catalogue of actions

**Decision**: `AuditActions` in `Scs.SharedKernel/Audit`: for each action its code, category,
name in Arabic and English, the sentence that describes an entry in both languages with named
placeholders, the fact keys it may carry, and whether it must carry before and after. Codes are
frozen by a committed list, `audit-actions.lock`: a test fails when a locked code is missing or
its category changed, and when a code of the catalogue is not in the list (the developer adds
it, on purpose). A second test fails when any action allows a fact key from the never-list
(`password`, `code`, `token`, `link`, `secret`, `key`, `email`, `proof`, `cookie`).

The catalogue is not exported to the web apps in this spec: there is no screen, and the reading
address answers words already in the reader's language (R9).

**Rationale**: FR-006 to FR-008, FR-016, FR-039; the same pattern as the permission catalogue.

## R7 — Facts, and before and after

**Decision**: two JSON columns. `Facts`: a flat object whose keys are checked against the
action's allowed keys when the entry is written; values are text up to 200 characters, numbers,
booleans, or lists of codes. `Changes`: a list of `{ field, before, after }` for single values,
and `{ field, added, removed }` for sets. An action marked as needing before and after is refused
with an empty `Changes`; an entry whose every change has `before` equal to `after` and nothing
added or removed is not written (FR-012).

**Rationale**: FR-002, FR-010 to FR-012. The shape of facts differs by action and there are
eighty-five specs to come; a column per fact is not possible.

## R8 — Nothing forgotten

**Decision**: every route that is not a read declares one of three things, as endpoint metadata
beside the permission declarations of spec 005: `.Records(code…)`, `.RecordsNothing(reason)`, or
`.RecordedBySignIn()` for the routes whose effect is a sign-in event. An architecture-level
integration test lists every mapped route and fails for any `POST`, `PUT`, `PATCH` or `DELETE`
that declares none, naming it. The role service of spec 005 has no route yet (spec 027 adds
them); it records inside its own methods, and its tests assert the entries.

No run-time check that a declared route did record: an action that leaves a value as it was
legitimately records nothing (FR-012).

**Rationale**: FR-023, FR-024, SC-001. The same mechanism and the same test style as spec 005,
so a developer meets one habit, not two.

## R9 — Reading

**Decision**: `GET /v1/audit/entries` (permission `reports.activity`): `from`, `to` (default the
last ninety days), `category`, `actor`, `action`, `q`, `page`, `size` (default 25, at most 100).
It answers the page of entries, the total, and the count in each category for the period and the
other facets. Each entry is answered as words in the reader's language (`Accept-Language`, as
every route): the action's name and the describing sentence are produced from the catalogue and
the stored facts. `q` matches the actor's name, and the action's name by finding the catalogue's
actions whose name in the reader's language contains it.

Index: `(ClinicId, OccurredAtUtc DESC, Id)` including `Category`, `ActionCode`, `ActorAccountId`.

A clinic's reading returns entries whose actor is one of its people, and the sign-in entries on
its accounts (which have the account as their subject and no acting person when the sign-in
failed); entries by the platform or an operator are filtered out (FR-030).

**Rationale**: FR-030 to FR-036, SC-007. Words are produced on the server because the catalogue
is there and the web apps have no screen for it yet; spec 059 may ask for codes as well if it
needs them.

## R10 — Address and device

**Decision**: the address and the user agent are stored as received, truncated like the
session's, and marked as personal data so that operational logs never carry them. The device is
described when read — "Chrome on Windows" / «متصفح Chrome على Windows» — by a small parser of
the user agent kept in the audit module (browser family and operating system; "unknown device"
when neither is recognised).

**Rationale**: FR-038. Storing the raw text keeps old entries describable when the parser learns
a new browser.

## R11 — Names at the moment

**Decision**: the actor's display name is stored on the entry: the email address today
(clarified), supplied by `ICurrentStaff`. A target that is an account is stored by reference
alone; when read, its name is asked from Identity for the page's accounts, and an account that
no longer exists reads «حساب محذوف» / "deleted account". Any other target stores its name at the
moment (a role's name; for a ready role under its own name, its ready key, so it is named in the
reader's language).

**Rationale**: FR-003, FR-018, FR-037.

## R12 — What spec 005's roles record

**Decision**: `ClinicRoles` records `role.created`, `role.updated`, `role.deleted` and
`account.role-changed` through `AuditedSave`. The ready roles a clinic gets when it is
established, and the roles the roll-out migration of spec 005 gave existing clinics, are recorded
with the platform as actor: the first by `ReadyRoles.EnsureAsync`, the second by this spec's
migration, dated at each role's creation.

**Rationale**: FR-025 to FR-027.

## R13 — Categories

**Decision**: security — `signin.succeeded`, `signin.failed-run`, `session.signed-out`,
`sessions.ended`, `password.reset-completed`, `password.temporary-replaced`; users and
permissions — `invitation.accepted`, `account.access-reissued`, `account.role-changed`,
`role.created`, `role.updated`, `role.deleted`. Changes, and records and invoices, have no action
yet.

**Decided by the owner** (11 Oct 2026): everything about access to accounts is under security.
The BRD's tab «أمنية» lists failed attempts, the lock and closing an alert and is silent on a
successful sign-in, a sign-out and the password events; the BRD should say so too.

## R15 — Wording from the design

**Decision**: the names and sentences of entries follow the frames of the Activity log page
(`5450:4`), read on 11 Oct 2026: frames "Log" (`5632:44831`, LTR `5632:52027`), "Filtered"
(`6589:934565`), "Activity Details" (`6636:967915`), "Security Details" (`5632:46851`). The
frames draw five rows; of this spec's twelve actions only the failed sign-in is among them. The
other eleven are written in the same form — a noun for the name, one passive sentence — and
listed as undrawn in the spec. The full table is in
[contracts/audit-trail.md](contracts/audit-trail.md) §7.

Two things the frames show that later specs must settle, noted for them: the security details
window shows the account's email address («الحساب») — it is read from the account when the
entry is read, not stored (FR-018); and the row «دُعيت د. ياسمين علي» addresses a person in the
feminine, which SH-098 forbids — spec 026 writes the invitation's sentence.

## R16 — Carried over from spec 006

**Decision** (owner, 11 Oct 2026): two items are settled with this work.

- *The timed test of spec 005.* "Not in your role within a second" passes alone and comes close
  to its limit when other suites run beside it, since the page is now inside the frame. The test
  is measured alone ten times; what the frame loads before the first page is reduced (below)
  rather than the limit raised.
- *The first download.* The clinic app builds at about 585 kB against a warning limit of 500 kB.
  The destinations' pages and the pages before sign-in other than the sign-in form are loaded
  when opened (`loadComponent`), so the frame and the sign-in form are what a person downloads
  first. `registerDestinationPage` takes a loader instead of a component.

**Rationale**: both get worse with every destination a later spec adds; fixing the mechanism
once now is cheaper than after eighty pages exist.

## R14 — Tests

Written first, because they are isolation and permission (Constitution, testing): an entry of one
clinic is unreadable from another by every way of reading; the reading address refuses without
`reports.activity`; the table's isolation sample. Then: the change and the entry together (a
forced failure on each side); update and delete refused; a fact outside the white list refused;
secrets passed alongside are nowhere in what is stored; the declaration test over every route;
the catalogue's lock and its two languages; one million entries read in under a second.

No new browser test and no design comparison: the spec builds no screen. The existing browser
suite must pass unchanged after the loading change of R16, and the build must show no size
warning. Principle VII's real-stack
check is done by changing a role and signing in on the running stack and asking the reading
address.
