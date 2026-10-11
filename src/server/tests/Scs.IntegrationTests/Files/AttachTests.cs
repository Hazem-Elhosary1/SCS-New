using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Files;
using Scs.Identity.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Files;

namespace Scs.IntegrationTests.Files;

/// <summary>
/// Spec 009, User Stories 1 and 6 (FR-007, FR-008, FR-030 to FR-031b): a record's files change
/// with the save of the module that owns the record — attached, removed, restored, held, and
/// given their retention clock — or not at all.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AttachTests(SqlServerFixture sql)
{
    private static FileRecord Visit() => new("visit", Guid.CreateVersion7());

    [Fact]
    public async Task Files_are_attached_by_the_save_that_names_them_and_listed_on_their_record()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var record = Visit();
        var first = await world.Browser.UploadedAsync(TestKinds.Three, "one.pdf", Samples.Pdf(1500));
        var second = await world.Browser.UploadedAsync(TestKinds.Three, "two.png", Samples.Png());

        Assert.Null(await world.AttachAsync(TestKinds.Three, record, first, second));

        var rows = await sql.Main.FilesAsync(world.Clinic);
        Assert.All(rows, row => Assert.Equal((record.Kind, (Guid?)record.Id), (row.RecordKind, row.RecordId)));

        // A file not scanned yet is attached all the same, and reads as being checked (FR-018).
        var listed = await world.ListAsync(record);
        Assert.Equal(
            [("one.pdf", 1500L, "pdf", "being-checked"), ("two.png", (long)Samples.Png().Length, "png", "being-checked")],
            listed.Select(f => (f.Name, f.SizeBytes, f.Type, f.State)));
        Assert.All(listed, f => Assert.Equal(world.Person.AccountId, f.UploadedByAccountId));
    }

    [Fact]
    public async Task When_the_save_fails_or_is_rolled_back_the_file_stays_unattached()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "one.pdf", Samples.Pdf());

        // The caller's transaction is rolled back after a save that succeeded.
        await world.Factory.AsStaffAsync(world.Clinic, world.Person.AccountId, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.AttachFiles(TestKinds.Three, Visit(), [id]);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        });
        Assert.Null(Assert.Single(await sql.Main.FilesAsync(world.Clinic)).RecordId);

        // A second change in the same save is refused: the first goes with it.
        var refused = await world.AsAsync(world.Person.AccountId, db =>
        {
            db.AttachFiles(TestKinds.Three, Visit(), [id]);
            db.AttachFiles(TestKinds.Three, Visit(), [Guid.CreateVersion7()]);
        });
        Assert.IsType<FileRefusedException>(refused);
        Assert.Null(Assert.Single(await sql.Main.FilesAsync(world.Clinic)).RecordId);

        // And it can still be attached by the next try.
        Assert.Null(await world.AttachAsync(TestKinds.Three, Visit(), id));
    }

    [Fact]
    public async Task What_is_not_this_persons_file_of_this_kind_and_unattached_is_refused_alike()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var elsewhere = await sql.Main.SeedClinicAsync();
        using var stranger = await world.Factory.SignInWithAsync(elsewhere, Permissions.BookingsView);

        var mine = await world.Browser.UploadedAsync(TestKinds.Three, "mine.pdf", Samples.Pdf());
        var theirs = await colleague.Browser.UploadedAsync(TestKinds.Three, "theirs.pdf", Samples.Pdf());
        var foreign = await stranger.Browser.UploadedAsync(TestKinds.Three, "foreign.pdf", Samples.Pdf());
        var (attached, _) = await world.CleanFileAsync();

        var cases = new (string Kind, Guid Id)[]
        {
            (TestKinds.Many, mine),            // another kind
            (TestKinds.Three, theirs),         // another person's
            (TestKinds.Three, foreign),        // another clinic's
            (TestKinds.Three, attached),       // attached already
            (TestKinds.Three, Guid.CreateVersion7()),
            ("no.such-kind", mine),
        };

        foreach (var (kind, id) in cases)
        {
            var refused = await world.AttachAsync(kind, Visit(), id);
            var problem = Assert.IsType<FileRefusedException>(refused);
            Assert.Equal((400, "FILE_NOT_ATTACHABLE"), (problem.Status, problem.Code));
        }

        // A record that is not named by a kind and an id.
        Assert.IsType<FileRefusedException>(await world.AttachAsync(TestKinds.Three, new FileRecord("", Guid.CreateVersion7()), mine));
        Assert.IsType<FileRefusedException>(await world.AttachAsync(TestKinds.Three, new FileRecord("visit", Guid.Empty), mine));

        Assert.Null((await sql.Main.FilesAsync(world.Clinic)).Single(f => f.Id == mine).RecordId);
    }

    [Fact]
    public async Task A_file_refused_before_its_record_was_saved_is_attached_all_the_same_and_reads_refused()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var record = Visit();
        var bad = await world.Browser.UploadedAsync(TestKinds.OnePdf, "bad.pdf", Samples.Eicar());
        var good = await world.Browser.UploadedAsync(TestKinds.OnePdf, "good.pdf", Samples.Pdf());

        // The scan was quicker than the person's save.
        await world.Factory.ScanFilesAsync(world.Clinic);

        // The save succeeds, whichever came first; and the refused file takes no place among
        // those the record may hold — this kind takes one.
        Assert.Null(await world.AttachAsync(TestKinds.OnePdf, record, bad, good));

        Assert.Equal(
            [("bad.pdf", "refused"), ("good.pdf", "available")],
            (await world.ListAsync(record)).Select(f => (f.Name, f.State)));
        using var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{bad}/access", UriKind.Relative), null);
        Assert.Equal((410, "FILE_REFUSED"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));
    }

    [Fact]
    public async Task A_record_holds_no_more_than_its_kind_allows()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var record = Visit();
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            ids.Add(await world.Browser.UploadedAsync(TestKinds.Three, $"f{i}.pdf", Samples.Pdf()));
        }

        var four = Assert.IsType<FileRefusedException>(await world.AttachAsync(TestKinds.Three, record, [.. ids]));
        Assert.Equal((409, "FILE_COUNT_REACHED", 3), (four.Status, four.Code, (int)four.Extensions!["max"]!));

        Assert.Null(await world.AttachAsync(TestKinds.Three, record, ids[0], ids[1]));
        Assert.Null(await world.AttachAsync(TestKinds.Three, record, ids[2]));
        Assert.IsType<FileRefusedException>(await world.AttachAsync(TestKinds.Three, record, ids[3]));

        // Another record of the same kind has its own count.
        Assert.Null(await world.AttachAsync(TestKinds.Three, Visit(), ids[3]));
    }

    [Fact]
    public async Task The_save_that_cannot_wait_is_refused()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "one.pdf", Samples.Pdf());

        var refused = await world.Factory.AsStaffAsync(world.Clinic, world.Person.AccountId, services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            db.AttachFiles(TestKinds.Three, Visit(), [id]);
            return Task.FromResult(Record.Exception(() => db.SaveChanges()));
        });

        Assert.IsType<FileRefusedException>(refused);
    }

    [Fact]
    public async Task A_removed_file_is_gone_from_its_record_and_can_be_put_back_for_thirty_days()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var (id, record) = await world.CleanFileAsync();

        Assert.Null(await world.AsAsync(colleague.AccountId, db => db.RemoveFile(TestKinds.Three, id)));

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(colleague.AccountId, row.RemovedBy);
        Assert.NotNull(row.RemovedAtUtc);
        Assert.Empty(await world.ListAsync(record));
        var removed = Assert.Single(await world.ListAsync(record, includeRemoved: true));
        Assert.Equal(("removed", row.RemovedAtUtc + FilesSql.RestoreFor), (removed.State, removed.RestorableUntil));
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, id));

        using (var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null))
        {
            Assert.Equal((410, "FILE_NOT_KEPT"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));
        }

        // Removing again, or under another kind, is a defect of the caller.
        Assert.IsType<FileRefusedException>(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.Three, id)));
        Assert.IsType<FileRefusedException>(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.Many, id)));

        // On the thirtieth day it still comes back, as it was.
        world.Factory.Clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromMinutes(1));
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.Three, id)));

        row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Null(row.RemovedAtUtc);
        Assert.NotNull(row.RestoredAtUtc);
        Assert.Equal("available", Assert.Single(await world.ListAsync(record)).State);
    }

    [Fact]
    public async Task After_thirty_days_or_into_a_full_record_nothing_is_restored()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (late, _) = await world.CleanFileAsync(TestKinds.OnePdf);
        var (crowded, record) = await world.CleanFileAsync(TestKinds.OnePdf);
        Assert.Null(await world.AsAsync(world.Person.AccountId, db =>
        {
            db.RemoveFile(TestKinds.OnePdf, late);
            db.RemoveFile(TestKinds.OnePdf, crowded);
        }));

        // The record takes one file, and has taken another since.
        var replacement = await world.Browser.UploadedAsync(TestKinds.OnePdf, "new.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.OnePdf, record, replacement));
        var full = Assert.IsType<FileRefusedException>(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.OnePdf, crowded)));
        Assert.Equal("FILE_COUNT_REACHED", full.Code);

        world.Factory.Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(1));
        var past = Assert.IsType<FileRefusedException>(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.OnePdf, late)));
        Assert.Equal((410, "FILE_NOT_KEPT"), (past.Status, past.Code));
    }

    [Fact]
    public async Task The_owner_starts_and_takes_back_the_retention_period_of_a_records_files()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, record) = await world.CleanFileAsync();
        var (other, _) = await world.CleanFileAsync();
        var closed = world.Factory.Clock.GetUtcNow().AddDays(-3);

        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.StartFileRetention(record, closed)));

        var rows = await sql.Main.FilesAsync(world.Clinic);
        Assert.Equal(closed, rows.Single(f => f.Id == id).RetainFromUtc);
        Assert.Null(rows.Single(f => f.Id == other).RetainFromUtc);

        // A file that joins the record afterwards shares its clock.
        var joined = await world.Browser.UploadedAsync(TestKinds.Three, "later.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.Three, record, joined));
        Assert.Equal(closed, (await sql.Main.FilesAsync(world.Clinic)).Single(f => f.Id == joined).RetainFromUtc);

        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.StopFileRetention(record)));
        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Null(f.RetainFromUtc));

        // Undone with a save that fails.
        Assert.NotNull(await world.AsAsync(world.Person.AccountId, db =>
        {
            db.StartFileRetention(record, closed);
            db.RemoveFile(TestKinds.Three, Guid.CreateVersion7());
        }));
        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Null(f.RetainFromUtc));
    }

    [Fact]
    public async Task A_hold_covers_every_file_of_the_record_and_those_that_join_it()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, record) = await world.CleanFileAsync();
        var (other, _) = await world.CleanFileAsync();

        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.HoldFiles(record)));

        var rows = await sql.Main.FilesAsync(world.Clinic);
        Assert.NotNull(rows.Single(f => f.Id == id).HeldAtUtc);
        Assert.Null(rows.Single(f => f.Id == other).HeldAtUtc);
        Assert.True(Assert.Single(await world.ListAsync(record)).Held);

        var joined = await world.Browser.UploadedAsync(TestKinds.Three, "later.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.Three, record, joined));
        Assert.NotNull((await sql.Main.FilesAsync(world.Clinic)).Single(f => f.Id == joined).HeldAtUtc);

        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.ReleaseFiles(record)));
        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Null(f.HeldAtUtc));
    }
}
