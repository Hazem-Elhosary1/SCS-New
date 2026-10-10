using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, User Story 5 (FR-034, FR-039 to FR-041, SC-006): a session that was ended, for
/// whatever reason, is refused on its very next request.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SessionEndingTests(SqlServerFixture sql)
{
    private static readonly Uri Session = new("/v1/auth/session", UriKind.Relative);

    private static async Task<string> AskAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(Session);
        return await SignInCodeTests.CodeAsync(response);
    }

    [Fact]
    public async Task Sign_out_ends_that_session_only()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var desk = await world.Factory.SignInAsAsync(email);
        using var laptop = await world.Factory.SignInAsAsync(email);

        using var response = await desk.SignOutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("401 UNAUTHENTICATED", await AskAsync(desk));
        Assert.Equal("200", await AskAsync(laptop));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndReason = 'signed-out'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndedAtUtc IS NULL"));
        Assert.Equal(
            1,
            await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'session-ended' AND Detail = 'signed-out'"));
    }

    [Fact]
    public async Task Sign_out_succeeds_with_no_session_and_in_a_suspended_clinic()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using var nobody = world.Factory.Browser();
        using var none = await nobody.SignOutAsync();
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);

        using var signedIn = await world.Factory.SignInAsAsync(email);
        await world.ExecuteAsync($"UPDATE tenancy.Clinic SET State = 'suspended' WHERE Id = '{world.Clinic}'");
        using var response = await signedIn.SignOutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("401 UNAUTHENTICATED", await AskAsync(signedIn));
    }

    [Fact]
    public async Task Disabling_an_account_ends_its_sessions_at_once()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var desk = await world.Factory.SignInAsAsync(email);
        using var laptop = await world.Factory.SignInAsAsync(email);

        await world.Factory.ForClinicAsync(
            world.Clinic, services => services.GetRequiredService<IStaffAccounts>().SetStateAsync(id, StaffAccountStates.Disabled, default));

        Assert.Equal("401 SESSION_ENDED", await AskAsync(desk));
        Assert.Equal("401 SESSION_ENDED", await AskAsync(laptop));
        Assert.Equal(2, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndReason = 'disabled'"));
    }

    [Theory]
    [InlineData(SessionEndings.EndedForAccount)]
    [InlineData(SessionEndings.PasswordReset)]
    [InlineData(SessionEndings.RoleChanged)]
    public async Task Ending_an_accounts_sessions_ends_all_of_them_and_nobody_elses(string reason)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        var colleague = await world.AccountAsync();
        using var mine = await world.Factory.SignInAsAsync(email);
        using var theirs = await world.Factory.SignInAsAsync(colleague.Email);

        var ended = await world.Factory.ForClinicAsync(
            world.Clinic, services => services.GetRequiredService<IStaffSessions>().EndForAccountAsync(id, reason, default));

        Assert.Equal(1, ended);
        Assert.Equal("401 SESSION_ENDED", await AskAsync(mine));
        Assert.Equal("200", await AskAsync(theirs));
        Assert.Equal(reason, await world.ScalarAsync<string>($"SELECT EndReason FROM [identity].Session WHERE AccountId = '{id}'"));
    }

    [Theory]
    [InlineData(SessionEndings.EndedForClinic)]
    [InlineData(SessionEndings.ClinicSuspended)]
    public async Task Ending_everyones_sessions_leaves_only_the_one_that_asked(string reason)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var manager = await world.AccountAsync();
        var first = await world.AccountAsync();
        var second = await world.AccountAsync();
        using var managerBrowser = await world.Factory.SignInAsAsync(manager.Email);
        using var firstBrowser = await world.Factory.SignInAsAsync(first.Email);
        using var secondBrowser = await world.Factory.SignInAsAsync(second.Email);
        var keep = await world.ScalarAsync<Guid>($"SELECT Id FROM [identity].Session WHERE AccountId = '{manager.Id}'");

        var ended = await world.Factory.ForClinicAsync(
            world.Clinic, services => services.GetRequiredService<IStaffSessions>().EndForClinicAsync(keep, reason, default));

        Assert.Equal(2, ended);
        Assert.Equal("200", await AskAsync(managerBrowser));
        Assert.Equal("401 SESSION_ENDED", await AskAsync(firstBrowser));
        Assert.Equal("401 SESSION_ENDED", await AskAsync(secondBrowser));
    }

    [Fact]
    public async Task Operations_for_one_clinic_do_not_reach_another_clinics_accounts_or_sessions()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);
        var other = await sql.Main.SeedClinicAsync();

        var ended = await world.Factory.ForClinicAsync(other, async services =>
        {
            var sessions = services.GetRequiredService<IStaffSessions>();
            return await sessions.EndForAccountAsync(id, SessionEndings.EndedForAccount, default)
                   + await sessions.EndForClinicAsync(null, SessionEndings.EndedForClinic, default);
        });

        Assert.Equal(0, ended);
        Assert.Equal("200", await AskAsync(browser));

        foreach (var operation in new Func<IStaffAccounts, Task>[]
                 {
                     accounts => accounts.UnlockAsync(id, default),
                     accounts => accounts.SetStateAsync(id, StaffAccountStates.Disabled, default),
                 })
        {
            var refusal = await Assert.ThrowsAsync<Scs.BuildingBlocks.Errors.ScsProblemException>(
                () => world.Factory.ForClinicAsync(other, services => operation(services.GetRequiredService<IStaffAccounts>())));
            Assert.Equal("NOT_FOUND", refusal.Code);
        }
    }

    [Fact]
    public async Task A_session_is_over_when_its_maximum_life_has_passed()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);

        world.Factory.Clock.Advance(TimeSpan.FromHours(12.01));

        // The access cookie ran out long ago; renewal is what discovers the session is over.
        Assert.Equal("401 ACCESS_EXPIRED", await AskAsync(browser));
        using var renewal = await browser.RenewAsync();
        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(renewal));
        Assert.Equal("expired", await world.ScalarAsync<string>($"SELECT EndReason FROM [identity].Session WHERE AccountId = '{id}'"));
    }

    [Fact]
    public async Task A_suspended_clinics_sessions_stop_at_their_next_renewal_at_the_latest()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);

        // Marked directly, with no call to end the sessions (spec 016 will make that call).
        await world.ExecuteAsync($"UPDATE tenancy.Clinic SET State = 'suspended' WHERE Id = '{world.Clinic}'");
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(5.1));

        Assert.Equal("401 ACCESS_EXPIRED", await AskAsync(browser));
        using var renewal = await browser.RenewAsync();
        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(renewal));
        Assert.Equal("clinic-suspended", await world.ScalarAsync<string>($"SELECT EndReason FROM [identity].Session WHERE AccountId = '{id}'"));
    }

    [Fact]
    public async Task Sign_in_events_cannot_be_changed_or_removed_by_the_application()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);
        var where = $"WHERE AccountId = '{id}'";

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, $"UPDATE [identity].SignInEvent SET Kind = 'x' {where}", world.Clinic));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, $"DELETE FROM [identity].SignInEvent {where}", world.Clinic));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Report, $"DELETE FROM [identity].SignInEvent {where}", world.Clinic));

        // Not even the operator login may change one; it may only remove (retention).
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Crossing, $"UPDATE [identity].SignInEvent SET Kind = 'x' {where}"));
        Assert.Equal("signed-in", await world.ScalarAsync<string>($"SELECT Kind FROM [identity].SignInEvent {where}"));
    }
}

