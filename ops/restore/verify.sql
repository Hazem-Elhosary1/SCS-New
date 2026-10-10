-- Verifies a restored copy of the production database (ops/runbooks/restore.md).
-- Run with sqlcmd against the RESTORED instance, never against production:
--
--   sqlcmd -S <restored-server> -d scs -b -v BACKUP_TAKEN_AT="2026-10-10T02:00:00Z" -i ops/restore/verify.sql
--
-- BACKUP_TAKEN_AT is the time the backup was taken, in UTC.
-- Any failed check raises an error, and -b makes sqlcmd exit non-zero.
-- A failed verification is a production incident, not a failed test.

SET NOCOUNT ON;

DECLARE @backupTakenAt datetimeoffset = CAST('$(BACKUP_TAKEN_AT)' AS datetimeoffset);
DECLARE @failures int = 0;

-- 1. The structure history is present and not empty.
IF OBJECT_ID(N'platform.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    PRINT 'FAIL  platform.__EFMigrationsHistory is missing';
    SET @failures += 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM platform.__EFMigrationsHistory)
BEGIN
    PRINT 'FAIL  platform.__EFMigrationsHistory is empty';
    SET @failures += 1;
END
ELSE
    PRINT 'ok    structure history present';

-- 2. Job storage is present.
IF OBJECT_ID(N'hangfire.[Schema]', N'U') IS NULL
BEGIN
    PRINT 'FAIL  job storage (schema hangfire) is missing';
    SET @failures += 1;
END
ELSE
    PRINT 'ok    job storage present';

-- 3. Exactly one heartbeat row.
IF OBJECT_ID(N'platform.JobHeartbeat', N'U') IS NULL
BEGIN
    PRINT 'FAIL  platform.JobHeartbeat is missing';
    SET @failures += 1;
END
ELSE
BEGIN
    DECLARE @rows int = (SELECT COUNT(*) FROM platform.JobHeartbeat);
    IF @rows <> 1
    BEGIN
        PRINT CONCAT('FAIL  platform.JobHeartbeat has ', @rows, ' rows; expected 1');
        SET @failures += 1;
    END
    ELSE
    BEGIN
        PRINT 'ok    one heartbeat row';

        -- 4. The data is as recent as the backup: the job runs every minute, so the last run
        --    must fall in the 15 minutes before the backup was taken, and not after it.
        DECLARE @lastRun datetimeoffset = (SELECT LastRunAtUtc FROM platform.JobHeartbeat);
        DECLARE @ageMinutes int = DATEDIFF(MINUTE, @lastRun, @backupTakenAt);

        IF @lastRun > DATEADD(MINUTE, 1, @backupTakenAt)
        BEGIN
            PRINT CONCAT('FAIL  last heartbeat ', CONVERT(varchar(40), @lastRun, 127),
                         ' is after the stated backup time; wrong backup or wrong time given');
            SET @failures += 1;
        END
        ELSE IF @ageMinutes > 15
        BEGIN
            PRINT CONCAT('FAIL  last heartbeat is ', @ageMinutes,
                         ' minutes older than the backup; the copy is stale or jobs had stopped');
            SET @failures += 1;
        END
        ELSE
            PRINT CONCAT('ok    last heartbeat ', @ageMinutes, ' minute(s) before the backup');
    END
END

IF @failures > 0
    RAISERROR('Restore verification FAILED (%d check(s)). Treat this as a production incident.', 16, 1, @failures);
ELSE
    PRINT 'Restore verification passed.';
