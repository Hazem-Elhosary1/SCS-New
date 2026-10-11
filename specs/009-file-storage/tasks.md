---

description: "Task list for 009-file-storage"
---

# Tasks: Files and Attachments

**Input**: Design documents from `/specs/009-file-storage/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 008 merged into `develop` and Docker running.

**Tests**: Included, and first. Isolation and permissions are test-first areas (constitution, testing): each of those test tasks is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Design**: two components of Figma file `4HLWEqlna9sbn68276LHYt` — section "File Upload" (`4260:814`, page "Components — Inputs") and "File Attachment Card" (`4252:63169`, page "Components — Clinic") — built in the shared library and compared in both languages. No page a clinic can open.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US7)
- Paths are relative to the repository root

## Path Conventions

- Files module: `src/server/modules/Files/Scs.Files/` (`Domain/`, `Infrastructure/`, `Api/`) and `src/server/modules/Files/Scs.Files.Contracts/`
- Table of kinds: `src/server/shared/Scs.SharedKernel/Files/`; the writer and the interfaces: `src/server/shared/Scs.BuildingBlocks/Files/`
- Server tests: `src/server/tests/Scs.IntegrationTests/Files/`, `src/server/tests/Scs.UnitTests/Files/`
- Web: `src/web/projects/core/src/lib/files/`, `src/web/projects/ui/src/lib/files/`, `src/web/projects/clinic/src/app/dev/`, `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: A known-good start and the empty module.

- [X] T001 Run the three gates on the branch before any change and note the counts for `validation.md`: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test` (from `src/web`)
- [X] T002 Create the projects `src/server/modules/Files/Scs.Files/Scs.Files.csproj` (references `Scs.BuildingBlocks`, `Scs.Files.Contracts`; `InternalsVisibleTo` the three test projects) and `src/server/modules/Files/Scs.Files.Contracts/Scs.Files.Contracts.csproj` (references `Scs.SharedKernel` only); add both to `src/server/Scs.sln`, `src/server/host/Scs.Api/Scs.Api.csproj`, `src/server/tools/Scs.Migrator/Scs.Migrator.csproj` and the three test projects; an empty `FilesModule.cs` registered after `NotificationsModule` in `src/server/host/Scs.Api/ModuleRegistration.cs` and `src/server/tools/Scs.Migrator/Migrator.cs`
- [X] T003 Add to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` the sections `Files` (`Root` — `SCS_FILES__ROOT`) and `Scan` (`Host`, `Port`, `TimeoutSeconds` default 30), and to `ScsOptionsValidator.cs` the rules of data-model §7: the root is required and "never inside the web root"; outside local and test a scanner host is required; `TimeoutSeconds` is "5 to 120"; with unit tests in `src/server/tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs`; add `SCS_FILES__ROOT` and a volume for it to `deploy/compose/docker-compose.yml` and `deploy/compose/.env.example`, and a `clamav` service under profile `scan`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The table of kinds, the table of files with its isolation, the store, the scanner and the owner interfaces. Every story needs them.

