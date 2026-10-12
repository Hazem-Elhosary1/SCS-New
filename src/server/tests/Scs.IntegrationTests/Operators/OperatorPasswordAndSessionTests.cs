using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 010, User Stories 5a and 5: the first operator chooses a password from an emailed
/// link, and a forgotten password is replaced through one.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorLinkTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_deployment_command_invites_and_the_operator_chooses_a_password_and_signs_in()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var email = $"first-{Guid.NewGuid():N}@heta.example";

        var invited = await world.Factory.InviteAsync("أحمد سامي", email);
        Assert.Equal(OperatorInvite.Invited, invited.Outcome);
        Assert.Empty(world.Factory.Mail.To(email));

        // The platform sends the invitation; the command never holds a link.
        Assert.True(await world.Factory.SendInvitationsAsync() >= 1);
        var message = Assert.Single(world.Factory.Mail.To(email));
        Assert.Contains(ApiFactory.OpsApp + "/password/first#token=", message.TextBody, StringComparison.Ordinal);
        Assert.Equal(0, await world.Factory.SendInvitationsAsync());

        // An invited account cannot sign in or ask for a reset.
        using var browser = world.Factory.Browser();
        using var early = await browser.OpsStepOneAsync(email);
        Assert.Equal("INVALID_CREDENTIALS", (await early.ReadAsync()).Code);
        using var forgot = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Single(world.Factory.Mail.To(email));

        var token = world.Factory.LatestLinkToken(email);
        using var read = await browser.OpsPostAsync("/v1/ops/password/link", new { token });
        var link = await read.ReadAsync();
        Assert.Equal(("first", email), (link.Body.GetProperty("kind").GetString(), link.Body.GetProperty("email").GetString()));

        using var weak = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = "Short-9!" });
        var problems = await weak.ReadAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (problems.Status, problems.Code));

        using var set = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = OpsWorld.Password });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        // It opened no session; the link works once; the account signs in like any other.
        using var none = await browser.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        using var twice = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = OpsWorld.Password });
        var gone = await twice.ReadAsync();
        Assert.Equal((410, "LINK_INVALID", "first"), (gone.Status, gone.Code, gone.Body.GetProperty("kind").GetString()));

        using var first = await browser.OpsStepOneAsync(email);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // For an account that is active the command changes nothing and says so.
        Assert.Equal(OperatorInvite.AlreadyActive, (await world.Factory.InviteAsync("أحمد سامي", email)).Outcome);
    }

    [Fact]
    public async Task Inviting_again_ends_the_earlier_link_withdrawing_removes_the_account_and_a_link_expires()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var email = $"first-{Guid.NewGuid():N}@heta.example";
        using var browser = world.Factory.Browser();

        await world.Factory.InviteAsync("منى يوسف", email);
        await world.Factory.SendInvitationsAsync();
        var earlier = world.Factory.LatestLinkToken(email);

        Assert.Equal(OperatorInvite.InvitedAgain, (await world.Factory.InviteAsync("منى يوسف", email)).Outcome);
        using var ended = await browser.OpsPostAsync("/v1/ops/password/link", new { token = earlier });
        Assert.Equal("LINK_INVALID", (await ended.ReadAsync()).Code);

        // An email that cannot leave stays owed, and nothing of its link is kept.
        world.Factory.Mail.Failing = true;
        Assert.Equal(0, await world.Factory.SendInvitationsAsync());
        world.Factory.Mail.Failing = false;
        Assert.Equal(1, await world.Factory.SendInvitationsAsync());
        var newer = world.Factory.LatestLinkToken(email);

        // Seventy-two hours later it no longer works.
        world.Factory.Clock.Advance(TimeSpan.FromHours(72.1));
        using var late = await browser.OpsPostAsync("/v1/ops/password/set", new { token = newer, password = OpsWorld.Password });
        Assert.Equal("LINK_INVALID", (await late.ReadAsync()).Code);

        // A mistaken invitation is taken back: the account is gone, with a record of both.
        await world.Factory.InviteAsync("منى يوسف", email);
        await world.Factory.SendInvitationsAsync();
        var last = world.Factory.LatestLinkToken(email);
        Assert.Equal(OperatorInvite.Withdrawn, (await world.Factory.WithdrawAsync(email)).Outcome);
        using var withdrawn = await browser.OpsPostAsync("/v1/ops/password/link", new { token = last });
        Assert.Equal("LINK_INVALID", (await withdrawn.ReadAsync()).Code);
        Assert.Equal(OperatorInvite.NothingToWithdraw, (await world.Factory.WithdrawAsync(email)).Outcome);
        Assert.Equal(OperatorInvite.NotInvited, (await world.Factory.WithdrawAsync(world.Email)).Outcome);

        var kinds = await world.Factory.OpsAsync(db => Task.FromResult(
            db.Records.Where(r => r.Facts == "test").Select(r => r.Kind).Distinct().ToList()));
        Assert.Contains(SignInRecordKinds.InvitedByDeployment, kinds);
        Assert.Contains(SignInRecordKinds.InviteWithdrawn, kinds);
    }

    [Fact]
    public async Task A_forgotten_password_is_replaced_through_a_link_which_ends_sessions_and_lifts_a_lock()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var signedIn = await world.SignInAsync();
        using var browser = world.Factory.Browser();

        // Lock the account, then ask: a locked account still gets its link.
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
        }

        var before = world.Factory.Mail.To(world.Email).Count;
        using var unknown = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = "nobody@heta.example" });
        using var asked = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted), (unknown.StatusCode, asked.StatusCode));
        Assert.Equal(before + 1, world.Factory.Mail.To(world.Email).Count);

        // A second within a minute sends none.
        using var again = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        Assert.Equal(before + 1, world.Factory.Mail.To(world.Email).Count);

        var token = world.Factory.LatestLinkToken(world.Email);
        Assert.Contains("/password/reset#token=", world.Factory.Mail.To(world.Email)[^1].TextBody, StringComparison.Ordinal);

        using var same = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = OpsWorld.Password });
        Assert.Equal(400, (await same.ReadAsync()).Status);

        const string Replaced = "Replaced-Horse-10!";
        using var set = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = Replaced });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.PasswordResetCompleted));

        // The lock is gone and the new password signs in.
        using var first = await browser.OpsStepOneAsync(world.Email, Replaced);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The reset link goes for a suspended account to nobody.
        var (_, suspended) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended);
        using var none = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = suspended });
        Assert.Empty(world.Factory.Mail.To(suspended));

        // Thirty minutes is the life of a reset link.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var more = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        var late = world.Factory.LatestLinkToken(world.Email);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(30.1));
        using var expired = await browser.OpsPostAsync("/v1/ops/password/link", new { token = late });
        var gone = await expired.ReadAsync();
        Assert.Equal((410, "reset"), (gone.Status, gone.Body.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task A_reset_signs_the_account_out_wherever_it_was_signed_in_and_says_why()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var signedIn = await world.SignInAsync();
        using var browser = world.Factory.Browser();

        using var asked = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        using var set = await browser.OpsPostAsync(
            "/v1/ops/password/set", new { token = world.Factory.LatestLinkToken(world.Email), password = "Replaced-Horse-10!" });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        using var next = await signedIn.OpsGetAsync("/v1/ops/profile");
        var read = await next.ReadAsync();
        Assert.Equal((401, "SESSION_ENDED", "password-reset"), (read.Status, read.Code, read.Body.GetProperty("reason").GetString()));

        // Told once: the cookies went with that answer.
        using var after = await signedIn.OpsGetAsync("/v1/ops/profile");
        Assert.Equal("UNAUTHENTICATED", (await after.ReadAsync()).Code);
    }
}

