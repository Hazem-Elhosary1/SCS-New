using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Files.Contracts;
using Scs.Files.Domain;
using Scs.Files.Infrastructure.Persistence;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Files;
using Scs.SharedKernel.Notifications;

namespace Scs.Files.Infrastructure;

/// <summary>
/// Scans the clinic's files that wait for it (spec 009, FR-014 to FR-018). A clean file becomes
/// available. One with malware in it, or one the scanner cannot look inside, is refused: its
/// content is erased, its uploader is told and the trail has its entry, in one save. When the
/// scanner gives no answer nothing changes and the next run asks again. Running it twice scans
/// nothing twice.
/// </summary>
[Records(AuditActions.FileScanRefused)]
internal sealed partial class FileScanJob(
    FilesDbContext db,
    IFileStore store,
    IMalwareScanner scanner,
    IFileKindCatalogue kinds,
    TimeProvider clock,
    ILogger<FileScanJob> logger) : IClinicJob
{
    /// <summary>One run scans at most this many, so one clinic's backlog does not hold the worker.</summary>
    public const int Batch = 100;

    public async Task RunAsync(CancellationToken cancellation)
    {
        var waiting = await db.Files
            .Where(f => f.Scan == FilesSql.Pending)
            .OrderBy(f => f.UploadedAtUtc)
            .Take(Batch)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        int clean = 0, refused = 0, unanswered = 0, settledElsewhere = 0;

        foreach (var file in waiting)
        {
            var answer = await AskAsync(file, cancellation).ConfigureAwait(false);
            var now = clock.GetUtcNow();

            switch (answer)
            {
                case ScanAnswer.Clean:
                    file.ScannedClean(now);
                    break;

                case ScanAnswer.Infected or ScanAnswer.Unreadable:
                    var reason = answer == ScanAnswer.Infected ? FilesSql.Infected : FilesSql.Unreadable;
                    file.ScanRefused(reason, now);
                    Tell(file, reason);
                    break;

                default:
                    file.ScanNotAnswered();
                    break;
            }

            // One at a time: the state, the entry and the notification of a file stand together.
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another run of this job settled the file a moment ago: its entry and its
                // notification are written, and nothing is told twice.
                db.Entry(file).State = EntityState.Detached;
                settledElsewhere++;
                continue;
            }

            if (file.Scan == FilesSql.Refused)
            {
                await store.DeleteAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
                refused++;
            }
            else if (file.Scan == FilesSql.Clean)
            {
                clean++;
            }
            else
            {
                unanswered++;
            }
        }

        if (waiting.Count > settledElsewhere)
        {
            LogScanned(logger, clean, refused, unanswered);
        }
    }

    private async Task<ScanAnswer> AskAsync(StoredFile file, CancellationToken cancellation)
    {
        try
        {
            if (file.Type == FileTypes.Pdf)
            {
                var pdf = await store.OpenAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
                if (pdf is null)
                {
                    return ScanAnswer.NoAnswer;
                }

                await using (pdf.ConfigureAwait(false))
                {
                    if (await PdfProtection.IsEncryptedAsync(pdf, cancellation).ConfigureAwait(false))
                    {
                        return ScanAnswer.Unreadable;
                    }
                }
            }

            var content = await store.OpenAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
            if (content is null)
            {
                return ScanAnswer.NoAnswer;
            }

            await using (content.ConfigureAwait(false))
            {
                return await scanner.ScanAsync(content, cancellation).ConfigureAwait(false);
            }
        }
        catch (FileStoreException)
        {
            return ScanAnswer.NoAnswer;
        }
    }

    /// <summary>The trail's entry and the uploader's notification, written by the same save as the state (FR-016a).</summary>
    private void Tell(StoredFile file, string reason)
    {
        var kind = kinds.Find(file.Kind);

        db.Record(new AuditRecord(
            AuditActions.FileScanRefused,
            new AuditTarget(AuditTarget.File, file.Id, file.Name),
            Facts: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = file.Kind,
                ["reason"] = reason,

                // The record it is on, when it is on one, and who uploaded it (FR-016a).
                ["record"] = file.RecordKind,
                ["recordId"] = file.RecordId?.ToString(),
                ["uploadedBy"] = file.UploadedByAccountId.ToString(),
            })
        {
            BySystem = true,
        });

        db.Notify(new RaisedEvent(
            reason == FilesSql.Infected ? NotificationEvents.FileScanRefused : NotificationEvents.FileUnreadable,
            new EventSubject(AuditTarget.File, file.Id),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = file.Name.Length <= NotifySql.FactTextMaxLength ? file.Name : file.Name[..NotifySql.FactTextMaxLength],

                // What it was for, in each language: the bell is read in either.
                ["forAr"] = kind?.InArabic ?? file.Kind,
                ["forEn"] = kind?.InEnglish ?? file.Kind,
            },
            Owners: [file.UploadedByAccountId]));
    }

    // Counts only: a file's name never reaches the logs (FR-036).
    [LoggerMessage(Level = LogLevel.Information, Message = "Files scanned: {clean} clean, {refused} refused, {unanswered} still waiting")]
    private static partial void LogScanned(ILogger logger, int clean, int refused, int unanswered);
}

