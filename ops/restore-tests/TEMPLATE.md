# Restore test — YYYY-MM-DD

Copy this file to `ops/restore-tests/<date>.md` (the day the trial ran) and fill every field.
Procedure: `ops/runbooks/restore.md`. One record is required every quarter; a quarter with no
record opens a reminder issue automatically.

| Field | Value |
|---|---|
| `date` | YYYY-MM-DD |
| `operator` | Name of the person who ran the trial |
| `backupTakenAt` | UTC time of the backup that was restored, e.g. `2026-10-10T02:00:00Z` |
| `backupKind` | `automated` or `native` |
| `durationMinutes` | From the start of the restore to a passing verification |
| `result` | `pass` or `fail` |
| `notes` | Required when `fail`. Otherwise anything worth knowing next time |

## Verification output

Paste the output of `ops/restore/verify.sql` here.

```text
```

## If the result is `fail`

A backup that cannot be restored is a **production incident**, not a failed test. Open the
incident before closing this record, and link it here.
