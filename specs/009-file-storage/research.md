# Research: Files and Attachments

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-10-11

Decisions taken before design. Each says what was chosen, why, and what was set aside.

## R1 — Where it lives

**Decision**: a new server module, `Scs.Files` with `Scs.Files.Contracts`, owning schema
`files`. The table of kinds is in the shared kernel (`FileKinds`, frozen by `file-kinds.lock`),
as the tables of permissions, audit actions and notification events are. The writer that
attaches, removes and restores a file on the caller's own save is in the shared project
(`Scs.BuildingBlocks/Files`), as the audit and notification writers are.

**Rationale**: the same split has held for specs 007 and 008. A module that owns a ticket must
attach a file in the transaction that saves the ticket (FR-007), and a contracts project may
not use the database library.

**Alternatives**: files inside each owning module (five copies of the checks, and the weaker
door the clarification closed); an attach call to the Files module after the save (the file
and the record can then disagree).

## R2 — Where the content is kept

**Decision**: behind one interface, `IFileStore`: put a stream under a key, open it, delete it,
delete everything under a clinic. The key is `{clinicId}/{fileId}` — never the file's name
(FR-011). One implementation is built now: a folder (`SCS_FILES__ROOT`), used on a developer's
machine and by the tests. The store for the cloud (object storage in the region of SH-090, a
private bucket, encrypted, no public access) is written with the cloud environment itself —
spec 001's tasks T055 to T059, which are open — against the same interface and the same
contract tests.

**Rationale**: there is no cloud environment yet; code for a bucket that cannot be run is not
verified work (Constitution VII). The interface and a suite of tests any store must pass make
the second implementation a small, checkable step. Content is kept apart by clinic in the key
itself, which makes "remove every file of a clinic" (FR-032) one prefix.

**Alternatives**: content in the database (ten megabytes a row burdens backups and the row
store); an object-storage emulator in the local stack now (another container to download and
run on machines already short of disk, to exercise code whose real target does not exist yet).

**Open, recorded**: the cloud store. Listed in the plan as carried to spec 001's cloud tasks.

## R3 — How a file comes in

**Decision**: the browser sends one file per request to the platform, `POST /v1/files?kind=…`,
as it is chosen. The platform reads it as a stream: the first bytes decide the type (R4); the
size is counted as it arrives and the request is cut off when it passes the kind's limit; the
content goes to the store under a new id; a row is written as "unattached, being checked". The
answer is the file's id, name, size, type and state. The request size limit of the route is
the largest limit of any kind plus the envelope. Before anything is read, two counts of the
person's own rows are checked — unattached now, and uploaded in the last ten minutes — and an
upload beyond thirty or sixty is refused with `FILE_LIMIT_REACHED` and a time to try again
(FR-012a). Counting rows, not requests, needs no shared limiter between instances.

**Rationale**: the largest file is 10 MB; passing it through the platform lets every check be
the platform's own (FR-012) before anything is kept, with one code path for every store.

**Alternatives**: the browser uploading straight to storage with a signed address (the type
and size checks would run after the fact, on a file already stored; it also ties the browser
to one kind of store); several files in one request (one refused file would fail the others —
FR-042).

## R4 — Reading the type from the content

**Decision**: a small table of signatures owned by the platform: PDF (`%PDF-`), PNG (its eight
bytes and an `IHDR` first), JPEG (`FF D8 FF` and a valid first segment). A kind lists the types
it takes by name (`pdf`, `png`, `jpeg`); a file whose first bytes match none of its kind's
types is refused with `TYPE_NOT_ACCEPTED`. The type stored is the one read, and the one the
file is later given as. The name and the browser's stated type are ignored for this.

**Rationale**: three formats with unmistakable signatures; a library that guesses among
hundreds would accept more than a kind asks for. A new format is one line of the table and its
tests.

**Alternatives**: a general type-detection package (more formats than wanted, one more
dependency to keep patched); trusting the extension (the thing ق-٣٦ forbids).

## R5 — The scanner

**Decision**: behind one interface, `IMalwareScanner`: scan a stream, answer clean, infected,
unreadable or "could not answer". "Unreadable" is a file the scanner cannot look inside: the
daemon is run with its alerts for encrypted documents and broken files on, and those findings
are read as unreadable, not as malware; the platform also reads a PDF's own declaration that
it is encrypted. An unreadable file is refused for the reason `unreadable` (FR-014a). The implementation for real environments speaks the ClamAV daemon's
stream protocol over the network (`SCS_SCAN__HOST`, `SCS_SCAN__PORT`), written directly — a few
dozen lines — with a time limit. The local stack gains an optional ClamAV service (a compose
profile, off by default because of its size). When no scanner address is configured, a
developer's machine and the tests use a scanner that knows only the standard malware test
signature (EICAR); the options validator refuses to start any other environment without a
scanner address.

