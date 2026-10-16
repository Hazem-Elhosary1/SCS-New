using System.Net.Http.Json;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Files;
using Scs.Files.Contracts;
using Scs.Files.Infrastructure;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Files;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Files;

/// <summary>
/// Spec 009, User Story 6 (FR-029 to FR-033): a file is not kept longer than it should be.
/// What was never attached goes after a day; what was removed is erased after thirty; and a
/// retention period erases only files whose owner started it — unless the record is on hold.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CleanupTests(SqlServerFixture sql) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>The periods are the platform's, shared by every test: none is left set.</summary>
    public async Task DisposeAsync()
    {
        foreach (var retentionClass in RetentionClasses.WithPeriod)
        {
            await sql.Main.SetRetentionAsync(retentionClass, null);
        }
    }

    [Fact]
    public async Task What_was_never_attached_goes_after_a_day_row_and_content()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var elsewhere = await sql.Main.SeedClinicAsync();
        using var stranger = await world.Factory.SignInWithAsync(elsewhere, Permissions.BookingsView);
        var old = await world.Browser.UploadedAsync(TestKinds.Three, "old.pdf", Samples.Pdf());
        var theirs = await stranger.Browser.UploadedAsync(TestKinds.Three, "theirs.pdf", Samples.Pdf());
        var (kept, _) = await world.CleanFileAsync();

        world.Factory.Clock.Advance(TimeSpan.FromHours(23));
        await world.Factory.CleanFilesAsync(world.Clinic);
        Assert.Equal(2, (await sql.Main.FilesAsync(world.Clinic)).Count);

        world.Factory.Clock.Advance(TimeSpan.FromHours(2));
        await world.Factory.CleanFilesAsync(world.Clinic);
        await world.Factory.CleanFilesAsync(world.Clinic);

        Assert.Equal(kept, Assert.Single(await sql.Main.FilesAsync(world.Clinic)).Id);
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, old));
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, kept));

        // Each clinic's turn is its own, and none of it is an audit entry.
        Assert.Single(await sql.Main.FilesAsync(elsewhere));
        Assert.True(await world.Factory.StoreHasAsync(elsewhere, theirs));
        Assert.DoesNotContain(await sql.Main.EntriesAsync(world.Clinic), e => e.ActionCode.StartsWith("file.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_removed_file_is_erased_after_thirty_days_and_its_row_stays()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, record) = await world.CleanFileAsync(name: "x-ray.pdf");
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.Three, id)));

        world.Factory.Clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromHours(1));
        await world.Factory.CleanFilesAsync(world.Clinic);
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, id));
        Assert.Null(Assert.Single(await sql.Main.FilesAsync(world.Clinic)).ErasedAtUtc);

        world.Factory.Clock.Advance(TimeSpan.FromHours(2));
        await world.Factory.CleanFilesAsync(world.Clinic);
        await world.Factory.CleanFilesAsync(world.Clinic);

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(("removed", "x-ray.pdf", world.Person.AccountId, (Guid?)world.Person.AccountId), (row.ErasedFor, row.Name, row.UploadedBy, row.RemovedBy));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));

        // What remains says a file was there, and it cannot come back.
        var left = Assert.Single(await world.ListAsync(record, includeRemoved: true));
        Assert.Equal((FileStates.NotKept, null), (left.State, left.RestorableUntil));
        var refused = Assert.IsType<FileRefusedException>(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.Three, id)));
        Assert.Equal("FILE_NOT_KEPT", refused.Code);
    }

    [Fact]
    public async Task A_retention_period_erases_only_files_whose_owner_started_it_and_only_of_its_class()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var now = world.Factory.Clock.GetUtcNow();
        var (never, _) = await world.CleanFileAsync(TestKinds.Ticket);
        var (past, pastRecord) = await world.CleanFileAsync(TestKinds.Ticket);
        var (recent, recentRecord) = await world.CleanFileAsync(TestKinds.Ticket);
        var (takenBack, takenBackRecord) = await world.CleanFileAsync(TestKinds.Ticket);
        var (otherClass, otherRecord) = await world.CleanFileAsync(TestKinds.Three);
        var (removedPast, removedRecord) = await world.CleanFileAsync(TestKinds.Ticket);

        Assert.Null(await world.AsAsync(world.Person.AccountId, db =>
        {
            db.StartFileRetention(pastRecord, now.AddDays(-91));
            db.StartFileRetention(recentRecord, now.AddDays(-10));
            db.StartFileRetention(takenBackRecord, now.AddDays(-200));
            db.StartFileRetention(otherRecord, now.AddDays(-200));
            db.StartFileRetention(removedRecord, now.AddDays(-91));
            db.RemoveFile(TestKinds.Ticket, removedPast);
        }));
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.StopFileRetention(takenBackRecord)));

        // With no period set, nothing is erased for its age.
        await world.Factory.CleanFilesAsync(world.Clinic);
        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Null(f.ErasedAtUtc));

        await sql.Main.SetRetentionAsync(RetentionClasses.TicketAttachments, 90);
        await world.Factory.CleanFilesAsync(world.Clinic);
        await world.Factory.CleanFilesAsync(world.Clinic);

        var rows = (await sql.Main.FilesAsync(world.Clinic)).ToDictionary(f => f.Id);
        Assert.Equal("age", rows[past].ErasedFor);

        // A removed file's retention is not extended by the thirty days of its removal.
        Assert.Equal("age", rows[removedPast].ErasedFor);
        Assert.All(new[] { never, recent, takenBack, otherClass }, id => Assert.Null(rows[id].ErasedAtUtc));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, past));
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, never));

        using var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{past}/access", UriKind.Relative), null);
        Assert.Equal((410, "FILE_NOT_KEPT"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));
    }

    [Fact]
    public async Task A_file_on_hold_is_erased_by_nothing_until_the_hold_is_lifted()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var now = world.Factory.Clock.GetUtcNow();
        var (aged, agedRecord) = await world.CleanFileAsync(TestKinds.Ticket);
        var (removed, removedRecord) = await world.CleanFileAsync(TestKinds.Ticket);
        Assert.Null(await world.AsAsync(world.Person.AccountId, db =>
        {
            db.StartFileRetention(agedRecord, now.AddDays(-400));
            db.RemoveFile(TestKinds.Ticket, removed);
            db.HoldFiles(agedRecord);
            db.HoldFiles(removedRecord);
        }));
        await sql.Main.SetRetentionAsync(RetentionClasses.TicketAttachments, 90);

        // Held, it is still not downloadable once removed.
        using (var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{removed}/access", UriKind.Relative), null))
        {
            Assert.Equal(410, (int)asked.StatusCode);
        }

        world.Factory.Clock.Advance(TimeSpan.FromDays(40));
        await world.Factory.CleanFilesAsync(world.Clinic);

        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Null(f.ErasedAtUtc));
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, aged) && await world.Factory.StoreHasAsync(world.Clinic, removed));

        Assert.Null(await world.AsAsync(world.Person.AccountId, db =>
        {
            db.ReleaseFiles(agedRecord);
            db.ReleaseFiles(removedRecord);
        }));
        await world.Factory.CleanFilesAsync(world.Clinic);

        var rows = (await sql.Main.FilesAsync(world.Clinic)).ToDictionary(f => f.Id);
        Assert.Equal(("age", "removed"), (rows[aged].ErasedFor, rows[removed].ErasedFor));
    }

    [Fact]
    public async Task Everything_of_one_clinic_can_be_removed_and_nothing_of_another()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var elsewhere = await sql.Main.SeedClinicAsync();
        using var stranger = await world.Factory.SignInWithAsync(elsewhere, Permissions.BookingsView);
        var theirs = await stranger.Browser.UploadedAsync(TestKinds.Three, "theirs.pdf", Samples.Pdf());
        var (mine, _) = await world.CleanFileAsync();
        await world.Browser.UploadedAsync(TestKinds.Three, "loose.pdf", Samples.Pdf());

        var removed = await world.Factory.ForClinicAsync(world.Clinic, services =>
            services.GetRequiredService<IClinicFiles>().RemoveClinicAsync(world.Clinic, CancellationToken.None));

        Assert.Equal(2, removed);
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, mine));
        Assert.True(await world.Factory.StoreHasAsync(elsewhere, theirs));
        Assert.Single(await sql.Main.FilesAsync(elsewhere));

        // It cannot be pointed at a clinic other than the one the work was started for.
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Factory.ForClinicAsync(world.Clinic, services =>
            services.GetRequiredService<IClinicFiles>().RemoveClinicAsync(elsewhere, CancellationToken.None)));
        Assert.Single(await sql.Main.FilesAsync(elsewhere));
    }
}

