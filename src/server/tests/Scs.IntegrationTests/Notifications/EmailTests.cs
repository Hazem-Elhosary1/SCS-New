using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.Notifications.Infrastructure;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Story 4 (FR-007, FR-030 to FR-036; SC-004 to SC-006): email follows the
/// event's kind, leaves after the change, is tried again, and never carries what it must not.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmailTests(SqlServerFixture sql)
{
    private Task<RefusalWorld> WorldAsync() => RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());

    private static RaisedEvent ToOwner(Guid owner, string label = "T-1046", string subject = "تأخّر وصول الرسائل") => new(
        TestEvents.ToOwner,
        Notify.Ticket(label),
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["subject"] = subject },
        [owner]);

    [Fact]
    public async Task An_event_that_is_always_emailed_owes_one_email_to_each_recipient_and_one_never_emailed_owes_none()
    {
        using var world = await WorldAsync();
        var (first, _) = await world.AccountAsync();
        var (second, _) = await world.AccountAsync();

        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic,
            new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [first, second]),
            new RaisedEvent(TestEvents.NeverEmailed, Notify.Ticket(), Owners: [first, second])));

        var stored = await sql.Main.NotificationsAsync(world.Clinic);
        Assert.Equal(4, stored.Count);
        Assert.All(stored.Where(n => n.EventKey == TestEvents.ToOwner), n => Assert.Equal("waiting", n.EmailState));
        Assert.All(stored.Where(n => n.EventKey == TestEvents.NeverEmailed), n => Assert.Null(n.EmailState));

        // Nothing leaves inside the save that raised the event.
        Assert.Empty(world.Factory.Mail.Sent);
    }

    [Fact]
    public async Task The_email_says_what_the_bell_says_in_the_recipients_language_and_one_language_only()
    {
        using var world = await WorldAsync();
        using var english = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        var (arabic, arabicEmail) = await world.AccountAsync();
        using (var saved = await english.Browser.PutAsJsonAsync("/v1/account/preferences", new { language = "en" }))
        {
            Assert.True(saved.IsSuccessStatusCode);
        }

        var ticket = Notify.Ticket("T-1046");
        Assert.Null(await world.Factory.RaiseAsync(
            world.Clinic,
            new RaisedEvent(
                TestEvents.ToOwner,
                ticket,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["subject"] = "Printer offline", ["count"] = 3 },
                [english.AccountId, arabic])));

        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        // Nobody chose Arabic: it is what a person who chose nothing reads.
        var toArabic = world.Factory.Mail.To(arabicEmail).Single(m => m.Subject == "حدث لصاحبه");
        // An email writes Western digits in both languages (spec 094, FR-011).
        Assert.Contains("التذكرة T-1046 · Printer offline · 3", toArabic.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.StaffApp}/tickets/{ticket.Id}", toArabic.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Event for its owner", toArabic.TextBody + toArabic.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", toArabic.HtmlBody, StringComparison.Ordinal);

        var toEnglish = world.Factory.Mail.To(english.Email).Single(m => m.Subject == "Event for its owner");
        Assert.Contains("Ticket T-1046 · Printer offline · 3", toEnglish.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("حدث لصاحبه", toEnglish.TextBody + toEnglish.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"ltr\"", toEnglish.HtmlBody, StringComparison.Ordinal);

        Assert.All(
            (await sql.Main.NotificationsAsync(world.Clinic)).Where(n => n.EventKey == TestEvents.ToOwner),
            n => Assert.Equal("sent", n.EmailState));

        // Running the job again sends nothing twice.
        var sent = world.Factory.Mail.Sent.Count;
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        Assert.Equal(sent, world.Factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task A_mail_server_that_is_down_does_not_fail_the_change_and_the_email_is_tried_again()
    {
        using var world = await WorldAsync();
        var (person, email) = await world.AccountAsync();

        world.Factory.Mail.Failing = true;
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, ToOwner(person)));

        // The bell has it whatever the mail server does (SC-004).
        Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));

        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        var waiting = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
        Assert.Equal(("waiting", 1), (waiting.EmailState, waiting.EmailAttempts));

        // Not tried again before its time: a minute after the first failure.
        world.Factory.Mail.Failing = false;
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        Assert.Empty(world.Factory.Mail.To(email));

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        Assert.Single(world.Factory.Mail.To(email));
        Assert.Equal("sent", Assert.Single(await sql.Main.NotificationsAsync(world.Clinic)).EmailState);
    }

    [Fact]
    public async Task The_waits_between_tries_grow_and_after_a_day_the_email_is_given_up()
    {
        using var world = await WorldAsync();
        var (person, email) = await world.AccountAsync();
        world.Factory.Mail.Failing = true;
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, ToOwner(person)));

        // One, five and thirty minutes, then every hour.
        foreach (var (wait, attempts) in new[] { (0, 1), (1, 2), (5, 3), (30, 4), (60, 5) })
        {
            world.Factory.Clock.Advance(TimeSpan.FromMinutes(wait) + TimeSpan.FromSeconds(1));
            await world.Factory.SendNotificationEmailsAsync(world.Clinic);
            Assert.Equal(("waiting", attempts), await LatestAsync());
        }

        // Too early for the next try: nothing moves.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(30));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        Assert.Equal(("waiting", 5), await LatestAsync());

        world.Factory.Clock.Advance(TimeSpan.FromHours(23));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        Assert.Equal("gave-up", (await LatestAsync()).State);
        Assert.Empty(world.Factory.Mail.To(email));

        // Given up is given up: a mail server that returns does not bring it back.
        world.Factory.Mail.Failing = false;
        world.Factory.Clock.Advance(TimeSpan.FromHours(2));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        Assert.Empty(world.Factory.Mail.To(email));

        async Task<(string? State, int? Attempts)> LatestAsync()
        {
            var row = Assert.Single(await sql.Main.NotificationsAsync(world.Clinic));
            return (row.EmailState, row.EmailAttempts);
        }
    }

    [Fact]
    public async Task An_account_disabled_since_the_event_gets_no_email_and_one_that_only_lost_the_permission_still_does()
    {
        using var world = await WorldAsync();
        var factory = world.Factory;
        var holderRole = await factory.CreateRoleAsync(world.Clinic, Permissions.TicketsView, TestEvents.Permission);
        var otherRole = await factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);
        var (leaving, leavingEmail) = await world.AccountAsync();
        var (demoted, demotedEmail) = await world.AccountAsync();
        await factory.RolesAsync(world.Clinic, r => r.AssignAsync(leaving, holderRole, default));
        await factory.RolesAsync(world.Clinic, r => r.AssignAsync(demoted, holderRole, default));

        Assert.Null(await factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));

        await factory.ForClinicAsync(world.Clinic, services =>
            services.GetRequiredService<IStaffAccounts>().SetStateAsync(leaving, StaffAccountStates.Disabled, default));
        await factory.RolesAsync(world.Clinic, r => r.AssignAsync(demoted, otherRole, default));

        await factory.SendNotificationEmailsAsync(world.Clinic);

        Assert.Empty(factory.Mail.To(leavingEmail));
        Assert.Single(factory.Mail.To(demotedEmail));
        Assert.Equal("dropped", Assert.Single(await sql.Main.NotificationsAsync(world.Clinic, leaving)).EmailState);
        Assert.Equal("sent", Assert.Single(await sql.Main.NotificationsAsync(world.Clinic, demoted)).EmailState);
    }

    [Fact]
    public async Task One_persons_failing_address_does_not_hold_up_a_colleagues_email()
    {
        using var world = await WorldAsync();
        var (first, firstEmail) = await world.AccountAsync();
        var (second, secondEmail) = await world.AccountAsync();
        world.Factory.Mail.FailingFor[firstEmail] = true;

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [first, second])));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        Assert.Empty(world.Factory.Mail.To(firstEmail));
        Assert.Single(world.Factory.Mail.To(secondEmail));
    }

    [Fact]
    public async Task An_email_that_cannot_be_composed_is_given_up_and_holds_up_nobody_elses()
    {
        // Spec 094, T051: composing used to happen outside what the job catches, so one such
        // email failed the job on every run and nothing after it in the batch was ever sent.
        using var world = await WorldAsync();
        var (first, firstEmail) = await world.AccountAsync();
        var (second, secondEmail) = await world.AccountAsync();

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.Unsendable, Notify.Ticket("T-BAD"), Owners: [first])));
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, ToOwner(second, "T-GOOD")));

        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        Assert.Empty(world.Factory.Mail.To(firstEmail));
        Assert.Single(world.Factory.Mail.To(secondEmail));

        var stored = await sql.Main.NotificationsAsync(world.Clinic);
        Assert.Equal("gave-up", stored.Single(n => n.SubjectLabel == "T-BAD").EmailState);
        Assert.Equal("sent", stored.Single(n => n.SubjectLabel == "T-GOOD").EmailState);

        // It is not tried again, and the bell has it all the same.
        var sent = world.Factory.Mail.Sent.Count;
        world.Factory.Clock.Advance(TimeSpan.FromHours(2));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        Assert.Equal(sent, world.Factory.Mail.Sent.Count);

        // The log says how many, and nothing of what the email would have said.
        Assert.Contains(world.Factory.Logs.Lines, line => line.Contains("Notification emails:", StringComparison.Ordinal) && line.Contains("\"gaveUp\":1", StringComparison.Ordinal));
        Assert.DoesNotContain("T-BAD", world.Factory.Logs.All, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Neither_an_address_nor_anything_secret_is_stored_or_logged()
    {
        const string password = "Zzq-Secret-Pass-9!";

        using var world = await WorldAsync();
        var localPart = $"zzq.notified.{Guid.NewGuid():N}";
        var (person, _) = await world.Factory.CreateAccountAsync(world.Clinic, email: $"{localPart}@zzqclinic.example", password: password);

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, ToOwner(person)));
        world.Factory.Mail.Failing = true;
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        world.Factory.Mail.Failing = false;
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);

        var stored = string.Join('\n', (await sql.Main.NotificationsAsync(world.Clinic)).Select(n => string.Join('|', n.EventKey, n.SubjectLabel, n.Facts, n.Link)));
        var columns = await TestDatabase.ReadColumnAsync(
            sql.Main.Admin,
            "SELECT c.name FROM sys.columns c WHERE c.object_id IN (OBJECT_ID('notify.OutgoingEmail'), OBJECT_ID('notify.Notification'))");

        foreach (var secret in new[] { localPart, "zzqclinic", password })
        {
            Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, world.Factory.Logs.All, StringComparison.Ordinal);
        }

        world.Factory.Mail.AssertNothingOfAnEmailIsIn(world.Factory.Logs.All);

        // No column that could hold an address or a text of the email exists at all.
        Assert.DoesNotContain(columns, name => name.Contains("Email", StringComparison.OrdinalIgnoreCase)
                                               || name.Contains("Address", StringComparison.OrdinalIgnoreCase)
                                               || name.Contains("Body", StringComparison.OrdinalIgnoreCase));

        // And the email itself holds no password: only the title, the line, the moment and the way
        // in, then the note that replies are not read and the template's footer.
        var email = Assert.Single(world.Factory.Mail.Sent, m => m.Subject == "حدث لصاحبه");
        Assert.DoesNotContain(password, email.TextBody + email.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(6, email.TextBody.Split("\n\n").Length);
    }

    [Fact]
    public async Task An_always_event_is_still_emailed_for_a_suspended_clinic()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi().WithTestEvents();
        var clinic = await sql.Main.SeedClinicAsync("suspended");
        var (person, email) = await factory.CreateAccountAsync(clinic);

        Assert.Null(await factory.RaiseAsync(clinic, new RaisedEvent(TestEvents.ToEveryone)));
        await factory.SendNotificationEmailsAsync(clinic);

        Assert.Single(factory.Mail.To(email));
        Assert.Equal("sent", Assert.Single(await sql.Main.NotificationsAsync(clinic, person)).EmailState);
    }

    [Fact]
    public async Task The_sweep_finds_the_clinics_with_email_due_and_no_other()
    {
        using var world = await WorldAsync();
        var quiet = await sql.Main.SeedClinicAsync();
        var later = await sql.Main.SeedClinicAsync();
        var (person, email) = await world.AccountAsync();
        var (other, _) = await world.Factory.CreateAccountAsync(later);
        await world.Factory.CreateAccountAsync(quiet);

        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, ToOwner(person)));

        // A clinic whose only email is not due yet: it failed a moment ago.
        Assert.Null(await world.Factory.RaiseAsync(later, ToOwner(other)));
        world.Factory.Mail.Failing = true;
        await world.Factory.SendNotificationEmailsAsync(later);
        world.Factory.Mail.Failing = false;

        var scheduler = new Remembering();
        using var scope = world.Factory.Services.CreateScope();
        var sweep = new EmailSweepJob(
            scope.ServiceProvider.GetRequiredService<ICrossClinicConnectionFactory>(), scheduler, world.Factory.Clock);
        await sweep.RunAsync(default);

        Assert.Contains(world.Clinic, scheduler.Clinics);
        Assert.DoesNotContain(quiet, scheduler.Clinics);
        Assert.DoesNotContain(later, scheduler.Clinics);
        Assert.Empty(world.Factory.Mail.To(email));
    }

    [Fact]
    public async Task The_emails_of_sign_in_and_passwords_are_as_they_were_and_are_not_notifications()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        using (await browser.StepOneAsync(email, SignIn.Password)) { }
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, email);

        // A code and a link were emailed as before, and the bell holds neither.
        Assert.Equal(2, world.Factory.Mail.To(email).Count);
        Assert.NotEmpty(token);
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [notify].OutgoingEmail WHERE ClinicId = '{world.Clinic}'"));
        Assert.Empty(await sql.Main.NotificationsAsync(world.Clinic, account));
    }

    /// <summary>Notes which clinics a job was asked for.</summary>
    private sealed class Remembering : Scs.BuildingBlocks.Jobs.IClinicJobScheduler
    {
        public List<Guid> Clinics { get; } = [];

        public string EnqueueForClinic<TJob>(Guid clinicId)
            where TJob : Scs.BuildingBlocks.Jobs.IClinicJob
        {
            Clinics.Add(clinicId);
            return "";
        }

        public bool TryEnqueueForClinic<TJob>(Guid clinicId)
            where TJob : Scs.BuildingBlocks.Jobs.IClinicJob
        {
            Clinics.Add(clinicId);
            return true;
        }

        public void AddOrUpdateRecurringPerClinic<TJob>(string jobName, string cron)
            where TJob : Scs.BuildingBlocks.Jobs.IClinicJob
        {
        }
    }
}
