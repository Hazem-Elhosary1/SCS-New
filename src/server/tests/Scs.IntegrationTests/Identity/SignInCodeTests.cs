using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, User Story 4 (SH-040, FR-022 to FR-032): the code is six digits, lasts ten minutes,
/// allows five tries, works once, and every way of getting it wrong has one answer.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SignInCodeTests(SqlServerFixture sql)
{
    private static readonly Action<ApiFactory> Roomy = f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120");

    internal static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (text.Length == 0)
        {
            return $"{(int)response.StatusCode}";
        }

        using var body = JsonDocument.Parse(text);
        return body.RootElement.TryGetProperty("code", out var code)
            ? $"{(int)response.StatusCode} {code.GetString()}"
            : $"{(int)response.StatusCode}";
    }

    private static string WrongCodeFor(string right) => right == "000000" ? "000001" : "000000";

    [Fact]
    public async Task Every_way_of_getting_the_code_wrong_has_one_answer()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (_, email) = await world.AccountAsync();
        var bodies = new List<string>();

        async Task CollectAsync(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();
            using var body = JsonDocument.Parse(text);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("CODE_INVALID", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.", body.RootElement.GetProperty("title").GetString());
            bodies.Add(text.Replace(body.RootElement.GetProperty("traceId").GetString()!, "", StringComparison.Ordinal));
        }

        // Wrong.
        using (var browser = world.Factory.Browser())
        {
            using var first = await browser.StepOneAsync(email, SignIn.Password);
            using var wrong = await browser.StepTwoAsync(WrongCodeFor(world.Factory.Mail.LatestCodeFor(email)));
            await CollectAsync(wrong);
        }

        // Expired: the right code, eleven minutes later.
        using (var browser = world.Factory.Browser())
        {
            using var first = await browser.StepOneAsync(email, SignIn.Password);
            var code = world.Factory.Mail.LatestCodeFor(email);
            world.Factory.Clock.Advance(TimeSpan.FromMinutes(10.1));
            using var late = await browser.StepTwoAsync(code);
            await CollectAsync(late);
        }

        // Used: the right code, a second time.
        using (var browser = world.Factory.Browser())
        {
            using var first = await browser.StepOneAsync(email, SignIn.Password);
            var code = world.Factory.Mail.LatestCodeFor(email);
            using var accepted = await browser.StepTwoAsync(code);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

            using var other = world.Factory.Browser();
            using var again = await other.StepTwoAsync(code);
            await CollectAsync(again);
        }

        // No pending sign-in at all, and a malformed code.
        using (var browser = world.Factory.Browser())
        {
            using var none = await browser.StepTwoAsync("123456");
            await CollectAsync(none);
            using var first = await browser.StepOneAsync(email, SignIn.Password);
            using var malformed = await browser.StepTwoAsync("12ab");
            await CollectAsync(malformed);
        }

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task The_sixth_try_fails_even_with_the_right_code_and_one_spent_code_does_not_lock()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var code = world.Factory.Mail.LatestCodeFor(email);

        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.StepTwoAsync(WrongCodeFor(code));
            Assert.Equal("401 CODE_INVALID", await CodeAsync(wrong));
        }

        using var sixth = await browser.StepTwoAsync(code);
        Assert.Equal("401 CODE_INVALID", await CodeAsync(sixth));

        Assert.Equal(5, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'wrong-code'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'code-spent'"));
        Assert.Null(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal((byte)1, await world.ScalarAsync<byte>($"SELECT SpentCodes FROM [identity].StaffAccount WHERE Id = '{id}'"));

        // Starting again works.
        using var restart = await browser.StepOneAsync(email, SignIn.Password);
        using var accepted = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal((byte)0, await world.ScalarAsync<byte>($"SELECT SpentCodes FROM [identity].StaffAccount WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task The_third_code_spent_in_a_row_locks_the_account()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (id, email) = await world.AccountAsync();
        using var signedIn = await world.Factory.SignInAsAsync(email);
        using var browser = world.Factory.Browser();

        for (var round = 1; round <= 3; round++)
        {
            using var first = await browser.StepOneAsync(email, SignIn.Password);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var wrong = WrongCodeFor(world.Factory.Mail.LatestCodeFor(email));
            for (var i = 0; i < 5; i++)
            {
                using var response = await browser.StepTwoAsync(wrong);
            }
        }

        Assert.NotNull(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'account-locked'"));

        using var locked = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal("403 ACCOUNT_LOCKED", await CodeAsync(locked));

        using var session = await signedIn.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal("401 SESSION_ENDED", await CodeAsync(session));
    }

    [Fact]
    public async Task Twenty_confirmations_of_the_right_code_open_one_session()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var code = world.Factory.Mail.LatestCodeFor(email);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => browser.StepTwoAsync(code)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}'"));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("suspended")]
    public async Task What_changed_between_the_two_steps_is_checked_again(string change)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var code = world.Factory.Mail.LatestCodeFor(email);

        if (change == "disabled")
        {
            await world.Factory.ForClinicAsync(
                world.Clinic, services => services.GetRequiredService<IStaffAccounts>().SetStateAsync(id, StaffAccountStates.Disabled, default));
        }
        else
        {
            await world.ExecuteAsync($"UPDATE tenancy.Clinic SET State = 'suspended' WHERE Id = '{world.Clinic}'");
        }

        using var response = await browser.StepTwoAsync(code);

        Assert.Equal(change == "disabled" ? "403 ACCOUNT_DISABLED" : "403 CLINIC_SUSPENDED", await CodeAsync(response));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}'"));
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith(SessionCookies.Challenge + "=;", StringComparison.Ordinal));
    }
}