/// <summary>
/// Every minute: finds the clinics that have files waiting for their scan and enqueues the
/// clinic's job for each, so a scanner that was down is caught up with when it returns and
/// nobody has to ask (FR-018). It crosses the clinic boundary to read clinic ids and a count,
/// and is declared as such. Files that have waited more than an hour are reported, by count,
/// for the people who run the platform (FR-019).
/// </summary>
[RecordsNothing("finds clinics with files waiting for a scan and enqueues their job; changes nothing")]
internal sealed partial class ScanSweepJob(
    ICrossClinicConnectionFactory crossing,
    IClinicJobScheduler scheduler,
    ScanBacklog backlog,
    TimeProvider clock,
    ILogger<ScanSweepJob> logger)
{
    public const string JobName = "files-scan-sweep";
    public const string Cron = "* * * * *";

    /// <summary>After this long a waiting file is something to be told about.</summary>
    public static readonly TimeSpan TooLong = TimeSpan.FromHours(1);

    /// <returns>How many files have waited longer than an hour.</returns>
    public async Task<int> RunAsync(CancellationToken cancellation)
    {
        var builder = new DbContextOptionsBuilder<FilesDbContext>();
        crossing.Configure(builder);
        var db = new FilesDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            var late = clock.GetUtcNow() - TooLong;

            // No clinic is established here: the application's own filter would match nothing.
            var clinics = await db.Files.AsNoTracking().IgnoreQueryFilters()
                .Where(f => f.Scan == FilesSql.Pending)
                .GroupBy(f => f.ClinicId)
                .Select(group => new { ClinicId = group.Key, Late = group.Count(f => f.UploadedAtUtc < late) })
                .ToListAsync(cancellation)
                .ConfigureAwait(false);

            foreach (var clinic in clinics)
            {
                scheduler.TryEnqueueForClinic<FileScanJob>(clinic.ClinicId);
            }

            var waitingTooLong = clinics.Sum(c => c.Late);
            backlog.Found(waitingTooLong, clinics.Count(c => c.Late > 0));
            if (waitingTooLong > 0)
            {
                LogBacklog(logger, waitingTooLong, clinics.Count(c => c.Late > 0));
            }

            return waitingTooLong;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Files have waited more than an hour for their malware scan: {files} files in {clinics} clinics. The scanner may be down.")]
    private static partial void LogBacklog(ILogger logger, int files, int clinics);
}

/// <summary>
/// The daily job <c>files-cleanup</c>, for each clinic in turn (spec 009, FR-029 to FR-033):
/// removes what was uploaded and never attached within a day; erases the content of what was
/// removed more than thirty days ago; and erases the content of files whose retention period —
/// set for their class, and started by their owner — has run out. A file on hold is erased by
/// neither of the last two. Running it twice has the same result as once.
/// </summary>
[RecordsNothing("removes files never attached and erases content past its time; each file's own record says what became of it")]
internal sealed partial class FileCleanupJob(
    FilesDbContext db,
    IFileStore store,
    IFileKindCatalogue catalogue,
    TimeProvider clock,
    ILogger<FileCleanupJob> logger) : IClinicJob
{
    public const string JobName = "files-cleanup";
    public const string Cron = "45 2 * * *";

    /// <summary>How long an uploaded file may stay unattached (FR-029).</summary>
    public static readonly TimeSpan UnattachedFor = TimeSpan.FromHours(24);

    public async Task RunAsync(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();

        // 1. Never attached: the row goes with the content.
        var staleBefore = now - UnattachedFor;
        var stale = await db.Files.Where(f => f.RecordId == null && f.UploadedAtUtc < staleBefore)
            .ToListAsync(cancellation).ConfigureAwait(false);
        foreach (var file in stale)
        {
            await store.DeleteAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
            db.Files.Remove(file);
        }

        // 2. Removed more than thirty days ago, and not on hold.
        var removedBefore = now - FilesSql.RestoreFor;
        var removed = await db.Files
            .Where(f => f.RemovedAtUtc != null && f.RemovedAtUtc < removedBefore && f.ErasedAtUtc == null && f.HeldAtUtc == null)
            .ToListAsync(cancellation).ConfigureAwait(false);
        await EraseAsync(removed, FilesSql.ErasedForRemoved, now, cancellation).ConfigureAwait(false);

        // 3. Past the retention period of their class, counted from when their owner started it.
        var periods = await db.RetentionPeriods.AsNoTracking().Where(p => p.Days != null)
            .ToDictionaryAsync(p => p.Class, p => p.Days!.Value, StringComparer.Ordinal, cancellation).ConfigureAwait(false);
        var aged = 0;
        if (periods.Count > 0)
        {
            var started = await db.Files
                .Where(f => f.RetainFromUtc != null && f.ErasedAtUtc == null && f.HeldAtUtc == null)
                .ToListAsync(cancellation).ConfigureAwait(false);
            var past = started
                .Where(f => catalogue.Find(f.Kind) is { } kind
                            && periods.TryGetValue(kind.RetentionClass, out var days)
                            && f.RetainFromUtc!.Value.AddDays(days) < now)
                .ToList();
            await EraseAsync(past, FilesSql.ErasedForAge, now, cancellation).ConfigureAwait(false);
            aged = past.Count;
        }

        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        LogCleaned(logger, stale.Count, removed.Count, aged);
    }

    private async Task EraseAsync(List<StoredFile> files, string reason, DateTimeOffset now, CancellationToken cancellation)
    {
        foreach (var file in files)
        {
            await store.DeleteAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
            file.Erased(reason, now);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Files cleaned: {unattached} never attached removed, {removed} removed ones erased, {aged} erased for age")]
    private static partial void LogCleaned(ILogger logger, int unattached, int removed, int aged);
}

/// <summary>Removes everything of one clinic: rows and content (FR-032). The platform's own work only.</summary>
internal sealed class ClinicFiles(FilesDbContext db, IFileStore store, IClinicContext clinic) : IClinicFiles
{
    public async Task<int> RemoveClinicAsync(Guid clinicId, CancellationToken cancellation)
    {
        // Only for the clinic the work was started for: this cannot be pointed at another.
        if (clinic.Require() != clinicId)
        {
            throw new InvalidOperationException("A clinic's files are removed by work established for that clinic.");
        }

        var removed = await db.Files.ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
        await store.DeleteClinicAsync(clinicId, cancellation).ConfigureAwait(false);
        return removed;
    }
}