/// <summary>
/// Spec 010, User Stories 6 and 7: one session at a time, ending when it should; a stolen
/// session is detected; and the two consoles never accept each other's session.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorSessionTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_second_sign_in_ends_the_first_session_and_that_browser_is_told_why()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var first = await world.SignInAsync();
        var newBrowserMails = world.Factory.Mail.To(world.Email).Count(m => !SignIn.CodePattern().IsMatch(m.TextBody));
        Assert.Equal(1, newBrowserMails);

        using var second = await world.SignInAsync();

        var sessions = await world.SessionsAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Equal("replaced", sessions[0].EndedFor);
        Assert.Null(sessions[1].EndedAtUtc);

        using var told = await first.OpsGetAsync("/v1/ops/auth/session");
        var read = await told.ReadAsync();
        Assert.Equal((401, "SESSION_ENDED", "replaced"), (read.Status, read.Code, read.Body.GetProperty("reason").GetString()));

        using var alive = await second.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.Contains(await world.RecordsAsync(SignInRecordKinds.SessionEnded), r => r.Facts == "replaced");

        // Each new browser was told by email; the same browser signing in again is not.
        Assert.Equal(2, world.Factory.Mail.To(world.Email).Count(m => !SignIn.CodePattern().IsMatch(m.TextBody)));
        using var sameBrowser = await world.SignInAsync(browser: second);
        Assert.Equal(2, world.Factory.Mail.To(world.Email).Count(m => !SignIn.CodePattern().IsMatch(m.TextBody)));
    }

    [Fact]
    public async Task A_notice_that_cannot_be_sent_does_not_fail_the_sign_in()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();
        using var first = await browser.OpsStepOneAsync(world.Email);
        var code = world.Factory.Mail.LatestCodeFor(world.Email);

        world.Factory.Mail.Failing = true;
        using var second = await browser.OpsStepTwoAsync(code);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(await world.RecordsAsync(SignInRecordKinds.NewBrowser), r => r.Facts == "not sent");
    }

    [Fact]
    public async Task Signing_out_ends_the_session_at_once()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        using var out1 = await browser.OpsPostAsync("/v1/ops/auth/sign-out");
        Assert.Equal(HttpStatusCode.NoContent, out1.StatusCode);
        Assert.Equal("signed-out", Assert.Single(await world.SessionsAsync()).EndedFor);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.SignedOut));

        using var after = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal("UNAUTHENTICATED", (await after.ReadAsync()).Code);

        // Signing out with no session succeeds too.
        using var again = await browser.OpsPostAsync("/v1/ops/auth/sign-out");
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Thirty_idle_minutes_end_a_session_and_what_the_page_asks_by_itself_is_not_activity()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        // Twenty minutes, then the operator does something: the clock starts again.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await RenewAsync(browser);
        using var active = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);

        // Twenty more with only renewals and background questions: thirty-one since the last act.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await RenewAsync(browser);
        using var background = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/auth/session");
        background.Headers.Add(OperatorSessionAuthenticationHandler.BackgroundHeader, "1");
        using var polled = await browser.SendAsync(background);
        Assert.Equal(HttpStatusCode.OK, polled.StatusCode);

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(11));
        using var renewal = await browser.OpsPostAsync("/v1/ops/auth/session/renew");
        var read = await renewal.ReadAsync();
        Assert.Equal((401, "SESSION_ENDED", "idle"), (read.Status, read.Code, read.Body.GetProperty("reason").GetString()));
        Assert.Equal("idle", Assert.Single(await world.SessionsAsync()).EndedFor);

        static async Task RenewAsync(HttpClient client)
        {
            using var renewed = await client.OpsPostAsync("/v1/ops/auth/session/renew");
            Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        }
    }

    [Fact]
    public async Task Twelve_hours_end_a_session_however_active()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        for (var i = 0; i < 29; i++)
        {
            world.Factory.Clock.Advance(TimeSpan.FromMinutes(25));
            using var renewed = await browser.OpsPostAsync("/v1/ops/auth/session/renew");
            if (renewed.StatusCode != HttpStatusCode.NoContent)
            {
                var read = await renewed.ReadAsync();
                Assert.Equal("expired", read.Body.GetProperty("reason").GetString());
                Assert.True(i >= 28, $"The session ended after {i * 25} minutes.");
                return;
            }

            using var act = await browser.OpsGetAsync("/v1/ops/profile");
            Assert.Equal(HttpStatusCode.OK, act.StatusCode);
        }

        Assert.Fail("The session outlived twelve hours.");
    }

    [Fact]
    public async Task A_suspended_account_is_signed_out_on_its_next_request_through_the_contract()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        using (var scope = world.Factory.Services.CreateScope())
        {
            var ended = await scope.ServiceProvider.GetRequiredService<Scs.Operators.Contracts.IOperatorSessions>()
                .EndAllAsync(world.OperatorId, Scs.Operators.Contracts.OperatorSessionEndings.Suspended, CancellationToken.None);
            Assert.Equal(1, ended);
        }

        using var next = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal("suspended", (await next.ReadAsync()).Body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Remember_me_decides_whether_the_cookies_outlive_the_browser()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        async Task<string> SessionCookieAsync(bool remember)
        {
            using var browser = world.Factory.Browser();
            using var first = await browser.OpsStepOneAsync(world.Email, remember: remember);
            using var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(world.Email));
            return second.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(OperatorCookies.Access + "=", StringComparison.Ordinal));
        }

        Assert.DoesNotContain("expires=", await SessionCookieAsync(remember: false), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", await SessionCookieAsync(remember: true), StringComparison.OrdinalIgnoreCase);
        Assert.True((await world.SessionsAsync())[^1].Remember);
    }

    [Fact]
    public async Task An_old_renewal_secret_presented_again_ends_the_session_and_is_recorded()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();
        using var first = await browser.OpsStepOneAsync(world.Email);
        using var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(world.Email));
        var stolen = second.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(OperatorCookies.Renewal + "=", StringComparison.Ordinal)).Split(';')[0];

        using var renewed = await browser.OpsPostAsync("/v1/ops/auth/session/renew");
        Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);

        // After the grace of two tabs renewing together, the old value is someone else's copy.
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(31));
        using var thief = world.Factory.CreateHttpsClient();
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/ops/auth/session/renew");
        replay.Headers.Add("Cookie", stolen);
        replay.Headers.Add(RequestGuardExtensions.RequestHeader, "1");
        using var refused = await thief.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        Assert.Equal("reuse", Assert.Single(await world.SessionsAsync()).EndedFor);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.SessionTokenReuse));

        using var owner = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, owner.StatusCode);

        // The operator signs in again normally.
        using var again = await world.SignInAsync();
    }

    [Fact]
    public async Task Neither_console_accepts_the_other_s_session_and_a_forged_request_is_refused()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var operatorBrowser = await world.SignInAsync();

        var clinic = await sql.Main.SeedClinicAsync();
        using var staffBrowser = await world.Factory.SignInAsAsync(clinic);

        using var staffOnOps = await staffBrowser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal("UNAUTHENTICATED", (await staffOnOps.ReadAsync()).Code);
        using var opsOnStaff = await operatorBrowser.OpsGetAsync("/v1/auth/session");
        Assert.Equal("UNAUTHENTICATED", (await opsOnStaff.ReadAsync()).Code);

        // A change without the console's own header is refused, session or not.
        operatorBrowser.DefaultRequestHeaders.Remove(RequestGuardExtensions.RequestHeader);
        using var forged = await operatorBrowser.OpsPostAsync("/v1/ops/auth/sign-out-everywhere");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        using var forgedOpen = await operatorBrowser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        Assert.Equal(HttpStatusCode.Forbidden, forgedOpen.StatusCode);
        Assert.Null((await world.SessionsAsync())[^1].EndedAtUtc);
    }

    [Fact]
    public async Task The_eleventh_request_in_a_minute_from_one_address_is_refused_before_any_account_is_looked_at()
    {
        using var world = await OpsWorld.CreateAsync(sql, configure: factory => factory.With("Scs:AUTH:REQUESTS_PER_MINUTE", "10"));
        using var browser = world.Factory.Browser();

        for (var i = 0; i < 10; i++)
        {
            using var asked = await browser.OpsPostAsync("/v1/ops/password/link", new { token = "not-a-token" });
            Assert.Equal(HttpStatusCode.Gone, asked.StatusCode);
        }

        using var eleventh = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
        Assert.Equal((429, "RATE_LIMITED"), ((await eleventh.ReadAsync()) is var r ? (r.Status, r.Code) : default));
        Assert.Empty(await world.RecordsAsync(SignInRecordKinds.LoginFailed));
    }
}

