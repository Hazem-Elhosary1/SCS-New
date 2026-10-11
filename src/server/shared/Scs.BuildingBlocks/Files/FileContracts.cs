using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;
using Scs.SharedKernel.Files;

namespace Scs.BuildingBlocks.Files;

/// <summary>What a file is attached to: a kind of record and its id (spec 009).</summary>
/// <param name="Kind">Such as <c>ticket-message</c> or <c>patient</c>; at most 32 characters.</param>
public sealed record FileRecord(string Kind, Guid Id)
{
    public const int KindMaxLength = 32;

    public const string Account = "account";
}

/// <summary>
/// Answers, for one kind of file, who may upload and who may see (spec 009, FR-009, FR-020).
/// Registered by the module that owns the records the files are attached to — only it knows who
/// may see a ticket. Both are asked on the server, for the signed-in person and the established
/// clinic, at every upload and every download. A kind with no owner is refused for everyone.
/// </summary>
public interface IFileKindOwner
{
    /// <summary>A key of <see cref="FileKinds"/>.</summary>
    string Kind { get; }

    Task<bool> MayUploadAsync(CancellationToken cancellation);

    Task<bool> MaySeeAsync(FileRecord record, CancellationToken cancellation);
}

/// <summary>
/// Where file content is kept (spec 009, FR-034). Content is keyed by clinic and by the file's
/// id, never by its name. One implementation exists now, a folder; the cloud's store is written
/// with the cloud environment against this interface and the same contract tests.
/// </summary>
public interface IFileStore
{
    /// <summary>Stores the stream whole, or nothing: a failure leaves no part of it behind.</summary>
    Task PutAsync(Guid clinicId, Guid fileId, Stream content, CancellationToken cancellation);

    /// <summary>Null when nothing is stored under that key.</summary>
    Task<Stream?> OpenAsync(Guid clinicId, Guid fileId, CancellationToken cancellation);

    /// <summary>Deleting what is not there is not an error.</summary>
    Task DeleteAsync(Guid clinicId, Guid fileId, CancellationToken cancellation);

    /// <summary>Removes everything kept for one clinic, and nothing of another.</summary>
    Task DeleteClinicAsync(Guid clinicId, CancellationToken cancellation);
}

/// <summary>The store could not take or give content. Carries nothing of the file.</summary>
public sealed class FileStoreException(string message, Exception? inner = null) : IOException(message, inner);

public enum ScanAnswer
{
    Clean,

    /// <summary>Malware was found.</summary>
    Infected,

    /// <summary>The scanner cannot look inside: protected by a password, or damaged (FR-014a).</summary>
    Unreadable,

    /// <summary>The scanner could not be asked, or did not answer in time: ask again later (FR-018).</summary>
    NoAnswer,
}

/// <summary>Checks content for malware (spec 009, FR-014). Never throws for an unreachable scanner.</summary>
public interface IMalwareScanner
{
    Task<ScanAnswer> ScanAsync(Stream content, CancellationToken cancellation);
}

/// <summary>
/// A change to a record's files was refused, and the save with it (spec 009, FR-007, FR-008).
/// It reaches the caller of an address as a problem with its code.
/// </summary>
public sealed class FileRefusedException : ScsProblemException
{
    public FileRefusedException(int status, string code, string reason)
        : base(status, code, reason)
    {
    }

    /// <summary>A defect in the caller, or a file that is not the caller's to attach: said alike.</summary>
    public static FileRefusedException NotAttachable(string reason) =>
        new(StatusCodes.Status400BadRequest, ErrorCodes.FileNotAttachable, reason);

    public static FileRefusedException CountReached(int max) =>
        new(StatusCodes.Status409Conflict, ErrorCodes.FileCountReached, "The record already holds as many files as its kind allows.")
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["max"] = max },
        };

    public static FileRefusedException NotKept(string reason) =>
        new(StatusCodes.Status410Gone, ErrorCodes.FileNotKept, reason);
}

/// <summary>
/// The refusals of the engine's addresses that are about who is asking (spec 009, FR-009,
/// FR-020, FR-021). Said here, once: a module does not answer for a missing permission itself.
/// </summary>
public static class FileRefusals
{
    /// <summary>The kind's owner says this person may not upload, or may not see the record.</summary>
    public static ScsProblemException OwnerSaysNo() => new(StatusCodes.Status403Forbidden, ErrorCodes.PermissionRequired);

    /// <summary>A kind that is not in the table, or that nobody owns: refused for everyone.</summary>
    public static ScsProblemException NobodyMay() => new(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
}

public static class FileExtensions
{
    /// <summary>What every host that saves a record with files needs: the table of kinds.</summary>
    public static IServiceCollection AddScsFiles(this IServiceCollection services)
    {
        services.TryAddSingleton<IFileKindCatalogue, FileKindCatalogue>();
        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>Registers the owner of one kind of file. One owner for a kind.</summary>
    public static IServiceCollection AddFileKindOwner<TOwner>(this IServiceCollection services)
        where TOwner : class, IFileKindOwner
    {
        services.AddScoped<IFileKindOwner, TOwner>();
        return services;
    }
}
