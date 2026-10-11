using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Tenancy;
using Scs.Files.Contracts;
using Scs.Files.Domain;
using Scs.Files.Infrastructure.Persistence;
using Scs.Identity.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Files;

namespace Scs.Files.Infrastructure;

/// <summary>A way in to one file, for one account, until a moment a few minutes on.</summary>
public sealed record FileWayIn(string Token, DateTimeOffset ExpiresAt, string Name, long SizeBytes, string Type);

/// <summary>The content of one file, to be sent as an attachment.</summary>
public sealed record FileContent(Stream Content, string Name, string Type, long SizeBytes);

/// <summary>
/// Gives a private file out (spec 009, FR-020 to FR-028a). A way in is issued only after the
/// kind's owner has said this person may see the record — asked every time — and only for a
/// file whose scan is clean; issuing it is what the audit trail records as the download. The
/// way in holds the file, the account and an expiry, protected by the platform's keys: it
/// works for that account alone, and stops after five minutes.
/// </summary>
internal sealed class FileDownloads(
    FilesDbContext db,
    IFileStore store,
    IEnumerable<IFileKindOwner> owners,
    ICurrentStaff staff,
    IDataProtectionProvider protection,
    TimeProvider clock)
{
    public static readonly TimeSpan WayInLasts = TimeSpan.FromMinutes(5);

    private const byte Version = 1;
    private const int TokenLength = 1 + 16 + 16 + 8;

    private readonly IDataProtector _protector = protection.CreateProtector("Scs.Files.Access.v1");

    public async Task<FileWayIn> IssueAsync(Guid fileId, CancellationToken cancellation)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == fileId, cancellation).ConfigureAwait(false);

        // Another clinic's file is not seen at all; another person's unattached file is nobody else's.
        if (file is null || (!file.IsAttached && file.UploadedByAccountId != staff.AccountId))
        {
            throw NotFound();
        }

        if (file.IsAttached)
        {
            var owner = owners.FirstOrDefault(o => o.Kind == file.Kind)
                        ?? throw FileRefusals.NobodyMay();
            if (!await owner.MaySeeAsync(new FileRecord(file.RecordKind!, file.RecordId!.Value), cancellation).ConfigureAwait(false))
            {
                throw FileRefusals.OwnerSaysNo();
            }
        }

        switch (StateOf(file))
        {
            case FileStates.Removed or FileStates.NotKept:
                throw new ScsProblemException(StatusCodes.Status410Gone, ErrorCodes.FileNotKept);
            case FileStates.Refused:
                throw new ScsProblemException(StatusCodes.Status410Gone, ErrorCodes.FileRefused)
                {
                    Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = file.RefusedFor },
                };
            case FileStates.BeingChecked:
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FileBeingChecked);
        }

        var expires = clock.GetUtcNow() + WayInLasts;

        // A file leaving the system is recorded, as an export is (FR-028a).
        db.Record(new AuditRecord(
            AuditActions.FileDownloaded,
            new AuditTarget(AuditTarget.File, file.Id, file.Name),
            Facts: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = file.Kind,
                ["record"] = file.RecordKind,
            }));
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        return new FileWayIn(Protect(file.Id, staff.AccountId, expires), expires, file.Name, file.SizeBytes, file.Type);
    }

    /// <returns>Null for a way in that is not this account's, is altered or expired, or whose file is no longer available.</returns>
    public async Task<FileContent?> OpenAsync(string token, CancellationToken cancellation)
    {
        if (Unprotect(token) is not var (fileId, accountId, expires)
            || accountId != staff.AccountId
            || expires <= clock.GetUtcNow())
        {
            return null;
        }

        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, cancellation).ConfigureAwait(false);
        if (file is null || StateOf(file) != FileStates.Available)
        {
            return null;
        }

        var content = await store.OpenAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
        return content is null ? null : new FileContent(content, file.Name, file.Type, file.SizeBytes);
    }

    /// <summary>What a reader is told, in the order of data-model.md §3.</summary>
    internal static string StateOf(StoredFile file)
    {
        if (file.Scan == FilesSql.Refused)
        {
            return FileStates.Refused;
        }

        if (!file.HasContent)
        {
            return FileStates.NotKept;
        }

        if (file.RemovedAtUtc is not null)
        {
            return FileStates.Removed;
        }

        return file.Scan == FilesSql.Clean ? FileStates.Available : FileStates.BeingChecked;
    }

    private static ScsProblemException NotFound() => new(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private string Protect(Guid fileId, Guid accountId, DateTimeOffset expires)
    {
        var payload = new byte[TokenLength];
        payload[0] = Version;
        fileId.TryWriteBytes(payload.AsSpan(1, 16));
        accountId.TryWriteBytes(payload.AsSpan(17, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(33, 8), expires.UtcTicks);
        return WebEncoders.Base64UrlEncode(_protector.Protect(payload));
    }

    private (Guid FileId, Guid AccountId, DateTimeOffset Expires)? Unprotect(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1024)
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(WebEncoders.Base64UrlDecode(token));
            if (payload.Length != TokenLength || payload[0] != Version)
            {
                return null;
            }

            return (
                new Guid(payload.AsSpan(1, 16)),
                new Guid(payload.AsSpan(17, 16)),
                new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(33, 8)), TimeSpan.Zero));
        }
        catch (Exception refused) when (refused is CryptographicException or FormatException or ArgumentException)
        {
            // Not ours, altered, or made with a key that has been retired.
            return null;
        }
    }
}

