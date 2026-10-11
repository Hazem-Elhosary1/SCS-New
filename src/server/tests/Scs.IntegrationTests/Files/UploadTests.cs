using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Security;
using Scs.Files.Contracts;
using Scs.Files.Infrastructure;
using Scs.Identity.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Files;

namespace Scs.IntegrationTests.Files;

/// <summary>A clinic with the test kinds, an owner and a scanner the test steers, and one person signed in.</summary>
internal sealed class FileWorld(RefusalWorld world, OwnerAnswers answers, FakeScanner scanner, SignedInStaff person) : IDisposable
{
    public RefusalWorld World { get; } = world;

    public ApiFactory Factory => World.Factory;

    public Guid Clinic => World.Clinic;

    public OwnerAnswers Owner { get; } = answers;

    public FakeScanner Scanner { get; } = scanner;

    public SignedInStaff Person { get; } = person;

    public HttpClient Browser => Person.Browser;

    public static async Task<FileWorld> CreateAsync(SqlServerFixture sql, Action<ApiFactory>? configure = null)
    {
        var answers = new OwnerAnswers();
        var scanner = new FakeScanner();
        var world = await RefusalWorld.CreateAsync(sql, factory =>
        {
            factory.WithTestFiles(answers, scanner);
            configure?.Invoke(factory);
        });
        var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        return new FileWorld(world, answers, scanner, person);
    }

    /// <summary>Saves a record with these files the way a module does: on its own context, in its own save.</summary>
    public Task<Exception?> AttachAsync(string kind, FileRecord record, params Guid[] fileIds) =>
        AsAsync(Person.AccountId, db => db.AttachFiles(kind, record, fileIds));

    public Task<Exception?> AsAsync(Guid accountId, Action<IdentityDbContext> change) =>
        Factory.AsStaffAsync<Exception?>(Clinic, accountId, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            change(db);
            return await Record.ExceptionAsync(() => db.SaveChangesAsync());
        });

    /// <summary>Uploads, attaches to a new record and scans: a clean file on a record.</summary>
    public async Task<(Guid FileId, FileRecord Record)> CleanFileAsync(string kind = TestKinds.Three, byte[]? content = null, string name = "scan.pdf")
    {
        var record = new FileRecord("visit", Guid.CreateVersion7());
        var id = await Browser.UploadedAsync(kind, name, content ?? Samples.Pdf());
        Assert.Null(await AttachAsync(kind, record, id));
        await Factory.ScanFilesAsync(Clinic);
        return (id, record);
    }

    public Task<IReadOnlyList<RecordFileView>> ListAsync(FileRecord record, bool includeRemoved = false) =>
        Factory.ForClinicAsync(Clinic, services =>
            services.GetRequiredService<IRecordFiles>().ListAsync(record.Kind, record.Id, includeRemoved, CancellationToken.None));

    public void Dispose()
    {
        Person.Dispose();
        World.Dispose();
    }
}

