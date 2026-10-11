using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Jobs;
using Scs.Files.Contracts;
using Scs.Files.Domain;
using Scs.Files.Infrastructure.Persistence;
using Scs.Identity.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Files;

namespace Scs.Files.Infrastructure;

/// <summary>What the upload address answers: the file as it stands, unattached and being checked.</summary>
public sealed record UploadedFileView(Guid Id, string Name, long SizeBytes, string Type, string State);

/// <summary>
/// Takes one file in (spec 009, FR-005 to FR-013): asks the kind's owner, checks the person's
/// limits, the size and — from the content — the type, stores the content under a new id, and
/// writes its row as the uploader's alone, unattached and waiting for its scan. Every check is
/// made here, whatever the browser did.
/// </summary>
internal sealed class Uploads(
    FilesDbContext db,
    IFileStore store,
    IFileKindCatalogue catalogue,
    IEnumerable<IFileKindOwner> owners,
    ICurrentStaff staff,
    IClinicJobScheduler jobs,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    /// <summary>One person's files uploaded and not yet attached, at one time (FR-012a).</summary>
    public const int MaxWaiting = 30;

    /// <summary>One person's uploads in any ten minutes (FR-012a).</summary>
    public const int MaxInWindow = 60;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public async Task<UploadedFileView> UploadAsync(string? kindKey, IFormFile? upload, CancellationToken cancellation)
    {
        var kind = await KindForUploadAsync(kindKey, cancellation).ConfigureAwait(false);
        if (upload is null)
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = ["REQUIRED"] });
        }

        var now = clock.GetUtcNow();
        await CheckLimitsAsync(now, cancellation).ConfigureAwait(false);

        if (upload.Length == 0)
        {
            throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.FileEmpty);
        }

        if (upload.Length > kind.MaxBytes)
        {
            throw new ScsProblemException(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge)
            {
                Extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["sizeBytes"] = upload.Length,
                    ["maxBytes"] = kind.MaxBytes,
                },
            };
        }

        var content = upload.OpenReadStream();
        await using (content.ConfigureAwait(false))
        {
            var type = await ReadTypeAsync(content, cancellation).ConfigureAwait(false);
            if (type is null || !kind.Types.Contains(type, StringComparer.Ordinal))
            {
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.FileTypeNotAccepted)
                {
                    Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["accepted"] = kind.Types },
                };
            }

            var id = Guid.CreateVersion7();
            var size = await StoreAsync(kind, type, id, content, cancellation).ConfigureAwait(false);
            var file = StoredFile.Uploaded(id, kind.Key, NameOf(upload.FileName), size, type, staff.AccountId, now);

            try
            {
                db.Files.Add(file);
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch
            {
                // No row, no content: a file is both or neither.
                await store.DeleteAsync(staff.ClinicId, id, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            jobs.TryEnqueueForClinic<FileScanJob>(staff.ClinicId);
            return new UploadedFileView(file.Id, file.Name, file.SizeBytes, file.Type, FileStates.BeingChecked);
        }
    }

    /// <summary>Removes a file the caller uploaded and has not attached. Again changes nothing.</summary>
    public async Task<bool> DiscardAsync(Guid fileId, CancellationToken cancellation)
    {
        var file = await db.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UploadedByAccountId == staff.AccountId && f.RecordId == null, cancellation)
            .ConfigureAwait(false);
        if (file is null)
        {
            return false;
        }

        db.Files.Remove(file);
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        await store.DeleteAsync(file.ClinicId, file.Id, cancellation).ConfigureAwait(false);
        return true;
    }

    private async Task<FileKind> KindForUploadAsync(string? kindKey, CancellationToken cancellation)
    {
        var kind = kindKey is { Length: > 0 and <= FileKinds.KeyMaxLength } ? catalogue.Find(kindKey) : null;
        var owner = kind is null ? null : owners.FirstOrDefault(o => o.Kind == kind.Key);

        // An unknown kind, a builders' kind outside a developer's machine, and a kind nobody owns
        // are refused alike, for everyone (FR-004, FR-021).
        if (kind is null || owner is null || (kind.LocalOnly && !options.Value.IsLocal))
        {
            throw FileRefusals.NobodyMay();
        }

        if (!await owner.MayUploadAsync(cancellation).ConfigureAwait(false))
        {
            throw FileRefusals.OwnerSaysNo();
        }

        return kind;
    }

    private async Task CheckLimitsAsync(DateTimeOffset now, CancellationToken cancellation)
    {
        var since = now - Window;
        var mine = db.Files.Where(f => f.UploadedByAccountId == staff.AccountId);

        var recent = await mine.Where(f => f.UploadedAtUtc > since).OrderBy(f => f.UploadedAtUtc)
            .Select(f => f.UploadedAtUtc).Take(MaxInWindow).ToListAsync(cancellation).ConfigureAwait(false);
        if (recent.Count >= MaxInWindow)
        {
            throw LimitReached((int)Math.Ceiling((recent[0] + Window - now).TotalSeconds));
        }

        if (await mine.CountAsync(f => f.RecordId == null, cancellation).ConfigureAwait(false) >= MaxWaiting)
        {
            throw LimitReached(60);
        }
    }

    private static ScsProblemException LimitReached(int retryAfterSeconds) =>
        new(StatusCodes.Status429TooManyRequests, ErrorCodes.FileLimitReached)
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retryAfterSeconds"] = Math.Max(1, retryAfterSeconds) },
        };

    private static async Task<string?> ReadTypeAsync(Stream content, CancellationToken cancellation)
    {
        var head = new byte[FileSignatures.HeadLength];
        var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellation).ConfigureAwait(false);
        content.Position = 0;
        return FileSignatures.Read(head.AsSpan(0, read));
    }

    /// <returns>The size as stored.</returns>
    private async Task<long> StoreAsync(FileKind kind, string type, Guid id, Stream content, CancellationToken cancellation)
    {
        try
        {
            if (!kind.Public)
            {
                // A private file is kept exactly as it was uploaded (FR-028b).
                await store.PutAsync(staff.ClinicId, id, content, cancellation).ConfigureAwait(false);
                return content.Length;
            }

            using var whole = new MemoryStream();
            await content.CopyToAsync(whole, cancellation).ConfigureAwait(false);
            var cleaned = ImageDetails.Strip(whole.ToArray(), type);
            using var stripped = new MemoryStream(cleaned, writable: false);
            await store.PutAsync(staff.ClinicId, id, stripped, cancellation).ConfigureAwait(false);
            return cleaned.Length;
        }
        catch (FileStoreException)
        {
            throw new ScsProblemException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.FileNotStored);
        }
    }

    /// <summary>The name as given, without any path or control character, cut to what the row holds.</summary>
    internal static string NameOf(string? given)
    {
        var name = (given ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string([.. name.Where(c => !char.IsControl(c))]).Trim();

        if (name.Length == 0)
        {
            return "file";
        }

        return name.Length <= FilesSql.NameMaxLength ? name : name[..FilesSql.NameMaxLength];
    }
}
