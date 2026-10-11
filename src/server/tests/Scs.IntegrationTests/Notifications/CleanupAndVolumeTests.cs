using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Notifications;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.Notifications.Contracts;
using Scs.Notifications.Infrastructure;
using Scs.SharedKernel.Access;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, FR-019b: after ninety days a notification is removed, with the record of its
/// email. It points at something that is recorded where it happened, so nothing is lost.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CleanupTests(SqlServerFixture sql)
{
    private static async Task RunAsync(RefusalWorld world)
    {
        using var scope = world.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<NotificationCleanupJob>>()
            .RunAsync(world.Clinic, CancellationToken.None);
    }

    [Fact]
    public async Task What_is_older_than_ninety_days_is_removed_read_or_not_with_its_email()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        var (person, _) = await world.AccountAsync();
        var other = await sql.Main.SeedClinicAsync();
        var (theirs, _) = await world.Factory.CreateAccountAsync(other);

        RaisedEvent Old(Guid owner, string label) => new(TestEvents.ToOwner, Notify.Ticket(label), Owners: [owner]);

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Old(person, "T-READ"), Old(person, "T-UNREAD")));
        Assert.Null(await world.Factory.RaiseAsync(other, Old(theirs, "T-THEIRS")));
        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var bell = services.GetRequiredService<IBell>();
            var read = (await sql.Main.NotificationsAsync(world.Clinic)).Single(n => n.SubjectLabel == "T-READ");
            await bell.MarkReadAsync(person, read.Id, default);
        });

        world.Factory.Clock.Advance(TimeSpan.FromDays(60));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Old(person, "T-NEWER")));
        var entries = (await sql.Main.EntriesAsync(world.Clinic)).Count;

        // Sixty days: nothing is old enough.
        await RunAsync(world);
        Assert.Equal(3, (await sql.Main.NotificationsAsync(world.Clinic)).Count);

        world.Factory.Clock.Advance(TimeSpan.FromDays(31));
        await RunAsync(world);

        Assert.Equal("T-NEWER", Assert.Single(await sql.Main.NotificationsAsync(world.Clinic)).SubjectLabel);
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].OutgoingEmail WHERE ClinicId = '{world.Clinic}'"));

        // Each clinic's turn is its own: the other clinic's are untouched by this run.
        Assert.Single(await sql.Main.NotificationsAsync(other));

        // Twice is the same as once, and none of it is an audit entry.
        await RunAsync(world);
        Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal(entries, (await sql.Main.EntriesAsync(world.Clinic)).Count);
    }
}

/// <summary>
/// Spec 008, SC-007 and SC-008: a bell with a thousand notifications opens and is cleared in
/// under a second, and telling two hundred people adds next to nothing to the change that tells
/// them.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Volume")]
public sealed class NotificationVolumeTests(SqlServerFixture sql, ITestOutputHelper output)
{
    [Fact]
    public async Task A_bell_with_a_thousand_notifications_opens_and_is_cleared_in_under_a_second()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        for (var batch = 0; batch < 10; batch++)
        {
            Assert.Null(await world.Factory.RaiseAsync(
                world.Clinic,
                [.. Enumerable.Range(0, 100).Select(_ => new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [person.AccountId]))]));
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(1000, (await sql.Main.NotificationsAsync(world.Clinic, person.AccountId)).Count);

        // Once, unmeasured: the first call of a process pays for building the queries.
        using (await person.Browser.GetAsync(new Uri("/v1/notifications?size=1", UriKind.Relative))) { }
        using (await person.Browser.GetAsync(new Uri("/v1/notifications/state", UriKind.Relative))) { }

        var opening = Stopwatch.StartNew();
        using var state = await person.Browser.GetAsync(new Uri("/v1/notifications/state", UriKind.Relative));
        using var list = await person.Browser.GetAsync(new Uri("/v1/notifications", UriKind.Relative));
        opening.Stop();
        var body = (await list.ReadAsync()).Body;

        var next = Stopwatch.StartNew();
        using var older = await person.Browser.GetAsync(
            new Uri($"/v1/notifications?before={Uri.EscapeDataString(body.GetProperty("next").GetString()!)}", UriKind.Relative));
        next.Stop();

        var marking = Stopwatch.StartNew();
        using var marked = await person.Browser.PostAsync(new Uri("/v1/notifications/read-all", UriKind.Relative), null);
        marking.Stop();

        output.WriteLine($"State and first page of a thousand: {opening.ElapsedMilliseconds} ms.");
        output.WriteLine($"The next page: {next.ElapsedMilliseconds} ms.");
        output.WriteLine($"Marking a thousand read: {marking.ElapsedMilliseconds} ms.");

        Assert.Equal(20, body.GetProperty("items").GetArrayLength());
        Assert.True(older.IsSuccessStatusCode && marked.IsSuccessStatusCode);
        Assert.Equal(0, await world.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [notify].Notification WHERE RecipientAccountId = '{person.AccountId}' AND ReadAtUtc IS NULL"));
        Assert.True(opening.Elapsed < TimeSpan.FromSeconds(1), $"Opening took {opening.ElapsedMilliseconds} ms.");
        Assert.True(next.Elapsed < TimeSpan.FromSeconds(1), $"The next page took {next.ElapsedMilliseconds} ms.");
        Assert.True(marking.Elapsed < TimeSpan.FromSeconds(1), $"Marking took {marking.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Telling_two_hundred_people_adds_less_than_a_tenth_of_a_second_to_a_save()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        await TestDatabase.ExecuteAsync(
            sql.Main.Crossing,
            $"""
             INSERT INTO [identity].StaffAccount (Id, ClinicId, Email, PasswordHash, State, CreatedAtUtc)
             SELECT TOP (200) NEWID(), '{world.Clinic}', CONCAT(REPLACE(CAST(NEWID() AS char(36)), '-', ''), '@volume.example'),
                    'v1.1.c2FsdA==.aGFzaA==', 'active', SYSDATETIMEOFFSET()
             FROM sys.all_objects
             """);

        async Task<double> SaveAsync(bool telling)
        {
            var timer = Stopwatch.StartNew();
            Assert.Null(await world.Factory.RaiseAsync(world.Clinic, telling ? [new RaisedEvent(TestEvents.ToEveryone)] : []));
            timer.Stop();
            return timer.Elapsed.TotalMilliseconds;
        }

        // The first of each pays for building its statements.
        await SaveAsync(telling: true);
        await SaveAsync(telling: false);

        var with = new List<double>();
        var without = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            with.Add(await SaveAsync(telling: true));
            without.Add(await SaveAsync(telling: false));
        }

        with.Sort();
        without.Sort();
        var added = with[with.Count / 2] - without[without.Count / 2];
        output.WriteLine(
            $"A save that tells two hundred people: median {with[with.Count / 2]:N1} ms; one that tells nobody: {without[without.Count / 2]:N1} ms; added {added:N1} ms.");

        // Two hundred notifications and two hundred emails owed, each time.
        Assert.Equal(16 * 200, (await sql.Main.NotificationsAsync(world.Clinic)).Count);
        Assert.All(await sql.Main.NotificationsAsync(world.Clinic), n => Assert.Equal("waiting", n.EmailState));
        Assert.True(added < 100, $"Telling two hundred people added {added:N1} ms.");
    }
}