/// <summary>
/// Spec 009, User Stories 1 and 2 (FR-004 to FR-013, FR-012a): taking a file in. Who may, what
/// is checked by the platform itself whatever the browser did, and that an uploaded file is its
/// uploader's alone.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UploadTests(SqlServerFixture sql)
{
    [Fact]
    public async Task With_no_session_nothing_is_taken()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var stranger = world.Factory.Browser();

        using var response = await stranger.UploadAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());

        Assert.Equal((401, "UNAUTHENTICATED"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    [Fact]
    public async Task An_accepted_file_is_stored_as_it_is_and_is_its_uploaders_alone()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var content = Samples.Pdf(2000);

        using var response = await world.Browser.UploadAsync(TestKinds.Three, "تقرير المعمل.pdf", content, browserSays: "image/png");
        var (status, _, body) = await response.ReadAsync();

        Assert.Equal(201, status);
        Assert.Equal(("تقرير المعمل.pdf", 2000L, "pdf", "being-checked"), (
            body.GetProperty("name").GetString(), body.GetProperty("sizeBytes").GetInt64(),
            body.GetProperty("type").GetString(), body.GetProperty("state").GetString()));

        var row = Assert.Single(await sql.Main.FilesAsync(world.Clinic));
        Assert.Equal((body.GetProperty("id").GetGuid(), TestKinds.Three, world.Person.AccountId, "pending"), (row.Id, row.Kind, row.UploadedBy, row.Scan));
        Assert.Null(row.RecordId);

        // The type is the one read from the content, not the one the browser said.
        Assert.Equal("pdf", row.Type);

        // Byte for byte, under the clinic and the id — never under the name.
        Assert.Equal(content, await world.Factory.StoredBytesAsync(world.Clinic, row.Id));
        Assert.Empty(Directory.EnumerateFiles(world.Factory.FilesRoot, "*.pdf", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task An_unattached_file_does_not_exist_for_a_colleague_or_another_clinic()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var elsewhere = await sql.Main.SeedClinicAsync();
        using var stranger = await world.Factory.SignInWithAsync(elsewhere, Permissions.BookingsView);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());
        await world.Factory.ScanFilesAsync(world.Clinic);

        foreach (var other in new[] { colleague.Browser, stranger.Browser })
        {
            using var asked = await other.PostAsync(new Uri($"/v1/files/{id}/access", UriKind.Relative), null);
            Assert.Equal((404, "NOT_FOUND"), ((int)asked.StatusCode, (await asked.ReadAsync()).Code));

            using var discarded = await other.DeleteAsync(new Uri($"/v1/files/{id}", UriKind.Relative));
            Assert.True(await world.Factory.StoreHasAsync(world.Clinic, id));
        }

        // A colleague cannot attach it either, and the save that tried fails whole.
        var refused = await world.AsAsync(colleague.AccountId, db => db.AttachFiles(TestKinds.Three, new FileRecord("visit", Guid.CreateVersion7()), [id]));
        Assert.Equal("FILE_NOT_ATTACHABLE", Assert.IsType<FileRefusedException>(refused).Code);
        Assert.Null(Assert.Single(await sql.Main.FilesAsync(world.Clinic)).RecordId);
    }

    [Theory]
    [InlineData("no.such-kind")]
    [InlineData(TestKinds.NoOwner)]
    [InlineData("")]
    public async Task A_kind_that_is_unknown_or_has_no_owner_is_refused_for_everyone(string kind)
    {
        using var world = await FileWorld.CreateAsync(sql);

        using var response = await world.Browser.UploadAsync(kind, "scan.pdf", Samples.Pdf());

        Assert.Equal((403, "FORBIDDEN"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    [Fact]
    public async Task The_real_kinds_of_the_BRD_have_no_owner_yet_and_are_refused()
    {
        using var world = await FileWorld.CreateAsync(sql);

        foreach (var kind in FileKinds.All.Where(k => !k.LocalOnly))
        {
            using var response = await world.Browser.UploadAsync(kind.Key, "scan.png", Samples.Png());
            Assert.Equal((403, "FORBIDDEN"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        }
    }

    [Fact]
    public async Task The_owners_no_refuses_the_upload()
    {
        using var world = await FileWorld.CreateAsync(sql);
        world.Owner.MayUpload = false;

        using var response = await world.Browser.UploadAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());

        Assert.Equal((403, "PERMISSION_REQUIRED"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    [Fact]
    public async Task One_file_at_a_time_and_it_must_be_there()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var address = new Uri($"/v1/files?kind={TestKinds.Three}", UriKind.Relative);

        using var none = new MultipartFormDataContent { { new StringContent("x"), "note" } };
        using var two = new MultipartFormDataContent
        {
            { new ByteArrayContent(Samples.Pdf()), "file", "a.pdf" },
            { new ByteArrayContent(Samples.Pdf()), "file", "b.pdf" },
        };

        foreach (var form in new[] { none, two })
        {
            using var response = await world.Browser.PostAsync(address, form);
            Assert.Equal((400, "VALIDATION_FAILED"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        }

        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    public static TheoryData<string, byte[]> NotWhatTheyClaim => new()
    {
        { "program.pdf", Samples.Program() },
        { "archive.pdf", Samples.Zip() },
        { "script.png", Samples.Script() },
        { "page.jpg", Samples.Html() },
        { "drawing.png", Samples.Svg() },
        { "moving.png", Samples.Gif() },
        { "photo.jpg", Samples.WebP() },
    };

    [Theory]
    [MemberData(nameof(NotWhatTheyClaim))]
    public async Task Content_that_is_not_one_of_the_kinds_types_is_refused_whatever_its_name(string name, byte[] content)
    {
        using var world = await FileWorld.CreateAsync(sql);

        using var response = await world.Browser.UploadAsync(TestKinds.Three, name, content, browserSays: "application/pdf");
        var (status, code, body) = await response.ReadAsync();

        Assert.Equal((400, "FILE_TYPE_NOT_ACCEPTED"), (status, code));
        Assert.Equal(["pdf", "png", "jpeg"], body.GetProperty("accepted").EnumerateArray().Select(t => t.GetString()));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    [Fact]
    public async Task A_known_type_the_kind_does_not_take_is_refused_too()
    {
        using var world = await FileWorld.CreateAsync(sql);

        using var response = await world.Browser.UploadAsync(TestKinds.OnePdf, "photo.pdf", Samples.Png());
        var (status, code, body) = await response.ReadAsync();

        Assert.Equal((400, "FILE_TYPE_NOT_ACCEPTED"), (status, code));
        Assert.Equal(["pdf"], body.GetProperty("accepted").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task An_empty_file_and_one_over_the_size_are_refused_and_one_exactly_at_it_is_taken()
    {
        using var world = await FileWorld.CreateAsync(sql);
        const int Limit = (int)FileKinds.Megabyte;

        using var empty = await world.Browser.UploadAsync(TestKinds.OnePdf, "empty.pdf", []);
        Assert.Equal((400, "FILE_EMPTY"), ((int)empty.StatusCode, (await empty.ReadAsync()).Code));

        using var over = await world.Browser.UploadAsync(TestKinds.OnePdf, "big.pdf", Samples.Pdf(Limit + 1));
        var (status, code, body) = await over.ReadAsync();
        Assert.Equal((413, "FILE_TOO_LARGE"), (status, code));
        Assert.Equal((Limit + 1L, (long)Limit), (body.GetProperty("sizeBytes").GetInt64(), body.GetProperty("maxBytes").GetInt64()));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
        Assert.False(Directory.Exists(world.Factory.FilesRoot) && Directory.EnumerateFiles(world.Factory.FilesRoot, "*", SearchOption.AllDirectories).Any());

        using var exact = await world.Browser.UploadAsync(TestKinds.OnePdf, "exact.pdf", Samples.Pdf(Limit));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
    }

    [Fact]
    public async Task A_store_that_fails_keeps_nothing_and_says_to_try_again()
    {
        using var world = await FileWorld.CreateAsync(sql);
        world.Factory.Services.GetRequiredService<FailingStore>().FailPuts = true;

        using var response = await world.Browser.UploadAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());

        Assert.Equal((503, "FILE_NOT_STORED"), ((int)response.StatusCode, (await response.ReadAsync()).Code));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
    }

    [Theory]
    [InlineData("C:\\Users\\sara\\Desktop\\scan.pdf", "scan.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("a\u0001b\u0000c.pdf", "abc.pdf")]
    public async Task A_name_is_kept_for_showing_without_its_path_and_never_decides_where_the_file_is(string given, string kept)
    {
        Assert.Equal(kept, Uploads.NameOf(given));
        Assert.Equal(255, Uploads.NameOf(new string('ن', 600) + ".pdf").Length);
        Assert.Equal("file", Uploads.NameOf("///"));

        // A browser cannot even send a name with a control character in it; the rest go through the address.
        if (given.Any(char.IsControl))
        {
            return;
        }

        using var world = await FileWorld.CreateAsync(sql);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, given, Samples.Pdf());

        Assert.Equal(kept, Assert.Single(await sql.Main.FilesAsync(world.Clinic)).Name);
        var stored = Assert.Single(Directory.EnumerateFiles(world.Factory.FilesRoot, "*", SearchOption.AllDirectories));
        Assert.Equal(id.ToString("N"), Path.GetFileName(stored));
        Assert.StartsWith(Path.GetFullPath(world.Factory.FilesRoot), Path.GetFullPath(stored), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Two_files_with_one_name_are_two_files()
    {
        using var world = await FileWorld.CreateAsync(sql);
        var content = Samples.Pdf();

        var first = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", content);
        var second = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", content);

        Assert.NotEqual(first, second);
        Assert.Equal(2, (await sql.Main.FilesAsync(world.Clinic)).Count);
    }

    [Fact]
    public async Task The_uploader_can_discard_what_they_have_not_attached_and_nobody_elses()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var id = await world.Browser.UploadedAsync(TestKinds.Three, "scan.pdf", Samples.Pdf());
        var address = new Uri($"/v1/files/{id}", UriKind.Relative);

        using (var theirs = await colleague.Browser.DeleteAsync(address))
        {
            Assert.Equal((404, "NOT_FOUND"), ((int)theirs.StatusCode, (await theirs.ReadAsync()).Code));
        }

        using var gone = await world.Browser.DeleteAsync(address);
        using var again = await world.Browser.DeleteAsync(address);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (gone.StatusCode, again.StatusCode));
        Assert.Empty(await sql.Main.FilesAsync(world.Clinic));
        Assert.False(await world.Factory.StoreHasAsync(world.Clinic, id));

        // An attached file is not discarded this way.
        var (attached, _) = await world.CleanFileAsync();
        using var kept = await world.Browser.DeleteAsync(new Uri($"/v1/files/{attached}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, kept.StatusCode);
        Assert.True(await world.Factory.StoreHasAsync(world.Clinic, attached));
    }

    [Fact]
    public async Task One_person_cannot_leave_more_than_thirty_files_waiting_nor_send_more_than_sixty_in_ten_minutes()
    {
        using var world = await FileWorld.CreateAsync(sql);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var record = new FileRecord("visit", Guid.CreateVersion7());
        var ids = new List<Guid>();

        for (var i = 0; i < Uploads.MaxWaiting; i++)
        {
            ids.Add(await world.Browser.UploadedAsync(TestKinds.Many, $"f{i}.pdf", Samples.Pdf()));
        }

        using (var refused = await world.Browser.UploadAsync(TestKinds.Many, "one-more.pdf", Samples.Pdf()))
        {
            var (status, code, body) = await refused.ReadAsync();
            Assert.Equal((429, "FILE_LIMIT_REACHED"), (status, code));
            Assert.True(body.GetProperty("retryAfterSeconds").GetInt32() > 0);
        }

        Assert.Equal(Uploads.MaxWaiting, (await sql.Main.FilesAsync(world.Clinic)).Count);

        // A colleague is not held back by it, and attaching frees the first limit.
        await colleague.Browser.UploadedAsync(TestKinds.Many, "theirs.pdf", Samples.Pdf());
        Assert.Null(await world.AttachAsync(TestKinds.Many, record, [.. ids]));

        for (var i = Uploads.MaxWaiting; i < Uploads.MaxInWindow; i++)
        {
            ids.Add(await world.Browser.UploadedAsync(TestKinds.Many, $"f{i}.pdf", Samples.Pdf()));
            if (ids.Count == 40)
            {
                Assert.Null(await world.AttachAsync(TestKinds.Many, record, [.. ids.Skip(30)]));
            }
        }

        using (var tooFast = await world.Browser.UploadAsync(TestKinds.Many, "sixty-one.pdf", Samples.Pdf()))
        {
            Assert.Equal((429, "FILE_LIMIT_REACHED"), ((int)tooFast.StatusCode, (await tooFast.ReadAsync()).Code));
        }

        // Ten minutes on, with the session renewed as the console does, the person uploads again.
        world.Factory.Clock.Advance(Uploads.Window + TimeSpan.FromSeconds(1));
        using var renewal = await world.Browser.PostAsync(new Uri("/v1/auth/session/renew", UriKind.Relative), null);
        Assert.True(renewal.IsSuccessStatusCode, $"Renewal answered {(int)renewal.StatusCode}.");
        Assert.Null(await world.AttachAsync(TestKinds.Many, record, [.. ids.Skip(40)]));
        using var fresh = await world.Browser.UploadAsync(TestKinds.Many, "later.pdf", Samples.Pdf());
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
    }

    [Fact]
    public async Task The_addresses_of_the_engine_say_who_may_call_them_and_what_they_record()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? "").Contains("/files", StringComparison.Ordinal))
            .ToDictionary(e => $"{e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods[0]} {e.RoutePattern.RawText}");

        Assert.Equal(
            [
                "DELETE /v1/files/{id:guid}", "GET /v1/dev/files/sample", "GET /v1/files/content/{token}",
                "GET /v1/public/files/{id:guid}", "POST /v1/dev/files/sample", "POST /v1/dev/files/sample/{id:guid}/remove",
                "POST /v1/dev/files/sample/{id:guid}/restore", "POST /v1/files", "POST /v1/files/{id:guid}/access",
            ],
            routes.Keys.Order(StringComparer.Ordinal));

        Assert.Equal(
            AuditDeclarationKind.Records,
            routes["POST /v1/files/{id:guid}/access"].Metadata.GetMetadata<AuditDeclarationMetadata>()!.Kind);
        Assert.Equal(
            AuditDeclarationKind.RecordsNothing,
            routes["POST /v1/files"].Metadata.GetMetadata<AuditDeclarationMetadata>()!.Kind);
    }
}
