using Scs.BuildingBlocks.Files;
using Scs.SharedKernel;

namespace Scs.Files.Domain;

/// <summary>
/// One uploaded file (spec 009, FR-010). The row says what the file is and what became of it;
/// its content is in the store, under the clinic and this id — never under its name. A file is
/// attached, removed, restored, held and given its retention clock by the shared writer, on the
/// save of the module that owns its record; this module uploads, scans, gives out and erases.
/// </summary>
public sealed class StoredFile : IClinicOwned
{
    private StoredFile()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>A key of the table of kinds.</summary>
    public string Kind { get; private set; } = "";

    /// <summary>As uploaded, without any path; shown and used for the download, nothing else.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The size as stored.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>As read from the content: <c>pdf</c>, <c>png</c> or <c>jpeg</c>.</summary>
    public string Type { get; private set; } = "";

    public Guid UploadedByAccountId { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    /// <summary><c>pending</c>, <c>clean</c> or <c>refused</c>.</summary>
    public string Scan { get; private set; } = FilesSql.Pending;

    /// <summary><c>infected</c> or <c>unreadable</c>, when the scan refused it.</summary>
    public string? RefusedFor { get; private set; }

    public DateTimeOffset? ScannedAtUtc { get; private set; }

    public int ScanTries { get; private set; }

    /// <summary>What it belongs to; null while unattached.</summary>
    public string? RecordKind { get; private set; }

    public Guid? RecordId { get; private set; }

    public DateTimeOffset? AttachedAtUtc { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public Guid? RemovedByAccountId { get; private set; }

    public DateTimeOffset? RestoredAtUtc { get; private set; }

    public Guid? RestoredByAccountId { get; private set; }

    /// <summary>When its owner said its retention period started; null while it has not.</summary>
    public DateTimeOffset? RetainFromUtc { get; private set; }

    /// <summary>While set, nothing erases the content.</summary>
    public DateTimeOffset? HeldAtUtc { get; private set; }

    public Guid? HeldByAccountId { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public Guid? ReleasedByAccountId { get; private set; }

    /// <summary>Null while the content exists.</summary>
    public DateTimeOffset? ErasedAtUtc { get; private set; }

    /// <summary><c>removed</c>, <c>age</c> or <c>refused</c>.</summary>
    public string? ErasedFor { get; private set; }

    public bool IsAttached => RecordId is not null;

    public bool HasContent => ErasedAtUtc is null;

    public static StoredFile Uploaded(Guid id, string kind, string name, long sizeBytes, string type, Guid uploader, DateTimeOffset now) =>
        new()
        {
            Id = id,
            Kind = kind,
            Name = name,
            SizeBytes = sizeBytes,
            Type = type,
            UploadedByAccountId = uploader,
            UploadedAtUtc = now,
        };

    public void ScannedClean(DateTimeOffset now)
    {
        Scan = FilesSql.Clean;
        ScannedAtUtc = now;
        ScanTries++;
    }

    /// <summary>Refused by the check: from here on it has no content.</summary>
    public void ScanRefused(string reason, DateTimeOffset now)
    {
        Scan = FilesSql.Refused;
        RefusedFor = reason;
        ScannedAtUtc = now;
        ScanTries++;
        Erased(FilesSql.ErasedForRefused, now);
    }

    public void ScanNotAnswered() => ScanTries++;

    public void Erased(string reason, DateTimeOffset now)
    {
        ErasedAtUtc = now;
        ErasedFor = reason;
    }

    public override string ToString() => $"{nameof(StoredFile)}#{Id}";
}

/// <summary>
/// How long files of one retention class are kept (SH-095). Platform-wide: four classes and a
/// number of days, empty until the lawyer's values are set by the operators' settings (spec 024).
/// </summary>
public sealed class RetentionPeriod
{
    private RetentionPeriod()
    {
    }

    public string Class { get; private set; } = "";

    /// <summary>Null: not set, nothing is erased for its age.</summary>
    public int? Days { get; private set; }

    public DateTimeOffset? SetAtUtc { get; private set; }
}
