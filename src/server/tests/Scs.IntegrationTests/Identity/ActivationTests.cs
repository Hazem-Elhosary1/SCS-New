using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 004, User Story 3: a new team member chooses a first password from an invitation link,
/// which activates the account.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ActivationTests(SqlServerFixture sql)
{
    internal static Task<RefusalWorld> WorldAsync(SqlServerFixture sql) =>
        RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));

    internal static string NewEmail() => $"person-{Guid.NewGuid():N}@clinic.example";

    [Fact]
    public async Task An_invitation_link_activates_the_account_and_the_person_signs_in_with_both_steps()
    {
        using var world = await WorldAsync(sql);
        var email = NewEmail();

        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        Assert.True(created.EmailSent);
        Assert.Null(created.GeneratedPassword);
        Assert.Equal("invited", await world.ScalarAsync<string>($"SELECT State FROM [identity].StaffAccount WHERE Id = '{created.AccountId}'"));

        var message = Assert.Single(world.Factory.Mail.To(email));
        Assert.Contains("عيادة تجريبية", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Test clinic", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.StaffApp}/sign-in", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("72", message.TextBody, StringComparison.Ordinal);
        var token = world.Factory.Mail.LatestLinkTokenFor(email);

        using var browser = world.Factory.Browser();
        using var opened = await browser.InspectLinkAsync(token);
        var view = await opened.ReadAsync();
        Assert.Equal(200, view.Status);
        Assert.Equal("new-account", view.Body.GetProperty("kind").GetString());
        Assert.Equal(email, view.Body.GetProperty("email").GetString());
        Assert.Equal("عيادة تجريبية", view.Body.GetProperty("clinicNameAr").GetString());
        Assert.Equal("Test clinic", view.Body.GetProperty("clinicNameEn").GetString());

        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.False(set.Headers.Contains("Set-Cookie"), "Activation must not open a session.");

        Assert.Equal(
            "active|0",
            await world.ScalarAsync<string>(
                $"SELECT CONCAT(State, '|', MustChangePassword) FROM [identity].StaffAccount WHERE Id = '{created.AccountId}'"));
        Assert.Equal(
            1,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{created.AccountId}' AND Kind = 'invitation-accepted' AND Category = 'activity'"));

        using var signedIn = await world.Factory.SignInAsAsync(email, Passwords.New);
        using var session = await signedIn.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.False((await session.ReadAsync()).Body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Sending_the_invitation_again_replaces_the_earlier_link()
    {
        using var world = await WorldAsync(sql);
        var email = NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        var first = world.Factory.Mail.LatestLinkTokenFor(email);

        var again = await world.Factory.HandOverAsync(world.Clinic, h => h.ResendInvitationAsync(created.AccountId, default));
        Assert.True(again.EmailSent);
        var second = world.Factory.Mail.LatestLinkTokenFor(email);
        Assert.NotEqual(first, second);

        using var browser = world.Factory.Browser();
        using var old = await browser.InspectLinkAsync(first);
        var refused = await old.ReadAsync();
        Assert.Equal((410, "LINK_INVALID", "new-account"), (refused.Status, refused.Code, refused.Body.GetProperty("kind").GetString()));

        using var fresh = await browser.SetWithLinkAsync(second, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

        // Once activated there is no invitation left to send.
        var failure = await Assert.ThrowsAsync<ScsProblemException>(
            () => world.Factory.HandOverAsync(world.Clinic, h => h.ResendInvitationAsync(created.AccountId, default)));
        Assert.Equal("NOT_FOUND", failure.Code);
    }

    [Fact]
    public async Task Forgot_password_sends_nothing_to_an_account_that_was_never_activated()
    {
        using var world = await WorldAsync(sql);
        var email = NewEmail();
        await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        using var browser = world.Factory.Browser();

        using var asked = await browser.ForgotAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        await world.Factory.RunAccountEmailJobAsync(world.Clinic);

        Assert.Single(world.Factory.Mail.To(email));
    }

    [Fact]
    public async Task When_the_invitation_cannot_be_sent_the_caller_is_told_and_no_link_is_left_behind()
    {
        using var world = await WorldAsync(sql);
        var email = NewEmail();
        world.Factory.Mail.Failing = true;

        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));

        Assert.False(created.EmailSent);
        Assert.Equal("invited", await world.ScalarAsync<string>($"SELECT State FROM [identity].StaffAccount WHERE Id = '{created.AccountId}'"));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].PasswordLink WHERE AccountId = '{created.AccountId}'"));

        world.Factory.Mail.Failing = false;
        var again = await world.Factory.HandOverAsync(world.Clinic, h => h.ResendInvitationAsync(created.AccountId, default));
        Assert.True(again.EmailSent);
    }

    [Fact]
    public async Task An_email_held_by_any_clinic_is_refused_the_same_way()
    {
        using var world = await WorldAsync(sql);
        var elsewhere = await sql.Main.SeedClinicAsync();
        var taken = await world.Factory.CreateAccountAsync(elsewhere);

        var failure = await Assert.ThrowsAsync<ScsProblemException>(
            () => world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(taken.Email, default)));

        Assert.Equal(["TAKEN"], failure.Errors!["email"]);
        Assert.Empty(world.Factory.Mail.Sent);
    }

    [Fact]
    public async Task A_link_opened_by_a_browser_signed_in_as_someone_else_works_and_leaves_that_session_alone()
    {
        using var world = await WorldAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var colleague = await world.Factory.CreateAccountAsync(other);
        using var browser = await world.Factory.SignInAsAsync(colleague.Email);

        var email = NewEmail();
        await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        var token = world.Factory.Mail.LatestLinkTokenFor(email);

        using var opened = await browser.InspectLinkAsync(token);
        Assert.Equal(email, (await opened.ReadAsync()).Body.GetProperty("email").GetString());
        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        // The browser is still signed in as the colleague, in the colleague's clinic.
        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var who = await session.ReadAsync();
        Assert.Equal(200, who.Status);
        Assert.Equal(colleague.Email, who.Body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task The_developer_invitation_address_exists_only_on_a_developer_machine()
    {
        using var world = await WorldAsync(sql);
        var email = NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        using var browser = world.Factory.Browser();

        // Locally it sends the invitation again, and answers the same for any address.
        using var sent = await browser.PostAsJsonAsync("/v1/dev/staff-invitations", new { email });
        using var unknown = await browser.PostAsJsonAsync("/v1/dev/staff-invitations", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(2, world.Factory.Mail.LinksSentTo(email));
        Assert.NotEqual(Guid.Empty, created.AccountId);

        foreach (var environment in new[] { "test", "production" })
        {
            using var deployed = new ApiFactory().With("Scs:ENVIRONMENT", environment);
            using var client = deployed.CreateHttpsClient();
            client.DefaultRequestHeaders.Add(RequestGuardExtensions.RequestHeader, "1");
            using var missing = await client.PostAsJsonAsync("/v1/dev/staff-invitations", new { email });
            Assert.True(
                missing.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
                $"{environment}: {(int)missing.StatusCode}");
            Assert.DoesNotContain(
                deployed.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints,
                e => e.DisplayName?.Contains("DevSendInvitation", StringComparison.Ordinal) == true
                     || (e as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText?.Contains("/dev/", StringComparison.Ordinal) == true);
        }
    }
}

/// <summary>
/// Spec 004, User Story 4 (FR-009 to FR-013, FR-021 to FR-024): a link works once, for a limited
/// time, and one that no longer works says which kind it was and never why.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordLinkLifeTests(SqlServerFixture sql)
{
    private static async Task AssertDeadAsync(HttpClient browser, string? token, string kind, string what)
    {
        using var opened = await browser.InspectLinkAsync(token);
        using var used = await browser.SetWithLinkAsync(token, Passwords.New);

        var answers = new List<string>();
        foreach (var response in new[] { opened, used })
        {
            var (status, code, body) = await response.ReadAsync();
            Assert.True(status == 410 && code == "LINK_INVALID", $"{what}: {status} {code}");
            Assert.Equal(kind, body.GetProperty("kind").GetString());
            Assert.Equal("انتهت صلاحية الرابط أو استُعمل", body.GetProperty("title").GetString());

            // Nothing but the kind: no reason, no account, no clinic.
            answers.Add(string.Join(',', body.EnumerateObject().Select(p => p.Name).Order()));
        }

        Assert.Equal("code,kind,status,title,traceId,type", Assert.Single(answers.Distinct()));
    }

    [Fact]
    public async Task A_reset_link_stops_after_thirty_minutes_after_use_and_when_a_newer_one_is_sent()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();

        // Too old, by one second.
        var late = await world.AccountAsync();
        var lateToken = await world.Factory.ResetTokenForAsync(world.Clinic, late.Email);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromSeconds(5));
        using (var justInTime = await browser.InspectLinkAsync(lateToken))
        {
            Assert.Equal(HttpStatusCode.OK, justInTime.StatusCode);
        }

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(6));
        await AssertDeadAsync(browser, lateToken, "reset", "expired");
        using (var oldStillWorks = await browser.StepOneAsync(late.Email, SignIn.Password))
        {
            Assert.Equal(HttpStatusCode.OK, oldStillWorks.StatusCode);
        }

        // Replaced by a newer one.
        var asker = await world.AccountAsync();
        var firstToken = await world.Factory.ResetTokenForAsync(world.Clinic, asker.Email);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        var secondToken = await world.Factory.ResetTokenForAsync(world.Clinic, asker.Email);
        await AssertDeadAsync(browser, firstToken, "reset", "replaced");

        // Used.
        using (var used = await browser.SetWithLinkAsync(secondToken, Passwords.New))
        {
            Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        }

        await AssertDeadAsync(browser, secondToken, "reset", "used");
    }

    [Fact]
    public async Task A_new_account_link_stops_after_seventy_two_hours_and_after_use()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();

        var email = ActivationTests.NewEmail();
        await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        var token = world.Factory.Mail.LatestLinkTokenFor(email);

        world.Factory.Clock.Advance(TimeSpan.FromHours(72) - TimeSpan.FromSeconds(5));
        using (var justInTime = await browser.InspectLinkAsync(token))
        {
            Assert.Equal(HttpStatusCode.OK, justInTime.StatusCode);
        }

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(6));
        await AssertDeadAsync(browser, token, "new-account", "expired");

        var second = ActivationTests.NewEmail();
        await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(second, default));
        var used = world.Factory.Mail.LatestLinkTokenFor(second);
        using (var activated = await browser.SetWithLinkAsync(used, Passwords.New))
        {
            Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        }

        await AssertDeadAsync(browser, used, "new-account", "used");
    }

    [Fact]
    public async Task A_link_stops_when_its_account_is_disabled_or_its_password_is_set_another_way()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();

        var disabled = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, disabled.Email);
        await world.Factory.ForClinicAsync(
            world.Clinic, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(disabled.Id, StaffAccountStates.Disabled, default));
        await AssertDeadAsync(browser, token, "reset", "disabled account");

        // Enabling it again does not bring the link back.
        await world.Factory.ForClinicAsync(
            world.Clinic, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(disabled.Id, StaffAccountStates.Active, default));
        await AssertDeadAsync(browser, token, "reset", "re-enabled account");

        // Someone at the clinic gives the account a new password while a link is out.
        var reissued = await world.AccountAsync();
        var outstanding = await world.Factory.ResetTokenForAsync(world.Clinic, reissued.Email);
        await world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(reissued.Id, HandoverMethods.Generated, null, default));
        await AssertDeadAsync(browser, outstanding, "reset", "password set another way");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task A_token_that_was_never_issued_is_answered_exactly_as_a_dead_reset_link(string? token)
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();

        await AssertDeadAsync(browser, token, "reset", "made-up token");
    }

    [Fact]
    public async Task An_altered_token_and_one_far_too_long_are_refused()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);

        var altered = token[..^3] + (token.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA");
        await AssertDeadAsync(browser, altered, "reset", "altered");
        await AssertDeadAsync(browser, new string('A', 5000), "reset", "too long");

        // The real one is none the worse for it.
        using var real = await browser.InspectLinkAsync(token);
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
    }

    [Fact]
    public async Task Two_submissions_of_one_link_at_the_same_moment_set_the_password_once()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var one = world.Factory.Browser();
        using var two = world.Factory.Browser();

        var responses = await Task.WhenAll(
            one.SetWithLinkAsync(token, "First-Winner-11!"),
            two.SetWithLinkAsync(token, "Second-Winner-22!"));

        Assert.Equal([200, 410], [.. responses.Select(r => (int)r.StatusCode).Order()]);
        var winner = responses[0].StatusCode == HttpStatusCode.OK ? "First-Winner-11!" : "Second-Winner-22!";
        var loser = winner == "First-Winner-11!" ? "Second-Winner-22!" : "First-Winner-11!";
        Array.ForEach(responses, r => r.Dispose());

        using var signedIn = await world.Factory.SignInAsAsync(account.Email, winner);
        using var refused = await one.StepOneAsync(account.Email, loser);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(
            1,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account.Id}' AND Kind = 'reset-completed'"));
    }

    [Fact]
    public async Task A_link_live_when_opened_and_expired_when_submitted_changes_nothing()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var browser = world.Factory.Browser();

        using (var opened = await browser.InspectLinkAsync(token))
        {
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        }

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using var late = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal("LINK_INVALID", (await late.ReadAsync()).Code);

        using var oldStillWorks = await browser.StepOneAsync(account.Email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, oldStillWorks.StatusCode);
    }

    [Fact]
    public async Task A_reset_link_survives_a_sign_in_with_the_old_password_and_a_suspended_clinic()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);

        // Remembering the old password after all does not cancel the link.
        using (await world.Factory.SignInAsAsync(account.Email)) { }
        using var browser = world.Factory.Browser();
        using (var stillLive = await browser.InspectLinkAsync(token))
        {
            Assert.Equal(HttpStatusCode.OK, stillLive.StatusCode);
        }

        // A suspended clinic's staff may still set a password; signing in is what is refused.
        await world.ExecuteAsync($"UPDATE tenancy.Clinic SET State = N'suspended' WHERE Id = '{world.Clinic}'");
        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        using var refused = await browser.StepOneAsync(account.Email, Passwords.New);
        Assert.Equal("CLINIC_SUSPENDED", (await refused.ReadAsync()).Code);
    }

    [Fact]
    public async Task A_link_of_one_clinic_reads_and_changes_nothing_of_another()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var otherClinic = await sql.Main.SeedClinicAsync();
        var mine = await world.AccountAsync();
        var theirs = await world.Factory.CreateAccountAsync(otherClinic);
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, mine.Email);
        using var browser = world.Factory.Browser();

        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        // Only the link's own account changed.
        using var untouched = await browser.StepOneAsync(theirs.Email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, untouched.StatusCode);
        Assert.Equal(
            0,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE ClinicId = '{otherClinic}' AND Kind LIKE 'reset-%'"));

        // The link's row is invisible from the other clinic, as every clinic-owned row is.
        await using var connection = await TestDatabase.OpenAsync(sql.Main.App, otherClinic);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [identity].PasswordLink";
        Assert.Equal(0, (int)(await command.ExecuteScalarAsync())!);
    }
}