**Rationale**: ClamAV is the common open scanner, runs as a service beside the platform in any
cloud, and its protocol is stable and tiny. The test-signature scanner lets the whole flow —
refused, told, recorded — be walked on a laptop without a gigabyte of signatures, and cannot
reach production by configuration.

**Alternatives**: a cloud provider's scanning service (ties the feature to one provider before
the hosting question of SH-090 is closed); a scanning library inside the process (none that is
maintained); no local scanner at all (the refused path could then be tested only with a
made-up one).

## R6 — When the scan runs, and when the scanner is down

**Decision**: the upload commits first; then a job for that clinic scans what is waiting. A
sweep every minute finds the clinics that still have files waiting and enqueues them — a
declared crossing that returns clinic ids only. Clean → the file becomes available. Infected or
unreadable → its content is erased at once, its state is `refused` with the reason, its uploader
is told and the trail has its entry (R13, R14), all in one save. Could not answer → nothing changes and the next sweep
tries again (FR-018). Files waiting longer than an hour are counted by a health check that
reads "degraded" and by one error-level log line per sweep, with counts and no names; the
alarm on it is wired with the monitoring of spec 001 (T077, open).

**Rationale**: the same shape as the notification emails of spec 008: the request never waits
for a slow service, an outage heals itself, and "being checked" is an honest state.

**Alternatives**: scanning inside the upload request (a slow or absent scanner fails or stalls
every upload — the clarification chose otherwise); a per-clinic recurring job for every clinic
every minute (thousands of empty jobs).

## R7 — Attaching, removing and restoring on the owner's save

**Decision**: three calls on every clinic-aware context, flushed by `SaveChangesAsync` in the
caller's transaction, as `db.Record(…)` and `db.Notify(…)` are:

- `db.AttachFiles(kind, record, fileIds)` — each file must be of that kind, uploaded by the
  current person, unattached, not removed; the record must not pass its kind's count.
- `db.RemoveFile(fileId)` and `db.RestoreFile(fileId)` — the file must be attached to a record
  of the caller's kind; restoring checks the thirty days and the count.
- `db.StartFileRetention(record, moment)` and `db.StopFileRetention(record)` — R11.
- `db.HoldFiles(record)` and `db.ReleaseFiles(record)` — R11.

What fails throws, and the save fails with it (FR-007). The engine keeps who and when; the
owning module records its own audit action beside the call (FR-035).

**Rationale**: one transaction for the record and its files, by the one way two specs already
use to write another module's table.

