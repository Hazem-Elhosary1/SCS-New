# Data Model: Files and Attachments

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## 1. The table of kinds — `FileKinds` (ships with the product; not stored)

`src/server/shared/Scs.SharedKernel/Files/FileKinds.cs`, frozen by `file-kinds.lock`.

| Field | Meaning |
|---|---|
| `Key` | `area.name`, lower case, at most 64; never renamed or reused |
| `Types` | from `pdf`, `png`, `jpeg` |
| `MaxBytes` | the largest one file |
| `MaxPerRecord` | how many one record holds; `null` for any number |
| `Public` | shown without signing in; never for anything about a patient |
| `RetentionClass` | `medical-record`, `conversations`, `ticket-attachments`, `clinic-data`, or `none` |
| `LocalOnly` | exists on a developer's machine and in tests only |

| Key | Types | Max | Per record | Public | Class | Owner arrives with |
|---|---|---|---|---|---|---|
| `ticket.attachment` | pdf, png, jpeg | 10 MB | 3 (one message) | no | ticket-attachments | spec 082 |
| `patient.file` | pdf, jpeg, png | 10 MB | any | no | medical-record | spec 032 |
| `consent.signed-copy` | jpeg, png, pdf | 10 MB | 1 | no | medical-record | the consents spec |
| `order.result-report` | pdf, jpeg, png | 10 MB | 1 | no | medical-record | the orders spec |
| `doctor.photo` | jpeg, png | 2 MB | 1 | yes | clinic-data | spec 029 |
| `dev.sample` | pdf, png, jpeg | 10 MB | 3 | no | none | this spec; `LocalOnly` |

For `ticket.attachment` the "record" a file is attached to is one message of a ticket.

## 2. `files.StoredFile` — clinic-owned

One row per uploaded file. Isolated by the policy of spec 002, created in the same migration.

| Column | Type | Rule |
|---|---|---|
| `Id` | uniqueidentifier | key; also the content's key in the store, under the clinic |
| `ClinicId` | uniqueidentifier | not null; the isolation column |
| `Kind` | varchar(64) | not null; a key of the table |
| `Name` | nvarchar(255) | not null; as uploaded, without any path, cut to 255; shown and used for download only |
| `SizeBytes` | bigint | not null; > 0; ≤ the kind's `MaxBytes`; the size as stored |
| `Type` | varchar(16) | not null; `pdf` · `png` · `jpeg`, as read from the content |
| `UploadedByAccountId` | uniqueidentifier | not null |
| `UploadedAtUtc` | datetimeoffset | not null |
| `Scan` | varchar(16) | not null; `pending` · `clean` · `refused` |
| `RefusedFor` | varchar(16) | null unless `Scan = 'refused'`; `infected` · `unreadable` |
| `ScannedAtUtc` | datetimeoffset | null until answered |
| `ScanTries` | int | not null; default 0 |
| `RecordKind` | varchar(32) | null while unattached; what it belongs to (`ticket-message`, `patient`, `account`…) |
| `RecordId` | uniqueidentifier | null while unattached |
| `AttachedAtUtc` | datetimeoffset | null while unattached |
| `RemovedAtUtc` | datetimeoffset | null unless removed |
| `RemovedByAccountId` | uniqueidentifier | null unless removed |
| `RestoredAtUtc` | datetimeoffset | the last restoring, if any |
| `RestoredByAccountId` | uniqueidentifier | |
| `RetainFromUtc` | datetimeoffset | null until the owner starts the file's retention period; cleared when the owner takes it back |
| `HeldAtUtc` | datetimeoffset | null unless on hold; while set nothing erases the content |
| `HeldByAccountId` | uniqueidentifier | who set the hold; `ReleasedAtUtc` and `ReleasedByAccountId` keep the last lifting |
| `ErasedAtUtc` | datetimeoffset | null while the content exists |
| `ErasedFor` | varchar(16) | null · `removed` · `age` · `refused` |