/// <summary>
/// Spec 010, User Story 8 and the polish tasks: the operator's own profile, changing the
/// password with a code, signing out everywhere, the clean-up and what is never kept.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorProfileTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_profile_is_the_operator_s_own_account_and_recent_sessions()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var earlier = await world.SignInAsync();
        using var browser = await world.SignInAsync();

        using var response = await browser.OpsGetAsync("/v1/ops/profile");
        var profile = (await response.ReadAsync()).Body;
        Assert.Equal(world.Email, profile.GetProperty("email").GetString());
        Assert.Equal("مسؤول عام", profile.GetProperty("roleName").GetString());
        Assert.True(profile.GetProperty("twoFactor").GetProperty("on").GetBoolean());
        Assert.True(profile.GetProperty("twoFactor").GetProperty("locked").GetBoolean());

        var sessions = profile.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        Assert.True(sessions[0].GetProperty("current").GetBoolean());
        Assert.False(sessions[1].GetProperty("current").GetBoolean());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, sessions[1].GetProperty("endedAt").ValueKind);
        Assert.DoesNotContain("hash", profile.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var none = await world.Factory.Browser().OpsGetAsync("/v1/ops/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_asks_for_a_code_and_only_the_right_code_changes_it()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        const string Next = "Changed-Horse-11!";

        using var wrongCurrent = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = "Wrong-Horse-9!", password = Next });
        var wrong = await wrongCurrent.ReadAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (wrong.Status, wrong.Code));
        Assert.Contains("current", wrong.Body.GetProperty("errors").EnumerateObject().Select(p => p.Name));

        using var same = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = OpsWorld.Password, password = OpsWorld.Password });
        Assert.Equal(400, (await same.ReadAsync()).Status);
        using var weak = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = OpsWorld.Password, password = "short" });
        Assert.Equal(400, (await weak.ReadAsync()).Status);

        var mails = world.Factory.Mail.To(world.Email).Count;
        using var begun = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = OpsWorld.Password, password = Next });
        Assert.Equal(HttpStatusCode.Accepted, begun.StatusCode);
        Assert.Equal(mails + 1, world.Factory.Mail.To(world.Email).Count);
        var code = world.Factory.Mail.LatestCodeFor(world.Email);

        // Nothing has changed yet: the old password still signs in elsewhere.
        using var stillOld = await world.Factory.Browser().OpsStepOneAsync(world.Email);
        Assert.Equal(HttpStatusCode.OK, stillOld.StatusCode);

        using var wrongCode = await browser.OpsPostAsync("/v1/ops/profile/password/confirm", new { code = code == "000000" ? "000001" : "000000" });
        Assert.Equal("CODE_INVALID", (await wrongCode.ReadAsync()).Code);

        using var confirmed = await browser.OpsPostAsync("/v1/ops/profile/password/confirm", new { code });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.PasswordChanged));

        // This session stays open; the new password is the account's.
        using var alive = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        using var old = await world.Factory.Browser().OpsStepOneAsync(world.Email);
        Assert.Equal("INVALID_CREDENTIALS", (await old.ReadAsync()).Code);
        using var fresh = await world.Factory.Browser().OpsStepOneAsync(world.Email, Next);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_or_closing_the_dialog_abandon_the_change_without_locking()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        using var begun = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = OpsWorld.Password, password = "Changed-Horse-11!" });
        var code = world.Factory.Mail.LatestCodeFor(world.Email);
        var wrongCode = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.OpsPostAsync("/v1/ops/profile/password/confirm", new { code = wrongCode });
        }

        using var spent = await browser.OpsPostAsync("/v1/ops/profile/password/confirm", new { code });
        Assert.Equal("STEP_EXPIRED", (await spent.ReadAsync()).Code);
        Assert.Empty(await world.RecordsAsync(SignInRecordKinds.AccountLocked));

        using var again = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = OpsWorld.Password, password = "Changed-Horse-11!" });
        var second = world.Factory.Mail.LatestCodeFor(world.Email);
        using var cancelled = await browser.OpsPostAsync("/v1/ops/profile/password/cancel");
        using var after = await browser.OpsPostAsync("/v1/ops/profile/password/confirm", new { code = second });
        Assert.Equal("STEP_EXPIRED", (await after.ReadAsync()).Code);
        Assert.Empty(await world.RecordsAsync(SignInRecordKinds.PasswordChanged));
    }

    [Fact]
    public async Task The_fifth_wrong_current_password_locks_the_account_and_signs_the_operator_out()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        for (var i = 0; i < 4; i++)
        {
            using var wrong = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = "Wrong-Horse-9!", password = "Changed-Horse-11!" });
            Assert.Equal(400, (await wrong.ReadAsync()).Status);
        }

        using var fifth = await browser.OpsPostAsync("/v1/ops/profile/password", new { current = "Wrong-Horse-9!", password = "Changed-Horse-11!" });
        var read = await fifth.ReadAsync();
        Assert.Equal((401, "SESSION_ENDED", "locked"), (read.Status, read.Code, read.Body.GetProperty("reason").GetString()));
        Assert.Equal("locked", Assert.Single(await world.SessionsAsync()).EndedFor);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.AccountLocked));
    }

    [Fact]
    public async Task Signing_out_of_all_devices_ends_every_session_of_the_account()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();

        using var everywhere = await browser.OpsPostAsync("/v1/ops/auth/sign-out-everywhere");
        Assert.Equal(HttpStatusCode.NoContent, everywhere.StatusCode);
        Assert.Equal("signed-out-everywhere", Assert.Single(await world.SessionsAsync()).EndedFor);

        using var after = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal("UNAUTHENTICATED", (await after.ReadAsync()).Code);
    }

    [Fact]
    public async Task The_clean_up_removes_what_is_over_and_a_record_is_kept_as_written()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        using var out1 = await browser.OpsPostAsync("/v1/ops/auth/sign-out");

        async Task<int> CleanAsync()
        {
            using var scope = world.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OperatorsCleanupJob>().RunAsync(CancellationToken.None);
        }

        // A record cannot be changed, nor removed while it is young.
        var record = (await world.RecordsAsync()).First();
        await Assert.ThrowsAnyAsync<Exception>(() => world.Factory.OpsSqlAsync(
            $"UPDATE [ops].[SignInRecord] SET [Facts] = 'changed' WHERE [OperatorId] = '{world.OperatorId}'"));
        await Assert.ThrowsAnyAsync<Exception>(() => world.Factory.OpsSqlAsync(
            $"DELETE FROM [ops].[SignInRecord] WHERE [OperatorId] = '{world.OperatorId}'"));
        Assert.NotNull(record);

        // Two days later the spent code is gone; the session and the records stay.
        world.Factory.Clock.Advance(TimeSpan.FromDays(2));
        await CleanAsync();
        Assert.Equal(0, await world.Factory.OpsAsync(db => Task.FromResult(db.Challenges.Count(c => c.OperatorId == world.OperatorId))));
        Assert.Single(await world.SessionsAsync());
        Assert.NotEmpty(await world.RecordsAsync());

        // Ninety-one days later the ended session is gone.
        world.Factory.Clock.Advance(TimeSpan.FromDays(91));
        await CleanAsync();
        Assert.Empty(await world.SessionsAsync());
    }

    [Fact]
    public async Task No_password_code_link_or_address_reaches_the_logs_and_only_hashes_are_kept()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        var code = world.Factory.Mail.To(world.Email)
            .Select(m => SignIn.CodePattern().Match(m.TextBody).Value).First(v => v.Length == 6);
        using var asked = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        var token = world.Factory.LatestLinkToken(world.Email);
        using var wrong = await world.Factory.Browser().OpsStepOneAsync(world.Email, "Wrong-Horse-9!");

        var logs = world.Factory.Logs.All;
        Assert.DoesNotContain(OpsWorld.Password, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Wrong-Horse-9!", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(token, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(world.Email, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"\"{code}\"", logs, StringComparison.Ordinal);

        var facts = (await world.RecordsAsync()).Select(r => r.Facts ?? "").ToList();
        Assert.DoesNotContain(facts, f => f.Contains(world.Email, StringComparison.OrdinalIgnoreCase) || f.Contains(code, StringComparison.Ordinal));

        var hash = await world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Single(o => o.Id == world.OperatorId).PasswordHash));
        Assert.StartsWith("v1.", hash, StringComparison.Ordinal);
    }
}

