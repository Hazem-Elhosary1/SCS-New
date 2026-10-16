# Validation: File Storage (spec 009)

**Date**: 11 Oct 2026 · **Branch**: `009-file-storage` · **Constitution**: v1.2.0, Principle VII

What was run, what was measured, what was walked on the real stack, what was laid beside the
design, what was built differently from the plan, and what was not done. Second writing, after
the convergence tasks T049–T052 (section 8); the figures of section 1 are from the last run.

## 1. What was run

| Gate | Result |
|---|---|
| `dotnet test src/server/Scs.sln` (Debug) | unit **551** · architecture **51** · integration **712** — all passed |
| `npm --prefix src/web run ci` (lint, translation check, tools, unit tests, build of the three apps) | passed — clinic **68**, core **164**, ui **95**, patient 1, ops 1; clinic's first download **452.91 kB**, no size warning |
| `npx playwright test` | **397 passed**, 4 skipped (the picture-only tests) |

In the run before convergence one test needed a second try: spec 005's timing test (`access.spec.ts`, "not in your role
within a second", SC-008 of that spec): 1352 ms the first time, with the whole browser suite
running beside it. It touches nothing of this spec.

In the last run the three gates ran one after the other; a single browser test was run beside
the server suite to take a picture.

**Before starting (T001)**: the unit and architecture suites were run and matched spec 008's
record, 499 and 44. The integration suite hung at the baseline because the local API was still
running and holding the build; it was stopped, and the baseline is taken from spec 008's last
full run, 640. New since: 52 unit, 7 architecture and 72 integration tests on the server; 19
unit tests in core and 23 in ui; 16 browser tests for files and 3 for the environment strip.

New test files: `Files/UploadTests` (with `FileWorld`), `AttachTests`, `ScanAndAccessTests`
(`ScanTests`, `AccessTests`, `PublicFileTests`), `KeepingTests` (`CleanupTests`, `NewKindTests`,
`FileVolumeTests`, `FileSecretsTests`), `FileStoreTests`, `Isolation/FileSamples`,
`Fixtures/FileFixtures` (integration); `Files/FileKindsTests` (unit); `FileRulesTests`
(architecture); `files.spec.ts` in core and in ui; `e2e/files.spec.ts`. The test files are
grouped by subject, several classes to a file, and not one file for each name in `tasks.md`.

**Existing tests that had to change**, and why:

- `DeclaredCrossingsTests`: nine crossings, not seven — the sweep that lists clinics with files
  to scan, and the reading of a public file with no clinic established.
- `IdentityRulesTests`, `RouteDeclarationTests`: "own account" is also allowed in
  `FileEndpoints.cs`; the engine asks the owner of the kind, not a permission. This widens a
  rule of spec 005 a second time and is worth a look in review.
- The rule that only two files may protect a token now names a third, `FileDownloads.cs`.
- `RouteAuditDeclarationTests`, `AuditActionsTests`, `AuditFactsTests`: two more actions,
  `file.downloaded` and `file.scan-refused`.
- `NotificationEventsTests`: two more events, `file.scan-refused` and `file.unreadable`.
- `ErrorCatalogueTests`: ten more codes, 42 in all.
- `ScsOptionsValidatorTests`, `ApiFactory`: outside a local environment a files root and a
  scanner are required; test hosts that pose as another environment are given a scanner host.

**Failures on the way, all fixed**: a class named `FileAccess` clashed with the framework's; the
web client could not use an absolute address for a download; the files of a record came back
in no fixed order; the Arabic date of a card was written in numbers only. Two more were found
on the real stack and are in section 3.

## 2. What was measured

| What | Figure | Limit |
|---|---|---|
| Taking a 10 MB file | **136 ms** | 15 s (SC-001) |
| Asking for it until the download starts | **77 ms** | 2 s (SC-006) |
| Scanning it, with the test scanner | **22 ms** | — |
| A hundred files left waiting, once the scanner answers | **1.39 s** | one minute (SC-007) |
| On the real stack: a saved file available for download | **under 2 s** after saving | one minute (SC-002) |

One unmeasured call comes first: the first of a process pays for building its statements. The
figures are with the scanner of tests, which looks for the standard test signature only; a real
scanner was not run (section 6).

## 3. What was walked on the real stack

With the local API, SQL Server and the clinic console, in a separate browser, as
`owner@shifa.example` and `reception@shifa.example`, with sample files made by a script
(`quickstart.md`, "By hand").

| Step | Seen |
|---|---|
| 1–2. The zone, and a chosen PDF | «إفلات الملف هنا أو النقر للاختيار», «ملف PDF أو صورة · حتى ١٠ ميجابايت»; the file reads as ready with its size |
| 3. Save and download | available two seconds after saving; downloaded under its Arabic name, byte for byte the upload |
| 4. A program renamed `.pdf` | refused by name and reason; the zone is ready again |
| 5. The limit | with one saved and two chosen the zone says «بلغ العدد حدَّه: ٣» |
| 6. A file with the test signature, and a PDF behind a password | both read as refused on the record, each with its reason; the bell holds one notification for each, with the file's name; the trail holds one entry for each |
| 7. A way in | opened with no session: 401 |
| 8. Another person | reception asking for the owner's file: 403; using the owner's way in: 404 |
| 9. The trail | one `file.downloaded` entry for each download |
| 10. Remove and restore | «أُزيل · يُستعاد حتى ١٠ نوفمبر ٢٠٢٦»; asked for while removed: no longer kept; restored: downloads again |
| 11. English | the zone, the cards and the refusals in English with Western digits |
| 12. Keyboard only | chosen and removed with Enter; both are announced |

