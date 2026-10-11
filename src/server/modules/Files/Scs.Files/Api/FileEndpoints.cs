using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Security;
using Scs.Files.Contracts;
using Scs.Files.Infrastructure;
using Scs.Files.Infrastructure.Persistence;
using Scs.Identity.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Files;

namespace Scs.Files.Api;

internal sealed record WayInResponse(string Url, DateTimeOffset ExpiresAt, string Name, long SizeBytes, string Type);

internal sealed record SampleFilesResponse(IReadOnlyList<RecordFileView> Files);

/// <summary>
/// The engine's addresses (contracts/files-api.openapi.yaml). Taking a file in, removing one
/// not yet attached, asking for a way in and fetching the content are open to any signed-in
/// person and decided by the kind's owner, the uploader's identity and the token — not by one
/// permission: who may see a file depends on its record, which only its owner knows. They are
/// on spec 005's written list of such addresses for that reason.
/// </summary>
internal static class FileEndpoints
{
    /// <summary>Room for a file somewhat over the largest limit, so it is refused by name and size, not by the server.</summary>
    private static readonly long UploadBodyLimit = (FileKinds.LargestBytes * 2) + FileKinds.Megabyte;

    public static IEndpointRouteBuilder MapFiles(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/files", async (HttpRequest request, Uploads uploads, CancellationToken cancellation) =>
            {
                if (!request.HasFormContentType)
                {
                    throw Invalid("file", "REQUIRED");
                }

                var form = await request.ReadFormAsync(cancellation);
                if (form.Files.Count != 1)
                {
                    throw Invalid("file", form.Files.Count == 0 ? "REQUIRED" : "ONE_AT_A_TIME");
                }

                var uploaded = await uploads.UploadAsync(request.Query["kind"].ToString(), form.Files[0], cancellation);
                return TypedResults.Json(uploaded, statusCode: StatusCodes.Status201Created);
            })
            .WithMetadata(new RequestSizeLimitAttribute(UploadBodyLimit))
            .OwnAccountOnly()
            .RecordsNothing("an uploaded file is nobody's until the save of its record attaches it")
            .WithName("UploadFile");

        v1.MapDelete("/files/{id:guid}", async (Guid id, Uploads uploads, FilesDbContext db, CancellationToken cancellation) =>
            {
                if (await uploads.DiscardAsync(id, cancellation))
                {
                    return Results.NoContent();
                }

                // Gone already answers as done; anything that is not the caller's to discard does not exist.
                return await StillThereAsync(db, id, cancellation)
                    ? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound)
                    : Results.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing("removes a file its uploader never attached")
            .WithName("DiscardUploadedFile");

        v1.MapPost("/files/{id:guid}/access", async (Guid id, FileDownloads downloads, CancellationToken cancellation) =>
            {
                var wayIn = await downloads.IssueAsync(id, cancellation);

                // A path of this platform, to be asked for as every other address is.
                var url = $"/v1/files/content/{wayIn.Token}";
                return TypedResults.Ok(new WayInResponse(url, wayIn.ExpiresAt, wayIn.Name, wayIn.SizeBytes, wayIn.Type));
            })
            .OwnAccountOnly()
            .Records(AuditActions.FileDownloaded)
            .WithName("AskForFile");

        v1.MapGet("/files/content/{token}", async (string token, HttpResponse response, FileDownloads downloads, CancellationToken cancellation) =>
            {
                var file = await downloads.OpenAsync(token, cancellation)
                           ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

                // Sent to be saved, never to be opened as part of the product (FR-028).
                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
                response.Headers.CacheControl = "private, no-store";
                return Results.Stream(file.Content, FileTypes.MediaType(file.Type), file.Name);
            })
            .OwnAccountOnly()
            .WithName("GetFileContent");

        v1.MapGet("/public/files/{id:guid}", async (Guid id, HttpResponse response, PublicFiles files, CancellationToken cancellation) =>
            {
                var file = await files.OpenAsync(id, cancellation)
                           ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers.CacheControl = "public, max-age=86400";
                return Results.Stream(file.Content, FileTypes.MediaType(file.Type));
            })
            .AllowAnonymous()
            .WithName("GetPublicFile");

        if (v1.ServiceProvider.GetRequiredService<IOptions<ScsOptions>>().Value.IsLocal)
        {
            MapSample(v1);
        }

        return v1;
    }

    /// <summary>
    /// A developer's machine only, like the other <c>/v1/dev/…</c> addresses: no page owns a kind
    /// of file until specs 029, 032 and 082, and the engine has to be walked somehow. The
    /// "record" is the caller's own account.
    /// </summary>
    private static void MapSample(IEndpointRouteBuilder v1)
    {
        const string Reason = "local and test environments only";

        v1.MapGet("/dev/files/sample", async (ICurrentStaff staff, IRecordFiles files, CancellationToken cancellation) =>
                TypedResults.Ok(new SampleFilesResponse(
                    await files.ListAsync(FileRecord.Account, staff.AccountId, includeRemoved: true, cancellation))))
            .OwnAccountOnly()
            .WithName("DevListSampleFiles");

        v1.MapPost("/dev/files/sample", async (JsonElement body, ICurrentStaff staff, FilesDbContext db, CancellationToken cancellation) =>
            {
                if (body.ValueKind != JsonValueKind.Object
                    || !body.TryGetProperty("fileIds", out var given)
                    || given.ValueKind != JsonValueKind.Array
                    || given.EnumerateArray().Any(id => !id.TryGetGuid(out _)))
                {
                    throw Invalid("fileIds", "REQUIRED");
                }

                db.AttachFiles(
                    FileKinds.DevSample,
                    new FileRecord(FileRecord.Account, staff.AccountId),
                    [.. given.EnumerateArray().Select(id => id.GetGuid())]);
                await db.SaveChangesAsync(cancellation);
                return TypedResults.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing(Reason)
            .WithName("DevAttachSampleFiles");

        v1.MapPost("/dev/files/sample/{id:guid}/remove", async (Guid id, ICurrentStaff staff, FilesDbContext db, IRecordFiles files, CancellationToken cancellation) =>
            {
                await OwnSampleAsync(files, staff, id, cancellation);
                db.RemoveFile(FileKinds.DevSample, id);
                await db.SaveChangesAsync(cancellation);
                return TypedResults.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing(Reason)
            .WithName("DevRemoveSampleFile");

        v1.MapPost("/dev/files/sample/{id:guid}/restore", async (Guid id, ICurrentStaff staff, FilesDbContext db, IRecordFiles files, CancellationToken cancellation) =>
            {
                await OwnSampleAsync(files, staff, id, cancellation);
                db.RestoreFile(FileKinds.DevSample, id);
                await db.SaveChangesAsync(cancellation);
                return TypedResults.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing(Reason)
            .WithName("DevRestoreSampleFile");
    }

    /// <summary>The page that owns a record decides what may be removed from it: here, only the caller's own.</summary>
    private static async Task OwnSampleAsync(IRecordFiles files, ICurrentStaff staff, Guid fileId, CancellationToken cancellation)
    {
        var mine = await files.ListAsync(FileRecord.Account, staff.AccountId, includeRemoved: true, cancellation);
        if (mine.All(file => file.Id != fileId))
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }
    }

    private static async Task<bool> StillThereAsync(FilesDbContext db, Guid id, CancellationToken cancellation) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Files, f => f.Id == id, cancellation);

    private static ScsProblemException Invalid(string field, string code) =>
        ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [code] });
}
