# Implementation Plan: Files and Attachments

**Branch**: `009-file-storage` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-file-storage/spec.md`

## Summary

One place where every part of the product stores and fetches files: a table of kinds kept with
the product; an upload that is checked by the platform itself — size, count, type read from
the content — and scanned for malware; a download given only to someone who may see the record
the file belongs to, through a way in that lasts five minutes and is recorded; removal that can
be undone for thirty days; retention periods the platform applies once they are set; and the
upload control and file card of the design, for every page that follows.

The approach: a new server module, `Scs.Files`, owning schema `files`. The table of kinds is
in the shared kernel, frozen by a lock file. A file is uploaded to the platform as it is chosen
and waits unattached; the module that owns a record attaches it on its own save —
`db.AttachFiles(…)` — in one transaction, as audit entries and notifications are written. Who
may upload and who may see is answered by the kind's owner, registered by the owning module;
with no owner, nobody. The content lives behind a store interface, keyed by clinic; the scan
runs in a job behind a scanner interface, so an outage delays availability and nothing else.
The console gets `scs-file-upload`, `scs-file-card` and `scs-file-field` in the shared library
and a builders-only page to see them on.

No page a clinic can open.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new as packages. EF Core, Hangfire through the platform's
schedulers, the platform's data protection, Transloco and the `ui` library. The ClamAV daemon is
a new service the platform talks to over the network, optional in the local stack.

**Storage**: SQL Server 2022 — new schema `files` with one clinic-owned table, `StoredFile`,
isolated by the policy of spec 002 in the migration that creates it, and one platform-wide
table, `RetentionPeriod`. File content in a store behind `IFileStore`: a folder locally and in
tests; the cloud's store with spec 001's cloud tasks.

**Testing**: xUnit on the SQL Server fixture; isolation and permission tests written first. A
store contract suite. A made-up scanner in the test host, the test-signature scanner on the
real stack. Vitest for the service, the state and the components; Playwright on the builders'
page with pictures beside the design.

**Target Platform**: the platform API and its worker; the clinic console (the components are in
the shared library for the other two apps).

**Project Type**: web application — a server module with its contracts, additions to the shared
projects, and shared web components.

**Performance Goals**: a 10 MB file accepted or refused within 15 seconds on an office
connection (SC-001); available within a minute of upload with the scanner working (SC-002); a
download starts within 2 seconds (SC-006); a backlog cleared within five minutes of the
scanner returning (SC-007).

**Constraints**: every check is the platform's own (FR-012); nothing unscanned is given out
(FR-015); a file and its record are one transaction (FR-007); who may see is asked at every
download (FR-020); no lasting address for a private file (FR-022); private content byte for
byte (FR-028b); no file name, content or way in in operational logs (FR-036); content kept
apart by clinic (FR-034).

**Scale/Scope**: 56 functional requirements; 6 kinds in the table (5 of the BRD, 1 for
builders); 2 tables; 5 addresses and 4 for builders; 3 jobs; 2 audit actions; 1 notification
event; 2 declared crossings; 3 web components and 1 builders' page.

**Open items carried from research**: the cloud store and the backlog alarm go with spec 001's
open cloud tasks (R2, R6). For the owner: the retention values (SH-095); the BRD lines the
clarifications changed (Chapters 03, 04, 08, 09, 10, 11); the wording of five undrawn states
(R15).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite SH-095, Chapter 08 (ق-٢٠, ق-٢٤, ق-٣٦, CL-TK-070…087) and Chapter 03 (ق-٤٤, CL-PT-096…106). The design's "File Upload" (`4260:814`) and "File Attachment Card" (`4252:63169`) were read and are what is built; each state is compared in both languages. Seven clarified answers go beyond the BRD and are listed for the owner |
| II. Tenant isolation in the database (non-negotiable) | Pass | `files.StoredFile` is clinic-owned with its policy in the creating migration and its sample in the isolation suite; content is keyed by clinic. `files.RetentionPeriod` is platform-wide by a reviewed entry: four classes and a number, no clinic data. Two declared crossings: clinic ids with files to scan; one row of a public kind |
| III. The server decides; deny by default | Pass | Every upload and download asks the kind's owner on the server; a kind with no owner is refused for everyone; limits are enforced on the server whatever the browser did; a way in is bound to its account and expires |
| IV. Financial records are immutable; changes are audited | Pass | No money moves. Downloads and refused scans are recorded by the engine; attaching, removing and restoring by the owning module in the same save |
| V. Secrets and patient data | Pass | Private storage, no public address; content given as an attachment that cannot run; names, content and tokens never logged; the test-signature scanner cannot start outside local and test; hidden details removed from public images |
| VI. Bilingual, complete screens | Pass | Both components in Arabic and English, both directions, with loading, error, disabled and offline states; sizes in the language's digits; keyboard and screen reader covered |
| VII. Tested before merge, verified after implementation | Pass, one exception | The refusal path "read-only clinic" (FR-009, FR-025) cannot be tested: no clinic can be read-only until the subscription specs. Recorded below; the spec that builds the state adds the two tests. Otherwise: full suites; the walk of quickstart "By hand" on the real stack; the components laid beside their design in both languages, with one pair kept as `design-vs-real.png`; recorded in `validation.md` |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged, with three things stated plainly. A second
shared writer joins the audit and notification ones: an owning module's save writes
`files.StoredFile` through it, the one sanctioned way. The two addresses that give a file out
— and the two that take one in and remove an unattached one — are open to any signed-in person
and decided by the kind's owner, the uploader's identity and the token, not by one permission;
the four join spec 005's written list with that reason. And one kind, `dev.sample`,
exists only on a developer's machine and in tests, so that the feature can be walked for real
before any page owns a kind.

## Project Structure

### Documentation (this feature)

```text
specs/009-file-storage/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── files-engine.md             # how a later spec adds, owns and uses a kind
│   └── files-api.openapi.yaml      # upload, remove, way in, content, public
├── checklists/requirements.md
└── tasks.md                        # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/
│   ├── Files/
│   │   ├── FileKinds.cs                 # the table: key, types, size, count, public, class
│   │   └── file-kinds.lock              # keys, frozen
│   ├── Audit/AuditActions.cs            # + file.downloaded, file.scan-refused
│   ├── Notifications/NotificationEvents.cs   # + file.scan-refused
│   └── Wording/Words.cs                 # + a size in words of the language
├── shared/Scs.BuildingBlocks/
│   ├── Files/
│   │   ├── FileChanges.cs               # FileRecord, what a caller passes
│   │   ├── FileWriter.cs                # attach, remove, restore, retention clock, hold — in the caller's transaction
│   │   ├── IFileKindOwner.cs            # who may upload, who may see
│   │   ├── IFileStore.cs                # put, open, delete, delete a clinic's
│   │   └── IMalwareScanner.cs           # clean, infected, could not answer
│   ├── Tenancy/ClinicAwareDbContext.cs  # + AttachFiles, RemoveFile, RestoreFile, Start/StopFileRetention, Hold/ReleaseFiles
│   ├── Tenancy/Crossing.cs              # + two crossings
│   ├── Tenancy/PlatformWideTables.cs    # + files.RetentionPeriod
│   └── Configuration/ScsOptions.cs      # + Files, Scan; validator rules
├── modules/Files/
│   ├── Scs.Files.Contracts/             # IRecordFiles, the views a page shows
│   └── Scs.Files/
│       ├── FilesModule.cs
│       ├── Domain/StoredFile.cs, RetentionPeriod.cs
│       ├── Infrastructure/
│       │   ├── Persistence/FilesDbContext.cs
│       │   ├── Migrations/…_InitialFiles.cs
│       │   ├── Uploads.cs               # stream in, count the size, read the type, store, row
│       │   ├── FileSignatures.cs        # pdf, png, jpeg from the first bytes
│       │   ├── ImageDetails.cs          # strips segments and chunks of public images
│       │   ├── FileAccess.cs            # the checks, the token, the audit entry
│       │   ├── RecordFiles.cs           # a record's files, for the owning page
│       │   ├── FolderFileStore.cs       # the local store
│       │   ├── ClamdScanner.cs          # the daemon's stream protocol
│       │   ├── TestSignatureScanner.cs  # local and test only
│       │   ├── FileScanJob.cs, ScanSweepJob.cs
│       │   ├── FileCleanupJob.cs        # unattached, removed, age; and a clinic's all
│       │   ├── ScanBacklogHealthCheck.cs
│       │   └── DevSampleOwner.cs        # the builders' kind
│       └── Api/FileEndpoints.cs
├── host/Scs.Api/ModuleRegistration.cs, tools/Scs.Migrator/Migrator.cs   # + module; + `file-kinds` tool
└── tests/
    ├── Scs.UnitTests/Files/             # the table and its lock, signatures, stripping, sizes, the protocol
    ├── Scs.ArchitectureTests/FileRulesTests.cs
    └── Scs.IntegrationTests/Files/, Isolation/FileSamples.cs, Fixtures/FileFixtures.cs

