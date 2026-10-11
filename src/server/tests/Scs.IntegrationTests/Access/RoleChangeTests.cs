using System.Net;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Story 6 (SH-045, ق-35, CL-AD-098; FR-014, FR-024, SC-005): a change of role
/// takes effect at once. Editing what a role holds is seen by its holders' very next request;
/// giving an account another role ends its sessions.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RoleChangeTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Taking_a_permission_out_of_a_role_refuses_its_holders_next_request_without_ending_the_session()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var cashier = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView, Permissions.MoneyCollect);

        using (var before = await cashier.Browser.NeedsAsync(Permissions.MoneyCollect))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        await world.Factory.RolesAsync(world.Clinic, r => r.UpdateAsync(
            cashier.RoleId, "cashier", null, [Permissions.MoneyView], default));

        using (var after = await cashier.Browser.NeedsAsync(Permissions.MoneyCollect))
        {
            var (status, code, _) = await after.ReadAsync();
            Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
        }

        // The same session goes on: what the role still holds still works, and nobody signed in again.
        using (var still = await cashier.Browser.NeedsAsync(Permissions.MoneyView))
        {
            Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        }

        Assert.Equal(1, await world.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{cashier.AccountId}' AND EndedAtUtc IS NULL"));
    }

    [Fact]
    public async Task Adding_a_permission_to_a_role_serves_its_holders_next_request_and_the_screen_is_told()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView);

        using (var before = await person.Browser.NeedsAsync(Permissions.MoneyRefund))
        {
            Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        }

        await world.Factory.RolesAsync(world.Clinic, r => r.UpdateAsync(
            person.RoleId, "money", null, [Permissions.MoneyView, Permissions.MoneyRefund], default));

        using (var after = await person.Browser.NeedsAsync(Permissions.MoneyRefund))
        {
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        }

        using var session = await person.Browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var (_, _, described) = await session.ReadAsync();
        Assert.Equal(
            [Permissions.MoneyView, Permissions.MoneyRefund],
            described.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!));
        Assert.Equal("money", described.GetProperty("role").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Giving_an_account_a_different_role_ends_every_one_of_its_sessions()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView);
        using var secondBrowser = await world.Factory.SignInAsAsync(person.Email);
        var other = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);

        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(person.AccountId, other, default));

        foreach (var browser in new[] { person.Browser, secondBrowser })
        {
            using var next = await browser.NeedsAsync(Permissions.BookingsView);
            var (status, code, _) = await next.ReadAsync();
            Assert.Equal((401, "SESSION_ENDED"), (status, code));
        }

        Assert.Equal(2, await world.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{person.AccountId}' AND EndReason = 'role-changed'"));
        Assert.Equal(2, await world.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{person.AccountId}' AND Kind = 'session-ended' AND Detail = 'role-changed'"));

        // Signing in again, the person has what the new role holds.
        using var again = await world.Factory.SignInAsAsync(person.Email);
        using var served = await again.NeedsAsync(Permissions.BookingsView);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task Giving_an_account_the_role_it_already_holds_ends_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView);

        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(person.AccountId, person.RoleId, default));

        using var next = await person.Browser.NeedsAsync(Permissions.MoneyView);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task A_request_already_under_way_finishes_under_the_set_it_began_with()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView, Permissions.MoneyCollect);

        // The slow probe passes the gate, waits, and only then answers.
        var slow = person.Browser.GetAsync(new Uri("/probe/slow-collect", UriKind.Relative));
        await ProbeEndpoints.SlowEntered.WaitAsync(TimeSpan.FromSeconds(10));
        await world.Factory.RolesAsync(world.Clinic, r => r.UpdateAsync(
            person.RoleId, "cashier", null, [Permissions.MoneyView], default));
        ProbeEndpoints.SlowRelease.Release();

        using var finished = await slow;
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);

        using var next = await person.Browser.GetAsync(new Uri("/probe/slow-collect", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
    }

    [Fact]
    public async Task An_account_of_another_clinic_is_not_found()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var (stranger, _) = await world.Factory.CreateAccountAsync(other);
        var role = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);

        var refusal = await Assert.ThrowsAsync<Scs.BuildingBlocks.Errors.ScsProblemException>(
            () => world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(stranger, role, default)));

        Assert.Equal((404, "NOT_FOUND"), (refusal.Status, refusal.Code));
    }
}