The 11 MB file of step 4 was not read back by the walk's script (it printed the row before it);
the size limit is covered by the browser tests and by `UploadTests`.

**Two defects found by the walk, fixed, each with a test**:

1. **A refused file was told twice.** Each upload asks for the clinic's scan, and the sweep
   does every minute; two runs scanned the same waiting file and each wrote an entry and a
   notification. The state of the scan is now checked when it is written, so the second run
   finds the file settled and tells nobody
   (`Two_scans_of_one_clinic_at_once_settle_each_file_once_and_tell_once`). After the fix the
   walk left exactly one entry and one notification for each refused file.
2. **A file refused before its record was saved could not be saved at all.** The scan is often
   quicker than the person; the save was refused and the page still showed the file as ready.
   A refused file is now attached like any other and reads as refused on its record; it takes
   no place among those the record may hold. `data-model.md` and
   `contracts/files-engine.md` say so
   (`A_file_refused_before_its_record_was_saved_is_attached_all_the_same_and_reads_refused`).

The walks left sample files, entries and notifications on the two made-up accounts of the
local database.

## 4. Beside the design

`design-vs-real.png` in this folder: the design's "File Upload" (`4260:814`) and "File
Attachment Card" (`4252:63169`) beside the builders' page, in Arabic. English was compared the
same way and is not in the picture.

| | Design | Built |
|---|---|---|
| Zone | 320 × 162; 320 × 178 while uploading | the same — it was 178 in every state and was corrected here, with a test |
| Card | 320 × 73 | the same |
| Words | title, hint, uploading, error, in both languages | the same, letter for letter |
| Card while uploading | a spinner | a spinner and a cross that stops the upload — an addition |
| Card types | image, PDF, document, other | image, PDF, other: no kind accepts a document yet |
| States not drawn | — | hover of the card, being checked, refused, behind a password, removed, no longer kept, the zone at its limit: built from the same parts, their wording is the owner's to confirm |

**A removed file is red, by the owner's word (11 Oct 2026).** The design does not draw a
removed file. The card for one has a dashed red border, a light red ground, its name struck
through in red and its line in red; the border is dashed so that it is not taken for a refused
file, whose border is solid. The owner asked for the colour during this work; the builders'
page shows the state and a browser test holds it.

The design writes «اختر ملفًا أصغر» and «أعد المحاولة» as orders; the wording rule (SH-098)
prefers statements. The design's words were kept.

The components were built from the frames' pictures and sizes; the file's variables were not
read for them, so tints come from the tokens already in the `ui` library.

## 5. Built differently from the plan

- FR-019's alert is a health check that reads degraded and an error line in the log; nothing
  pages anybody until the monitoring of spec 001 is wired.
- `RemoveFile` and `RestoreFile` take the kind of file as well as its identity.
- The way in is a relative address, `/v1/files/content/{token}`.
- Two notification events, `file.scan-refused` and `file.unreadable`, with one audit action
  for both.
- `FileRefusals` lives in the building blocks, so that no module names the permission code.
- A refused file can be attached (section 3).
- `RetentionPeriod` is not write-protected against the application's own login.
- `SCS_FILES__ROOT` may be left out locally; a temporary folder is used.
- The cloud store is carried to spec 001; this spec holds files in a folder.

## 6. Not done, and for the owner

- **A real scanner was not run.** The ClamAV profile in the compose file and `ClamdScanner`
  were written to the daemon's documented protocol and never met a daemon.
- **A read-only clinic** could not be tried: nothing yet makes a clinic read-only.
- **The retention periods** seeded are placeholders; the figures, and whether backups fall
  under them, are for the owner and the lawyer.
- **BRD chapters** 03, 04, 08, 09, 10 and 11 name files this engine will hold; their kinds are
  added by their own specs.
- **`checklists/security.md`**: thirty items, unchecked; they are the reviewer's.
- GitHub cannot run the checks while the account is locked for billing; the figures above are
  from this machine.

## 7. A defect outside the spec, reported by the owner during this work

The environment strip did not reach the full width when the page was wider than the window.
The app's host now takes the width of its content; three browser tests in
`e2e/shell-sources.spec.ts`.

## 8. Convergence (T049–T052)

| Task | What was missing | What was done |
|---|---|---|
| T049 | FR-016a: the entry of a refused scan did not name the record or the uploader, and the notification did not say what the file was for | The entry holds `record`, `recordId` (empty while unattached) and `uploadedBy`; each kind has a name in both languages and the bell's line carries it: «report.pdf (مرفق تذكرة) · لم يجتز الفحص الأمني» |
| T050 | US4, scenario 8: a file chosen with no connection was lost when the connection returned, because the console reads the page again (spec 006, FR-021) | A page that holds a file not yet saved says so (`UnsavedWork` in core), and the console leaves it as it is; what the person may do is still read again. The browser test goes offline, chooses, comes back and uploads with "try again" |
| T051 | FR-019, T035: the health check of the plan was not built | `files-scan-backlog`: degraded with the counts of files and clinics while any file has waited more than an hour; not a check of readiness; in the runbook |
| T052 | — | The three gates were run again; the figures of section 1 are theirs |

T050 changes a rule of spec 006: with unsaved files on screen, the page shown after a
reconnect may be older than the platform's. It is worth a look in review.