- [X] T004 [P] Write failing unit tests `src/server/tests/Scs.UnitTests/Files/FileKindsTests.cs`: the table holds exactly the six kinds of data-model §1 with their types, sizes, counts, public flag and retention class; keys are unique and match `area.name`, "lower case, at most 64"; every key is in `file-kinds.lock` and every locked key still exists; only `doctor.photo` is public and no kind of class `medical-record` is public; only `dev.sample` is `LocalOnly`; `Find` returns null for a stranger
- [X] T005 Create `src/server/shared/Scs.SharedKernel/Files/FileKinds.cs` (`FileKind(Key, Types, MaxBytes, MaxPerRecord, Public, RetentionClass, LocalOnly)`, `FileTypes` `pdf`·`png`·`jpeg`, `RetentionClasses` `medical-record`·`conversations`·`ticket-attachments`·`clinic-data`·`none`, the six kinds, `Find`, `Locked()`) and `file-kinds.lock` embedded in the project; T004 passes
- [X] T006 [P] Write failing isolation tests first: `src/server/tests/Scs.IntegrationTests/Isolation/FileSamples.cs` (a sample for `files.StoredFile`) and `src/server/tests/Scs.IntegrationTests/Files/FileIsolationTests.cs` — a clinic reads, updates and deletes only its own rows with the application's identity; with no clinic established it reads none; `files.RetentionPeriod` is listed in `PlatformWideTables` and readable by the application's identity but not writable; the guardrail passes with the new schema
- [X] T007 Create `Domain/StoredFile.cs` and `Domain/RetentionPeriod.cs`, `Infrastructure/Persistence/FilesDbContext.cs` (with its design-time factory) and migration `InitialFiles` exactly as data-model §2 and §4: `Kind` varchar(64) not null; `Name` nvarchar(255) not null; `SizeBytes` bigint > 0; `Type` varchar(16) of `pdf`·`png`·`jpeg`; `Scan` varchar(16) of `pending`·`clean`·`refused`; `RefusedFor` varchar(16) of `infected`·`unreadable` "set exactly when `Scan = 'refused'`"; `HeldAtUtc`, `HeldByAccountId`, `ReleasedAtUtc`, `ReleasedByAccountId` ("a hold requires attached"); `ScanTries` int default 0; `RecordKind` varchar(32), `RecordId` and `AttachedAtUtc` "null together or set together"; `RemovedAtUtc` set requires attached; `RetainFromUtc` datetimeoffset "null until the owner starts the file's retention period"; `ErasedFor` of `removed`·`age`·`refused` "set exactly when `ErasedAtUtc` is"; the five indexes `IX_StoredFile_Record`, `IX_StoredFile_Unattached`, `IX_StoredFile_Pending`, `IX_StoredFile_Removed`, `IX_StoredFile_Retained`, and `IX_StoredFile_Uploader` (`ClinicId`, `UploadedByAccountId`, `UploadedAtUtc`), with their filters; `CreateClinicIsolation("files", "StoredFile")`; `RetentionPeriod` (`Class` varchar(32) key, `Days` int null or > 0, `SetAtUtc`) seeded with the four classes and no days; add `files.RetentionPeriod` to `src/server/shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs` with its reason; T006 passes
- [X] T008 [P] Create the interfaces in `src/server/shared/Scs.BuildingBlocks/Files/`: `IFileStore.cs` (`PutAsync(clinicId, fileId, stream)`, `OpenAsync`, `DeleteAsync`, `DeleteClinicAsync`), `IMalwareScanner.cs` (`ScanAsync(stream)` → `Clean`·`Infected`·`Unreadable`·`NoAnswer`), `IFileKindOwner.cs` (`Kind`, `MayUploadAsync()`, `MaySeeAsync(recordKind, recordId)`) with `AddFileKindOwner<T>()`, and `FileChanges.cs` (`FileRecord(Kind, Id)`, `FileRefusedException` with a code)
- [X] T009 [P] Write a store contract suite `src/server/tests/Scs.IntegrationTests/Files/FileStoreContract.cs` (abstract; any store must pass it) and `FolderFileStoreTests.cs`: what is put is read back byte for byte; a key is `{clinicId}/{fileId}` and a name with a path or `..` cannot escape the root; deleting twice is not an error; `DeleteClinicAsync` removes that clinic's content and no other's; opening what is not there says so; a full or missing root fails the put and leaves nothing behind
- [X] T010 Create `Infrastructure/FolderFileStore.cs` and register it in `FilesModule.cs`; T009 passes
- [X] T011 [P] Write unit tests `src/server/tests/Scs.UnitTests/Files/ClamdScannerTests.cs` against a made-up socket — the stream protocol's framing, `OK` → clean, `FOUND` → infected, a `FOUND` whose name is one of the daemon's encrypted or broken findings → unreadable, a closed socket, a timeout and an error line → no answer — and `TestSignatureScannerTests.cs` (the standard test signature anywhere in the stream → infected; a PDF that declares itself encrypted → unreadable; anything else → clean); then create `Infrastructure/ClamdScanner.cs` and `Infrastructure/TestSignatureScanner.cs`, chosen in `FilesModule.cs` by whether a host is configured
- [X] T012 Create `src/server/tests/Scs.IntegrationTests/Fixtures/FileFixtures.cs`: test kinds registered through a replaceable table (`test.three-pdf-png-jpeg` 10 MB ×3, `test.one-pdf` 1 MB ×1, `test.public-image` public, `test.no-owner`), a `TestOwner` whose answers a test sets, a `FakeScanner` whose next answers a test sets, `UploadAsync(client, kind, name, bytes)`, sample bytes (`Pdf()`, `Png()`, `Jpeg(withExif)`, `Program()`, `Zip()`, `Script()`, `Eicar()`), `FilesAsync(database, clinic)` and `StoreHas(clinic, fileId)`

**Checkpoint**: the module exists, its table is isolated, content can be put and read, a scanner can be asked.

