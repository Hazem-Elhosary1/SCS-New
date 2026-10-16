using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Tenancy;
using Scs.Files.Infrastructure;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Files;

/// <summary>
/// Spec 009, User Stories 2 and 5 (FR-014 to FR-019): nothing is given out before its scan is
/// clean; a file with malware in it, or one the scanner cannot look inside, is refused and its
/// uploader told; and a scanner that is down delays availability and nothing else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ScanTests(SqlServerFixture sql)
{
    private static Uri Access(Guid id) => new($"/v1/files/{id}/access", UriKind.Relative);

    private static async Task<HealthStatus> BacklogAsync(FileWorld world)
    {
        var report = await world.Factory.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Name == ScanBacklogHealthCheck.Name);
        return Assert.Single(report.Entries).Value.Status;
    }

    private async Task<int> SweepAsync(FileWorld world)
    {
        using var scope = world.Factory.Services.CreateScope();
        return await ActivatorUtilities.CreateInstance<ScanSweepJob>(scope.ServiceProvider).RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_clean_file_becomes_available_and_scanning_again_changes_nothing()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.Three, new FileRecord("visit", Guid.CreateVersion7()), id));

        using (var early = await world.Browser.PostAsync(Access(id), null))
        {
            Assert.Equal((409, "FILE_BEING_CHECKED"), ((int)early.StatusCode, (await early.ReadAsync()).Code));
        }

        await world.Factory.ScanFilesAsync(world.Clinic);
        await world.Factory.ScanFilesAsync(world.Clinic);

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(("clean", 1), (row.Scan, row.ScanTries));
        Assert.Equal(1, world.Scanner.Scans);

        using var asked = await world.Browser.PostAsync(Access(id), null);
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_file_with_malware_in_it_is_refused_erased_recorded_and_its_uploader_told(bool attachedFirst)
    {
        using var world = await FileWorld.CreateAsync(sql);
        var record = new FileRecord("visit", Guid.CreateVersion7());
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "نتيجة.pdf", Samples.Eicar());
        if (attachedFirst)
        {
            Assert.Null(await world.AttachAsync(TestKinds.Three, record, id));
        }

        await world.Factory.ScanFilesAsync(world.Clinic);

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(("refused", "infected", "refused"), (row.Scan, row.RefusedFor, row.ErasedFor));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));

        // One entry under security, by the platform, naming the file.
        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.FileScanRefused));
        Assert.Equal(("security", "system", "file", (Guid?)id, "نتيجة.pdf"), (entry.Category, entry.ActorKind, entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Contains("\"reason\":\"infected\"", entry.Facts, StringComparison.Ordinal);

        // It names who uploaded the file, and the record it is on when it is on one (FR-016a).
        Assert.Contains($"\"uploadedBy\":\"{world.Person.AccountId}\"", entry.Facts, StringComparison.Ordinal);
        if (attachedFirst)
        {
            Assert.Contains($"\"record\":\"{record.Kind}\"", entry.Facts, StringComparison.Ordinal);
            Assert.Contains($"\"recordId\":\"{record.Id}\"", entry.Facts, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(record.Id.ToString(), entry.Facts, StringComparison.Ordinal);
        }

        // One notification for the uploader, in the bell only.
        var told = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal((NotificationEvents.FileScanRefused, world.Person.AccountId, "warning"), (told.EventKey, told.RecipientAccountId, told.Mark));
        Assert.Null(told.EmailState);
        using var bell = await world.Browser.GetAsync(new Uri("/v1/notifications", UriKind.Relative));
        var item = Assert.Single((await bell.ReadAsync()).Body.GetProperty("items").EnumerateArray());
        Assert.Equal(("ملف مرفوض", $"نتيجة.pdf ({TestKinds.Three}) · لم يجتز الفحص الأمني"), (item.GetProperty("title").GetString(), item.GetProperty("text").GetString()));

        // Nobody gets it, and it cannot be attached any more.
        using var asked = await world.Browser.PostAsync(Access(id), null);
        var (status, code, body) = await asked.ReadAsync();
        if (attachedFirst)
        {
            Assert.Equal((410, "FILE_REFUSED", "infected"), (status, code, body.GetProperty("reason").GetString()));
            Assert.Equal("refused", Assert.Single(await world.ListAsync(record)).State);
        }
        else
        {
            Assert.Equal((410, "FILE_REFUSED"), (status, code));

            // Saved afterwards, it is on its record as a refused file.
            Assert.Null(await world.AttachAsync(TestKinds.Three, record, id));
            Assert.Equal("refused", Assert.Single(await world.ListAsync(record)).State);
        }

        // Scanning again tells nobody twice.
        await world.Factory.ScanFilesAsync(world.Clinic);
        Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
    }

    [Fact]
    public async Task Two_scans_of_one_clinic_at_once_settle_each_file_once_and_tell_once()
    {
        using var world = await FileWorld.CreateAsync(sql);
        await world.Browser.UploadedAsync(TestKinds.Three, "bad.pdf", Samples.Eicar());
        await world.Browser.UploadedAsync(TestKinds.Three, "locked.pdf", Samples.ProtectedPdf());
        await world.Browser.UploadedAsync(TestKinds.Three, "good.pdf", Samples.Pdf());

        // Each upload asks for the clinic's job, and the sweep does too: runs overlap.
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => world.Factory.ScanFilesAsync(world.Clinic)));

        Assert.Equal(
            ["clean", "refused", "refused"],
            (await sql.Main.FilesAsync(world.Clinic)).Select(f => f.Scan).Order(StringComparer.Ordinal));
        Assert.Equal(2, (await sql.Main.NotificationsAsync(world.Clinic)).Count);
        Assert.Equal(2, (await sql.Main.EntriesAsync(world.Clinic, AuditActions.FileScanRefused)).Count);
    }

    [Fact]
    public async Task A_file_behind_a_password_cannot_be_checked_and_is_refused_with_that_reason()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, record) = await world.CleanFileAsync(content: Samples.ProtectedPdf(), name: "lab.pdf");

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(("refused", "unreadable"), (row.Scan, row.RefusedFor));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));
        Assert.Equal(0, world.Scanner.Scans);

        var told = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal(NotificationEvents.FileUnreadable, told.EventKey);
        Assert.Contains("\"reason\":\"unreadable\"", Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.FileScanRefused)).Facts, StringComparison.Ordinal);

        using var asked = await world.Browser.PostAsync(Access(id), null);
        var (status, code, body) = await asked.ReadAsync();
        Assert.Equal((410, "FILE_REFUSED", "unreadable"), (status, code, body.GetProperty("reason").GetString()));
        Assert.Equal("unreadable", Assert.Single(await world.ListAsync(record)).RefusedFor);
    }

    [Fact]
    public async Task What_the_scanner_itself_cannot_read_is_refused_the_same_way()
    {
        using var world = await FileWorld.CreateAsync(sql);
        world.Scanner.Answer = ScanAnswer.Unreadable;

        var (id, _) = await world.CleanFileAsync(content: Samples.Png(), name: "broken.png");

        Assert.Equal("unreadable", Assert.Single(await sql.Main.FilesAsync(world.Clinic)).RefusedFor);
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));
    }

    [Fact]
    public async Task An_uploader_who_can_no_longer_sign_in_is_not_told_and_the_entry_is_still_written()
    {
        using var world = await FileWorld.CreateAsync(sql);
        await world.Browser.UploadedAsync(TestKinds.Three, "bad.pdf", Samples.Eicar());
        await world.World.ExecuteAsync($"UPDATE [identity].StaffAccount SET State = 'disabled' WHERE Id = '{world.Person.AccountId}'");

        await world.Factory.ScanFilesAsync(world.Clinic);

        Assert.Empty(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.FileScanRefused));
    }

    [Fact]
    public async Task A_scanner_that_gives_no_answer_stops_nothing_but_the_download()
    {
        using var world = await FileWorld.CreateAsync(sql);
        world.Scanner.Answer = ScanAnswer.NoAnswer;
        var record = new FileRecord("visit", Guid.CreateVersion7());

        var id = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.Three, record, id));
        await world.Factory.ScanFilesAsync(world.Clinic);
        await world.Factory.ScanFilesAsync(world.Clinic);

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal(("pending", 2), (row.Scan, row.ScanTries));
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, id));
        using (var asked = await world.Browser.PostAsync(Access(id), null))
        {
            Assert.Equal((409, "FILE_BEING_CHECKED"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));
        }

        Assert.Equal("being-checked", Assert.Single(await world.ListAsync(record)).State);

        // The scanner returns: the next run scans it, with nobody asking.
        world.Scanner.Answer = ScanAnswer.Clean;
        await world.Factory.ScanFilesAsync(world.Clinic);
        Assert.Equal("clean", Assert.Single(await sql.Main.FilesAsync(world.Clinic)).Scan);
    }

    [Fact]
    public async Task The_sweep_finds_the_clinics_with_files_waiting_and_says_when_they_wait_too_long()
    {
        using var world = await FileWorld.CreateAsync(sql);
        world.Scanner.Answer = ScanAnswer.NoAnswer;
        await world.Browser.UploadedAsync(TestKinds.Three, "secret-name.pdf", Samples.Pdf());
        await world.Factory.ScanFilesAsync(world.Clinic);

        // What the sweep reads across clinics: ids and nothing else.
        var builder = new DbContextOptionsBuilder<Scs.Files.Infrastructure.Persistence.FilesDbContext>();
        world.Factory.Services.GetRequiredService<ICrossClinicConnectionFactory>().Configure(builder);
        await using (var across = new Scs.Files.Infrastructure.Persistence.FilesDbContext(builder.Options, new ClinicContext()))
        {
            var waiting = await across.Files.IgnoreQueryFilters().Where(f => f.Scan == "pending").Select(f => f.ClinicId).Distinct().ToListAsync();
            Assert.Contains(world.Clinic, waiting);
        }

        Assert.DoesNotContain("more than an hour", world.Factory.Logs.All, StringComparison.Ordinal);
        var lateBefore = await SweepAsync(world);
        Assert.Equal(lateBefore == 0 ? HealthStatus.Healthy : HealthStatus.Degraded, await BacklogAsync(world));

        world.Factory.Clock.Advance(ScanSweepJob.TooLong + TimeSpan.FromMinutes(1));
        var late = await SweepAsync(world);
        Assert.True(late > lateBefore);

        var line = Assert.Single(world.Factory.Logs.Lines, l => l.Contains("more than an hour", StringComparison.Ordinal));
        Assert.DoesNotContain("secret-name", world.Factory.Logs.All, StringComparison.Ordinal);

        // The check reads degraded, and the platform is still ready to serve.
        Assert.Equal(HealthStatus.Degraded, await BacklogAsync(world));
        using var ready = await world.Browser.GetAsync(new Uri("/health/ready", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        // The scanner returns, the files are scanned, and the next sweep finds nothing late.
        world.Scanner.Answer = ScanAnswer.Clean;
        await world.Factory.ScanFilesAsync(world.Clinic);
        var lateAfter = await SweepAsync(world);
        Assert.True(lateAfter < late);
        Assert.Equal(lateAfter == 0 ? HealthStatus.Healthy : HealthStatus.Degraded, await BacklogAsync(world));
    }
}

/// <summary>
/// Spec 009, User Story 3 (FR-020 to FR-028c): a private file is given only to someone its
/// owner says may see its record, asked every time, through a way in that is that account's
/// alone and stops after five minutes; and each way in given is a line in the audit trail.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccessTests(SqlServerFixture sql)
{
    private static Uri Access(Guid id) => new($"/v1/files/{id}/access", UriKind.Relative);

    private static async Task<Uri> WayInAsync(HttpClient browser, Guid id)
    {
        using var asked = await browser.PostAsync(Access(id), null);
        var (status, code, body) = await asked.ReadAsync();
        Assert.True(status == 200, $"Asking answered {status} {code}.");
        return new Uri(body.GetProperty("url").GetString()!, UriKind.Relative);
    }

    [Fact]
    public async Task Whoever_may_see_the_record_downloads_the_file_as_it_was_uploaded_under_its_name()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var content = Samples.Jpeg(withExif: true);
        var (id, record) = await world.CleanFileAsync(content: content, name: "أشعة الصدر.jpg");

        using var asked = await colleague.Browser.PostAsync(Access(id), null);
        var (status, _, body) = await asked.ReadAsync();
        Assert.Equal(200, status);
        Assert.Equal(("أشعة الصدر.jpg", (long)content.Length, "jpeg"), (
            body.GetProperty("name").GetString(), body.GetProperty("sizeBytes").GetInt64(), body.GetProperty("type").GetString()));
        var expires = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expires - world.Factory.Clock.GetUtcNow(), FileDownloads.WayInLasts - TimeSpan.FromSeconds(10), FileDownloads.WayInLasts);

        Assert.StartsWith("/v1/files/content/", body.GetProperty("url").GetString(), StringComparison.Ordinal);
        using var file = await colleague.Browser.GetAsync(new Uri(body.GetProperty("url").GetString()!, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);

        // A private file is given byte for byte, hidden details and all (FR-028b).
        Assert.Equal(content, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("أشعة الصدر.jpg", file.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.StartsWith("sandbox", file.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Contains("no-store", file.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

        // The owner was asked about that record.
        Assert.Contains(record, world.Owner.AskedToSee);
    }

    [Fact]
    public async Task The_owner_is_asked_every_time_and_a_no_refuses()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, _) = await world.CleanFileAsync();

        await WayInAsync(world.Browser, id);
        world.Owner.MaySee = false;

        using var refused = await world.Browser.PostAsync(Access(id), null);
        Assert.Equal((403, "PERMISSION_REQUIRED"), ((int)refused.StatusCode, (await refused.ReadAsync()).Code));
        Assert.Equal(2, world.Owner.AskedToSee.Count);
    }

    [Fact]
    public async Task Another_clinic_no_session_and_no_such_file_get_nothing()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var elsewhere = await sql.Main.SeedClinicAsync();
        using var stranger = await world.Factory.SignInWithAsync(elsewhere, Permissions.BookingsView);
        using var nobody = world.Factory.Browser();
        var (id, _) = await world.CleanFileAsync();
        var wayIn = await WayInAsync(world.Browser, id);

        foreach (var target in new[] { id, Guid.CreateVersion7() })
        {
            using var asked = await stranger.Browser.PostAsync(Access(target), null);
            Assert.Equal((404, "NOT_FOUND"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));
        }

        using (var unsigned = await nobody.PostAsync(Access(id), null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        }

        // The way in itself, in other hands.
        using var signedOut = await nobody.GetAsync(wayIn);
        using var otherClinic = await stranger.Browser.GetAsync(wayIn);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherClinic.StatusCode);
    }

    [Fact]
    public async Task A_way_in_is_one_accounts_works_again_within_its_time_and_then_stops()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var (id, _) = await world.CleanFileAsync();
        var wayIn = await WayInAsync(world.Browser, id);

        using (var first = await world.Browser.GetAsync(wayIn))
        using (var second = await world.Browser.GetAsync(wayIn))
        using (var theirs = await colleague.Browser.GetAsync(wayIn))
        using (var altered = await world.Browser.GetAsync(new Uri(wayIn.OriginalString[..^3] + "AAA", UriKind.Relative)))
        {
            Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
            Assert.Equal((404, "NOT_FOUND"), ((int)theirs.StatusCode, (await theirs.ReadAsync()).Code));
            Assert.Equal(HttpStatusCode.NotFound, altered.StatusCode);
        }

        // One entry for the way in, however often it was used; none for a refusal.
        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.FileDownloaded));
        Assert.Equal(("records", world.Person.AccountId, "file", (Guid?)id, "scan.pdf"), (
            entry.Category, entry.ActorAccountId!.Value, entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Contains($"\"kind\":\"{TestKinds.Three}\"", entry.Facts, StringComparison.Ordinal);
        Assert.Contains("\"record\":\"visit\"", entry.Facts, StringComparison.Ordinal);

        // Four minutes on it still works; past five it does not.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(4));
        using (var late = await world.Browser.GetAsync(wayIn))
        {
            Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        }
    }

    [Fact]
    public async Task A_way_in_that_has_expired_or_whose_file_was_removed_gives_nothing()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var (id, _) = await world.CleanFileAsync();
        var (removed, _) = await world.CleanFileAsync();
        var wayIn = await WayInAsync(world.Browser, id);
        var toRemoved = await WayInAsync(world.Browser, removed);
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.Three, removed)));

        using (var gone = await world.Browser.GetAsync(toRemoved))
        {
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }

        // The session lasts hours; the way in lasts five minutes. A session renewed past that
        // still cannot use the old way in.
        using var renewed = await RenewedAsync(world, TimeSpan.FromMinutes(4));
        using var stillGood = await renewed.GetAsync(wayIn);
        Assert.Equal(HttpStatusCode.OK, stillGood.StatusCode);

        using var later = await RenewedAsync(world, TimeSpan.FromMinutes(2));
        using var expired = await later.GetAsync(wayIn);
        Assert.Equal((404, "NOT_FOUND"), ((int)expired.StatusCode, (await expired.ReadAsync()).Code));
    }

    /// <summary>Moves the clock and renews the person's session, as the console does by itself.</summary>
    private static async Task<HttpClient> RenewedAsync(FileWorld world, TimeSpan after)
    {
        world.Factory.Clock.Advance(after);
        using var renewal = await world.Browser.PostAsync(new Uri("/v1/auth/session/renew", UriKind.Relative), null);
        Assert.True(renewal.IsSuccessStatusCode, $"Renewal answered {(int)renewal.StatusCode}.");
        return world.Browser;
    }
}