**Alternatives**: attaching by an address the browser calls after saving (two requests that
can half-succeed); a foreign key from every owner table to `files.StoredFile` (every module
would then reference another module's table).

## R8 — Who may upload, and who may see

**Decision**: each kind has an owner, registered by the module that owns it: `IFileKindOwner`
with the kind's key, `MayUploadAsync()` and `MaySeeAsync(recordKind, recordId)`. The upload
address asks the first; the download address asks the second, every time (FR-020). A kind with
no registered owner is refused for everyone (FR-021); an architecture test lists the kinds of
the table and their owners so a kind is not shipped without one unknowingly. A read-only clinic
does not exist in the product yet: the upload is an ordinary changing request and the download
asks nothing of the clinic's state, so the spec that builds the state refuses the one and
leaves the other, and adds the two tests (FR-009, FR-025).

In this spec no page owns a kind yet. The five kinds of FR-002 are in the table with no owner
and are therefore refused until their specs arrive. To walk the feature on a real stack, the
Files module registers one kind of its own, `dev.sample`, on a developer's machine and in tests
only — any signed-in person uploads, and sees what they attached to their own account — beside
the existing `/v1/dev/…` addresses.

**Rationale**: only the owning module knows who may see a ticket. Deny by default without an
answer is Constitution III.

**Alternatives**: a permission code on each kind (who may see a ticket is not one permission);
letting the engine guess from the record's kind.

## R9 — Giving a private file out

**Decision**: the four addresses a signed-in person uses — upload, remove an unattached file,
ask for a way in, fetch the content — are not guarded by one permission: the kind's owner, the
uploader's identity and the token decide. All four join spec 005's written list of such
addresses with that reason.

Giving a file out is two steps. `POST /v1/files/{id}/access` checks the clinic, the state and the
owner's answer, writes the audit entry `file.downloaded`, and returns an address with a token
and its expiry. `GET /v1/files/content/{token}` gives the bytes. The token is made by the
platform's data protection, holds the file's id, the account it was issued to and an expiry of
five minutes, and is useless to anyone else: the address also requires that account's session.
The content is sent as an attachment under the name it was uploaded with, with the type the
platform read, "no sniffing", a sandboxing content policy and "do not store".

A file being checked, refused, removed, or erased for its age answers the first step with its
own code (`FILE_BEING_CHECKED`, `FILE_REFUSED`, `FILE_NOT_KEPT`) so the page can say it
(FR-026); another clinic's file, another person's unattached file and no file answer 404 alike.

A way in may be used any number of times in its five minutes by its account — a browser asks
again for ranges and retries; the audit entry is written once, when it is issued. A private
file is only ever sent as an attachment (FR-028c).

**Rationale**: "a temporary link after the permission check, no public address" (Chapter 08
§3-3). Binding the token to the account answers the spec's "nobody signed in gets nothing" and
makes a forwarded address worthless even inside its five minutes. One audit entry per issued
way in is "one entry per download" without counting range requests.

**Alternatives**: a signed address of the storage itself (works only for one kind of store, is
usable by anyone who has it, and cannot carry the response headers of FR-028); streaming on
the first request with no token (a download started by a link or a new tab then needs the
check repeated with no place to record it once).

## R10 — Public kinds

**Decision**: a kind marked public is served at `GET /v1/public/files/{id}` with no session,
only when it is attached and clean, with a long cache time. Because that request has no
clinic, the read is a declared crossing, `read-public-file`, that can return rows of public
kinds only. At upload, an image of a public kind has its descriptive parts removed without
re-encoding: for JPEG the application and comment segments that carry Exif, XMP and IPTC; for
PNG the text, time and Exif chunks. Private kinds are stored byte for byte (FR-028b).

**Rationale**: removing whole segments and chunks is lossless, needs no imaging library, and
is small enough to test exhaustively. The only public kind today is a doctor's photo; it has no
owner until the doctors' spec, so the public address is exercised by tests and the dev kind.

**Alternatives**: an imaging library to re-encode (a large dependency, and a licence question,
for one operation); a public address that carries the clinic (lets a visitor probe clinic
ids).

## R11 — Removal, restoring, erasing

**Decision**: a file's row carries `RemovedAtUtc`/`RemovedBy` and `ErasedAtUtc`. A daily job,
clinic by clinic:

1. deletes rows and content of files unattached for more than 24 hours (FR-029);
2. erases the content of files removed more than 30 days ago and marks them erased (FR-030a);
3. erases the content of files whose retention period has run out — a period is set for
   their class, their owner has started it (`RetainFromUtc`), and that moment plus the period
   is past (FR-031) — removed or not.

A file on hold (`HeldAtUtc`) is skipped by rules 2 and 3 until released. The owner sets and
lifts a hold for a record — `db.HoldFiles(record)`, `db.ReleaseFiles(record)` — and a file
attached to a record that has a held file is held with it (FR-031b).

The clock is the owner's: `db.StartFileRetention(record, moment)` and
`db.StopFileRetention(record)` on the owner's save set and clear `RetainFromUtc` on every file
of that record (FR-031a). Nothing starts at upload.

Erasing deletes the content from the store and keeps the row: name, size, who and when, and why
it has no content (`removed`, `age` or `refused`). A second job entry point removes everything
of one clinic, rows included (FR-032), for the spec that erases a cancelled clinic.

**Rationale**: one job, three rules, each idempotent. The row that stays is what lets a record
say "a file was here".

**Alternatives**: deleting rows on removal (the record silently loses a line, and the thirty
days cannot exist); a queue of erasures (a daily pass is enough for periods measured in days);
counting a file's age from its upload (found at analysis: SH-095's periods run from an event of
the owner — a clinic cancelled, a ticket closed — so a doctor's photo of a working clinic would
have been erased).

## R12 — Retention periods

**Decision**: a small platform-wide table, `files.RetentionPeriod`: one row per retention
class — `medical-record`, `conversations`, `ticket-attachments`, `clinic-data` — with a number
of days that is empty until set. It belongs to no clinic and holds no patient data, so it is
added to the reviewed list of platform-wide tables. Nothing in this spec sets it: the page is
the operators' settings (spec 024). The clean-up reads it; tests set it directly.