/// <summary>
/// Spec 009, User Story 7 (SC-009): a kind added to the table, with its owner, goes through the
/// whole of the engine with no change to it. The kinds used across these tests are exactly such
/// kinds; this walks one from end to end in one place.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class NewKindTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_new_kind_is_taken_checked_scanned_attached_listed_given_removed_restored_and_cleaned()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var record = new FileRecord("referral", Guid.CreateVersion7());

        using (var wrong = await world.Browser.UploadAsync(TestKinds.OnePdf, "photo.pdf", Samples.Png()))
        {
            Assert.Equal(400, (int)wrong.StatusCode);
        }

        var id = await world.Browser.UploadedAsync(TestKinds.OnePdf, "referral.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.OnePdf, record, id));
        await world.Factory.ScanFilesAsync(world.Clinic);
        Assert.Equal("available", Assert.Single(await world.ListAsync(record)).State);

        using (var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null))
        {
            Assert.Equal(200, (int)asked.StatusCode);
        }

        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.OnePdf, id)));
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RestoreFile(TestKinds.OnePdf, id)));
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.OnePdf, id)));
        world.Factory.Clock.Advance(TimeSpan.FromDays(31));
        await world.Factory.CleanFilesAsync(world.Clinic);

        Assert.Equal("removed", Assert.Single(await sql.Main.FilesAsync(world.Clinic)).ErasedFor);
    }

    [Fact]
    public async Task The_builders_kind_exists_on_a_developers_machine_and_nowhere_else()
    {
        using var world = await FileWorld.CreateAsync(sql);
        Assert.True(world.Factory.Services.GetRequiredService<IOptions<ScsOptions>>().Value.IsLocal);

        // Here it is the caller's own: uploaded, attached to their account, listed, removed, restored.
        var id = await world.Browser.UploadedAsync(FileKinds.DevSample, "sample.pdf", Samples.Pdf());
        using (var attached = await world.Browser.PostAsJsonAsync("/v1/dev/files/sample", new { fileIds = new[] { id } }))
        {
            Assert.Equal(204, (int)attached.StatusCode);
        }

        await world.Factory.ScanFilesAsync(world.Clinic);
        using (var listed = await world.Browser.GetAsync(new Uri("/v1/dev/files/sample", UriKind.Relative)))
        {
            var file = Assert.Single((await listed.ReadAsync()).Body.GetProperty("files").EnumerateArray());
            Assert.Equal(("sample.pdf", "available"), (file.GetProperty("name").GetString(), file.GetProperty("state").GetString()));
        }

        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        using (var theirs = await colleague.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null))
        {
            Assert.Equal((403, "PERMISSION_REQUIRED"), ((int)theirs.StatusCode, (await theirs.ReadAsync()).Code));
        }

        using (var notTheirs = await colleague.Browser.PostAsync(new Uri($"/v1/dev/files/sample/{id}/remove", UriKind.Relative), null))
        {
            Assert.Equal(404, (int)notTheirs.StatusCode);
        }

        using (var removed = await world.Browser.PostAsync(new Uri($"/v1/dev/files/sample/{id}/remove", UriKind.Relative), null))
        using (var restored = await world.Browser.PostAsync(new Uri($"/v1/dev/files/sample/{id}/restore", UriKind.Relative), null))
        {
            Assert.Equal((204, 204), ((int)removed.StatusCode, (int)restored.StatusCode));
        }

        // In an environment that is not a developer's machine neither the kind nor its addresses exist.
        using var deployed = sql.CreateApi()
            .With("Scs:ENVIRONMENT", "test")
            .With("Scs:FILES:ROOT", world.Factory.FilesRoot)
            .With("Scs:SCAN:HOST", "scanner.internal")
            .With("Scs:AUTH:STAFF_APP_URL", "https://app.scs.test");
        var routes = deployed.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().Select(e => e.RoutePattern.RawText ?? "").ToList();
        Assert.DoesNotContain(routes, r => r.Contains("/dev/files", StringComparison.Ordinal));
        Assert.Contains("/v1/files", routes);
    }
}