---

## Phase 3: User Story 1 - A person attaches a file to what they are writing (Priority: P1) 🎯 MVP

**Goal**: A file is uploaded as it is chosen, is the uploader's alone, and becomes part of a record with the save that names it.

**Independent Test**: upload a file for a test kind, save a record that names it, find it on that record with its name, size, uploader and time.

- [X] T013 [P] [US1] Write failing `src/server/tests/Scs.IntegrationTests/Files/UploadTests.cs`: `POST /v1/files?kind=` answers 201 with `id`, `name`, `sizeBytes`, `type`, `state: being-checked`; the row is unattached, of the caller's clinic and account, and the content is in the store; signed out → 401; an unknown kind and a kind with no owner → 403 `FORBIDDEN`; the owner's "no" → 403 `PERMISSION_REQUIRED`; a `LocalOnly` kind does not exist outside local and test; no file or two files in one request → 400 `VALIDATION_FAILED`; a colleague and a person of another clinic get 404 for the uploader's unattached file by every address; `DELETE /v1/files/{id}` removes the caller's unattached file, row and content, answers 204 twice, and 404 for another's or an attached one; the name is kept as given, cut to 255, without its path; the two routes carry their access and audit declarations
- [X] T014 [US1] Create `Infrastructure/Uploads.cs` and `Api/FileEndpoints.cs` (upload and delete) per contracts/files-api.openapi.yaml: read one part as a stream, ask the kind's owner, store under a new id, write the row; the route's request size limit is the largest kind plus the envelope; declare both routes as open to any signed-in person and decided by the kind's owner and the uploader's identity — add them to the own-account list of `RouteDeclarationTests` and of `specs/005-permissions-engine/contracts/access-engine.md` §1 with that reason, and to the allowed file of `IdentityRulesTests` — and to `RouteAuditDeclarationTests.Declared` (`RecordsNothing`); T013 passes except its type and size cases, which belong to US2
- [X] T015 [P] [US1] Write failing `src/server/tests/Scs.IntegrationTests/Files/AttachTests.cs`: `db.AttachFiles(kind, record, ids)` then `SaveChangesAsync` attaches each file in the caller's transaction — after a save that fails, and after a rolled-back caller transaction, the files are still unattached; refused with `FileRefusedException`, taking the save with it: a file of another kind, another person's file, another clinic's id, a file already attached, a removed or refused file, more than the kind's count on one record (`FILE_COUNT_REACHED`), an id that does not exist, a kind not in the table; a file not yet scanned can be attached (FR-018); the synchronous `SaveChanges` with file changes pending is refused; `IRecordFiles.ListAsync(recordKind, recordId, includeRemoved)` answers id, name, size, type, who uploaded and when, and the reader's state
- [X] T016 [US1] Create `src/server/shared/Scs.BuildingBlocks/Files/FileWriter.cs` and add `AttachFiles`, `RemoveFile`, `RestoreFile` to `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicAwareDbContext.cs`, flushed by `SaveChangesAsync` with audit records and raised events in one transaction; create `Scs.Files.Contracts/FileContracts.cs` (`IRecordFiles`, `RecordFileView`, the states `available`·`being-checked`·`refused`·`removed`·`not-kept`) and `Infrastructure/RecordFiles.cs`; T015 passes (its remove and restore cases belong to US6)

**Checkpoint**: a file can be uploaded and attached; nobody can fetch it yet.

---

## Phase 4: User Story 2 - Only a file that is what it says, and is clean, is ever given out (Priority: P1)

**Goal**: The platform's own checks — empty, size, count, type from content — and the scan.

**Independent Test**: a renamed program, an oversize file, one file too many and a file with the test signature are each refused by their own reason.

