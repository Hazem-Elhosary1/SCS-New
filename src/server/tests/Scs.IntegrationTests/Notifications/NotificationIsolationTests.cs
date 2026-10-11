using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Notifications;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.IntegrationTests.Isolation;
using Scs.Notifications.Contracts;
using Scs.Notifications.Infrastructure.Persistence;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, FR-014 and constitution II: a notification belongs to one clinic and one person,
/// and no way of reading or marking reaches it from another. Written before the tables existed.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class NotificationIsolationTests(SqlServerFixture sql)
{
    /// <summary>Two clinics; a person of the first has one notification.</summary>
    private async Task<(RefusalWorld World, Guid A, Guid B, Guid Person, StoredNotification Notification)> TwoClinicsAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        var other = await sql.Main.SeedClinicAsync();
        var (person, _) = await world.AccountAsync();

        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic, new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [person])));

        return (world, world.Clinic, other, person, Assert.Single(await sql.Main.NotificationsAsync(world.Clinic)));
    }

    [Fact]
    public async Task The_tables_are_isolated_like_every_clinic_owned_table()
    {
        await sql.EnsureMigratedAsync();

        var violations = await IsolationCatalog.FindViolationsAsync(sql.AdminConnection);

        Assert.DoesNotContain(violations, line => line.StartsWith("notify.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(violations, line => line.Contains("KnownDevice", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            3,
            await sql.ScalarAsync<int>(
                sql.AdminConnection,
                """
                SELECT COUNT(DISTINCT t.name) FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.security_predicates p ON p.target_object_id = t.object_id
                WHERE s.name = 'notify'
                """));
    }

    [Fact]
    public async Task One_clinics_notification_is_not_read_from_another()
    {
        var (world, a, b, person, notification) = await TwoClinicsAsync();
        using var _ = world;

        var own = await world.Factory.ForClinicAsync(a, services =>
            services.GetRequiredService<IBell>().ListAsync(person, null, 20, "ar", default));
        var fromOther = await world.Factory.ForClinicAsync(b, services =>
            services.GetRequiredService<IBell>().ListAsync(person, null, 20, "ar", default));
        var stateFromOther = await world.Factory.ForClinicAsync(b, services =>
            services.GetRequiredService<IBell>().StateAsync(person, default));

        Assert.Equal(notification.Id, Assert.Single(own.Items).Id);
        Assert.Empty(fromOther.Items);
        Assert.Equal((false, (DateTimeOffset?)null), (stateFromOther.Unread, stateFromOther.LatestAt));

        // The application's own filter set aside, the database still answers nothing.
        var pastTheFilter = await world.Factory.ForClinicAsync(b, services =>
            services.GetRequiredService<NotificationsDbContext>().Notifications.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, pastTheFilter);
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT COUNT(*) FROM [notify].Notification", b));
    }

    [Fact]
    public async Task With_no_clinic_established_nothing_is_read_and_nothing_is_written()
    {
        var (world, a, _, _, _) = await TwoClinicsAsync();
        using var __ = world;

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT COUNT(*) FROM [notify].Notification"));

        await using var nobody = await TestDatabase.OpenAsync(sql.Main.App);
        await Assert.ThrowsAsync<SqlException>(() => NotificationRows.InsertNotificationAsync(nobody, a));
    }

    [Fact]
    public async Task A_notification_cannot_be_written_for_another_clinic()
    {
        var (world, a, b, _, _) = await TwoClinicsAsync();
        using var _ = world;

        await using var asA = await TestDatabase.OpenAsync(sql.Main.App, a);
        await Assert.ThrowsAsync<SqlException>(() => NotificationRows.InsertNotificationAsync(asA, b));

        Assert.Empty(await sql.Main.NotificationsAsync(b));
    }

    [Fact]
    public async Task One_persons_notification_is_not_anothers_to_read_or_to_mark_in_the_same_clinic()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        using var owner = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        using var colleague = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic, new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [owner.AccountId])));
        var theirs = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic, owner.AccountId));

        using var list = await colleague.Browser.GetAsync(new Uri("/v1/notifications", UriKind.Relative));
        var (_, _, body) = await list.ReadAsync();
        Assert.DoesNotContain(theirs.Id.ToString(), body.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var mark = await colleague.Browser.PostAsync(new Uri($"/v1/notifications/{theirs.Id}/read", UriKind.Relative), null);
        using var markAll = await colleague.Browser.PostAsync(new Uri("/v1/notifications/read-all", UriKind.Relative), null);
        using var unknown = await colleague.Browser.PostAsync(new Uri($"/v1/notifications/{Guid.NewGuid()}/read", UriKind.Relative), null);

        // Another person's is answered exactly as one that does not exist.
        Assert.Equal(HttpStatusCode.NotFound, mark.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal((await unknown.ReadAsync()).Code, (await mark.ReadAsync()).Code);
        Assert.Equal(HttpStatusCode.NoContent, markAll.StatusCode);
        Assert.Null(Assert.Single(await sql.Main.NotificationsAsync(world.Clinic, owner.AccountId)).ReadAtUtc);
    }

    [Fact]
    public async Task A_person_of_another_clinic_cannot_mark_it_by_its_identity()
    {
        var (world, _, b, person, notification) = await TwoClinicsAsync();
        using var _ = world;
        using var outsider = await world.Factory.SignInWithAsync(b, Permissions.BookingsView);

        using var mark = await outsider.Browser.PostAsync(new Uri($"/v1/notifications/{notification.Id}/read", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, mark.StatusCode);
        Assert.Null(Assert.Single(await sql.Main.NotificationsAsync(world.Clinic, person)).ReadAtUtc);
    }
}
