using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.Notifications.Contracts;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Stories 1, 3 and 8 (FR-019 to FR-029): a person's bell as the console asks
/// for it — the list, the state, and the person's own read marks.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BellTests(SqlServerFixture sql)
{
    private static readonly Uri List = new("/v1/notifications", UriKind.Relative);
    private static readonly Uri State = new("/v1/notifications/state", UriKind.Relative);
    private static readonly Uri ReadAll = new("/v1/notifications/read-all", UriKind.Relative);

    private sealed record Person(RefusalWorld World, SignedInStaff Staff) : IDisposable
    {
        public Guid Clinic => World.Clinic;

        public Guid Id => Staff.AccountId;

        public async Task<(int Status, string? Code, JsonElement Body)> GetAsync(Uri address, string language = "ar")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            using var response = await Staff.Browser.SendAsync(request);
            return await response.ReadAsync();
        }

        public async Task<List<JsonElement>> ItemsAsync(string query = "", string language = "ar")
        {
            var (status, _, body) = await GetAsync(new Uri("/v1/notifications" + query, UriKind.Relative), language);
            Assert.Equal(200, status);
            return [.. body.GetProperty("items").EnumerateArray()];
        }

        public async Task TellAsync(string label = "T-1046", string subject = "تأخّر وصول الرسائل", int? count = null)
        {
            var facts = new Dictionary<string, object?>(StringComparer.Ordinal) { ["subject"] = subject };
            if (count is { } number)
            {
                facts["count"] = number;
            }

            Assert.Null(await World.Factory.RaiseAsync(
                Clinic, new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(label), facts, [Id])));
        }

        public void Dispose()
        {
            Staff.Dispose();
            World.Dispose();
        }
    }

    /// <summary>A signed-in person holding nothing that matters here: the bell needs no permission.</summary>
    private async Task<Person> PersonAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        return new Person(world, await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView));
    }

    [Fact]
    public async Task With_no_session_the_bell_answers_unauthenticated()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();

        using var list = await nobody.GetAsync(List);
        using var state = await nobody.GetAsync(State);
        using var mark = await nobody.PostAsync(ReadAll, null);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, state.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mark.StatusCode);
    }

    [Fact]
    public async Task The_four_addresses_are_the_persons_own_and_need_no_permission()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? "").StartsWith("/v1/notifications", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(4, routes.Count);
        Assert.All(routes, route => Assert.Equal(AccessRuleKind.OwnAccount, route.Metadata.GetMetadata<AccessRuleMetadata>()?.Kind));
    }

    [Fact]
    public async Task With_nothing_to_show_the_list_is_empty_and_nothing_is_unread()
    {
        using var person = await PersonAsync();

        var (status, _, list) = await person.GetAsync(List);
        var (_, _, state) = await person.GetAsync(State);

        Assert.Equal(200, status);
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());
        Assert.False(list.GetProperty("unread").GetBoolean());
        Assert.False(list.TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null);
        Assert.False(state.GetProperty("unread").GetBoolean());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("latestAt").ValueKind);
    }

    [Fact]
    public async Task A_notification_is_answered_as_words_in_the_shape_the_contract_gives()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-1046", "تأخّر وصول رسائل واتساب", count: 3);
        var stored = Assert.Single(await sql.Main.NotificationsAsync(person.Clinic));

        var (_, _, body) = await person.GetAsync(List);
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());

        Assert.Equal(
            ["at", "event", "id", "kind", "link", "read", "text", "title"],
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((stored.Id, "ticket", TestEvents.ToOwner), (item.GetProperty("id").GetGuid(), item.GetProperty("kind").GetString(), item.GetProperty("event").GetString()));
        Assert.Equal("حدث لصاحبه", item.GetProperty("title").GetString());
        Assert.Equal("التذكرة T-1046 · تأخّر وصول رسائل واتساب · ٣", item.GetProperty("text").GetString());
        Assert.Equal($"tickets/{stored.SubjectId}", item.GetProperty("link").GetString());
        Assert.False(item.GetProperty("read").GetBoolean());
        Assert.Equal(stored.RaisedAtUtc, item.GetProperty("at").GetDateTimeOffset());
        Assert.True(body.GetProperty("unread").GetBoolean());
    }

    [Fact]
    public async Task One_page_reads_in_arabic_for_one_reader_and_in_english_for_another()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-1046", "Printer offline", count: 12);

        var arabic = Assert.Single(await person.ItemsAsync(language: "ar"));
        var english = Assert.Single(await person.ItemsAsync(language: "en"));

        Assert.Equal(arabic.GetProperty("id").GetGuid(), english.GetProperty("id").GetGuid());
        Assert.Equal(("حدث لصاحبه", "Event for its owner"), (arabic.GetProperty("title").GetString(), english.GetProperty("title").GetString()));

        // The identifier and what a person wrote are as they are; the number follows the language.
        Assert.Equal("التذكرة T-1046 · Printer offline · ١٢", arabic.GetProperty("text").GetString());
        Assert.Equal("Ticket T-1046 · Printer offline · 12", english.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Notifications_come_newest_first_twenty_at_a_time_and_older_ones_follow_without_a_gap()
    {
        using var person = await PersonAsync();

        // Forty-five, several of them at one moment.
        for (var i = 0; i < 9; i++)
        {
            Assert.Null(await person.World.Factory.RaiseAsync(
                person.Clinic,
                [.. Enumerable.Range(0, 5).Select(_ => new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [person.Id]))]));
            person.World.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var seen = new List<(Guid Id, DateTimeOffset At)>();
        string? next = null;
        var pages = 0;
        do
        {
            var (status, _, body) = await person.GetAsync(
                new Uri("/v1/notifications" + (next is null ? "" : $"?before={Uri.EscapeDataString(next)}"), UriKind.Relative));
            Assert.Equal(200, status);
            var items = body.GetProperty("items").EnumerateArray().ToList();
            seen.AddRange(items.Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("at").GetDateTimeOffset())));
            next = body.TryGetProperty("next", out var cursor) ? cursor.GetString() : null;
            pages++;
            Assert.True(items.Count <= 20);
        }
        while (next is not null && pages < 10);

        Assert.Equal(3, pages);
        Assert.Equal(45, seen.Count);
        Assert.Equal(45, seen.Select(s => s.Id).Distinct().Count());
        Assert.Equal(seen.Select(s => s.At).OrderByDescending(at => at), seen.Select(s => s.At));
    }

    [Theory]
    [InlineData("?size=1", 1)]
    [InlineData("?size=50", 30)]
    [InlineData("", 20)]
    public async Task The_size_of_a_page_is_what_was_asked_up_to_fifty(string query, int expected)
    {
        using var person = await PersonAsync();
        Assert.Null(await person.World.Factory.RaiseAsync(
            person.Clinic,
            [.. Enumerable.Range(0, 30).Select(_ => new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [person.Id]))]));

        Assert.Equal(expected, (await person.ItemsAsync(query)).Count);
    }

    [Theory]
    [InlineData("?size=0", "size")]
    [InlineData("?size=51", "size")]
    [InlineData("?size=many", "size")]
    [InlineData("?before=yesterday", "before")]
    [InlineData("?before=12_zz", "before")]
    public async Task What_cannot_be_read_is_refused_by_its_name(string query, string field)
    {
        using var person = await PersonAsync();

        var (status, code, body) = await person.GetAsync(new Uri("/v1/notifications" + query, UriKind.Relative));

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("INVALID", body.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Fact]
    public async Task What_is_older_than_ninety_days_is_not_shown_and_not_counted()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-OLD");

        // Asked of the bell itself: a session does not live ninety days.
        Task<T> BellAsync<T>(Func<IBell, Task<T>> ask) =>
            person.World.Factory.ForClinicAsync(person.Clinic, services => ask(services.GetRequiredService<IBell>()));

        person.World.Factory.Clock.Advance(TimeSpan.FromDays(89));
        Assert.True((await BellAsync(bell => bell.StateAsync(person.Id, default))).Unread);
        Assert.Single((await BellAsync(bell => bell.ListAsync(person.Id, null, 20, "ar", default))).Items);

        person.World.Factory.Clock.Advance(TimeSpan.FromDays(2));
        var state = await BellAsync(bell => bell.StateAsync(person.Id, default));
        var list = await BellAsync(bell => bell.ListAsync(person.Id, null, 20, "ar", default));

        Assert.Equal((false, (DateTimeOffset?)null), (state.Unread, state.LatestAt));
        Assert.Empty(list.Items);
        Assert.False(list.Unread);
    }

    [Fact]
    public async Task The_state_says_whether_anything_is_unread_and_when_the_newest_came()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-1");
        person.World.Factory.Clock.Advance(TimeSpan.FromSeconds(5));
        await person.TellAsync("T-2");
        var newest = (await sql.Main.NotificationsAsync(person.Clinic)).Max(n => n.RaisedAtUtc);

        var (_, _, state) = await person.GetAsync(State);

        Assert.True(state.GetProperty("unread").GetBoolean());
        Assert.Equal(newest, state.GetProperty("latestAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Marking_one_read_marks_that_one_and_again_changes_nothing()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-1");
        await person.TellAsync("T-2");
        var first = (await sql.Main.NotificationsAsync(person.Clinic)).First(n => n.SubjectLabel == "T-1");
        var address = new Uri($"/v1/notifications/{first.Id}/read", UriKind.Relative);
        var entries = (await sql.Main.EntriesAsync(person.Clinic)).Count;

        using var marked = await person.Staff.Browser.PostAsync(address, null);
        var readAt = (await sql.Main.NotificationsAsync(person.Clinic)).Single(n => n.Id == first.Id).ReadAtUtc;
        using var again = await person.Staff.Browser.PostAsync(address, null);

        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.NotNull(readAt);

        var after = await sql.Main.NotificationsAsync(person.Clinic);
        Assert.Equal(readAt, after.Single(n => n.Id == first.Id).ReadAtUtc);
        Assert.Null(after.Single(n => n.SubjectLabel == "T-2").ReadAtUtc);

        var items = await person.ItemsAsync();
        Assert.Equal([false, true], items.Select(i => i.GetProperty("read").GetBoolean()));
        Assert.True((await person.GetAsync(State)).Body.GetProperty("unread").GetBoolean());

        // A person's own read mark is not something the audit trail holds.
        Assert.Equal(entries, (await sql.Main.EntriesAsync(person.Clinic)).Count);
    }

    [Fact]
    public async Task Marking_all_read_marks_every_one_of_the_persons_and_nobody_elses()
    {
        using var person = await PersonAsync();
        using var colleague = await person.World.Factory.SignInWithAsync(person.Clinic, Permissions.BookingsView);
        await person.TellAsync("T-1");
        await person.TellAsync("T-2");
        Assert.Null(await person.World.Factory.RaiseAsync(
            person.Clinic, new RaisedEvent(TestEvents.ToOwnerAndHolders, Notify.Ticket("T-3"), Owners: [person.Id, colleague.AccountId])));

        using var marked = await person.Staff.Browser.PostAsync(ReadAll, null);

        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.False((await person.GetAsync(State)).Body.GetProperty("unread").GetBoolean());
        Assert.All(await person.ItemsAsync(), item => Assert.True(item.GetProperty("read").GetBoolean()));

        // The colleague's copy of the same notification is still theirs to read (أ-ق١٧).
        Assert.Null(Assert.Single(await sql.Main.NotificationsAsync(person.Clinic, colleague.AccountId)).ReadAtUtc);
        using var theirs = await colleague.Browser.GetAsync(State);
        Assert.True((await theirs.ReadAsync()).Body.GetProperty("unread").GetBoolean());
    }

    [Fact]
    public async Task What_is_read_on_one_browser_is_read_on_the_persons_other_browser()
    {
        using var person = await PersonAsync();
        await person.TellAsync("T-1");
        using var elsewhere = await person.World.Factory.SignInAsAsync(person.Staff.Email);

        using (await person.Staff.Browser.PostAsync(ReadAll, null)) { }

        // (The second browser is also a new device for the account; that was told before the mark.)
        using var list = await elsewhere.GetAsync(List);
        var items = (await list.ReadAsync()).Body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Single(i => i.GetProperty("event").GetString() == TestEvents.ToOwner).GetProperty("read").GetBoolean());
    }
}
