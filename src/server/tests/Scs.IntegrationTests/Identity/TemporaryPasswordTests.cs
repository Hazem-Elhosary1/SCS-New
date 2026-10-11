using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 004, User Story 6 (FR-031, FR-035 to FR-038): a password chosen by someone else must be
/// replaced by its owner before anything else, and stops working if nobody does in time.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TemporaryPasswordTests(SqlServerFixture sql)
{
    private const string Typed = "Typed-By-Manager-5!";

    private static async Task<(Guid Id, string Email)> TemporaryAccountAsync(RefusalWorld world)
    {
        var email = ActivationTests.NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithTypedPasswordAsync(email, Typed, default));
        return (created.AccountId, email);
    }

    [Fact]
    public async Task The_owner_is_held_until_the_password_is_replaced_and_then_continues_in_the_same_session()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await TemporaryAccountAsync(world);
        using var browser = await world.Factory.SignInAsAsync(account.Email, Typed);
        using var elsewhere = await world.Factory.SignInAsAsync(account.Email, Typed);

        using (var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative)))
        {
            Assert.True((await session.ReadAsync()).Body.GetProperty("mustChangePassword").GetBoolean());
        }

        using (var held = await browser.GetAsync(new Uri("/probe/whoami", UriKind.Relative)))
        {
            Assert.Equal((403, "PASSWORD_CHANGE_REQUIRED"), ((int)held.StatusCode, (await held.ReadAsync()).Code));
        }

        // The temporary password itself, and a password that breaks a rule, change nothing.
        using (var same = await browser.ChangePasswordAsync(Typed))
        {
            Assert.Equal(["SAME_AS_CURRENT"], (await same.ReadAsync()).Body.PasswordCodes());
        }

        using (var weak = await browser.ChangePasswordAsync("weak"))
        {
            Assert.Equal(["TOO_SHORT", "NO_UPPER_LOWER", "NO_DIGIT", "NO_SYMBOL"], (await weak.ReadAsync()).Body.PasswordCodes());
        }

        Assert.True(await world.ScalarAsync<bool>($"SELECT MustChangePassword FROM [identity].StaffAccount WHERE Id = '{account.Id}'"));

        using (var changed = await browser.ChangePasswordAsync(Passwords.New))
        {
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            Assert.False(changed.Headers.Contains("Set-Cookie"));
        }

        // The same session, now free; the other browser's session is over.
        using (var free = await browser.GetAsync(new Uri("/probe/whoami", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        }

        using (var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative)))
        {
            Assert.False((await session.ReadAsync()).Body.GetProperty("mustChangePassword").GetBoolean());
        }

        using (var other = await elsewhere.GetAsync(new Uri("/v1/auth/session", UriKind.Relative)))
        {
            Assert.Equal("SESSION_ENDED", (await other.ReadAsync()).Code);
        }

        Assert.Equal(
            "0||1",
            await world.ScalarAsync<string>(
                $"""
                 SELECT CONCAT(MustChangePassword, '|', TemporaryPasswordExpiresAtUtc, '|',
                     (SELECT COUNT(*) FROM [identity].SignInEvent e WHERE e.AccountId = a.Id AND e.Kind = 'temp-password-changed' AND e.Category = 'activity'))
                 FROM [identity].StaffAccount a WHERE Id = '{account.Id}'
                 """));

        // There is nothing left to replace here; and the next sign-in asks for nothing.
        using (var again = await browser.ChangePasswordAsync("Yet-Another-One-3!"))
        {
            Assert.Equal((409, "PASSWORD_CHANGE_NOT_REQUIRED"), ((int)again.StatusCode, (await again.ReadAsync()).Code));
        }

        using var next = await world.Factory.SignInAsAsync(account.Email, Passwords.New);
        using var open = await next.GetAsync(new Uri("/probe/whoami", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
    }

    [Fact]
    public async Task A_temporary_password_older_than_seventy_two_hours_is_refused_as_a_wrong_one()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await TemporaryAccountAsync(world);
        using var early = await world.Factory.SignInAsAsync(account.Email, Typed);

        world.Factory.Clock.Advance(TimeSpan.FromHours(72) + TimeSpan.FromSeconds(1));
        using var browser = world.Factory.Browser();

        using var refused = await browser.StepOneAsync(account.Email, Typed);
        var (status, code, body) = await refused.ReadAsync();
        Assert.Equal((401, "INVALID_CREDENTIALS"), (status, code));
        Assert.Equal("code,status,title,traceId,type", string.Join(',', body.EnumerateObject().Select(p => p.Name).Order()));

        // It counts toward the lock like any wrong password.
        Assert.Equal((byte)1, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{account.Id}'"));

        // The account is still active: its owner can ask for a link, which also clears the mark.
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.False(await world.ScalarAsync<bool>($"SELECT MustChangePassword FROM [identity].StaffAccount WHERE Id = '{account.Id}'"));
        using var signedIn = await world.Factory.SignInAsAsync(account.Email, Passwords.New);
        early.Dispose();
    }

    [Fact]
    public async Task A_person_already_on_the_screen_when_the_time_runs_out_can_still_finish()
    {
        // A session lasts twelve hours at most, so the temporary password is given one hour.
        using var world = await RefusalWorld.CreateAsync(
            sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120").With("Scs:AUTH:TEMP_PASSWORD_HOURS", "1"));
        var account = await TemporaryAccountAsync(world);
        using var browser = await world.Factory.SignInAsAsync(account.Email, Typed);

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(62));
        using (var renewed = await browser.RenewAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        }

        using var changed = await browser.ChangePasswordAsync(Passwords.New);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
    }

    [Fact]
    public async Task Changing_needs_a_session()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        using var browser = world.Factory.Browser();

        using var refused = await browser.ChangePasswordAsync(Passwords.New);

        Assert.Equal((401, "UNAUTHENTICATED"), ((int)refused.StatusCode, (await refused.ReadAsync()).Code));
    }
}

