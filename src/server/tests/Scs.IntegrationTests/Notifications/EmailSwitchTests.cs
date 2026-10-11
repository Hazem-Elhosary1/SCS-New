using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Story 6 (FR-038; constitution III): the clinic's email switches are read with
/// «عرض الإعدادات» and changed with «إدارة التنبيهات», and only their own clinic's. Written
/// before the addresses existed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmailSwitchAccessTests(SqlServerFixture sql)
{
    private static readonly Uri Switches = new("/v1/settings/notification-emails", UriKind.Relative);

    private static Uri SwitchOf(string key) => new($"/v1/settings/notification-emails/{key}", UriKind.Relative);

    [Fact]
    public async Task With_no_session_both_addresses_answer_unauthenticated()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();

        using var read = await nobody.GetAsync(Switches);
        using var change = await nobody.PutAsJsonAsync(SwitchOf(NotificationEvents.InvoiceIssued), new { enabled = false });

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
    }

    [Fact]
    public async Task Reading_needs_the_settings_view_and_changing_needs_the_notifications_permission()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var outsider = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        using var viewer = await world.Factory.SignInWithAsync(world.Clinic, Permissions.SettingsView);
        using var manager = await world.Factory.SignInWithAsync(world.Clinic, Permissions.SettingsView, Permissions.SettingsNotifications);

        using var readByOutsider = await outsider.Browser.GetAsync(Switches);
        using var readByViewer = await viewer.Browser.GetAsync(Switches);
        using var changeByViewer = await viewer.Browser.PutAsJsonAsync(SwitchOf(NotificationEvents.InvoiceIssued), new { enabled = false });
        using var changeByManager = await manager.Browser.PutAsJsonAsync(SwitchOf(NotificationEvents.InvoiceIssued), new { enabled = false });

        var refusedRead = await readByOutsider.ReadAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (refusedRead.Status, refusedRead.Code));
        Assert.Equal(Permissions.SettingsView, refusedRead.Body.GetProperty("permission").GetString());

        Assert.Equal(HttpStatusCode.OK, readByViewer.StatusCode);

        var refusedChange = await changeByViewer.ReadAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (refusedChange.Status, refusedChange.Code));
        Assert.Equal(Permissions.SettingsNotifications, refusedChange.Body.GetProperty("permission").GetString());

        Assert.Equal(HttpStatusCode.OK, changeByManager.StatusCode);
    }

    [Fact]
    public async Task A_refused_change_changes_nothing_and_does_not_look_at_what_was_asked()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(world.Clinic, Permissions.SettingsView);

        using var nonsense = await viewer.Browser.PutAsJsonAsync(SwitchOf("no.such-event"), new { enabled = "maybe" });

        Assert.Equal(HttpStatusCode.Forbidden, nonsense.StatusCode);
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].EmailSwitch WHERE ClinicId = '{world.Clinic}'"));
    }

    [Fact]
    public async Task The_addresses_declare_the_permissions_they_need()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var read = routes.Single(e => e.RoutePattern.RawText == "/v1/settings/notification-emails");
        var change = routes.Single(e => e.RoutePattern.RawText == "/v1/settings/notification-emails/{eventKey}");

        Assert.Equal(Permissions.SettingsView, read.Metadata.GetMetadata<AccessRuleMetadata>()?.Permission);
        Assert.Equal(Permissions.SettingsNotifications, change.Metadata.GetMetadata<AccessRuleMetadata>()?.Permission);
        Assert.Equal(
            [AuditActions.SettingsNotificationEmailChanged],
            change.Metadata.GetMetadata<AuditDeclarationMetadata>()?.Actions);
    }

    [Fact]
    public async Task One_clinics_switch_is_never_another_clinics_to_read_or_to_change()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        using var manager = await world.Factory.SignInWithAsync(world.Clinic, Permissions.SettingsView, Permissions.SettingsNotifications);
        using var theirs = await world.Factory.SignInWithAsync(other, Permissions.SettingsView, Permissions.SettingsNotifications);

        using (var off = await manager.Browser.PutAsJsonAsync(SwitchOf(NotificationEvents.InvoiceIssued), new { enabled = false }))
        {
            Assert.True(off.IsSuccessStatusCode);
        }

        using var read = await theirs.Browser.GetAsync(Switches);
        var switches = (await read.ReadAsync()).Body.GetProperty("switches").EnumerateArray().ToList();

        Assert.All(switches, each => Assert.True(each.GetProperty("enabled").GetBoolean()));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].EmailSwitch WHERE ClinicId = '{other}'"));
    }
}