Checks: `RefusedFor` is set exactly when `Scan = 'refused'`; a hold requires attached;
`RecordKind`, `RecordId` and `AttachedAtUtc` are null together or set together;
`RemovedAtUtc` set requires attached; `ErasedFor` set exactly when `ErasedAtUtc` is.

Indexes:

- `IX_StoredFile_Record` (`ClinicId`, `RecordKind`, `RecordId`) including `RemovedAtUtc` — the
  files of a record, and the count.
- `IX_StoredFile_Unattached` (`ClinicId`, `UploadedAtUtc`) where `RecordId IS NULL` — the daily
  removal.
- `IX_StoredFile_Uploader` (`ClinicId`, `UploadedByAccountId`, `UploadedAtUtc`) including
  `RecordId` — a person's thirty waiting and sixty in ten minutes.
- `IX_StoredFile_Pending` (`ClinicId`, `UploadedAtUtc`) where `Scan = 'pending'` — the scan and
  its backlog.
- `IX_StoredFile_Removed` (`ClinicId`, `RemovedAtUtc`) where `RemovedAtUtc IS NOT NULL AND
  ErasedAtUtc IS NULL` — the thirty days.
- `IX_StoredFile_Retained` (`ClinicId`, `Kind`, `RetainFromUtc`) where `RetainFromUtc IS NOT
  NULL AND ErasedAtUtc IS NULL` — erasing for age.

No address, no content and no path is stored in the row.

## 3. States of a file

```text
            upload accepted
                  │
                  ▼
        unattached · pending ──(24 h, never attached)──▶ row and content deleted
                  │  ▲
     attach with  │  │ scan could not answer: stays pending, tried again
     the owner's  │  │
     save         ▼  │
        attached · pending ──scan──▶ attached · clean ──remove──▶ removed ──restore──▶ attached · clean
                  │                        │                        │
                  │ scan: infected         │ retention period       │ 30 days
                  ▼                        ▼                        ▼
        refused (content erased)     erased for age           erased (removed)
```

- "Retention period" in the drawing means: a period is set for the file's class, the owner
  has started it (`RetainFromUtc`), and `RetainFromUtc` plus the period is past. A file whose
  owner never started it is never erased for age.
- A file on hold is not erased by the thirty days or by its retention period until the hold
  is lifted; the drawing's two erasing arrows wait.
- `refused` is for `infected` or `unreadable`; both erase the content at once.
- Attaching does not wait for the scan (FR-018); downloading does (FR-015).
- An unattached file can be scanned and refused too. It is attached all the same when its
  record is saved, and reads as refused there: what a person sees does not depend on whether
  the scan or the save came first. A refused file, like a removed one, takes no place among
  those a record may hold.
- `refused`, `erased` are final. An erased row keeps `Name`, `SizeBytes`, who and when.
- What a reader is told, in order: not theirs or not there → 404; removed → `FILE_NOT_KEPT`
  for anyone but the restoring owner page; erased for age → `FILE_NOT_KEPT`; `refused` →
  `FILE_REFUSED`; `pending` → `FILE_BEING_CHECKED`; else the way in.

## 4. `files.RetentionPeriod` — platform-wide

| Column | Type | Rule |
|---|---|---|
| `Class` | varchar(32) | key; one of the four classes |
| `Days` | int | null = not set, nothing is removed for age; else > 0 |
| `SetAtUtc` | datetimeoffset | null until set |

Seeded with the four classes and no days. Added to `PlatformWideTables` with its reason: it
belongs to no clinic and holds no clinic or patient data. Readable by the application's
identity; nothing in this spec writes it but the tests.

## 5. Not stored

- **A way in**: a protected token — file id, account id, expiry (five minutes) — made and read
  with the platform's data protection, purpose `Scs.Files.Access.v1`. Nothing is kept.
- **The content**: in the store, at `{ClinicId}/{Id}`.

## 6. Additions to other tables of the product

**Audit catalogue** (`AuditActions`, `audit-actions.lock`):

| Code | Category | Target | Facts | By |
|---|---|---|---|---|
| `file.downloaded` | records | kind `file`, the file's id, its name | `kind`, `record` (the record's kind) | the person |
| `file.scan-refused` | security | kind `file`, the file's id, its name | `kind`, `reason` (`infected` · `unreadable`), `record` and `recordId` (empty while unattached), `uploadedBy` (the account) | the system |