/// <summary>
/// Spec 004, FR-036 and SC-007: with a temporary password, every endpoint that needs a session
/// is refused except the two that are marked — including endpoints written by later specs.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TemporaryPasswordGateTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Only_reading_the_session_and_changing_the_password_are_reachable()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var email = ActivationTests.NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithGeneratedPasswordAsync(email, default));
        using var held = await world.Factory.SignInAsAsync(email, created.GeneratedPassword!);
        using var ordinary = await world.Factory.SignInAsAsync(world.Clinic);

        var endpoints = world.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();

        // The test means something only if there are session endpoints beyond this module's own.
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/probe/whoami");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/probe/protected");

        var marked = endpoints
            .Where(e => e.Metadata.GetMetadata<AllowsTemporaryPasswordMetadata>() is not null)
            .Select(e => e.RoutePattern.RawText)
            .Order()
            .ToList();
        Assert.Equal(["/v1/auth/password/change", "/v1/auth/session"], marked);

        foreach (var endpoint in endpoints.Where(e => e.Metadata.GetMetadata<AllowsTemporaryPasswordMetadata>() is null))
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            var method = methods is { Count: > 0 } ? methods[0] : "GET";
            var path = (endpoint.RoutePattern.RawText ?? "/").Replace("{id}", "1", StringComparison.Ordinal);
            if (path.Contains('{', StringComparison.Ordinal))
            {
                continue;
            }

            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
            using var response = await held.SendAsync(request);
            var (status, code, _) = await response.ReadAsync();
            Assert.True(status == 403 && code == "PASSWORD_CHANGE_REQUIRED", $"{method} {path}: {status} {code}");
        }

        // What needs no session is nobody's business; renewing and signing out stay possible.
        using (var open = await held.GetAsync(new Uri("/probe/open", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        }

        using (var support = await held.GetAsync(new Uri("/v1/meta/support", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, support.StatusCode);
        }

        using (var renewed = await held.RenewAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        }

        // An ordinary session is never touched by the gate.
        using (var free = await ordinary.GetAsync(new Uri("/probe/whoami", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        }

        using (var signedOut = await held.SignOutAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        }
    }
}

/// <summary>
/// Spec 004, FR-030 to FR-034 and FR-044: handing an account over in the three ways of SH-044,
/// and handing it over again — the operations specs 015 and 026 will call.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountHandoverTests(SqlServerFixture sql)
{
    private static Task<string?> AccountAsync(RefusalWorld world, Guid id) => world.ScalarAsync<string>(
        $"""
         SELECT CONCAT(State, '|', MustChangePassword, '|',
             CASE WHEN TemporaryPasswordExpiresAtUtc IS NULL THEN 'none'
                  ELSE CAST((DATEDIFF(MINUTE, CreatedAtUtc, TemporaryPasswordExpiresAtUtc) + 1) / 60 AS varchar(10)) END)
         FROM [identity].StaffAccount WHERE Id = '{id}'
         """);

    [Fact]
    public async Task A_generated_password_is_returned_once_meets_the_rules_and_is_kept_nowhere()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var email = ActivationTests.NewEmail();

        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithGeneratedPasswordAsync(email, default));
        var password = created.GeneratedPassword!;

        Assert.True(created.EmailSent);
        Assert.Equal(14, password.Length);
        Assert.Equal("active|1|72", await AccountAsync(world, created.AccountId));
        Assert.DoesNotContain(password, created.ToString(), StringComparison.Ordinal);

        // The owner is told an account exists and where to sign in — never the password.
        var message = Assert.Single(world.Factory.Mail.To(email));
        Assert.Contains($"{ApiFactory.StaffApp}/sign-in", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("عيادة تجريبية", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(password, message.TextBody + message.HtmlBody + message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("#t=", message.TextBody, StringComparison.Ordinal);

        Assert.DoesNotContain(password, world.Factory.Logs.All, StringComparison.Ordinal);
        Assert.Equal(
            0,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].StaffAccount WHERE Id = '{created.AccountId}' AND PasswordHash LIKE '%{password.Replace("'", "''", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal)}%'"));

        using var signedIn = await world.Factory.SignInAsAsync(email, password);
    }

    [Fact]
    public async Task A_typed_password_must_meet_the_rules_and_is_temporary_too()
    {
        using var world = await ActivationTests.WorldAsync(sql);

        var failure = await Assert.ThrowsAsync<ScsProblemException>(
            () => world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithTypedPasswordAsync(ActivationTests.NewEmail(), "weak", default)));
        Assert.Equal(["TOO_SHORT", "NO_UPPER_LOWER", "NO_DIGIT", "NO_SYMBOL"], failure.Errors!["password"]);
        Assert.Empty(world.Factory.Mail.Sent);

        var email = ActivationTests.NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithTypedPasswordAsync(email, "Typed-By-Manager-5!", default));
        Assert.Null(created.GeneratedPassword);
        Assert.Equal("active|1|72", await AccountAsync(world, created.AccountId));
        Assert.DoesNotContain("Typed-By-Manager-5!", Assert.Single(world.Factory.Mail.To(email)).TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_the_account_created_email_fails_the_account_stands_and_the_caller_is_told()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        world.Factory.Mail.Failing = true;

        var created = await world.Factory.HandOverAsync(
            world.Clinic, h => h.CreateWithGeneratedPasswordAsync(ActivationTests.NewEmail(), default));

        Assert.False(created.EmailSent);
        Assert.NotNull(created.GeneratedPassword);
        Assert.Equal("active|1|72", await AccountAsync(world, created.AccountId));
    }

    [Theory]
    [InlineData(HandoverMethods.Generated)]
    [InlineData(HandoverMethods.Typed)]
    public async Task Re_issuing_by_password_replaces_it_at_once_and_closes_everything_else(string method)
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        using var session = await world.Factory.SignInAsAsync(account.Email);
        var outstanding = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        await world.ExecuteAsync(
            $"UPDATE [identity].StaffAccount SET LockedUntilUtc = DATEADD(MINUTE, 15, SYSDATETIMEOFFSET()) WHERE Id = '{account.Id}'");
        var mailBefore = world.Factory.Mail.To(account.Email).Count;

        var reissued = await world.Factory.HandOverAsync(
            world.Clinic, h => h.ReissueAsync(account.Id, method, method == HandoverMethods.Typed ? "Typed-By-Manager-5!" : null, default));
        var password = reissued.GeneratedPassword ?? "Typed-By-Manager-5!";

        Assert.True(reissued.EmailSent);
        Assert.Equal(method == HandoverMethods.Generated, reissued.GeneratedPassword is not null);

        // The session is over, the link is dead, the lock is gone, the mark is set.
        using (var ended = await session.GetAsync(new Uri("/v1/auth/session", UriKind.Relative)))
        {
            Assert.Equal("SESSION_ENDED", (await ended.ReadAsync()).Code);
        }

        using var browser = world.Factory.Browser();
        using (var dead = await browser.InspectLinkAsync(outstanding))
        {
            Assert.Equal("LINK_INVALID", (await dead.ReadAsync()).Code);
        }

        using (var old = await browser.StepOneAsync(account.Email, SignIn.Password))
        {
            Assert.Equal("INVALID_CREDENTIALS", (await old.ReadAsync()).Code);
        }

        Assert.Equal(
            $"1||access-reissued:security:{method}",
            await world.ScalarAsync<string>(
                $"""
                 SELECT CONCAT(a.MustChangePassword, '|', a.LockedUntilUtc, '|', e.Kind, ':', e.Category, ':', e.Detail)
                 FROM [identity].StaffAccount a JOIN [identity].SignInEvent e ON e.AccountId = a.Id AND e.Kind = 'access-reissued'
                 WHERE a.Id = '{account.Id}'
                 """));

        var notice = world.Factory.Mail.To(account.Email).Skip(mailBefore).Single();
        Assert.Contains("أُعيد ضبط الدخول", notice.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(password, notice.TextBody + notice.HtmlBody, StringComparison.Ordinal);

        using var held = await world.Factory.SignInAsAsync(account.Email, password);
        using var gate = await held.GetAsync(new Uri("/probe/whoami", UriKind.Relative));
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", (await gate.ReadAsync()).Code);
    }

    [Fact]
    public async Task Re_issuing_never_compares_the_new_password_with_the_current_one()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();

        // Whoever re-issues must not be able to learn a colleague's password by trying it.
        var reissued = await world.Factory.HandOverAsync(
            world.Clinic, h => h.ReissueAsync(account.Id, HandoverMethods.Typed, SignIn.Password, default));

        Assert.Equal(account.Id, reissued.AccountId);
        Assert.True(await world.ScalarAsync<bool>($"SELECT MustChangePassword FROM [identity].StaffAccount WHERE Id = '{account.Id}'"));
    }

    [Fact]
    public async Task Re_issuing_by_link_sends_a_reset_link_and_leaves_the_password_working()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();

        // Not held back by what a person may ask for themselves — but it counts toward it.
        for (var i = 0; i < 6; i++)
        {
            var sent = await world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(account.Id, HandoverMethods.Link, null, default));
            Assert.True(sent.EmailSent);
            Assert.Null(sent.GeneratedPassword);
        }

        Assert.Equal(6, world.Factory.Mail.LinksSentTo(account.Email));
        using var browser = world.Factory.Browser();
        using (await browser.ForgotAsync(account.Email)) { }
        await world.Factory.RunAccountEmailJobAsync(world.Clinic);
        Assert.Equal(6, world.Factory.Mail.LinksSentTo(account.Email));

        using (var old = await browser.StepOneAsync(account.Email, SignIn.Password))
        {
            Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        }

        using var opened = await browser.InspectLinkAsync(world.Factory.Mail.LatestLinkTokenFor(account.Email));
        Assert.Equal("reset", (await opened.ReadAsync()).Body.GetProperty("kind").GetString());
        Assert.Equal(
            6,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account.Id}' AND Kind = 'access-reissued' AND Detail = 'link'"));
    }

    [Fact]
    public async Task An_invited_account_is_re_issued_an_invitation_by_link_or_made_active_by_password()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var byLink = await world.AccountAsync(StaffAccountStates.Invited);
        var byPassword = await world.AccountAsync(StaffAccountStates.Invited);

        await world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(byLink.Id, HandoverMethods.Link, null, default));
        using var browser = world.Factory.Browser();
        using var opened = await browser.InspectLinkAsync(world.Factory.Mail.LatestLinkTokenFor(byLink.Email));
        Assert.Equal("new-account", (await opened.ReadAsync()).Body.GetProperty("kind").GetString());

        var reissued = await world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(byPassword.Id, HandoverMethods.Generated, null, default));
        Assert.StartsWith("active|1|", await AccountAsync(world, byPassword.Id), StringComparison.Ordinal);
        using var signedIn = await world.Factory.SignInAsAsync(byPassword.Email, reissued.GeneratedPassword!);
    }

    [Fact]
    public async Task A_disabled_account_and_an_account_of_another_clinic_are_refused()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var disabled = await world.AccountAsync(StaffAccountStates.Disabled);
        var elsewhere = await world.Factory.CreateAccountAsync(await sql.Main.SeedClinicAsync());

        var refused = await Assert.ThrowsAsync<ScsProblemException>(
            () => world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(disabled.Id, HandoverMethods.Generated, null, default)));
        Assert.Equal((409, "ACCOUNT_DISABLED"), (refused.Status, refused.Code));

        foreach (var method in new[] { HandoverMethods.Link, HandoverMethods.Generated })
        {
            var missing = await Assert.ThrowsAsync<ScsProblemException>(
                () => world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(elsewhere.Id, method, null, default)));
            Assert.Equal("NOT_FOUND", missing.Code);
        }

        using var browser = world.Factory.Browser();
        using var untouched = await browser.StepOneAsync(elsewhere.Email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, untouched.StatusCode);
    }

    [Fact]
    public async Task An_account_holding_a_temporary_password_can_be_disabled()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var email = ActivationTests.NewEmail();
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithGeneratedPasswordAsync(email, default));

        await world.Factory.ForClinicAsync(
            world.Clinic, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(created.AccountId, StaffAccountStates.Disabled, default));

        Assert.StartsWith("disabled|1|", await AccountAsync(world, created.AccountId), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_person_who_re_issues_is_recorded_as_the_actor()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();

        // No signed-in person here (a seed, a job): the actor is simply absent.
        await world.Factory.HandOverAsync(world.Clinic, h => h.ReissueAsync(account.Id, HandoverMethods.Generated, null, default));

        Assert.Equal(
            1,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account.Id}' AND Kind = 'access-reissued' AND ActorAccountId IS NULL"));
    }
}