**Rationale**: SH-095 makes the periods a platform setting and the lawyer's values are not in.
The mechanism must exist and be proven before they are.

**Alternatives**: configuration values in the environment (an operator could not change them,
and a change would need a release); a period on each kind (SH-095 speaks of classes of data).

## R13 — What the trail records

**Decision**: two actions join the audit catalogue, written by the engine: `file.downloaded`
(category `records`; target the file by its name; facts: the kind and the record's kind) and
`file.scan-refused` (category `security`; by the system; target the file; fact: the kind).
Attaching, removing and restoring are recorded by the owning module with its own actions; the
dev kind records nothing.

**Rationale**: the clarified answers. The file's name is the target's label, as a role's name
is — it is what a person reading the trail needs; the trail is clinic data, not an operational
log.

**Alternatives**: one generic `file.attached` written by the engine (the owning page's own
action says more — "a result was attached to order 12").

## R14 — Telling the uploader

**Decision**: one new event in the notifications table, `file.scan-refused`: told to its
owner (the uploader), in the bell only, never emailed, drawn with the warning mark, leading
nowhere. Its line names the file: «{name} · لم يجتز الفحص الأمني» · "{name} · did not pass the
security check". For a file that could not be checked the line is «{name} · تعذّر فحصه — أرسلوه بلا كلمة
مرور» · "{name} · could not be checked — send it without a password", chosen by a second fact,
`reason`. Raised in the same save that marks the file refused.

**Rationale**: spec 008's way of adding an event; the clarified answer says bell, no email.

## R15 — The console's side

**Decision**:

- `core`: `file-kinds.json`, written by the migrator's tool from the server's table and checked
  by a test, as the permission catalogue is; `FilesService` — upload with progress, ask for a
  way in and start the download, remove before saving; `FileUploads`, the state of one
  control: its rows, whether anything is still uploading, the ids to send with the save.
- `ui`: two components from the design. `scs-file-upload` — the zone of section "File Upload"
  (`4260:814`), six states. `scs-file-card` — "File Attachment Card" (`4252:63169`): a mark by
  type (image, PDF, other), the name, a second line (size and date; or «جارٍ الرفع… ٦٤٪»; or
  the reason in the danger colour), and the actions (download and more; a spinner; retry and
  remove). And `scs-file-field`, which puts the zone over its cards and drives them from
  `FileUploads`.
- The states the design does not draw — being checked, refused by the check, no longer kept,
  the count reached, not uploaded for lack of connection — are built in the card's language
  and listed for the page review.
- A builders-only page, `dev/files`, beside `dev/sample-list`: every state of both components
  in both languages, and a live field on the `dev.sample` kind.

**Rationale**: the design draws exactly these two components and says how they combine. `ui`
may use `core`; `core` may not use `ui`.

## R16 — Tests

- **First, failing** (Constitution VII — isolation and permissions): isolation samples for
  `files.StoredFile`; another clinic's file and another person's unattached file are "not
  found" by every address; a kind with no owner is refused; an owner's "no" refuses; the
  crossings return what they declare and no more.
- **Integration**: upload checks (type by content against renamed programs, scripts and
  archives; size at and over the limit; empty; count); the scan's three answers and the
  scanner-down path; attach, remove, restore and their refusals on a save that then fails; the
  way in (expiry, another account, signed out, reuse); headers of FR-028; the audit entries and
  the notification; the clean-up's three rules with the clock moved; the public address and
  the stripped image; a store contract suite.
- **Unit**: the kinds table and its lock; signatures; segment and chunk stripping; sizes as
  words in both languages; the ClamAV protocol against a made-up socket.
- **Architecture**: only the shared writer writes `files.StoredFile` from outside the module;
  only the module reads it; every kind has an owner or is listed as waiting for its spec.
- **Web**: Vitest for `FilesService`, `FileUploads` and the three components; Playwright on
  `dev/files` for each state, the keyboard, the screen reader's words, and pictures for the
  design comparison.
- **Volume**: a 10 MB upload and a download start within the times of SC-001 and SC-006.
- **By hand**: the real stack with the test-signature scanner, and once with the ClamAV
  profile if the machine can hold it.

## Carried forward, not decided here

| Item | To |
|---|---|
| The store for the cloud | spec 001, T055–T059 |
| The alarm on files waiting for a scan | spec 001, T077 |
| The retention values | the lawyer, through the owner (SH-095) |
| The page that sets retention periods | spec 024 |
| Owners of the five kinds | their specs: 082, 032, consents, orders, 029 |
| Wording of the undrawn states | the page review |
