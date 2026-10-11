using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Notifications;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Stories 1 and 7 (FR-004 to FR-009, FR-016 to FR-019a; SC-004, SC-009): an
/// event is raised with the change that causes it, by one way, and the engine refuses what it
/// must not store.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RaisingTests(SqlServerFixture sql)
{
    private Task<RefusalWorld> WorldAsync() => RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());

    private static Dictionary<string, object?> Facts(string key, object? value) => new(StringComparer.Ordinal) { [key] = value };

    private static Role NewRole(string name)
    {
        var role = Role.Custom(name, null, null, DateTimeOffset.UtcNow);
        role.Hold([Permissions.BookingsView]);
        return role;
    }

    /// <summary>Saves a change (a new role) together with an event, as a module does.</summary>
    private static Task<Exception?> ChangeAndRaiseAsync(RefusalWorld world, string roleName, RaisedEvent raised) =>
        world.Factory.ForClinicAsync<Exception?>(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            db.Roles.Add(NewRole(roleName));
            db.Notify(raised);
            return await Record.ExceptionAsync(() => db.SaveChangesAsync());
        });

    private static Task<int> RolesNamedAsync(RefusalWorld world, string name) =>
        world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{world.Clinic}' AND Name = N'{name}'");

    [Fact]
    public async Task An_event_and_its_change_are_both_there()
    {
        using var world = await WorldAsync();
        var (person, _) = await world.AccountAsync();
        var ticket = Notify.Ticket("T-1046");
        var before = world.Factory.Clock.GetUtcNow();

        var failure = await ChangeAndRaiseAsync(
            world, "مع حدث", new RaisedEvent(TestEvents.ToOwner, ticket, Facts("subject", "تأخّر وصول الرسائل"), [person]));

        Assert.Null(failure);
        Assert.Equal(1, await RolesNamedAsync(world, "مع حدث"));

        var stored = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal((world.Clinic, person, TestEvents.ToOwner, "ticket"), (stored.ClinicId, stored.RecipientAccountId, stored.EventKey, stored.Mark));
        Assert.Equal(("ticket", ticket.Id, "T-1046", ""), (stored.SubjectKind, stored.SubjectId, stored.SubjectLabel, stored.Occasion));
        Assert.Equal("تأخّر وصول الرسائل", stored.FactsJson.GetProperty("subject").GetString());
        Assert.Equal($"tickets/{ticket.Id}", stored.Link);
        Assert.Null(stored.ReadAtUtc);

        // The platform's clock, and an email owed because the event is always emailed.
        Assert.InRange(stored.RaisedAtUtc, before.AddSeconds(-1), before.AddMinutes(1));
        Assert.Equal(("waiting", 0), (stored.EmailState, stored.EmailAttempts));
    }

    [Fact]
    public async Task When_the_change_fails_no_notification_remains()
    {
        using var world = await WorldAsync();
        var (person, _) = await world.AccountAsync();
        var raised = new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [person]);

        Assert.Null(await ChangeAndRaiseAsync(world, "مكرر", raised));

        // The same name twice in one clinic: the database refuses the change.
        Assert.IsType<DbUpdateException>(await ChangeAndRaiseAsync(world, "مكرر", raised));

        Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
    }

    [Fact]
    public async Task When_the_callers_transaction_is_rolled_back_the_notification_and_its_email_go_too()
    {
        using var world = await WorldAsync();
        var (person, _) = await world.AccountAsync();

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();

            db.Roles.Add(NewRole("تراجع"));
            db.Notify(new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [person]));
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [notify].Notification").SingleAsync());
            await transaction.RollbackAsync();
        });

        Assert.Equal(0, await RolesNamedAsync(world, "تراجع"));
        Assert.Empty(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].OutgoingEmail WHERE ClinicId = '{world.Clinic}'"));
    }

    [Fact]
    public async Task What_the_engine_refuses_takes_the_change_with_it()
    {
        using var world = await WorldAsync();
        var (person, _) = await world.AccountAsync();
        Guid[] owner = [person];

        (string Case, RaisedEvent Raised)[] refused =
        [
            ("a key that is not in the table", new RaisedEvent("refund.issued", Notify.Ticket(), Owners: owner)),
            ("an event that has no line yet", new RaisedEvent(TestEvents.WithoutLine, Notify.Ticket(), Owners: owner)),
            ("a real event whose cause is not built", new RaisedEvent(NotificationEvents.TicketResolved, Notify.Ticket(), Owners: owner)),
            ("a fact the event does not allow", new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Facts("diagnosis", "x"), owner)),
            ("a fact that could hold a secret", new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Facts("password", "x"), owner)),
            ("any fact on a clinical event", new RaisedEvent(TestEvents.Clinical, Notify.Ticket(), Facts("value", "9.1"), owner)),
            ("a fact that is not a short value", new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Facts("subject", new string('x', 201)), owner)),
            ("an owner rule with no owner", new RaisedEvent(TestEvents.ToOwner, Notify.Ticket())),
            ("an occasion too long to store", new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: owner, Occasion: new string('d', 33))),
        ];

        foreach (var (name, raised) in refused)
        {
            var failure = await ChangeAndRaiseAsync(world, $"r-{Guid.NewGuid():N}"[..20], raised);
            Assert.True(failure is NotificationRefusedException, $"{name}: {failure?.GetType().Name ?? "saved"}");
        }

        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{world.Clinic}'"));
        Assert.Empty(await sql.Main.NotificationsAsync(world.Clinic));
    }

    [Fact]
    public async Task A_clinical_event_is_raised_with_the_record_alone_and_owes_no_email()
    {
        using var world = await WorldAsync();
        var (doctor, _) = await world.AccountAsync();

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.Clinical, Notify.Ticket("LAB-7"), Owners: [doctor])));

        var stored = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal(("LAB-7", (string?)null, (string?)null), (stored.SubjectLabel, stored.Facts, stored.EmailState));
    }

    [Fact]
    public async Task Raising_with_no_clinic_established_is_refused()
    {
        using var world = await WorldAsync();

        using var scope = world.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Notify(new RaisedEvent(TestEvents.ToEveryone));

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_save_that_cannot_wait_for_the_notification_is_refused()
    {
        using var world = await WorldAsync();

        await world.Factory.ForClinicAsync(world.Clinic, services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            db.Roles.Add(NewRole("بلا معاملة"));
            db.Notify(new RaisedEvent(TestEvents.ToEveryone));

            Assert.Throws<NotificationRefusedException>(() => db.SaveChanges());
            return Task.CompletedTask;
        });

        Assert.Equal(0, await RolesNamedAsync(world, "بلا معاملة"));
    }

    [Fact]
    public async Task Every_raise_tells_unless_it_names_an_occasion_already_told()
    {
        using var world = await WorldAsync();
        var (person, _) = await world.AccountAsync();
        var ticket = Notify.Ticket();
        var invoice = Notify.Ticket("INV-2026-0012");

        // Two replies on one ticket are two happenings (FR-008a).
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToOwner, ticket, Owners: [person])));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToOwner, ticket, Owners: [person])));
        Assert.Equal(2, (await sql.Main.NotificationsAsync(world.Clinic)).Count(n => n.EventKey == TestEvents.ToOwner));

        // A reminder a job raised twice for the same day-count tells once (FR-008) …
        RaisedEvent Reminder(int days) => new(
            TestEvents.Reminder, invoice, Facts("days", days), [person], Occasion: $"{days}-days");

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Reminder(14)));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Reminder(14)));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Reminder(14), Reminder(14)));
        var reminders = (await sql.Main.NotificationsAsync(world.Clinic)).Where(n => n.EventKey == TestEvents.Reminder).ToList();
        Assert.Equal("14-days", Assert.Single(reminders).Occasion);

        // … the next day-count tells again, and the same day-count about another record too.
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, Reminder(7)));
        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic, new RaisedEvent(TestEvents.Reminder, Notify.Ticket("INV-2026-0013"), Facts("days", 14), [person], "14-days")));
        Assert.Equal(3, (await sql.Main.NotificationsAsync(world.Clinic)).Count(n => n.EventKey == TestEvents.Reminder));

        // One email owed for each notification written, never for one that was skipped.
        Assert.All(await sql.Main.NotificationsAsync(world.Clinic), n => Assert.Equal("waiting", n.EmailState));
    }

    [Fact]
    public async Task A_notification_keeps_the_facts_of_its_moment()
    {
        using var world = await WorldAsync();
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var ticket = Notify.Ticket("T-2001");

        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic, new RaisedEvent(TestEvents.ToOwner, ticket, Facts("subject", "العنوان الأول"), [person.AccountId])));

        // The record is edited afterwards and tells again: the first notification is as it was.
        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic, new RaisedEvent(TestEvents.ToOwner, ticket, Facts("subject", "عنوان معدَّل"), [person.AccountId])));

        using var response = await person.Browser.GetAsync(new Uri("/v1/notifications", UriKind.Relative));
        var (_, _, body) = await response.ReadAsync();
        var texts = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()).ToList();

        Assert.Contains(texts, text => text!.Contains("العنوان الأول", StringComparison.Ordinal));
        Assert.Contains(texts, text => text!.Contains("عنوان معدَّل", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_platform_raises_with_nobody_signed_in()
    {
        using var world = await WorldAsync();
        await world.AccountAsync();
        await world.AccountAsync(StaffAccountStates.Disabled);

        // A scheduled job: a clinic, and no person.
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToEveryone)));

        Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
    }
}
