using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Files;

namespace Scs.Files.Infrastructure;

/// <summary>
/// Keeps file content in a folder, one folder for each clinic (spec 009, research R2): the
/// store of a developer's machine and of the tests. A file is written under a temporary name
/// and moved into place when it is whole, so a failed upload leaves nothing behind. The path is
/// built from two identifiers and nothing a person typed.
/// </summary>
internal sealed class FolderFileStore(IOptions<ScsOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.Files.RootOrLocal());

    public async Task PutAsync(Guid clinicId, Guid fileId, Stream content, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathOf(clinicId, fileId);
        var partial = path + ".part";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await using (target.ConfigureAwait(false))
            {
                await content.CopyToAsync(target, cancellation).ConfigureAwait(false);
            }

            File.Move(partial, path, overwrite: true);
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Discard(partial);
            if (failed is OperationCanceledException)
            {
                throw;
            }

            throw new FileStoreException("The file could not be stored.", failed);
        }
    }

    public Task<Stream?> OpenAsync(Guid clinicId, Guid fileId, CancellationToken cancellation)
    {
        var path = PathOf(clinicId, fileId);
        try
        {
            return Task.FromResult<Stream?>(
                File.Exists(path)
                    ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true)
                    : null);
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            throw new FileStoreException("The file could not be read.", failed);
        }
    }

    public Task DeleteAsync(Guid clinicId, Guid fileId, CancellationToken cancellation)
    {
        try
        {
            File.Delete(PathOf(clinicId, fileId));
            return Task.CompletedTask;
        }
        catch (DirectoryNotFoundException)
        {
            return Task.CompletedTask;
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            throw new FileStoreException("The file could not be deleted.", failed);
        }
    }

    public Task DeleteClinicAsync(Guid clinicId, CancellationToken cancellation)
    {
        try
        {
            var folder = FolderOf(clinicId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            return Task.CompletedTask;
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            throw new FileStoreException("The clinic's files could not be deleted.", failed);
        }
    }

    private string FolderOf(Guid clinicId) => Path.Combine(_root, clinicId.ToString("N"));

    private string PathOf(Guid clinicId, Guid fileId) => Path.Combine(FolderOf(clinicId), fileId.ToString("N"));

    private static void Discard(string partial)
    {
        try
        {
            File.Delete(partial);
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            // Nothing more can be done here; a partial file has no row and is never read.
        }
    }
}