/// <summary>
/// Spec 003, User Story 6 (FR-037, FR-038, SC-007): quiet renewal, and a replaced secret shown
/// again after its grace is treated as theft.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RenewalTests(SqlServerFixture sql)
{
    private static readonly Uri Session = new("/v1/auth/session", UriKind.Relative);

    private static string? Cookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(v => v.StartsWith(name + "=", StringComparison.Ordinal)).Select(v => v.Split(';')[0][(name.Length + 1)..]).LastOrDefault()
            : null;

    /// <summary>Signs in by hand so the test holds the raw cookies, as a thief would.</summary>
    private static async Task<(string Access, string Renewal)> SignInRawAsync(RefusalWorld world, string email)
    {
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        using var second = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email));
        return (Cookie(second, SessionCookies.Access)!, Cookie(second, SessionCookies.Renewal)!);
    }

    private static async Task<HttpResponseMessage> RenewWithAsync(HttpClient client, string renewal)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/session/renew");
        request.Headers.Add("Cookie", $"{SessionCookies.Renewal}={renewal}");
        return await client.SendAsync(request);
    }

    private static HttpClient Bare(RefusalWorld world)
    {
        var client = world.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add(RequestGuardExtensions.RequestHeader, "1");
        return client;
    }

    [Fact]
    public async Task After_the_access_life_the_request_says_renew_and_renewing_carries_on()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);
        var before = await world.ScalarAsync<DateTimeOffset>($"SELECT LastUsedAtUtc FROM [identity].Session WHERE AccountId = '{id}'");

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(5.1));
        using var expired = await browser.GetAsync(Session);
        Assert.Equal("401 ACCESS_EXPIRED", await SignInCodeTests.CodeAsync(expired));

        using var renewed = await browser.RenewAsync();
        Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        Assert.NotNull(Cookie(renewed, SessionCookies.Access));
        Assert.NotNull(Cookie(renewed, SessionCookies.Renewal));

        using var after = await browser.GetAsync(Session);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.True(await world.ScalarAsync<DateTimeOffset>($"SELECT LastUsedAtUtc FROM [identity].Session WHERE AccountId = '{id}'") > before);
    }

    [Fact]
    public async Task A_replaced_secret_is_honoured_within_its_grace_and_ends_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        var (_, original) = await SignInRawAsync(world, email);
        using var client = Bare(world);

        using var first = await RenewWithAsync(client, original);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // A second tab, a moment later, still holding the earlier secret.
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        using var second = await RenewWithAsync(client, original);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.NotNull(Cookie(second, SessionCookies.Access));
        Assert.Null(Cookie(second, SessionCookies.Renewal));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndedAtUtc IS NULL"));
    }

    [Fact]
    public async Task A_replaced_secret_shown_after_its_grace_ends_every_session_of_the_account()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        var (_, stolen) = await SignInRawAsync(world, email);
        using var elsewhere = await world.Factory.SignInAsAsync(email);
        using var client = Bare(world);

        using var owner = await RenewWithAsync(client, stolen);
        var current = Cookie(owner, SessionCookies.Renewal)!;

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(31));
        using var thief = await RenewWithAsync(client, stolen);

        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(thief));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndedAtUtc IS NULL"));
        Assert.Equal(2, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndReason = 'renewal-reused'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'renewal-reused' AND Category = 'security'"));

        // The owner's own, newer secret no longer works either, nor the session on another device.
        using var ownerAgain = await RenewWithAsync(client, current);
        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(ownerAgain));
        using var other = await elsewhere.GetAsync(Session);
        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(other));
    }

    [Fact]
    public async Task Renewal_is_refused_for_a_disabled_account_and_for_anything_that_is_not_a_renewal_cookie()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        var (access, renewal) = await SignInRawAsync(world, email);
        using var client = Bare(world);

        foreach (var forged in new[] { "", "nonsense", access, renewal[..^4] + "AAAA" })
        {
            using var response = await RenewWithAsync(client, forged);
            Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(response));
        }

        // None of that ended the real session.
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndedAtUtc IS NULL"));

        await world.ExecuteAsync($"UPDATE [identity].StaffAccount SET State = 'disabled' WHERE Id = '{id}'");
        using var refused = await RenewWithAsync(client, renewal);
        Assert.Equal("401 SESSION_ENDED", await SignInCodeTests.CodeAsync(refused));
        Assert.Equal("disabled", await world.ScalarAsync<string>($"SELECT EndReason FROM [identity].Session WHERE AccountId = '{id}'"));
    }
}

