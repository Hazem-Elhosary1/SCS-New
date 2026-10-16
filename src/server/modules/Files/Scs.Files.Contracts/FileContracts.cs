namespace Scs.Files.Contracts;

/// <summary>What a reader is told about a file's availability (spec 009, FR-026).</summary>
public static class FileStates
{
    public const string Available = "available";
    public const string BeingChecked = "being-checked";
    public const string Refused = "refused";
    public const string Removed = "removed";
    public const string NotKept = "not-kept";
}

/// <summary>One file of a record, as the page that owns the record shows it.</summary>
/// <param name="Type"><c>pdf</c>, <c>png</c> or <c>jpeg</c>, as read from the content.</param>
/// <param name="State">One of <see cref="FileStates"/>.</param>
/// <param name="RefusedFor"><c>infected</c> or <c>unreadable</c>, when the check refused it.</param>
/// <param name="RestorableUntil">For a removed file whose content is still kept: until when it can be put back.</param>
public sealed record RecordFileView(
    Guid Id,
    string Name,
    long SizeBytes,
    string Type,
    string State,
    string? RefusedFor,
    DateTimeOffset UploadedAt,
    Guid UploadedByAccountId,
    DateTimeOffset? RestorableUntil,
    bool Held);

/// <summary>
/// A record's files, for the module that owns the record (spec 009, contracts/files-engine.md
/// §4). It reads for the established clinic only. The owning page puts the answer in its own;
/// the browser downloads through the engine's addresses.
/// </summary>
public interface IRecordFiles
{
    Task<IReadOnlyList<RecordFileView>> ListAsync(string recordKind, Guid recordId, bool includeRemoved, CancellationToken cancellation);
}

/// <summary>
/// What the platform's own work asks of the engine: removing everything of one clinic when a
/// cancelled clinic's data is erased (FR-032). Never offered to a request.
/// </summary>
public interface IClinicFiles
{
    /// <returns>How many files were removed.</returns>
    Task<int> RemoveClinicAsync(Guid clinicId, CancellationToken cancellation);
}