/// <summary>
/// Spec 008, User Story 6 (FR-037 to FR-041; SC-005): four switches, on until turned off, for
/// the whole clinic, each change on the audit trail — and never a way to stop the bell.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmailSwitchTests(SqlServerFixture sql)
{
    private static readonly Uri Switches = new("/v1/settings/notification-emails", UriKind.Relative);

    private static Uri SwitchOf(string key) => new($"/v1/settings/notification-emails/{key}", UriKind.Relative);

    private sealed record Manager(RefusalWorld World, SignedInStaff Staff) : IDisposable
    {
        public Guid Clinic => World.Clinic;

        public async Task<(int Status, string? Code, JsonElement Body)> SetAsync(string key, object body)
        {
            using var response = await Staff.Browser.PutAsJsonAsync(SwitchOf(key), body);
            return await response.ReadAsync();
        }

        public async Task<List<JsonElement>> ListAsync(string language = "ar")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Switches);
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            using var response = await Staff.Browser.SendAsync(request);
            return [.. (await response.ReadAsync()).Body.GetProperty("switches").EnumerateArray()];
        }

        public void Dispose()
        {
            Staff.Dispose();
            World.Dispose();
        }
    }

    private async Task<Manager> ManagerAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        return new Manager(
            world, await world.Factory.SignInWithAsync(world.Clinic, Permissions.SettingsView, Permissions.SettingsNotifications));
    }

    private static RaisedEvent Invoice(Guid owner, string key = NotificationEvents.InvoiceIssued) =>
        new(key, new EventSubject("invoice", Guid.CreateVersion7(), "INV-2026-0012"), Owners: [owner]);

    [Fact]
    public async Task A_new_clinic_has_four_switches_all_on_named_in_the_readers_language()
    {
        using var manager = await ManagerAsync();

        var arabic = await manager.ListAsync("ar");
        var english = await manager.ListAsync("en");

        Assert.Equal(
            ["invoice.issued", "payment.received", "subscription.ending", "licence.expiring"],
            arabic.Select(s => s.GetProperty("event").GetString()));
        Assert.All(arabic, s => Assert.True(s.GetProperty("enabled").GetBoolean()));
        Assert.Equal(
            ["صدرت فاتورة", "استُلمت الدفعة", "قرب انتهاء الاشتراك", "اقتراب انتهاء رخصة"],
            arabic.Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(
            ["Invoice issued", "Payment received", "Subscription ending soon", "Licence expiring soon"],
            english.Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(["billing", "billing", "billing", "accounts"], arabic.Select(s => s.GetProperty("group").GetString()));
    }

    [Fact]
    public async Task Turning_a_switch_off_stops_the_email_from_the_next_event_and_never_the_bell()
    {
        using var manager = await ManagerAsync();
        var (person, email) = await manager.World.AccountAsync();

        Assert.Null(await manager.World.Factory.RaiseAsync(manager.Clinic, Invoice(person)));

        var (status, _, body) = await manager.SetAsync(NotificationEvents.InvoiceIssued, new { enabled = false });
        Assert.Equal(200, status);
        Assert.Equal((NotificationEvents.InvoiceIssued, false), (body.GetProperty("event").GetString(), body.GetProperty("enabled").GetBoolean()));

        Assert.Null(await manager.World.Factory.RaiseAsync(manager.Clinic, Invoice(person)));

        // Another switchable event of the same clinic is untouched.
        Assert.Null(await manager.World.Factory.RaiseAsync(manager.Clinic, Invoice(person, NotificationEvents.PaymentReceived)));

        var told = await sql.Main.NotificationsAsync(manager.Clinic, person);
        Assert.Equal(3, told.Count);
        Assert.Equal(["waiting", null, "waiting"], told.Select(n => n.EmailState));

        // What was owed before the switch was turned off is still sent.
        await manager.World.Factory.SendNotificationEmailsAsync(manager.Clinic);
        Assert.Equal(2, manager.World.Factory.Mail.To(email).Count);

        Assert.False((await manager.ListAsync()).Single(s => s.GetProperty("event").GetString() == NotificationEvents.InvoiceIssued).GetProperty("enabled").GetBoolean());

        // And on again.
        await manager.SetAsync(NotificationEvents.InvoiceIssued, new { enabled = true });
        Assert.Null(await manager.World.Factory.RaiseAsync(manager.Clinic, Invoice(person)));
        Assert.Equal("waiting", (await sql.Main.NotificationsAsync(manager.Clinic, person))[^1].EmailState);
    }

    [Fact]
    public async Task One_clinics_switch_does_not_touch_another_clinics_email()
    {
        using var manager = await ManagerAsync();
        var other = await sql.Main.SeedClinicAsync();
        var (theirs, _) = await manager.World.Factory.CreateAccountAsync(other);

        await manager.SetAsync(NotificationEvents.InvoiceIssued, new { enabled = false });
        Assert.Null(await manager.World.Factory.RaiseAsync(other, Invoice(theirs)));

        Assert.Equal("waiting", Assert.Single(await sql.Main.NotificationsAsync(other, theirs)).EmailState);
    }

    [Fact]
    public async Task A_change_is_on_the_audit_trail_with_before_and_after_and_no_change_is_not()
    {
        using var manager = await ManagerAsync();

        await manager.SetAsync(NotificationEvents.SubscriptionEnding, new { enabled = false });
        await manager.SetAsync(NotificationEvents.SubscriptionEnding, new { enabled = false });
        await manager.SetAsync(NotificationEvents.SubscriptionEnding, new { enabled = true });
        await manager.SetAsync(NotificationEvents.InvoiceIssued, new { enabled = true });

        var entries = await sql.Main.EntriesAsync(manager.Clinic, AuditActions.SettingsNotificationEmailChanged);
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.Equal(("changes", "staff", manager.Staff.AccountId), (entry.Category, entry.ActorKind, entry.ActorAccountId));
            Assert.Equal(("notification-event", NotificationEvents.SubscriptionEnding), (entry.TargetKind, entry.TargetName));
        });
        Assert.Equal(("on", "off"), (entries[0].Change("enabled").GetProperty("before").GetString(), entries[0].Change("enabled").GetProperty("after").GetString()));
        Assert.Equal(("off", "on"), (entries[1].Change("enabled").GetProperty("before").GetString(), entries[1].Change("enabled").GetProperty("after").GetString()));

        // Read by the clinic, the entry names the event in the reader's language.
        var arabic = await manager.World.Factory.ReadTrailAsync(manager.Clinic, Trail.Everything("ar") with { Category = "changes" });
        var english = await manager.World.Factory.ReadTrailAsync(manager.Clinic, Trail.Everything("en") with { Category = "changes" });

        Assert.Equal("غُيِّر بريد «قرب انتهاء الاشتراك» من «يعمل» إلى «متوقف»", arabic.Entries[^1].Sentence);
        Assert.Equal("Email for “Subscription ending soon” was changed from “on” to “off”", english.Entries[^1].Sentence);
        Assert.Equal(("تغيير بريد تنبيه", "Notification email changed"), (arabic.Entries[0].Action.Name, english.Entries[0].Action.Name));
        Assert.Equal("Subscription ending soon", english.Entries[0].Target!.Name);
    }

    [Theory]
    [InlineData(NotificationEvents.TicketSupportReplied)]
    [InlineData(NotificationEvents.AccountNewDevice)]
    [InlineData(NotificationEvents.ResultOutOfRange)]
    [InlineData(NotificationEvents.OfferExpired)]
    public async Task An_event_that_is_always_or_never_emailed_has_no_switch(string key)
    {
        using var manager = await ManagerAsync();

        var (status, code, body) = await manager.SetAsync(key, new { enabled = false });

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("NOT_SWITCHABLE", body.GetProperty("errors").GetProperty("event")[0].GetString());
        Assert.Equal(0, await manager.World.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].EmailSwitch WHERE ClinicId = '{manager.Clinic}'"));
    }

    [Fact]
    public async Task What_cannot_be_understood_is_refused()
    {
        using var manager = await ManagerAsync();

        var unknown = await manager.SetAsync("no.such-event", new { enabled = false });
        var missing = await manager.SetAsync(NotificationEvents.InvoiceIssued, new { });
        var wrong = await manager.SetAsync(NotificationEvents.InvoiceIssued, new { enabled = "off" });

        Assert.Equal((404, "NOT_FOUND"), (unknown.Status, unknown.Code));
        Assert.Equal((400, "REQUIRED"), (missing.Status, missing.Body.GetProperty("errors").GetProperty("enabled")[0].GetString()));
        Assert.Equal((400, "REQUIRED"), (wrong.Status, wrong.Body.GetProperty("errors").GetProperty("enabled")[0].GetString()));
        Assert.Empty(await sql.Main.EntriesAsync(manager.Clinic, AuditActions.SettingsNotificationEmailChanged));
    }

    [Fact]
    public async Task Nothing_a_clinic_can_set_stops_an_email_that_is_always_sent()
    {
        using var manager = await ManagerAsync();
        var (person, _) = await manager.World.AccountAsync();

        foreach (var known in NotificationEvents.Switchable)
        {
            await manager.SetAsync(known.Key, new { enabled = false });
        }

        Assert.Null(await manager.World.Factory.RaiseAsync(
            manager.Clinic, new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [person])));

        Assert.Equal("waiting", Assert.Single(await sql.Main.NotificationsAsync(manager.Clinic, person)).EmailState);
    }
}