- [X] T017 [P] [US2] Write failing unit tests `src/server/tests/Scs.UnitTests/Files/FileSignaturesTests.cs`: a PDF, a PNG and a JPEG are read as themselves whatever their name; a Windows program, a shell script, a ZIP, an HTML page, an SVG, a GIF, a WebP and an empty buffer are none of the three; a PNG without `IHDR` first and a JPEG without a valid first segment are refused; fewer bytes than a signature needs is "none"
- [X] T018 [US2] Create `Infrastructure/FileSignatures.cs` and use it in `Uploads.cs`; add to `UploadTests.cs` and make pass: content that is none of the kind's types under an allowed name → 400 `FILE_TYPE_NOT_ACCEPTED` with `accepted`; a type the kind does not list although it is a known one → the same; an empty file → 400 `FILE_EMPTY`; one byte over the kind's size → 413 `FILE_TOO_LARGE` with `sizeBytes` and `maxBytes`, cut off while it arrives and nothing kept; exactly the size → accepted; the stored `Type` is the one read, not the browser's; a store that fails → 503 `FILE_NOT_STORED` and no row; a person with thirty unattached files, or sixty uploads in the last ten minutes, is refused with 429 `FILE_LIMIT_REACHED` and `retryAfterSeconds`, nothing is kept, a colleague is not affected, and attaching or removing files frees the first limit (FR-012a)
- [X] T019 [P] [US2] Write failing `src/server/tests/Scs.IntegrationTests/Files/ScanTests.cs`: after an upload the scan job marks a clean file `clean`; an infected one becomes `refused` with `RefusedFor = infected`, its content is gone from the store, `ErasedFor` is `refused`, and its row stays; a password-protected PDF becomes `refused` with `RefusedFor = unreadable` and is treated the same (FR-014a); an unattached refused file cannot be attached; a file found infected after it was attached reads `refused` on its record; the job is idempotent; the sweep's crossing `list-clinics-with-files-to-scan` returns exactly the clinics with a pending file and ids only
- [X] T020 [US2] Create `Infrastructure/FileScanJob.cs` (`IClinicJob`, enqueued after an upload commits) and `Infrastructure/ScanSweepJob.cs` (every minute) with the crossing in `src/server/shared/Scs.BuildingBlocks/Tenancy/Crossing.cs`; both jobs carry their audit declaration; update `DeclaredCrossingsTests` for the new count; T019 passes
- [X] T021 [P] [US2] Write failing tests for telling: in `ScanTests.cs` — a refused scan writes one audit entry `file.scan-refused` under `security`, by the system, with the file as its target and the fact `reason`, and one notification whose line differs for `infected` and `unreadable` `file.scan-refused` for the uploader, never emailed, in the same save as the state; an uploader who can no longer sign in is not told and the entry is still written; in `src/server/tests/Scs.UnitTests/Audit/` and `Notifications/` — the two actions and the event are in their catalogues and locks with names and sentences in both languages (data-model §6)
- [X] T022 [US2] Add `file.downloaded` and `file.scan-refused` to `src/server/shared/Scs.SharedKernel/Audit/AuditActions.cs` and `audit-actions.lock`, `AuditTarget.File`, and the event `file.scan-refused` to `src/server/shared/Scs.SharedKernel/Notifications/NotificationEvents.cs` and `notification-events.lock`, with the words of data-model §6; raise and record them in `FileScanJob.cs`; update the catalogue tests that count; T021 passes

**Checkpoint**: nothing but a checked, clean file can ever become available.

---

## Phase 5: User Story 3 - A file is given only to someone who may see what it belongs to (Priority: P1)

**Goal**: A way in that is checked every time, lasts five minutes, belongs to one account and is recorded.

**Independent Test**: one person downloads a file under its name; another person and another clinic are refused; the way in dies.

- [X] T023 [P] [US3] Write failing `src/server/tests/Scs.IntegrationTests/Files/AccessTests.cs`: `POST /v1/files/{id}/access` for a clean attached file whose owner says yes → 200 with `url`, `expiresAt` five minutes on, `name`, `sizeBytes`, `type`; the owner's "no" → 403 `PERMISSION_REQUIRED`; a kind with no owner → 403; the owner is asked at every request (yes, then no → refused); another clinic's file, another person's unattached file and no file → 404 alike; `pending` → 409 `FILE_BEING_CHECKED`; `refused` → 410 `FILE_REFUSED` with its `reason`; a way in works again within its five minutes for its account and writes no second audit entry; signed out → 401; `GET /v1/files/content/{token}` gives the bytes identical to the upload with `Content-Disposition: attachment` carrying the original name (Arabic and Latin), the type as read, `X-Content-Type-Options: nosniff`, `Content-Security-Policy: sandbox`, `Cache-Control: private, no-store`; the token is refused (404) after five minutes, for another account of the same clinic, signed out (401), when altered, and when the file was refused or removed since; one audit entry `file.downloaded` under `records` per issued way in, with the file as target and the facts `kind` and `record`, and none for any refusal
- [X] T024 [US3] Create `Infrastructure/FileAccess.cs` (the checks in the order of data-model §3, the token with purpose `Scs.Files.Access.v1`, the audit entry in the same save) and the two routes in `Api/FileEndpoints.cs`; add them beside the two of T014 to the own-account list of `RouteDeclarationTests` and `specs/005-permissions-engine/contracts/access-engine.md` §1 with their reason, and to `RouteAuditDeclarationTests.Declared`; T023 passes
- [X] T025 [P] [US3] Write failing `src/server/tests/Scs.IntegrationTests/Files/PublicFileTests.cs` and `src/server/tests/Scs.UnitTests/Files/ImageDetailsTests.cs`: `GET /v1/public/files/{id}` with no session gives an attached, clean file of a public kind with a cache time, and 404 for a private kind, an unattached, pending, refused or removed file and a stranger; the crossing `read-public-file` can return rows of public kinds only; a JPEG uploaded to a public kind with Exif, XMP and a comment is stored without them and still opens as the same picture, and a PNG without its text, time and Exif chunks; the same files uploaded to a private kind are stored byte for byte
- [X] T026 [US3] Create `Infrastructure/ImageDetails.cs`, use it in `Uploads.cs` for public kinds, add the public route and the crossing; declare the route anonymous in `RouteDeclarationTests`; T025 passes

