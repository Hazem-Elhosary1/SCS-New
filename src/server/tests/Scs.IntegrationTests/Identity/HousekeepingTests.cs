using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Infrastructure;
using Scs.IntegrationTests.Fixtures;
using Scs.Tenancy.Contracts;

namespace Scs.IntegrationTests.Identity;

/// <summary>Spec 003: the daily cleanup of old codes and long-ended sessions, clinic by clinic.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CleanupJobTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Old_codes_and_long_ended_sessions_are_removed_and_everything_else_kept()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        var otherClinic = await sql.Main.SeedClinicAsync();
        var (otherId, otherEmail) = await world.Factory.CreateAccountAsync(otherClinic);

        // An unfinished sign-in and a finished one in this clinic; a session in another clinic.
        using (var abandoned = world.Factory.Browser())
        {
            using var first = await abandoned.StepOneAsync(email, SignIn.Password);
        }

        using var ended = await world.Factory.SignInAsAsync(email);
        using var signedOut = await ended.SignOutAsync();
        using var alive = await world.Factory.SignInAsAsync(email);
        using var elsewhere = await world.Factory.SignInAsAsync(otherEmail);
        using var elsewhereOut = await elsewhere.SignOutAsync();

        async Task RunAsync() => await world.Factory.ForClinicAsync(
            world.Clinic, services => ActivatorUtilities.CreateInstance<CleanupJob>(services).RunAsync(default));

        // The next day: codes go, sessions stay.
        world.Factory.Clock.Advance(TimeSpan.FromDays(1.1));
        await RunAsync();
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInChallenge WHERE AccountId = '{id}'"));
        Assert.Equal(2, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}'"));

        // Three months on: the session that ended goes; the one never ended stays, as do the events.
        world.Factory.Clock.Advance(TimeSpan.FromDays(90));
        await RunAsync();
        await RunAsync();
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{id}' AND EndedAtUtc IS NULL"));
        Assert.True(await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}'") >= 3);

        // The other clinic's rows were not this run's to touch.
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{otherId}'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInChallenge WHERE AccountId = '{otherId}'"));
    }

    [Fact]
    public async Task Dead_password_links_and_handled_email_requests_are_removed_and_live_ones_kept()
    {
        // Spec 004: a link is removed only when it is older than any life it could have had.
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var (id, email) = await world.AccountAsync();
        var otherClinic = await sql.Main.SeedClinicAsync();
        var (otherId, otherEmail) = await world.Factory.CreateAccountAsync(otherClinic);

        await world.Factory.ResetTokenForAsync(world.Clinic, email);
        await world.Factory.ResetTokenForAsync(otherClinic, otherEmail);
        using (var browser = world.Factory.Browser())
        {
            // A request nobody has handled yet must survive the cleanup.
            using var waiting = await browser.ForgotAsync(email);
        }

        async Task RunAsync() => await world.Factory.ForClinicAsync(
            world.Clinic, services => ActivatorUtilities.CreateInstance<CleanupJob>(services).RunAsync(default));

        Task<int> LinksAsync(Guid account) =>
            world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].PasswordLink WHERE AccountId = '{account}'");

        Task<string?> RequestsAsync(Guid account) => world.ScalarAsync<string>(
            $"SELECT CONCAT(SUM(IIF(HandledAtUtc IS NULL, 1, 0)), '|', SUM(IIF(HandledAtUtc IS NULL, 0, 1))) FROM [identity].AccountEmailRequest WHERE AccountId = '{account}'");

        world.Factory.Clock.Advance(TimeSpan.FromDays(1.1));
        await RunAsync();
        Assert.Equal(1, await LinksAsync(id));
        Assert.Equal("1|0", await RequestsAsync(id));

        world.Factory.Clock.Advance(TimeSpan.FromDays(21));
        await RunAsync();
        await RunAsync();
        Assert.Equal(0, await LinksAsync(id));
        Assert.Equal("1|0", await RequestsAsync(id));

        // The other clinic's rows were not this run's to touch.
        Assert.Equal(1, await LinksAsync(otherId));
        Assert.Equal("0|1", await RequestsAsync(otherId));
    }
}

/// <summary>
/// Spec 003, research R9: the key ring that protects cookies is in the database, so a cookie
/// written by one server instance is read by another.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CookieProtectionTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_session_opened_on_one_instance_is_honoured_by_another()
    {
        await sql.EnsureMigratedAsync();
        using var one = sql.CreateApi();
        using var two = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var (_, email) = await one.CreateAccountAsync(clinic);

        // Step one on the first instance, step two and the session on the second.
        using var browser = one.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var challenge = first.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using var other = two.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using var confirm = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/sign-in/code")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { code = one.Mail.LatestCodeFor(email) }),
        };
        confirm.Headers.Add("Cookie", challenge);
        confirm.Headers.Add(RequestGuardExtensions.RequestHeader, "1");
        using var second = await other.SendAsync(confirm);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var access = second.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(SessionCookies.Access + "=", StringComparison.Ordinal)).Split(';')[0];

        // And back on the first instance, the session is alive.
        using var plain = one.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using var ask = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/session");
        ask.Headers.Add("Cookie", access);
        using var answer = await plain.SendAsync(ask);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);

        Assert.True(await TestDatabase.ScalarAsync<int>(sql.Main.Admin, "SELECT COUNT(*) FROM platform.DataProtectionKey") >= 1);
    }
}

/// <summary>Spec 003, research R14: another module asks for the established clinic's own profile.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicProfileTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_established_clinics_own_profile_is_returned_and_no_other()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var mine = await sql.Main.SeedClinicAsync("suspended");
        await sql.Main.SeedClinicAsync();

        var profile = await factory.ForClinicAsync(mine, services => services.GetRequiredService<IClinicProfile>().GetOwnAsync(default));

        Assert.Equal(new ClinicProfile(mine, "عيادة تجريبية", "Test clinic", "suspended"), profile);
    }

    [Fact]
    public async Task With_no_clinic_established_it_is_refused()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        using var scope = factory.Services.CreateScope();

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => scope.ServiceProvider.GetRequiredService<IClinicProfile>().GetOwnAsync(default));

        Assert.Equal("CLINIC_REQUIRED", refusal.Code);
        Assert.Null(scope.ServiceProvider.GetRequiredService<IClinicContext>().ClinicId);
    }
}