/// <summary>
/// Spec 009, SC-001, SC-002, SC-006 and SC-007: a ten-megabyte file is taken and given in
/// moments, and a backlog left by a scanner that was down is cleared quickly when it returns.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Volume")]
public sealed class FileVolumeTests(SqlServerFixture sql, ITestOutputHelper output)
{
    [Fact]
    public async Task A_ten_megabyte_file_is_taken_in_seconds_and_its_download_starts_at_once()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var content = Samples.Pdf((int)(10 * FileKinds.Megabyte));

        // Once, unmeasured: the first call of a process pays for building its statements.
        await world.CleanFileAsync();

        var upload = Stopwatch.StartNew();
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "big.pdf", content);
        upload.Stop();
        Assert.Null(await world.AttachAsync(TestKinds.Three, new FileRecord("visit", Guid.CreateVersion7()), id));

        var scan = Stopwatch.StartNew();
        await world.Factory.ScanFilesAsync(world.Clinic);
        scan.Stop();

        var download = Stopwatch.StartNew();
        using var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null);
        var url = new Uri((await asked.ReadAsync()).Body.GetProperty("url").GetString()!, UriKind.Relative);
        using var file = await world.Browser.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        download.Stop();
        var got = await file.Content.ReadAsByteArrayAsync();

        output.WriteLine($"Taking a 10 MB file: {upload.ElapsedMilliseconds} ms.");
        output.WriteLine($"Scanning it (test scanner): {scan.ElapsedMilliseconds} ms.");
        output.WriteLine($"Asking for it until the download starts: {download.ElapsedMilliseconds} ms.");

        Assert.Equal(content.Length, got.Length);
        Assert.True(upload.Elapsed < TimeSpan.FromSeconds(15), $"The upload took {upload.ElapsedMilliseconds} ms.");
        Assert.True(download.Elapsed < TimeSpan.FromSeconds(2), $"The download took {download.ElapsedMilliseconds} ms to start.");
    }

    [Fact]
    public async Task A_hundred_files_left_waiting_are_all_scanned_soon_after_the_scanner_returns()
    {
        using var world = await FileWorld.CreateAsync(sql, factory => factory.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        world.Scanner.Answer = ScanAnswer.NoAnswer;
        var record = new FileRecord("archive", Guid.CreateVersion7());

        for (var batch = 0; batch < 5; batch++)
        {
            var ids = new List<Guid>();
            for (var i = 0; i < 20; i++)
            {
                ids.Add(await world.Browser.UploadedAsync(TestKinds.Many, $"f{batch}-{i}.pdf", Samples.Pdf()));
            }

            Assert.Null(await world.AttachAsync(TestKinds.Many, record, [.. ids]));
            world.Factory.Clock.Advance(Uploads.Window);
            using var renewal = await world.Browser.PostAsync(new Uri("/v1/auth/session/renew", UriKind.Relative), null);
        }

        await world.Factory.ScanFilesAsync(world.Clinic);
        Assert.All(await sql.Main.FilesAsync(world.Clinic), f => Assert.Equal("pending", f.Scan));

        world.Scanner.Answer = ScanAnswer.Clean;
        var catchUp = Stopwatch.StartNew();
        await world.Factory.ScanFilesAsync(world.Clinic);
        catchUp.Stop();

        output.WriteLine($"A hundred waiting files scanned in {catchUp.ElapsedMilliseconds} ms once the scanner answered.");
        var rows = await sql.Main.FilesAsync(world.Clinic);
        Assert.Equal(100, rows.Count);
        Assert.All(rows, f => Assert.Equal("clean", f.Scan));
        Assert.True(catchUp.Elapsed < TimeSpan.FromMinutes(5), $"Catching up took {catchUp.ElapsedMilliseconds} ms.");
    }
}