/// <summary>
/// Spec 004, User Story 5 (FR-025 to FR-029): the four rules are the server's, whatever the
/// screen did, and a new password must differ from the one it replaces.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordRulesEndpointTests(SqlServerFixture sql)
{
    [Theory]
    [InlineData("Short1!aB", "TOO_SHORT")]
    [InlineData("alllowercase1!", "NO_UPPER_LOWER")]
    [InlineData("ALLUPPERCASE1!", "NO_UPPER_LOWER")]
    [InlineData("No-Digits-Here!", "NO_DIGIT")]
    [InlineData("NoSymbolHere12", "NO_SYMBOL")]
    [InlineData("Space only 123", "NO_SYMBOL")]
    [InlineData("", "REQUIRED")]
    [InlineData(null, "REQUIRED")]
    [InlineData("short", "TOO_SHORT,NO_UPPER_LOWER,NO_DIGIT,NO_SYMBOL")]
    public async Task A_password_that_breaks_a_rule_is_refused_naming_each_rule_and_the_link_stays_live(string? password, string codes)
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var browser = world.Factory.Browser();

        using var refused = await browser.SetWithLinkAsync(token, password);
        var (status, code, body) = await refused.ReadAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal(codes.Split(','), body.PasswordCodes());

        // Nothing changed: the old password still works and the link can still be used.
        using var old = await browser.StepOneAsync(account.Email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        using var accepted = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task A_password_over_two_hundred_characters_is_refused_and_letters_of_any_script_are_accepted()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var browser = world.Factory.Browser();

        using var tooLong = await browser.SetWithLinkAsync(token, "Aa1!" + new string('x', 197));
        Assert.Equal(["TOO_LONG"], (await tooLong.ReadAsync()).Body.PasswordCodes());

        const string Mixed = "كلمة سرّ Aa1! 🙂";
        using var accepted = await browser.SetWithLinkAsync(token, Mixed);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var signedIn = await world.Factory.SignInAsAsync(account.Email, Mixed);
    }

    [Fact]
    public async Task The_current_password_is_refused_with_its_own_code_and_the_link_stays_live()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var browser = world.Factory.Browser();

        using var same = await browser.SetWithLinkAsync(token, SignIn.Password);
        var (status, code, body) = await same.ReadAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal(["SAME_AS_CURRENT"], body.PasswordCodes());

        using var different = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, different.StatusCode);
    }

    [Fact]
    public async Task A_first_password_has_nothing_to_differ_from()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var email = ActivationTests.NewEmail();
        await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));
        using var browser = world.Factory.Browser();

        using var weak = await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), "weak");
        Assert.Equal(400, (int)weak.StatusCode);
        using var set = await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
    }
}