src/web/
├── projects/core/src/lib/files/
│   ├── file-kinds.json                  # written by the migrator's tool; checked by a test
│   ├── file-kinds.ts                    # reads it; sizes and types as words
│   ├── files.service.ts                 # upload with progress, way in and download, remove
│   └── file-uploads.ts                  # the state of one control
├── projects/ui/src/lib/files/
│   ├── file-upload.component.ts         # the zone, six states
│   ├── file-card.component.ts           # the card and its states
│   └── file-field.component.ts          # the zone over its cards
├── projects/clinic/src/app/dev/files.page.ts      # builders only; route dev/files
├── projects/{clinic,ops,patient}/public/i18n/{ar,en}.json   # ui.files.*
└── e2e/
    ├── staff-platform.ts                # answers the files addresses
    └── files.spec.ts                    # every state, both languages, keyboard, pictures

deploy/compose/docker-compose.yml        # + clamav under profile `scan`; + SCS_FILES__ROOT volume
docs/adding-a-module.md, docs/setup.md, ops/runbooks/data-handling.md
specs/005-permissions-engine/contracts/access-engine.md   # the own-account list
```

**Structure Decision**: one new module beside Audit and Notifications, with the same split —
the table in the shared kernel, the writer and the two interfaces a host must supply in the
shared project, the table, the jobs and the addresses in the module. The web's two drawn
components live in `ui`; what talks to the platform lives in `core`.

**Terms**: a "kind" is a row of `FileKinds`; a "file" is a row of `files.StoredFile` and its
content; a "record" is whatever a file is attached to, named by a kind and an id; "the owner"
of a kind is the module that answers who may upload and see; a "way in" is the token address of
one download.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| A third shared writer on the caller's connection (`FileWriter`) | A record and its files must commit together (FR-007) | An attach address called after the save can leave a record without its file, or a file on a record that was never saved |
| A platform-wide table, `files.RetentionPeriod` | SH-095 makes retention a platform setting an operator changes | Environment configuration cannot be changed by an operator and needs a release |
| Two more crossings of the clinic boundary | The scan sweep must find work without asking every clinic every minute; a public image is asked for with no clinic | A recurring job per clinic per minute is thousands of empty jobs; a clinic id in a public address lets visitors probe clinics |
| Two scanners in the product, one for local and test only | The refused path must be walkable on a laptop (Constitution VII) without a gigabyte of signatures | A made-up scanner in tests alone leaves the real stack unable to show a refusal; requiring ClamAV everywhere makes the local stack too heavy for the team's machines |
| A kind and an owner that exist only locally (`dev.sample`) | No page owns a kind until specs 029, 032, 082; the engine must be walked before then | Waiting for the first owner leaves this spec verified by automated tests alone |
| The cloud store is not built here | There is no cloud environment to run it against (spec 001, T055–T059 open) | Writing it unrun would be unverified code presented as done |
| Scripts inside a PDF are not looked for | Finding them reliably means rewriting the file, and a private file is kept byte for byte (FR-028b) | A best-effort check misses hidden ones and gives false comfort; the scan, download-only and never opening inside the product are the protection |
| The read-only refusal path is not tested (Constitution VII) | No clinic can be read-only before the subscription specs; there is no state to put a clinic in | Inventing a read-only switch here would build part of another spec to test this one |
| A file's retention period is started by its owner, not by its upload | SH-095's periods run from an event of the owner — a clinic cancelled, a ticket closed | Counting from upload would erase a working clinic's files |
| Give-out addresses not guarded by one permission | Who may see a file depends on its record, which only its owner module knows | A permission per kind cannot say "may see this ticket" |