/// <summary>Spec 003, FR-026 to FR-028: sending the code again.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResendTests(SqlServerFixture sql)
{
    private static readonly Action<ApiFactory> Roomy = f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120");

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    [Fact]
    public async Task Before_the_countdown_ends_a_resend_is_too_early()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (_, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(20));
        using var response = await browser.ResendAsync();

        Assert.Equal("409 RESEND_TOO_EARLY", await SignInCodeTests.CodeAsync(response));
        Assert.InRange((await JsonAsync(response)).GetProperty("resendAvailableInSeconds").GetInt32(), 38, 40);
        Assert.Single(world.Factory.Mail.To(email));
    }

    [Fact]
    public async Task A_resend_sends_a_new_code_cancels_the_earlier_one_and_does_not_extend_the_sign_in()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (_, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var earlier = world.Factory.Mail.LatestCodeFor(email);

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var response = await browser.ResendAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(60, body.GetProperty("resendAvailableInSeconds").GetInt32());
        Assert.Equal(2, body.GetProperty("resendsLeft").GetInt32());
        Assert.InRange(body.GetProperty("expiresInSeconds").GetInt32(), 530, 540);
        Assert.Equal(2, world.Factory.Mail.To(email).Count);

        var newer = world.Factory.Mail.LatestCodeFor(email);
        if (newer != earlier)
        {
            using var old = await browser.StepTwoAsync(earlier);
            Assert.Equal("401 CODE_INVALID", await SignInCodeTests.CodeAsync(old));
        }

        using var accepted = await browser.StepTwoAsync(newer);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task After_three_resends_no_more_are_sent_and_the_last_code_still_works()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (_, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);

        foreach (var left in new[] { 2, 1, 0 })
        {
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            using var response = await browser.ResendAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(left, (await JsonAsync(response)).GetProperty("resendsLeft").GetInt32());
        }

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var fourth = await browser.ResendAsync();
        Assert.Equal("409 RESEND_LIMIT_REACHED", await SignInCodeTests.CodeAsync(fourth));
        Assert.Equal(4, world.Factory.Mail.To(email).Count);

        using var accepted = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // A new correct password starts a sign-in with three resends of its own.
        using var again = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal(3, (await JsonAsync(again)).GetProperty("resendsLeft").GetInt32());
    }

    [Fact]
    public async Task When_a_resend_cannot_be_sent_the_earlier_code_stays_valid_and_no_resend_is_used_up()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (_, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var earlier = world.Factory.Mail.LatestCodeFor(email);

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        world.Factory.Mail.Failing = true;
        using var failed = await browser.ResendAsync();
        Assert.Equal("503 SIGN_IN_CODE_NOT_RESENT", await SignInCodeTests.CodeAsync(failed));

        world.Factory.Mail.Failing = false;
        using var retried = await browser.ResendAsync();
        Assert.Equal(2, (await JsonAsync(retried)).GetProperty("resendsLeft").GetInt32());

        // And when it failed, the earlier code was still good: shown on a fresh sign-in.
        using var other = world.Factory.Browser();
        using var again = await other.StepOneAsync(email, SignIn.Password);
        var code = world.Factory.Mail.LatestCodeFor(email);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        world.Factory.Mail.Failing = true;
        using var failedAgain = await other.ResendAsync();
        using var accepted = await other.StepTwoAsync(code);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.NotNull(earlier);
    }

    [Fact]
    public async Task When_the_first_code_cannot_be_sent_there_is_no_second_step()
    {
        using var world = await RefusalWorld.CreateAsync(sql, Roomy);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        world.Factory.Mail.Failing = true;

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal("503 SIGN_IN_CODE_NOT_SENT", await SignInCodeTests.CodeAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInChallenge WHERE AccountId = '{id}'"));

        using var resend = await browser.ResendAsync();
        Assert.Equal("401 CODE_INVALID", await SignInCodeTests.CodeAsync(resend));
    }
}

/// <summary>Spec 003, FR-022: the second step can be switched off for the whole platform only.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmailCodeSwitchTests(SqlServerFixture sql)
{
    [Fact]
    public async Task With_the_switch_off_a_correct_password_opens_the_session_and_refusals_are_unchanged()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:EMAIL_CODE_ENABLED", "false"));
        var (id, email) = await world.AccountAsync();
        var disabled = await world.AccountAsync(StaffAccountStates.Disabled);
        using var browser = world.Factory.Browser();

        using var wrong = await browser.StepOneAsync(email, "Wrong-Password-1!");
        Assert.Equal("401 INVALID_CREDENTIALS", await SignInCodeTests.CodeAsync(wrong));
        using var refused = await browser.StepOneAsync(disabled.Email, SignIn.Password);
        Assert.Equal("403 ACCOUNT_DISABLED", await SignInCodeTests.CodeAsync(refused));

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, body.RootElement.GetProperty("accountId").GetGuid());
        Assert.Empty(world.Factory.Mail.Sent);
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInChallenge WHERE AccountId = '{id}'"));

        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }
}
