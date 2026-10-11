using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Infrastructure;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 004, research R9: setting a password changes the hash, the lock, the links, the mark,
/// the sessions and the record together — or none of them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordSetterTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_failure_part_way_through_leaves_nothing_changed()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();
        using var session = await world.Factory.SignInAsAsync(account.Email);
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        await world.ExecuteAsync(
            $"UPDATE [identity].StaffAccount SET LockedUntilUtc = DATEADD(MINUTE, 15, SYSDATETIMEOFFSET()), FailedAttempts = 2 WHERE Id = '{account.Id}'");

        Task<string?> StateAsync() => world.ScalarAsync<string>(
            $"""
             SELECT CONCAT(a.PasswordHash, '|', a.FailedAttempts, '|', IIF(a.LockedUntilUtc IS NULL, 'open', 'locked'), '|',
                 (SELECT COUNT(*) FROM [identity].PasswordLink l WHERE l.AccountId = a.Id AND l.UsedAtUtc IS NULL AND l.ReplacedAtUtc IS NULL), '|',
                 (SELECT COUNT(*) FROM [identity].Session s WHERE s.AccountId = a.Id AND s.EndedAtUtc IS NULL), '|',
                 (SELECT COUNT(*) FROM [identity].SignInEvent e WHERE e.AccountId = a.Id))
             FROM [identity].StaffAccount a WHERE a.Id = '{account.Id}'
             """);

        var before = await StateAsync();

        // The record is the last thing written. One the table cannot hold makes that write fail
        // after the link was used up, the hash replaced and the sessions ended.
        await Assert.ThrowsAnyAsync<Exception>(() => world.Factory.ForClinicAsync(
            world.Clinic,
            services => services.GetRequiredService<PasswordSetter>().SetAsync(
                new SetPassword(account.Id, Passwords.New, new string('x', 40), RequestOrigin.None)
                {
                    Link = services.GetRequiredService<PasswordLinkTokens>().Read(token),
                    MustDiffer = true,
                },
                CancellationToken.None)));

        Assert.Equal(before, await StateAsync());

        // The session is still alive, and the link can still be used — properly, this time.
        using (var alive = await session.GetAsync(new Uri("/v1/auth/session", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        }

        using var browser = world.Factory.Browser();
        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.NotEqual(before, await StateAsync());
    }

    [Fact]
    public async Task Two_routes_setting_one_accounts_password_at_the_same_moment_take_their_turns()
    {
        using var world = await ActivationTests.WorldAsync(sql);
        var account = await world.AccountAsync();

        Task<SetPasswordResult> SetAsync(string password) => world.Factory.ForClinicAsync(
            world.Clinic,
            services => services.GetRequiredService<PasswordSetter>().SetAsync(
                new SetPassword(account.Id, password, "access-reissued", RequestOrigin.None) { Temporary = true },
                CancellationToken.None));

        var results = await Task.WhenAll(SetAsync("First-Of-Two-11!"), SetAsync("Second-Of-Two-22!"));

        Assert.All(results, result => Assert.Equal(SetPasswordResult.Done, result));
        Assert.Equal(
            2,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account.Id}' AND Kind = 'access-reissued'"));

        // Exactly one of the two is the password now, whole — never a mixture.
        using var browser = world.Factory.Browser();
        using var first = await browser.StepOneAsync(account.Email, "First-Of-Two-11!");
        using var second = await browser.StepOneAsync(account.Email, "Second-Of-Two-22!");
        Assert.Single(new[] { first, second }, response => response.StatusCode == HttpStatusCode.OK);
    }
}