/// <summary>
/// Spec 009, FR-027 and FR-028b: a file of a public kind is shown to anyone, with what it says
/// of where and with what it was taken removed; nothing else is ever shown without a session.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PublicFileTests(SqlServerFixture sql)
{
    private static Uri Public(Guid id) => new($"/v1/public/files/{id}", UriKind.Relative);

    [Fact]
    public async Task A_public_image_is_shown_to_a_visitor_without_its_hidden_details()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var visitor = world.Factory.Browser();
        var photo = Samples.Jpeg(withExif: true);
        var (id, _) = await world.CleanFileAsync(TestKinds.PublicImage, photo, "doctor.jpg");

        using var shown = await visitor.GetAsync(Public(id));
        var bytes = await shown.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Equal("image/jpeg", shown.Content.Headers.ContentType!.MediaType);
        Assert.True(shown.Headers.CacheControl!.Public);

        // The picture is the same; where and with what it was taken is gone.
        var text = System.Text.Encoding.ASCII.GetString(bytes);
        Assert.DoesNotContain("GPS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("xmpmeta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("a comment", text, StringComparison.Ordinal);
        Assert.Equal(Samples.Jpeg(withExif: false), bytes);
        Assert.Equal(bytes.Length, Assert.Single(await sql.Main.FilesAsync(world.Clinic)).SizeBytes);
    }

    [Fact]
    public async Task A_png_loses_its_text_time_and_exif_chunks()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var visitor = world.Factory.Browser();
        var (id, _) = await world.CleanFileAsync(TestKinds.PublicImage, Samples.Png(withText: true), "logo.png");

        using var shown = await visitor.GetAsync(Public(id));

        Assert.Equal(Samples.Png(withText: false), await shown.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Nothing_private_unattached_unscanned_refused_or_removed_is_ever_shown_without_a_session()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var visitor = world.Factory.Browser();

        var (privateFile, _) = await world.CleanFileAsync();
        var unattached = await world.Browser.UploadedAsync(TestKinds.PublicImage, "a.png", Samples.Png());
        var unscanned = await world.Browser.UploadedAsync(TestKinds.PublicImage, "b.png", Samples.Png());
        Assert.Null(await world.AttachAsync(TestKinds.PublicImage, new FileRecord("doctor", Guid.CreateVersion7()), unscanned));
        var (removed, _) = await world.CleanFileAsync(TestKinds.PublicImage, Samples.Png(), "c.png");
        Assert.Null(await world.AsAsync(world.Person.AccountId, db => db.RemoveFile(TestKinds.PublicImage, removed)));

        // The clean-file helper scanned everything waiting; make one wait again.
        await world.World.ExecuteAsync($"UPDATE [files].StoredFile SET Scan = 'pending' WHERE Id = '{unscanned}'");

        foreach (var id in new[] { privateFile, unattached, unscanned, removed, Guid.CreateVersion7() })
        {
            using var shown = await visitor.GetAsync(Public(id));
            Assert.Equal((404, "NOT_FOUND"), ((int)shown.StatusCode, (await shown.ReadAsync()).Code));
        }
    }
}