**Checkpoint**: all P1 stories are done on the server; a file goes in, is checked, and comes out only to the right person.

---

## Phase 6: User Story 4 - Every page offers files the same way (Priority: P2)

**Goal**: The zone, the card and the field of the design, in both languages, on a builders' page.

**Independent Test**: on `dev/files`, each state of both components in Arabic and English beside its design.

- [X] T027 [P] [US4] Add the tool `file-kinds <path>` to `src/server/tools/Scs.Migrator/` and write `src/web/projects/core/src/lib/files/file-kinds.json` with it; add `FileKindsFileTests` beside `CatalogueFileTests` (the file on disk equals the table); create `src/web/projects/core/src/lib/files/file-kinds.ts` (`fileKind(key)`, `acceptedLabel(kind, language)`, `sizeLabel(bytes, language)` — «٢٫٤ ميجابايت» · "2.4 MB", «٢١٠ كيلوبايت» · "210 KB") with `file-kinds.spec.ts`
- [X] T028 [P] [US4] Write failing `src/web/projects/core/src/lib/files/files.service.spec.ts` and `file-uploads.spec.ts`: upload posts one file with its kind and reports progress in per cent; a refusal becomes a reason with its code and figures; `FileUploads` holds a row per chosen file (`uploading` with per cent · `uploaded` · `refused` with reason · `failed`), refuses before sending what the kind's size, types by name and count already forbid, keeps other files going when one is refused, exposes `busy()` and `ids()`, `remove(row)` deletes an uploaded one on the platform, `retry(row)` sends again, takes no more when the count is reached, and with no connection marks a file `failed` without asking; `download(id)` asks for a way in and opens it, and maps `FILE_BEING_CHECKED`, `FILE_REFUSED` (with its two reasons), `FILE_NOT_KEPT` to their states, and `FILE_LIMIT_REACHED` to "try again later"
- [X] T029 [US4] Create `src/web/projects/core/src/lib/files/files.service.ts` and `file-uploads.ts`, exported from `src/web/projects/core/src/public-api.ts`; T028 passes
- [X] T030 [P] [US4] Write failing unit tests in `src/web/projects/ui/src/lib/files/`: `file-upload.component.spec.ts` — the title and the hint from the kind in both languages; `data-state` of `default`, `drag`, `uploading`, `error`, `disabled`; a drop and a chosen file are emitted; Enter and Space open the chooser; the reason names the file; an error leaves the zone ready; `file-card.component.spec.ts` — the mark by type (image, PDF, other); name with `dir="auto"` and shortened by style; second line: size and date · «جارٍ الرفع… ٦٤٪» · the reason in the danger colour · «قيد الفحص» · «مرفوض» · «لم يعد محفوظًا»; actions by state (download and more · spinner · retry and remove), where "more" lists the actions the page passes in and is absent when it passes none; every control has an accessible name; `file-field.component.spec.ts` — the zone over its cards, driven by `FileUploads`; the limit said when reached; the live region announces accepted and refused
- [X] T031 [US4] Create `file-upload.component.ts`, `file-card.component.ts` and `file-field.component.ts` in `src/web/projects/ui/src/lib/files/` as section "File Upload" (`4260:814`) and "File Attachment Card" (`4252:63169`) draw them — read each with `get_screenshot` and `get_variable_defs` first — exported from `src/web/projects/ui/src/public-api.ts`; add `ui.files.*` to `src/web/projects/{clinic,ops,patient}/public/i18n/{ar,en}.json` with the design's words for the zone and the reasons of spec Assumptions; `npm run i18n:check` passes; T030 passes
- [X] T032 [US4] Create `Infrastructure/DevSampleOwner.cs` (local and test only: any signed-in person uploads; sees what is attached to their own account) with `POST /v1/dev/files/sample` (attach ids to the caller's account), `GET /v1/dev/files/sample` (the caller's files, removed ones included) and `POST /v1/dev/files/sample/{id}/remove|restore`, as contracts/files-api.openapi.yaml and data-model §8 give them, beside the existing `/v1/dev/…` routes and declared like them, with a test that they do not exist outside local and test; create `src/web/projects/clinic/src/app/dev/files.page.ts` with route `dev/files` in `src/web/projects/clinic/src/app/shell/shell.routes.ts`: a gallery of every state of the zone and the card, and a live field on `dev.sample` with «حفظ», the saved files with download, remove and restore
- [X] T033 [US4] Make `src/web/e2e/staff-platform.ts` answer the files addresses (upload with a settable answer, access, content, the dev routes) and write `src/web/e2e/files.spec.ts`: each state of the zone and the card in Arabic and English; a dropped and a chosen file; progress, then the card; save disabled while uploading; a refused file names itself and its reason and the zone is ready; three files then the limit; remove before saving; a failed upload and «إعادة المحاولة»; no connection; being checked, refused and no longer kept on a saved file; the keyboard alone; the announced words; sizes in the language's digits; and, when `SCS_SHOTS` is set, pictures of every state at 1440×900

