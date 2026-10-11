using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.Audit.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Stories 6 and 7 (FR-030 to FR-038): the clinic's reading of its trail — the
/// period, the facets, the counts, the pages and the words — and the operators' reading as a
/// contract. There is no screen yet; this is asked the way spec 059's page will ask it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditReadingTests(SqlServerFixture sql)
{
    /// <summary>A clinic, a signed-in reader who holds the permission, and "now".</summary>
    private sealed record Reader(RefusalWorld World, SignedInStaff Staff, DateTimeOffset Now) : IDisposable
    {
        public Guid Clinic => World.Clinic;

        public async Task<(int Status, string? Code, JsonElement Body)> AskAsync(string query = "", string language = "ar")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/v1/audit/entries{query}", UriKind.Relative));
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            using var response = await Staff.Browser.SendAsync(request);
            return await response.ReadAsync();
        }

        /// <summary>The entries that are not the reader's own sign-in.</summary>
        public async Task<List<JsonElement>> EntriesAsync(string query = "", string language = "ar")
        {
            var (status, _, body) = await AskAsync(query, language);
            Assert.Equal(200, status);
            return [.. body.GetProperty("entries").EnumerateArray()];
        }

        public void Dispose()
        {
            Staff.Dispose();
            World.Dispose();
        }
    }

    private async Task<Reader> ReaderAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql);
        var staff = await world.Factory.SignInWithAsync(world.Clinic, Permissions.ReportsActivity);
        return new Reader(world, staff, world.Factory.Clock.GetUtcNow());
    }

    private static string Iso(DateTimeOffset moment) =>
        Uri.EscapeDataString(moment.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture));

    private static List<Guid> Ids(IEnumerable<JsonElement> entries) => [.. entries.Select(e => e.GetProperty("id").GetGuid())];

    [Fact]
    public async Task Entries_come_newest_first()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        var oldest = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-3), actor: actor, actorName: "a@x.example");
        var newest = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-1), actor: actor, actorName: "a@x.example");
        var middle = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-2), actor: actor, actorName: "a@x.example");

        var entries = await reader.EntriesAsync($"?actor={actor}");

        Assert.Equal([newest, middle, oldest], Ids(entries));
    }

    [Fact]
    public async Task With_no_period_asked_the_last_ninety_days_are_read()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        var inside = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-89), actor: actor, actorName: "a@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-91), actor: actor, actorName: "a@x.example");

        var (status, _, body) = await reader.AskAsync($"?actor={actor}");

        Assert.Equal(200, status);
        Assert.Equal([inside], Ids(body.GetProperty("entries").EnumerateArray()));
        Assert.Equal(1, body.GetProperty("total").GetInt32());

        var from = body.GetProperty("from").GetDateTimeOffset();
        var to = body.GetProperty("to").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromDays(90), to - from);
        Assert.InRange(to, reader.Now.AddSeconds(-1), reader.Now.AddMinutes(1));
    }

    [Fact]
    public async Task A_period_asked_for_is_the_period_read()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        var old = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-200), actor: actor, actorName: "a@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-10), actor: actor, actorName: "a@x.example");

        var entries = await reader.EntriesAsync($"?actor={actor}&from={Iso(reader.Now.AddDays(-210))}&to={Iso(reader.Now.AddDays(-190))}");

        Assert.Equal([old], Ids(entries));
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_or_is_longer_than_a_year_is_refused()
    {
        using var reader = await ReaderAsync();

        var backwards = await reader.AskAsync($"?from={Iso(reader.Now)}&to={Iso(reader.Now.AddDays(-1))}");
        var tooLong = await reader.AskAsync($"?from={Iso(reader.Now.AddDays(-367))}&to={Iso(reader.Now)}");
        var aYear = await reader.AskAsync($"?from={Iso(reader.Now.AddDays(-366))}&to={Iso(reader.Now)}");
        var nonsense = await reader.AskAsync("?from=yesterday");

        Assert.Equal((400, "VALIDATION_FAILED"), (backwards.Status, backwards.Code));
        Assert.Equal("INVALID", backwards.Body.GetProperty("errors").GetProperty("period")[0].GetString());
        Assert.Equal((400, "VALIDATION_FAILED"), (tooLong.Status, tooLong.Code));
        Assert.Equal("TOO_LONG", tooLong.Body.GetProperty("errors").GetProperty("period")[0].GetString());
        Assert.Equal(200, aYear.Status);
        Assert.Equal((400, "INVALID"), (nonsense.Status, nonsense.Body.GetProperty("errors").GetProperty("period")[0].GetString()));
    }

    [Fact]
    public async Task The_category_the_actor_and_the_action_each_narrow()
    {
        using var reader = await ReaderAsync();
        var sara = Guid.NewGuid();
        var omar = Guid.NewGuid();
        var signedIn = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.SignInSucceeded, reader.Now.AddHours(-3), actor: sara, actorName: "sara@x.example");
        var roleBySara = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-2), actor: sara, actorName: "sara@x.example");
        var roleByOmar = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-1), actor: omar, actorName: "omar@x.example");

        Assert.Equal([roleBySara, signedIn], Ids(await reader.EntriesAsync($"?actor={sara}")));
        Assert.Equal([roleByOmar, roleBySara], Ids(await reader.EntriesAsync("?action=role.deleted")));
        Assert.Equal([roleBySara], Ids(await reader.EntriesAsync($"?category=users&actor={sara}")));
        Assert.Equal([signedIn], Ids(await reader.EntriesAsync($"?category=security&actor={sara}")));
        Assert.Empty(await reader.EntriesAsync($"?category=changes&actor={sara}"));
    }

    [Theory]
    [InlineData("?category=everything", "category")]
    [InlineData("?action=refund.issued", "action")]
    [InlineData("?actor=sara", "actor")]
    [InlineData("?page=0", "page")]
    [InlineData("?page=first", "page")]
    [InlineData("?size=0", "size")]
    [InlineData("?size=101", "size")]
    public async Task A_facet_that_does_not_exist_is_refused_by_its_name(string query, string field)
    {
        using var reader = await ReaderAsync();

        var (status, code, body) = await reader.AskAsync(query);

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("INVALID", body.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Fact]
    public async Task The_search_matches_the_actors_name_and_the_actions_name_as_the_reader_reads_it()
    {
        using var reader = await ReaderAsync();
        var sara = Guid.NewGuid();
        var omar = Guid.NewGuid();
        var signOut = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.SessionSignedOut, reader.Now.AddHours(-3), actor: sara, actorName: "sara.zzq@x.example");
        var roleBySara = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-2), actor: sara, actorName: "sara.zzq@x.example");
        var roleByOmar = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-1), actor: omar, actorName: "omar.zzq@x.example");

        // Part of a name.
        Assert.Equal([roleBySara, signOut], Ids(await reader.EntriesAsync("?q=sara.zzq")));
        Assert.Equal([roleByOmar, roleBySara, signOut], Ids(await reader.EntriesAsync("?q=.zzq@")));

        // The action's name, in Arabic for one reader and in English for another.
        Assert.Equal([roleByOmar, roleBySara], Ids(await reader.EntriesAsync($"?q={Uri.EscapeDataString("حذف دور")}")));
        Assert.Equal([roleByOmar, roleBySara], Ids(await reader.EntriesAsync("?q=role%20del", "en")));
        Assert.Equal([signOut], Ids(await reader.EntriesAsync($"?q={Uri.EscapeDataString("تسجيل خروج")}")));
        Assert.Equal([signOut], Ids(await reader.EntriesAsync("?q=SIGN-OUT", "en")));

        // An English name is not what an Arabic reader reads, and the reverse.
        Assert.Empty(await reader.EntriesAsync("?q=role%20del", "ar"));
        Assert.Empty(await reader.EntriesAsync($"?q={Uri.EscapeDataString("حذف دور")}", "en"));
        Assert.Empty(await reader.EntriesAsync("?q=nothing-like-this"));

        var tooLong = await reader.AskAsync($"?q={new string('x', 101)}");
        Assert.Equal((400, "TOO_LONG"), (tooLong.Status, tooLong.Body.GetProperty("errors").GetProperty("q")[0].GetString()));
    }

    [Fact]
    public async Task The_counts_follow_every_facet_but_the_category_and_the_total_follows_them_all()
    {
        using var reader = await ReaderAsync();
        var sara = Guid.NewGuid();
        var omar = Guid.NewGuid();
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.SignInSucceeded, reader.Now.AddHours(-5), actor: sara, actorName: "sara@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.SessionSignedOut, reader.Now.AddHours(-4), actor: sara, actorName: "sara@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-3), actor: sara, actorName: "sara@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-2), actor: omar, actorName: "omar@x.example");
        await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddDays(-100), actor: sara, actorName: "sara@x.example");

        var (_, _, all) = await reader.AskAsync($"?actor={sara}");
        var (_, _, users) = await reader.AskAsync($"?actor={sara}&category=users");
        var (_, _, changes) = await reader.AskAsync($"?actor={sara}&category=changes");

        static (int All, int Security, int Changes, int Records, int Users) Counts(JsonElement body)
        {
            var counts = body.GetProperty("counts");
            return (counts.GetProperty("all").GetInt32(), counts.GetProperty("security").GetInt32(),
                counts.GetProperty("changes").GetInt32(), counts.GetProperty("records").GetInt32(), counts.GetProperty("users").GetInt32());
        }

        // The same counts whichever tab is open: they are what the tabs show.
        Assert.Equal((3, 2, 0, 0, 1), Counts(all));
        Assert.Equal((3, 2, 0, 0, 1), Counts(users));
        Assert.Equal((3, 2, 0, 0, 1), Counts(changes));

        Assert.Equal(3, all.GetProperty("total").GetInt32());
        Assert.Equal(1, users.GetProperty("total").GetInt32());
        Assert.Equal(0, changes.GetProperty("total").GetInt32());
        Assert.Equal(1, users.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task Pages_neither_repeat_nor_skip_an_entry_when_entries_share_a_moment()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        var moment = reader.Now.AddHours(-1);
        var put = new List<Guid>();
        for (var i = 0; i < 7; i++)
        {
            put.Add(await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, moment, actor: actor, actorName: "a@x.example"));
        }

        var seen = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var (status, _, body) = await reader.AskAsync($"?actor={actor}&size=3&page={page}");
            Assert.Equal(200, status);
            Assert.Equal((7, page, 3), (body.GetProperty("total").GetInt32(), body.GetProperty("page").GetInt32(), body.GetProperty("size").GetInt32()));
            seen.AddRange(Ids(body.GetProperty("entries").EnumerateArray()));
        }

        Assert.Equal(7, seen.Count);
        Assert.Equal(put.Order(), seen.Order());
    }

    [Fact]
    public async Task A_page_is_twenty_five_entries_unless_asked_otherwise_and_never_more_than_a_hundred()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        for (var i = 0; i < 30; i++)
        {
            await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddMinutes(-i - 1), actor: actor, actorName: "a@x.example");
        }

        var (_, _, byDefault) = await reader.AskAsync($"?actor={actor}");
        var (_, _, asked) = await reader.AskAsync($"?actor={actor}&size=100");

        Assert.Equal((25, 25, 1), (byDefault.GetProperty("entries").GetArrayLength(), byDefault.GetProperty("size").GetInt32(), byDefault.GetProperty("page").GetInt32()));
        Assert.Equal(30, asked.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task What_the_platform_or_an_operator_did_is_never_part_of_the_clinics_reading()
    {
        using var reader = await ReaderAsync();
        var person = Guid.NewGuid();
        var own = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-1), actor: person, actorName: "a@x.example");
        var bySystem = await sql.Main.PutEntryAsync(reader.Clinic, AuditActions.RoleCreated, reader.Now.AddHours(-2), actorKind: "system");
        var byOperator = await sql.Main.PutEntryAsync(
            reader.Clinic, AuditActions.RoleDeleted, reader.Now.AddHours(-3), actorKind: "operator", actor: person, actorName: "ops@heta.example");

        var (_, _, body) = await reader.AskAsync("?size=100");
        var ids = Ids(body.GetProperty("entries").EnumerateArray());

        Assert.Contains(own, ids);
        Assert.DoesNotContain(bySystem, ids);
        Assert.DoesNotContain(byOperator, ids);

        // Not counted either, and not reachable by asking for the operator by id.
        Assert.Equal(ids.Count, body.GetProperty("total").GetInt32());
        Assert.Equal([own], Ids(await reader.EntriesAsync($"?actor={person}")));
    }

    [Fact]
    public async Task The_answer_has_the_shape_the_contract_gives_it()
    {
        using var reader = await ReaderAsync();
        var (colleague, colleagueEmail) = await reader.World.AccountAsync();
        using var wrong = reader.World.Factory.Browser();
        wrong.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15");
        using (await wrong.StepOneAsync(colleagueEmail, "Wrong-Password-1!")) { }
        using (await wrong.StepOneAsync(colleagueEmail, "Wrong-Password-1!")) { }

        var (status, _, body) = await reader.AskAsync("?action=signin.failed-run");

        Assert.Equal(200, status);
        Assert.Equal(
            ["counts", "entries", "from", "page", "size", "to", "total"],
            body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["all", "changes", "records", "security", "users"],
            body.GetProperty("counts").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        var entry = Assert.Single(body.GetProperty("entries").EnumerateArray());
        Assert.Equal(
            ["action", "actor", "attempts", "category", "device", "id", "ipAddress", "locked", "occurredAt", "sentence", "target"],
            entry.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        Assert.Equal("security", entry.GetProperty("category").GetString());
        Assert.Equal(("signin.failed-run", "محاولة دخول فاشلة"), (entry.GetProperty("action").GetProperty("code").GetString(), entry.GetProperty("action").GetProperty("name").GetString()));
        Assert.Equal($"محاولتان على حساب {colleagueEmail}", entry.GetProperty("sentence").GetString());

        // Nobody is known to have acted (FR-035).
        var actor = entry.GetProperty("actor");
        Assert.False(actor.GetProperty("known").GetBoolean());
        Assert.Equal(JsonValueKind.Null, actor.GetProperty("accountId").ValueKind);
        Assert.Equal("غير معروف", actor.GetProperty("name").GetString());

        var target = entry.GetProperty("target");
        Assert.Equal(("account", colleague, colleagueEmail, true),
            (target.GetProperty("kind").GetString(), target.GetProperty("id").GetGuid(), target.GetProperty("name").GetString(), target.GetProperty("exists").GetBoolean()));

        Assert.Equal("متصفح Safari على macOS", entry.GetProperty("device").GetString());
        Assert.Equal((2, false), (entry.GetProperty("attempts").GetInt32(), entry.GetProperty("locked").GetBoolean()));
        Assert.InRange(entry.GetProperty("occurredAt").GetDateTimeOffset(), reader.Now.AddMinutes(-1), reader.Now.AddMinutes(1));
    }

    [Fact]
    public async Task One_page_reads_in_arabic_for_one_reader_and_in_english_for_another()
    {
        using var reader = await ReaderAsync();
        var (actor, actorEmail) = await reader.World.AccountAsync();
        var (person, personEmail) = await reader.World.AccountAsync();
        var doctor = await reader.World.Factory.ReadyRoleAsync(reader.Clinic, "doctor");

        var custom = await reader.World.Factory.RolesAsAsync(reader.Clinic, actor, roles => roles.CreateAsync(
            "الاستقبال", null, [Permissions.BookingsView, Permissions.BookingsManage, Permissions.PatientsView, Permissions.MoneyView], default));
        await reader.World.Factory.RolesAsAsync(reader.Clinic, actor, roles => roles.UpdateAsync(
            custom, "مكتب الاستقبال", null, [Permissions.BookingsView, Permissions.MoneyCollect, Permissions.MoneyView], default));
        await reader.World.Factory.RolesAsAsync(reader.Clinic, actor, roles => roles.AssignAsync(person, doctor, default));
        await reader.World.Factory.RolesAsAsync(reader.Clinic, actor, roles => roles.AssignAsync(person, custom, default));

        var arabic = await reader.EntriesAsync($"?actor={actor}", "ar");
        var english = await reader.EntriesAsync($"?actor={actor}", "en");

        static string[] Sentences(List<JsonElement> entries) => [.. entries.Select(e => e.GetProperty("sentence").GetString()!).Reverse()];
        static string[] Names(List<JsonElement> entries) => [.. entries.Select(e => e.GetProperty("action").GetProperty("name").GetString()!).Reverse()];
        string Ar(string code) => PermissionCatalogue.Require(code).NameAr;
        string En(string code) => PermissionCatalogue.Require(code).NameEn;

        // The same entries, in the same order.
        Assert.Equal(Ids(arabic), Ids(english));
        Assert.Equal(["إنشاء دور", "تعديل دور", "تغيير دور مستخدم", "تغيير دور مستخدم"], Names(arabic));
        Assert.Equal(["Role created", "Role edited", "User’s role changed", "User’s role changed"], Names(english));

        Assert.Equal(
            [
                "أُنشئ الدور «الاستقبال» بـ٤ صلاحيات",
                $"عُدِّل الدور «مكتب الاستقبال»: غُيِّر اسمه من «الاستقبال»، وأُضيف {Ar(Permissions.MoneyCollect)}، ونُزع {Ar(Permissions.BookingsManage)}، {Ar(Permissions.PatientsView)}",
                $"غُيِّر دور {personEmail} من «بلا دور» إلى «طبيب»",
                $"غُيِّر دور {personEmail} من «طبيب» إلى «مكتب الاستقبال»",
            ],
            Sentences(arabic));

        Assert.Equal(
            [
                "Role “الاستقبال” was created with 4 permissions",
                $"Role “مكتب الاستقبال” was edited: renamed from “الاستقبال”; added {En(Permissions.MoneyCollect)}; removed {En(Permissions.BookingsManage)}, {En(Permissions.PatientsView)}",
                $"{personEmail}’s role was changed from “no role” to “Doctor”",
                $"{personEmail}’s role was changed from “Doctor” to “مكتب الاستقبال”",
            ],
            Sentences(english));

        // The actor is named as they were called then; a target that is an account as it is called now.
        Assert.All(arabic, e => Assert.Equal(actorEmail, e.GetProperty("actor").GetProperty("name").GetString()));
        Assert.Equal("Unknown device", english[0].GetProperty("device").GetString());
        Assert.Equal("جهاز غير معروف", arabic[0].GetProperty("device").GetString());
    }

    [Fact]
    public async Task An_account_that_no_longer_exists_reads_as_a_deleted_account()
    {
        using var reader = await ReaderAsync();
        var actor = Guid.NewGuid();
        var gone = Guid.NewGuid();
        await sql.Main.PutEntryAsync(
            reader.Clinic, AuditActions.InvitationAccepted, reader.Now.AddHours(-1), actor: actor, actorName: "was@x.example", target: gone, targetKind: "account");

        var arabic = Assert.Single(await reader.EntriesAsync($"?actor={actor}", "ar"));
        var english = Assert.Single(await reader.EntriesAsync($"?actor={actor}", "en"));

        Assert.Equal(("حساب محذوف", false), (arabic.GetProperty("target").GetProperty("name").GetString(), arabic.GetProperty("target").GetProperty("exists").GetBoolean()));
        Assert.Equal("قُبلت دعوة حساب محذوف", arabic.GetProperty("sentence").GetString());
        Assert.Equal("deleted account’s invitation was accepted", english.GetProperty("sentence").GetString());
        Assert.Equal("was@x.example", english.GetProperty("actor").GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_operators_reading_returns_what_operators_did_across_clinics_and_nothing_else()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var now = world.Factory.Clock.GetUtcNow();
        var ops = Guid.NewGuid();

        var inFirst = await sql.Main.PutEntryAsync(world.Clinic, AuditActions.RoleDeleted, now.AddHours(-2), actorKind: "operator", actor: ops, actorName: "ops@heta.example");
        var inSecond = await sql.Main.PutEntryAsync(other, AuditActions.SessionsEnded, now.AddHours(-1), actorKind: "operator", actor: ops, actorName: "ops@heta.example");
        await sql.Main.PutEntryAsync(world.Clinic, AuditActions.RoleDeleted, now.AddHours(-1), actor: Guid.NewGuid(), actorName: "staff@x.example");
        await sql.Main.PutEntryAsync(other, AuditActions.RoleCreated, now.AddHours(-1), actorKind: "system");

        using var scope = world.Factory.Services.CreateScope();
        var reading = scope.ServiceProvider.GetRequiredService<IOperatorAuditReading>();
        var asked = Trail.Everything("en") with { Actor = ops };

        var across = await reading.ReadAsync(asked, null, default);
        var one = await reading.ReadAsync(asked, other, default);
        var security = await reading.ReadAsync(asked with { Category = "security" }, null, default);

        Assert.Equal([inSecond, inFirst], across.Entries.Select(e => e.Id));
        Assert.Equal((2, 1, 1), (across.Counts.All, across.Counts.Security, across.Counts.Users));
        Assert.Equal([inSecond], one.Entries.Select(e => e.Id));
        Assert.Equal([inSecond], security.Entries.Select(e => e.Id));
        Assert.All(across.Entries, e => Assert.Equal("ops@heta.example", e.Actor.Name));
        Assert.Equal("Role deleted", across.Entries[1].Action.Name);
    }
}
