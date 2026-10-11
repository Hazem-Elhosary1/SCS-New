using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Tenancy;
using Scs.Files.Infrastructure;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Files;

/// <summary>
/// What any place that keeps file content must do (spec 009, research R2). The folder store
/// passes it here; the store written with the cloud environment passes the same.
/// </summary>
public abstract class FileStoreContract
{
    protected abstract IFileStore Store { get; }

    private static MemoryStream Content(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    private async Task<string?> ReadAsync(Guid clinic, Guid file)
    {
        var content = await Store.OpenAsync(clinic, file, CancellationToken.None);
        if (content is null)
        {
            return null;
        }

        await using (content)
        {
            using var reader = new StreamReader(content);
            return await reader.ReadToEndAsync();
        }
    }

    [Fact]
    public async Task What_is_put_is_read_back_byte_for_byte_under_its_clinic_and_id()
    {
        var (clinic, file) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var bytes = new byte[300_000];
        Random.Shared.NextBytes(bytes);

        using (var content = new MemoryStream(bytes))
        {
            await Store.PutAsync(clinic, file, content, CancellationToken.None);
        }

        var read = await Store.OpenAsync(clinic, file, CancellationToken.None);
        Assert.NotNull(read);
        await using (read)
        {
            using var whole = new MemoryStream();
            await read.CopyToAsync(whole);
            Assert.Equal(bytes, whole.ToArray());
        }

        // The same id under another clinic is another place.
        Assert.Null(await ReadAsync(Guid.CreateVersion7(), file));
    }

    [Fact]
    public async Task What_is_not_there_is_not_found_and_deleting_it_is_not_an_error()
    {
        var (clinic, file) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Null(await ReadAsync(clinic, file));
        await Store.DeleteAsync(clinic, file, CancellationToken.None);

        using (var content = Content("one"))
        {
            await Store.PutAsync(clinic, file, content, CancellationToken.None);
        }

        await Store.DeleteAsync(clinic, file, CancellationToken.None);
        await Store.DeleteAsync(clinic, file, CancellationToken.None);
        Assert.Null(await ReadAsync(clinic, file));
    }

    [Fact]
    public async Task Removing_a_clinics_files_removes_all_of_them_and_none_of_anothers()
    {
        var (mine, theirs) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var (first, second, other) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        foreach (var (clinic, file) in new[] { (mine, first), (mine, second), (theirs, other) })
        {
            using var content = Content(file.ToString());
            await Store.PutAsync(clinic, file, content, CancellationToken.None);
        }

        await Store.DeleteClinicAsync(mine, CancellationToken.None);
        await Store.DeleteClinicAsync(mine, CancellationToken.None);

        Assert.Null(await ReadAsync(mine, first));
        Assert.Null(await ReadAsync(mine, second));
        Assert.Equal(other.ToString(), await ReadAsync(theirs, other));
    }

    [Fact]
    public async Task A_put_that_fails_part_way_leaves_nothing_behind()
    {
        var (clinic, file) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        await Assert.ThrowsAnyAsync<Exception>(() => Store.PutAsync(clinic, file, new BreakingStream(), CancellationToken.None));

        Assert.Null(await ReadAsync(clinic, file));
    }

    /// <summary>Gives some bytes, then breaks, as a connection that drops does.</summary>
    private sealed class BreakingStream : MemoryStream
    {
        private int _reads;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_reads++ > 0)
            {
                throw new IOException("The connection dropped.");
            }

            buffer.Span[..10].Fill(1);
            return ValueTask.FromResult(10);
        }
    }
}

public sealed class FolderFileStoreTests : FileStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scs-test-files", "store-" + Guid.NewGuid().ToString("N"));

    protected override IFileStore Store => new FolderFileStore(Options.Create(new ScsOptions { Files = new FilesOptions { Root = _root } }));

    [Fact]
    public async Task Nothing_is_ever_kept_outside_the_root_or_under_a_name()
    {
        var (clinic, file) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        using (var content = new MemoryStream([1, 2, 3]))
        {
            await Store.PutAsync(clinic, file, content, CancellationToken.None);
        }

        var kept = Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
        Assert.Equal(Path.Combine(Path.GetFullPath(_root), clinic.ToString("N"), file.ToString("N")), Path.GetFullPath(kept));
    }

    [Fact]
    public async Task A_root_that_cannot_be_written_fails_the_put_as_a_store_failure()
    {
        // A file where the root folder should be: nothing can be created under it.
        var blocked = Path.Combine(Path.GetTempPath(), "scs-test-files", "blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blocked)!);
        await File.WriteAllTextAsync(blocked, "not a folder");
        var store = new FolderFileStore(Options.Create(new ScsOptions { Files = new FilesOptions { Root = blocked } }));

        try
        {
            using var content = new MemoryStream([1]);
            await Assert.ThrowsAsync<FileStoreException>(() => store.PutAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), content, CancellationToken.None));
        }
        finally
        {
            File.Delete(blocked);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>Spec 009: the platform-wide table of retention periods is what it is declared to be.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class RetentionPeriodTableTests(SqlServerFixture sql)
{
    [Fact]
    public async Task It_is_declared_platform_wide_holds_the_four_classes_and_no_period_yet()
    {
        await sql.EnsureMigratedAsync();

        Assert.True(PlatformWideTables.Contains("files", "RetentionPeriod"));
        Assert.False(PlatformWideTables.Contains("files", "StoredFile"));

        // Read by the application's own login, with no clinic established.
        Assert.Equal(4, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT COUNT(*) FROM [files].RetentionPeriod"));
        Assert.Equal(
            4,
            await TestDatabase.ScalarAsync<int>(
                sql.Main.App,
                "SELECT COUNT(*) FROM [files].RetentionPeriod WHERE Class IN ('medical-record', 'conversations', 'ticket-attachments', 'clinic-data')"));
    }
}