**Checkpoint**: the control exists and can be seen and driven.

---

## Phase 7: User Story 5 - A scanner that is down does not stop the clinic's work (Priority: P2)

**Goal**: Accept and hold; scan when the scanner returns; say when files wait too long.

**Independent Test**: with the scanner giving no answer, upload and attach; the file reads as being checked and cannot be downloaded; when it answers, the file becomes available.

- [X] T034 [P] [US5] Add failing cases to `ScanTests.cs`: a scanner that gives no answer leaves the file `pending`, raises `ScanTries`, fails nothing, and the upload and the attach still succeed; `access` answers 409 meanwhile; when the scanner answers, the next sweep scans every waiting file of every clinic with nobody asking; one clinic's failing scan does not hold up another's; files waiting longer than an hour make the health check `files-scan-backlog` read degraded and write one error-level line per sweep that holds counts and no file name
- [X] T035 [US5] Complete `FileScanJob.cs` and `ScanSweepJob.cs` for "no answer"; create `Infrastructure/ScanBacklogHealthCheck.cs` registered in `FilesModule.cs` so that readiness is not failed by it; T034 passes

---

## Phase 8: User Story 6 - A file is not kept longer than it should be (Priority: P2)

**Goal**: Unattached files go after a day; a removed file can be restored for thirty days and is then erased; retention periods are applied once set.

**Independent Test**: move the clock: an unsaved file is gone after a day; a removed file is restorable on day thirty and erased after; only files past a set period are erased.