/// <summary>
/// Spec 003, User Story 7 (SH-041, FR-044, FR-045) and research R10: the request limit, blocked
/// networks, and the check on requests that change something.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OuterDefenceTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_eleventh_sign_in_request_in_a_minute_is_refused_and_touches_no_account()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        // Ten requests across the three sign-in addresses share one allowance.
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        for (var i = 0; i < 4; i++)
        {
            using var code = await browser.StepTwoAsync("000000");
            using var resend = await browser.ResendAsync();
        }

        using var tenth = await browser.StepOneAsync($"nobody-{Guid.NewGuid():N}@clinic.example", "x");
        Assert.Equal(HttpStatusCode.Unauthorized, tenth.StatusCode);
        var attemptsBefore = await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'");
        var mailBefore = world.Factory.Mail.Sent.Count;

        using var eleventh = await browser.StepOneAsync(email, "Wrong-Password-1!");

        Assert.Equal("429 RATE_LIMITED", await SignInCodeTests.CodeAsync(eleventh));
        Assert.InRange(int.Parse(eleventh.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture), 1, 60);
        Assert.Equal(attemptsBefore, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal(mailBefore, world.Factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task Renewals_have_their_own_allowance()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);

        for (var i = 0; i < 30; i++)
        {
            using var renewed = await browser.RenewAsync();
            Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        }

        // The sign-in allowance was used twice (the two steps) and by none of the renewals.
        using var other = world.Factory.Browser();
        for (var i = 0; i < 8; i++)
        {
            using var response = await other.StepOneAsync($"nobody-{Guid.NewGuid():N}@clinic.example", "x");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_blocked_network_is_refused_before_anything_about_the_account()
    {
        var blocked = new TestBlockedNetworks { BlockEveryone = true };
        using var world = await RefusalWorld.CreateAsync(
            sql, f => f.WithServices(services => services.AddSingleton<IBlockedNetworks>(blocked)));
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        // More than the request limit: a blocked request is not even counted.
        for (var i = 0; i < 12; i++)
        {
            using var response = await browser.StepOneAsync(email, i % 2 == 0 ? SignIn.Password : "Wrong-Password-1!");
            Assert.Equal("403 NETWORK_BLOCKED", await SignInCodeTests.CodeAsync(response));
        }

        Assert.Empty(world.Factory.Mail.Sent);
        Assert.Equal((byte)0, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}'"));

        blocked.BlockEveryone = false;
        using var allowed = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task A_request_that_changes_something_needs_our_header_and_one_of_our_origins()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using var plain = world.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        using var withoutHeader = await plain.PostAsJsonAsync("/v1/auth/sign-in", new { email, password = SignIn.Password });
        Assert.Equal("403 FORBIDDEN", await SignInCodeTests.CodeAsync(withoutHeader));

        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new { email, password = SignIn.Password }),
        };
        foreign.Headers.Add(RequestGuardExtensions.RequestHeader, "1");
        foreign.Headers.Add("Origin", "https://evil.example");
        using var fromElsewhere = await plain.SendAsync(foreign);
        Assert.Equal("403 FORBIDDEN", await SignInCodeTests.CodeAsync(fromElsewhere));

        using var ours = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new { email, password = SignIn.Password }),
        };
        ours.Headers.Add(RequestGuardExtensions.RequestHeader, "1");
        ours.Headers.Add("Origin", "https://localhost");
        using var sameOrigin = await plain.SendAsync(ours);
        Assert.Equal(HttpStatusCode.OK, sameOrigin.StatusCode);

        // Reading needs neither.
        using var read = await plain.GetAsync(new Uri("/v1/meta/version", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Empty(world.Factory.Mail.Sent.Take(0));
    }
}