Sentences: «نُزِّل الملف «{target}»» · "File “{target}” was downloaded"; «رُفض الملف «{target}»
لأنه لم يجتز الفحص الأمني» · "File “{target}” was refused: it did not pass the security check".
Names: «تنزيل ملف» · "File downloaded"; «رفض ملف» · "File refused".

**Notification events** (`NotificationEvents`, `notification-events.lock`):

| Key | Group | Recipients | Email | Mark | Facts | Leads to |
|---|---|---|---|---|---|---|
| `file.scan-refused`, `file.unreadable` | accounts | the owner (the uploader) | never | warning | `name`, and what the file was for: `forAr`, `forEn` (the kind's name in each language) | nowhere |

Name: «ملف مرفوض» · "File refused". Line: «{name} · لم يجتز الفحص الأمني» · "{name} · did not
pass the security check"; for `unreadable`: «{name} · تعذّر فحصه — أرسلوه بلا كلمة مرور» ·
"{name} · could not be checked — send it without a password". The audit sentence for
`unreadable`: «رُفض الملف «{target}» لتعذّر فحصه» · "File “{target}” was refused: it could not be
checked".

**Declared crossings** (`Crossing`):

| Operation | For | Returns |
|---|---|---|
| `list-clinics-with-files-to-scan` | the scan sweep | clinic ids only |
| `read-public-file` | the public address | one row of a public kind: clinic id, type, size |

**Access lists**: `POST /v1/files`, `DELETE /v1/files/{id}`, `POST /v1/files/{id}/access` and
`GET /v1/files/content/{token}` are open to any signed-in person and decided by the kind's
owner, the uploader's identity and the token; the four join the own-account list of spec 005's
contract with that reason. `GET /v1/public/files/{id}` is declared anonymous. The four
`/v1/dev/files/sample…` addresses exist on a developer's machine and in tests only and are
declared like the other `/v1/dev/…` addresses.

## 7. Configuration

| Key | Meaning | Rule |
|---|---|---|
| `SCS_FILES__ROOT` | the folder of the local store | required where the folder store is used; never inside the web root |
| `SCS_SCAN__HOST`, `SCS_SCAN__PORT` | the scanner | required outside local and test; with none, local and test use the test-signature scanner |
| `SCS_SCAN__TIMEOUT_SECONDS` | how long one scan may take | 5 to 120; default 30 |

Fixed by the product, not configured: 24 hours unattached; 30 days removed; 5 minutes for a
way in; one hour before waiting files are reported; 30 files waiting and 60 uploads in ten
minutes for one person.

## 8. Declarations

| Route or job | Access | Audit |
|---|---|---|
| `POST /v1/files?kind=` | the kind's owner (`MayUploadAsync`); refused when read-only | records nothing: an unattached file is nobody's yet |
| `DELETE /v1/files/{id}` | the uploader, while unattached | records nothing |
| `GET /v1/files?record=` — not offered | — | the owning page lists its own files through the contract |
| `POST /v1/files/{id}/access` | the kind's owner (`MaySeeAsync`) | `file.downloaded` |
| `GET /v1/files/content/{token}` | the token and its account's session | — (read) |
| `GET /v1/public/files/{id}` | anonymous, public kinds | — |
| `POST /v1/dev/files/sample`, `GET /v1/dev/files/sample`, `POST /v1/dev/files/sample/{id}/remove`, `POST /v1/dev/files/sample/{id}/restore` | local and test only; any signed-in person, their own account's files | records nothing: local and test environments only |
| job `files-scan` (per clinic), `files-scan-sweep` (every minute) | — | `file.scan-refused` |
| job `files-cleanup` (daily, per clinic) | — | records nothing: removes what its rules say |

## 9. Migrations

1. `files` — `InitialFiles`: `StoredFile` with its checks, indexes and isolation policy;
   `RetentionPeriod` with its four rows.