- [X] T036 [P] [US6] Add failing cases to `AttachTests.cs`: `db.RemoveFile(id)` on a save makes the file `removed` with who and when, not downloadable (`FILE_NOT_KEPT`), not counted towards its record, still listed with `includeRemoved` and its restore-until date; `db.RestoreFile(id)` within thirty days puts it back as it was with who and when; refused, taking the save with it: restoring after thirty days, restoring into a record that is full (`FILE_COUNT_REACHED`), restoring a refused or erased file, removing or restoring a file of another kind's record or another clinic; `db.StartFileRetention(record, moment)` sets `RetainFromUtc` on every file of that record and `db.StopFileRetention(record)` clears it, both in the caller's transaction and undone when the save fails (FR-031a); `db.HoldFiles(record)` marks every file of the record held with who and when, `db.ReleaseFiles(record)` lifts it with who and when, and a file attached to a record that has a held file is held with it (FR-031b)
- [X] T037 [US6] Complete `FileWriter.cs` for remove and restore, and add `StartFileRetention`, `StopFileRetention`, `HoldFiles` and `ReleaseFiles` to it and to `ClinicAwareDbContext.cs`; T036 passes
- [X] T038 [P] [US6] Write failing `src/server/tests/Scs.IntegrationTests/Files/CleanupTests.cs`, moving the clock: a file unattached for more than 24 hours loses its row and its content, one of 23 hours stays; a file removed 30 days ago is still restorable and one removed more than 30 days ago has its content erased, `ErasedFor = removed`, its row kept with name, size, uploader and remover; with no retention period set nothing is erased for age; with one set for a class, files of kinds of that class whose `RetainFromUtc` plus the period is past are erased (`ErasedFor = age`), removed or not; a file whose owner never started its period, however old, a file whose period was started and taken back, files not yet past it and files of other classes are untouched; a removed file whose period ends first is erased then; a held file is erased by neither rule however old, still cannot be downloaded if removed, and is erased by the next run after its hold is lifted; erased files answer `FILE_NOT_KEPT`; running twice changes nothing more; each clinic's turn touches only its own; `RemoveClinicAsync(clinicId)` deletes every row and all content of that clinic and no other's; the job writes no audit entry
- [X] T039 [US6] Create `Infrastructure/FileCleanupJob.cs` (`IClinicJob`, daily, with its audit declaration; and the entry point that removes a clinic's all) and register it in `FilesModule.cs`; T038 passes

---

## Phase 9: User Story 7 - A later spec adds its kind of file without touching the engine (Priority: P3)

**Goal**: The rules that keep the engine the only door, and the guide for the specs that follow.

**Independent Test**: a kind declared in a test is uploaded, attached, downloaded and removed with no change to the engine.

- [X] T040 [P] [US7] Write `src/server/tests/Scs.ArchitectureTests/FileRulesTests.cs`: only `FileWriter` writes `files.StoredFile` from outside the module, and no other module reads it or uses `FilesDbContext`; only the Files module uses `IFileStore` and `IMalwareScanner`; every kind of the table has a registered owner or is in a written list "waiting for its spec" that names the spec; no module registers an owner for a kind it does not name
- [X] T041 [US7] Add `src/server/tests/Scs.IntegrationTests/Files/NewKindTests.cs`: a kind and an owner made in the test go through upload, checks, scan, attach, list, download, remove, restore and clean-up with no change to the module (SC-009); a kind marked public is served without a session and a private one never is

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T042 [P] Write `src/server/tests/Scs.IntegrationTests/Files/FileVolumeTests.cs` (`Category=Volume`): a 10 MB file is accepted in under 15 seconds and its download starts in under 2 seconds (SC-001, SC-006); a hundred waiting files are all scanned within five minutes of the scanner answering (SC-007); of a hundred files uploaded with the scanner working at least ninety-five are available within a minute (SC-002); the figures are written to the test's output
- [X] T043 [P] Write `src/server/tests/Scs.IntegrationTests/Files/FileSecretsTests.cs`: after uploads, refusals, downloads and scans, the captured operational logs hold no file name, no token and no content (FR-036); the set of renamed programs, scripts and archives of SC-003 is refused to the last one; no address gives a file carrying the test signature (SC-004) or a password-protected PDF (SC-004a)
- [X] T044 [P] Update `docs/adding-a-module.md` (holding files: the steps of contracts/files-engine.md §1 to §4 and its rules for the caller), `docs/setup.md` (the builders' page, the local scanner and the ClamAV profile, where local files are kept) and `ops/runbooks/data-handling.md` (what `files` and the store hold, the thirty days, retention, that private files are never altered and public images are cleaned)
- [X] T045 Run every gate with nothing else running: `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T046 Walk the real stack as `quickstart.md` "By hand": the twelve steps with `owner@shifa.example` and `reception@shifa.example`, in Arabic and English, with sample files made by a script; and the ClamAV steps if the machine holds the image, recorded as skipped if not
- [X] T047 Lay the components beside their design: `SCS_SHOTS=<folder> npx playwright test files`, compare every state of the zone and the card in Arabic and English with `4260:814` and `4252:63169` — sizes, borders, tints, marks, spacing, words — fix what differs, and save one pair as `specs/009-file-storage/design-vs-real.png`
- [X] T048 Write `specs/009-file-storage/validation.md`: what was run with its counts, what was walked, the measured times, what was compared with the design and what differs, what was built differently from the plan, and what was not done; record that the read-only clinic of FR-009 and FR-025 cannot be tested until the subscription specs, and that the alert of FR-019 is a health reading and a log line until spec 001's monitoring; record the owner items — the retention values, the BRD lines of Chapters 03, 04, 08, 09, 10 and 11, the wording of the undrawn states — and what is carried to spec 001 (the cloud store, the backlog alarm) and to the specs that own the five kinds; and the fix to the environment strip in a narrow window reported by the owner during planning

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → stories.
- **US1 (3)** needs only the foundation. **US2 (4)** needs US1 (there must be an upload to check). **US3 (5)** needs US1 and US2 (only a clean file is given).
- **US4 (6)** needs the addresses of US1 and US3 as a contract; its web tasks T027–T031 can be written beside the server stories, T032–T033 after US3.
- **US5 (7)** needs US2. **US6 (8)** needs US1 and US3.
- **US7 (9)** needs US1–US3 and US6.
- **Polish (10)** last.

### Within Each Story

- The test task first, seen to fail; then the implementation that makes it pass.
- T006 and T013 are written before any code they test exists (isolation, permission).

### Parallel Opportunities

- T004, T006, T008, T009 and T011 together; T013 and T015 together; T017, T019 and T021 together; T023 and T025 together.
- T027, T028 and T030 beside the server tasks of US1 to US3.
- T042, T043 and T044 together.

## Parallel Example: User Story 2

```text
T017  FileSignaturesTests.cs   (unit)
T019  ScanTests.cs             (integration)
T021  the telling: audit and notification tests
```

## Implementation Strategy

### MVP First

1. Phases 1 and 2 — the table, the table of files, the store, the scanner.
2. Phases 3 to 5 — in, checked, out.
3. **Stop and validate**: upload, attach and download a file through a test kind.

### Incremental Delivery

4. Phase 6 — the components and the builders' page; the real stack can now be walked.
5. Phases 7 and 8 — the scanner down, and not keeping.
6. Phase 9 — the rules for later specs.
7. Phase 10 — volume, secrets, documents, gates, the walk, the design comparison, `validation.md`.

## Notes

- Requirement coverage: FR-001–004 T004–T005, T040–T041 · FR-005–011 T013–T016 · FR-012–013, FR-012a, FR-017 T017–T018 · FR-014–016a T019–T022 · FR-028c T023 · FR-031b T036–T038 · FR-018–019 T034–T035 · FR-020–028a T023–T024 · FR-027, FR-028b T025–T026 · FR-029–033 and FR-031a T036–T039 · FR-034 T006–T007, T009 · FR-035 T016, T022, T024 · FR-036 T043 · FR-037–046 T027–T033 · SC-001, SC-002, SC-006, SC-007 T042 · SC-003, SC-004 T043 · SC-010 T047 · SC-011 T033.
- Not testable yet, and recorded: the read-only clinic of FR-009 and FR-025 (no such state exists before the subscription specs).
- The implementation is not complete until T045 to T048 are done and written down (constitution VII).

## Phase 11: Convergence

- [X] T049 Make a refused scan say what FR-016a asks: in `src/server/modules/Files/Scs.Files/Infrastructure/FileJobs.cs` (`Tell`) the audit entry `file.scan-refused` also names the record the file is on (`recordKind`, `recordId`, absent while unattached) and its uploader (`uploadedBy`, the account), and the notification carries what the file was for (the kind, worded in both languages in the bell's line); update `AuditActions`/`NotificationEvents` facts and their lock files, the sentences and words, `data-model.md` §6, and the cases in `Files/ScanAndAccessTests.cs`, `AuditFactsTests` and `NotificationEventsTests` per FR-016a (partial)
- [X] T050 Keep a file chosen with no connection until it can be tried again: when the connection returns the console reads the page again (`src/web/projects/clinic/src/app/shell/shell.component.ts`, spec 006 FR-021) and the rows that were not uploaded are lost; make the rows of `createFileUploads` (`src/web/projects/core/src/lib/files/file-uploads.ts`) survive that — the shell does not redraw a page that says it holds unsaved uploads, or the uploads are tried again by themselves when the connection returns — and replace the case in `src/web/e2e/files.spec.ts` with one that goes offline, chooses a file, comes back and uploads it with "try again" per US4/AC8 (partial)
- [X] T051 Create `src/server/modules/Files/Scs.Files/Infrastructure/ScanBacklogHealthCheck.cs`, registered in `FilesModule.cs` as `files-scan-backlog`: degraded when files have waited more than an hour for their scan, never failing readiness, beside the error line the sweep already writes; add the case to `Files/ScanAndAccessTests.cs` and name the check in `ops/runbooks/data-handling.md` per FR-019, T035, plan: research R on the scan backlog (missing)
- [ ] T052 Run the three gates again and bring `specs/009-file-storage/validation.md` in line: its sections 5 and 6 no longer list the health check and the lost offline rows as differences per Constitution VII (partial)