/// <summary>
/// Spec 009, FR-036, SC-003, SC-004 and SC-004a: a file's name, its content and the ways in
/// never reach the operational logs; nothing that is not what it says is taken; and nothing the
/// check refused is given by any address.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FileSecretsTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Neither_a_name_nor_content_nor_a_way_in_reaches_the_logs()
    {
        using var world = await FileWorld.CreateAsync(sql);
        const string Name = "zzq-patient-mariam-xray.pdf";
        const string Marker = "zzq-content-marker-7731";

        var (id, _) = await world.CleanFileAsync(content: Samples.Pdf(marker: Marker), name: Name);
        using var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null);
        var url = (await asked.ReadAsync()).Body.GetProperty("url").GetString()!;
        var token = url.Split('/')[^1];
        using (await world.Browser.GetAsync(new Uri(url, UriKind.Relative))) { }
        using (await world.Browser.UploadAsync(TestKinds.Three, "zzq-refused-program.pdf", Samples.Program())) { }
        await world.Browser.UploadedAsync(TestKinds.Three, "zzq-infected.pdf", Samples.Eicar());
        await world.Factory.ScanFilesAsync(world.Clinic);
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.Three, id)));
        world.Factory.Clock.Advance(TimeSpan.FromDays(31));
        await world.Factory.CleanFilesAsync(world.Clinic);

        var logs = world.Factory.Logs.All;
        Assert.NotEmpty(logs);
        Assert.DoesNotContain("zzq-", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(token, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(token[..40], logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_the_check_refused_is_given_by_any_address()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var visitor = world.Factory.Browser();
        var record = new FileRecord("visit", Guid.CreateVersion7());
        var infected = await world.Browser.UploadedAsync(TestKinds.Three, "a.pdf", Samples.Eicar());
        var locked = await world.Browser.UploadedAsync(TestKinds.Three, "b.pdf", Samples.ProtectedPdf());
        var loose = await world.Browser.UploadedAsync(TestKinds.Three, "c.pdf", Samples.Eicar());
        Assert.Null(await world.AttachAsync(TestKinds.Three, record, infected, locked));
        await world.Factory.ScanFilesAsync(world.Clinic);
        await world.Factory.ScanFilesAsync(world.Clinic);

        foreach (var id in new[] { infected, locked, loose })
        {
            using var asked = await world.Browser.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null);
            using var shown = await visitor.GetAsync(new Uri($"/v1/public/files/{id}", UriKind.Relative));
            Assert.Equal((410, 404), ((int)asked.StatusCode, (int)shown.StatusCode));
            Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));
        }

        Assert.All(await world.ListAsync(record), f => Assert.Equal(FileStates.Refused, f.State));
    }
}