/// <summary>
/// Spec 010, convergence: two sign-ins of one account that complete at the same moment, and a
/// deployment command that changes nothing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorConvergenceTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Two_sign_ins_completing_together_leave_one_session_and_the_other_browser_is_told_why()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        // Several rounds, since the two must meet inside one moment for the fault to show.
        for (var round = 0; round < 6; round++)
        {
            using var one = world.Factory.Browser();
            using var two = world.Factory.Browser();

            using var askedOne = await one.OpsStepOneAsync(world.Email);
            var codeOne = world.Factory.Mail.LatestCodeFor(world.Email);
            using var askedTwo = await two.OpsStepOneAsync(world.Email);
            var codeTwo = world.Factory.Mail.LatestCodeFor(world.Email);
            Assert.NotEqual(codeOne, codeTwo);

            var both = await Task.WhenAll(one.OpsStepTwoAsync(codeOne), two.OpsStepTwoAsync(codeTwo));
            Assert.All(both, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Array.ForEach(both, response => response.Dispose());

            // One session is open; every other one of the account ended as "replaced".
            var sessions = await world.SessionsAsync();
            Assert.Equal(2 * (round + 1), sessions.Count);
            Assert.Single(sessions, s => s.EndedAtUtc is null);
            Assert.All(sessions.Where(s => s.EndedAtUtc is not null), s => Assert.Equal("replaced", s.EndedFor));

            // One browser is inside; the other is told it was signed in elsewhere.
            using var readOne = await one.OpsGetAsync("/v1/ops/auth/session");
            using var readTwo = await two.OpsGetAsync("/v1/ops/auth/session");
            var answers = new[] { await readOne.ReadAsync(), await readTwo.ReadAsync() };
            Assert.Single(answers, a => a.Status == 200);
            var told = Assert.Single(answers, a => a.Status == 401);
            Assert.Equal(("SESSION_ENDED", "replaced"), (told.Code, told.Body.GetProperty("reason").GetString()));
        }
    }

    [Fact]
    public async Task A_command_that_changes_nothing_is_recorded_with_who_ran_it_and_not_the_email()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        // The account is active: inviting it again and withdrawing it both change nothing.
        Assert.Equal(OperatorInvite.AlreadyActive, (await world.Factory.InviteAsync("سارة أحمد", world.Email)).Outcome);
        Assert.Equal(OperatorInvite.NotInvited, (await world.Factory.WithdrawAsync(world.Email)).Outcome);

        var lines = await world.RecordsAsync(SignInRecordKinds.CommandChangedNothing);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith(OperatorInvite.AlreadyActive + ";", lines[0].Facts, StringComparison.Ordinal);
        Assert.StartsWith(OperatorInvite.NotInvited + ";", lines[1].Facts, StringComparison.Ordinal);
        Assert.All(lines, line => Assert.DoesNotContain(world.Email, line.Facts!, StringComparison.Ordinal));

        // Withdrawing an email nobody has: recorded with no account.
        var nobody = $"nobody-{Guid.NewGuid():N}@heta.example";
        Assert.Equal(OperatorInvite.NothingToWithdraw, (await world.Factory.WithdrawAsync(nobody)).Outcome);
        var unowned = await world.Factory.OpsAsync(db => db.Records
            .Where(r => r.Kind == SignInRecordKinds.CommandChangedNothing && r.OperatorId == null)
            .Select(r => r.Facts)
            .ToListAsync());
        Assert.Contains(unowned, facts => facts!.StartsWith(OperatorInvite.NothingToWithdraw + ";", StringComparison.Ordinal));
        Assert.DoesNotContain(unowned, facts => facts!.Contains(nobody, StringComparison.Ordinal));
    }
}
