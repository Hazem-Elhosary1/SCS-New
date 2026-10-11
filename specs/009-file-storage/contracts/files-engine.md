# Contract: Giving a Record its Files

**Spec**: [../spec.md](../spec.md) FR-001 to FR-036 | **Data**: [../data-model.md](../data-model.md)

What every later spec does to let a record hold files, and what the engine promises in return.
Decided once here.

## 1. To add a kind of file

1. Add its row to `Scs.SharedKernel/Files/FileKinds`: key, types, largest size, how many a
   record holds, private or public, retention class.
2. Add the key to `file-kinds.lock`.
3. Write the web's copy: `dotnet run --project src/server/tools/Scs.Migrator -- file-kinds
   src/web/projects/core/src/lib/files/file-kinds.json`.

The five kinds of the BRD are already in the table; a spec that owns one skips to §2.

## 2. To own a kind

Register one owner in the module that owns the record:

```text
services.AddFileKindOwner<TicketAttachmentOwner>();

class TicketAttachmentOwner : IFileKindOwner
    Kind            => FileKinds.TicketAttachment
    MayUploadAsync()                      → does the person hold «فتح التذاكر» or «الرد على التذاكر»?
    MaySeeAsync(recordKind, recordId)     → may the person see the ticket this message is on?
```

Both are asked on the server at every upload and every download. A kind with no owner is
refused for everyone.

## 3. To attach, remove and restore

Where the record is saved, on the module's own context:

```text
db.AttachFiles(FileKinds.TicketAttachment, new FileRecord("ticket-message", message.Id), fileIds);
db.Record(new AuditRecord(…the module's own action…));
await db.SaveChangesAsync(cancellation);
```

```text
db.RemoveFile(FileKinds.TicketAttachment, fileId);    // not downloadable from this save; restorable for thirty days
db.RestoreFile(FileKinds.TicketAttachment, fileId);   // back on its record, if within thirty days and there is room
```

```text
db.StartFileRetention(new FileRecord("ticket-message", message.Id), closedAt);   // the period starts here
db.StopFileRetention(new FileRecord("ticket-message", message.Id));              // reopened: not started
```

```text
db.HoldFiles(new FileRecord("patient", patient.Id));       // nothing of them is erased until released
db.ReleaseFiles(new FileRecord("patient", patient.Id));
```

The save does it in one transaction with the record. Nothing in the Files module changes
(SC-009). A file's retention period never starts by itself: until its owner starts it, the
file is not erased for its age (FR-031).

## 4. To show a record's files

`IRecordFiles.ListAsync(recordKind, recordId, includeRemoved)` answers, for the established
clinic: id, name, size, type, who uploaded and when, the state a reader sees (`available`,
`being-checked`, `refused`, `removed`, `not-kept`), and for a removed file until when it can be
restored. The owning page puts these in its own answer; the browser downloads through the
engine's address.

## 5. What the engine takes itself

The clinic; who uploaded and when; the type, read from the content; the size; the scan; where
the content is; who removed and restored and when; the audit entry of each download; telling
the uploader of a refused file. A caller cannot supply them.

## 6. What the engine refuses

| Refused | Code | Why |
|---|---|---|
| A kind that is not in the table, or has no owner | `FORBIDDEN` | FR-004, FR-021 |
| The owner says the person may not upload; a read-only clinic | `PERMISSION_REQUIRED` · the read-only refusal | FR-009 |
| An empty file | `FILE_EMPTY` | FR-012 |
| Larger than the kind allows | `FILE_TOO_LARGE` with `sizeBytes`, `maxBytes` | FR-012 |
| Content that is none of the kind's types | `FILE_TYPE_NOT_ACCEPTED` with `accepted` | FR-013 |
| Storage unreachable | `FILE_NOT_STORED` | edge case; nothing half-kept |
| Thirty files waiting, or sixty uploads in ten minutes, for this person | `FILE_LIMIT_REACHED` with `retryAfterSeconds` | FR-012a |
| Attaching a file of another kind, of another person or already attached | the save fails: `FileRefusedException`, `FILE_NOT_ATTACHABLE`, said alike for each | FR-008 |
| Attaching more than the kind's count; restoring into a full record | the save fails, `FILE_COUNT_REACHED` with `max` | FR-012, FR-030a |
| Restoring after thirty days, or a file with no content | the save fails, `FILE_NOT_KEPT` | FR-030a |
| The synchronous save with file changes pending | `FileRefusedException` | the writer needs the asynchronous one |

## 7. What a reader is told

| Situation | Answer |
|---|---|
| Not of this clinic; another person's unattached file; no such file | 404 `NOT_FOUND`, alike |
| The owner says no | 403 `PERMISSION_REQUIRED`, as for the record |
| Still being checked | 409 `FILE_BEING_CHECKED` |
| Refused by the check | 410 `FILE_REFUSED` with `reason`: `infected` · `unreadable` |
| Removed, or erased for its age | 410 `FILE_NOT_KEPT` |
| Available | 200 with a way in that lasts five minutes |

## 8. Rules for the caller

- Attach with the save of the record, never after it.
- Record your own audit action for attaching, removing and restoring; the engine records
  downloads and refused scans.
- Do not read or write `files.StoredFile` yourself, and do not reach the store
  (`FileRulesTests`).
- Do not put a file's name or a way in into a log.
- A public kind is for what everyone may see. Nothing about a patient.
- Whether a file may be removed at all — a file of a signed visit may not — is your rule;
  refuse before calling.
- A private file is a download. If your page previews one, it does so from the downloaded
  bytes, in a context that cannot act as the product; the engine never serves a private file
  to be opened in place.
- Who may put a record's files on hold, and lift it, is your rule; record both in the audit
  trail.
- Say when the retention period of a record's files starts — its ticket closed, its clinic
  cancelled, its patient's record removed — in the save that causes it.
- The card's "more" control shows what you give it (remove, restore) and is absent when you
  give nothing.

## 9. For the page

Place `<scs-file-field kind="ticket.attachment" [uploads]="uploads" />` and send
`uploads.ids()` with the save; keep the save disabled while `uploads.busy()`. To show files
that are already attached, give `<scs-file-card>` each item of your answer. The words, the
limits in the hint and the reasons come from the kind.