/// <summary>A record's files, for the module that owns the record.</summary>
internal sealed class RecordFiles(FilesDbContext db) : IRecordFiles
{
    public async Task<IReadOnlyList<RecordFileView>> ListAsync(
        string recordKind, Guid recordId, bool includeRemoved, CancellationToken cancellation)
    {
        var files = await db.Files.AsNoTracking()
            .Where(f => f.RecordKind == recordKind && f.RecordId == recordId && (includeRemoved || f.RemovedAtUtc == null))
            .OrderBy(f => f.UploadedAtUtc).ThenBy(f => f.Id)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return
        [
            .. files.Select(f => new RecordFileView(
                f.Id,
                f.Name,
                f.SizeBytes,
                f.Type,
                FileDownloads.StateOf(f),
                f.RefusedFor,
                f.UploadedAtUtc,
                f.UploadedByAccountId,
                f.RemovedAtUtc is { } removed && f.HasContent ? removed + FilesSql.RestoreFor : null,
                f.HeldAtUtc is not null)),
        ];
    }
}

/// <summary>
/// Reads one file of a public kind for a request that has no clinic (spec 009, FR-027): a
/// declared crossing that can return an attached, clean file of a public kind and nothing else.
/// </summary>
internal sealed class PublicFiles(ICrossClinicConnectionFactory crossing, IFileKindCatalogue catalogue, IFileStore store)
{
    public async Task<FileContent?> OpenAsync(Guid fileId, CancellationToken cancellation)
    {
        var builder = new DbContextOptionsBuilder<FilesDbContext>();
        crossing.Configure(builder);
        var db = new FilesDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            // No clinic is established here: the application's own filter would match nothing.
            var file = await db.Files.AsNoTracking().IgnoreQueryFilters()
                .Where(f => f.Id == fileId
                            && f.RecordId != null
                            && f.RemovedAtUtc == null
                            && f.ErasedAtUtc == null
                            && f.Scan == FilesSql.Clean)
                .Select(f => new { f.ClinicId, f.Kind, f.Name, f.Type, f.SizeBytes })
                .FirstOrDefaultAsync(cancellation)
                .ConfigureAwait(false);

            if (file is null || catalogue.Find(file.Kind) is not { Public: true })
            {
                return null;
            }

            var content = await store.OpenAsync(file.ClinicId, fileId, cancellation).ConfigureAwait(false);
            return content is null ? null : new FileContent(content, file.Name, file.Type, file.SizeBytes);
        }
    }
}
