using System.Net;
using System.Text.Json;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Story 1 (SH-050, SH-056, FR-019 to FR-024, SC-002): what a person's role does
/// not allow is refused by the server, before anything is read or changed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PermissionGateTests(SqlServerFixture sql)
{
    [Fact]
    public async Task For_each_of_the_83_permissions_a_holder_is_served_and_a_person_without_it_is_refused()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        using var limited = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        Assert.Equal(83, PermissionCatalogue.Count);

        foreach (var permission in PermissionCatalogue.All)
        {
            // Everything the catalogue holds, except this permission and what needs it.
            await world.Factory.RolesAsync(world.Clinic, roles => roles.UpdateAsync(
                limited.RoleId, "limited", null, Fixtures.Access.AllCodesExcept(permission.Code), CancellationToken.None));

            var before = Fixtures.Access.RunsOf(permission.Code);

            using (var refused = await limited.Browser.NeedsAsync(permission.Code))
            {
                var (status, code, body) = await refused.ReadAsync();
                Assert.True(status == 403 && code == "PERMISSION_REQUIRED", $"{permission.Code}: {status} {code}");
                Assert.Equal(permission.Code, body.GetProperty("permission").GetString());
            }

            Assert.Equal(before, Fixtures.Access.RunsOf(permission.Code));

            using (var served = await owner.Browser.NeedsAsync(permission.Code))
            {
                Assert.True(served.StatusCode == HttpStatusCode.OK, $"{permission.Code}: owner got {(int)served.StatusCode}");
            }

            Assert.Equal(before + 1, Fixtures.Access.RunsOf(permission.Code));
        }
    }

    [Fact]
    public async Task With_no_session_the_answer_is_the_sign_in_refusal_and_names_no_permission()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();

        using var response = await nobody.NeedsAsync(Permissions.MoneyCollect);
        var (status, code, body) = await response.ReadAsync();

        Assert.Equal((401, "UNAUTHENTICATED"), (status, code));
        Assert.False(body.TryGetProperty("permission", out _));
    }

    [Fact]
    public async Task A_temporary_password_is_refused_before_any_permission_is_looked_at()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var owner = await world.Factory.ReadyRoleAsync(world.Clinic, "owner");
        var email = $"temp-{Guid.NewGuid():N}@clinic.example";
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithGeneratedPasswordAsync(email, default));
        await world.Factory.RolesAsync(world.Clinic, roles => roles.AssignAsync(created.AccountId, owner, default));
        using var held = await world.Factory.SignInAsAsync(email, created.GeneratedPassword!);

        using var response = await held.NeedsAsync(Permissions.MoneyCollect);
        var (status, code, _) = await response.ReadAsync();
        Assert.Equal((403, "PASSWORD_CHANGE_REQUIRED"), (status, code));

        // And the session tells the screen it may do nothing yet, though the role holds everything.
        using var session = await held.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var (_, _, described) = await session.ReadAsync();
        Assert.True(described.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(0, described.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task An_account_with_no_role_holds_no_permission_and_still_reaches_its_own_account()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var browser = await world.Factory.SignInAsAsync(world.Clinic);

        using (var refused = await browser.NeedsAsync(Permissions.BookingsView))
        {
            var (status, code, body) = await refused.ReadAsync();
            Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
            Assert.Equal(Permissions.BookingsView, body.GetProperty("permission").GetString());
        }

        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var (ok, _, described) = await session.ReadAsync();
        Assert.Equal(200, ok);
        Assert.Equal(JsonValueKind.Null, described.GetProperty("role").ValueKind);
        Assert.Equal(0, described.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task The_view_alone_opens_what_reads_and_none_of_what_changes()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var reader = await world.Factory.SignInWithAsync(world.Clinic, Permissions.MoneyView);

        using (var read = await reader.Browser.NeedsAsync(Permissions.MoneyView))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        foreach (var write in PermissionCatalogue.All.Where(p => p.Requires == Permissions.MoneyView))
        {
            using var refused = await reader.Browser.NeedsAsync(write.Code);
            var (status, code, _) = await refused.ReadAsync();
            Assert.True(status == 403 && code == "PERMISSION_REQUIRED", write.Code);
        }
    }

    [Fact]
    public async Task The_refusal_is_the_same_whether_or_not_the_record_exists()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var outsider = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        using var holder = await world.Factory.SignInWithAsync(world.Clinic, Permissions.PatientsView, Permissions.PatientsManage);

        static JsonElement WithoutTrace(JsonElement body) => JsonSerializer.SerializeToElement(
            body.EnumerateObject().Where(p => p.Name != "traceId").ToDictionary(p => p.Name, p => p.Value.Clone()));

        using var there = await outsider.Browser.GetAsync(new Uri("/probe/needs-record/1", UriKind.Relative));
        using var notThere = await outsider.Browser.GetAsync(new Uri("/probe/needs-record/999", UriKind.Relative));
        var first = await there.ReadAsync();
        var second = await notThere.ReadAsync();

        Assert.Equal((403, "PERMISSION_REQUIRED"), (first.Status, first.Code));
        Assert.Equal(WithoutTrace(first.Body).GetRawText(), WithoutTrace(second.Body).GetRawText());

        // The holder is the one who learns which record exists.
        using var found = await holder.Browser.GetAsync(new Uri("/probe/needs-record/1", UriKind.Relative));
        using var missing = await holder.Browser.GetAsync(new Uri("/probe/needs-record/999", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Holding_every_permission_in_one_clinic_reaches_nothing_of_another()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        await world.Factory.ProvisionRolesAsync(other);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);

        using var roles = await owner.Browser.GetAsync(new Uri("/v1/roles", UriKind.Relative));
        var (status, _, body) = await roles.ReadAsync();
        var mine = await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default));
        var theirs = await world.Factory.RolesAsync(other, r => r.ListAsync(default));

        Assert.Equal(200, status);
        var listed = body.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(mine.Select(r => r.Id), listed);
        Assert.DoesNotContain(listed, id => theirs.Any(r => r.Id == id));

        // And a role of the other clinic cannot be given to this clinic's account.
        var (accountId, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        var refusal = await Assert.ThrowsAsync<Scs.BuildingBlocks.Errors.ScsProblemException>(
            () => world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(accountId, theirs[0].Id, default)));
        Assert.Equal(404, refusal.Status);
    }

    [Fact]
    public async Task A_refusal_is_logged_with_identifiers_and_the_code_and_nothing_personal()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        using var refused = await person.Browser.NeedsAsync(Permissions.MoneyRefund);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var entry = Assert.Single(
            world.Factory.Logs.Lines,
            line => line.Contains("is not held", StringComparison.Ordinal) && line.Contains(Permissions.MoneyRefund, StringComparison.Ordinal));
        Assert.Contains(person.AccountId.ToString(), entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(world.Clinic.ToString(), entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/probe/needs/" + Permissions.MoneyRefund, entry, StringComparison.Ordinal);
        Assert.DoesNotContain(person.Email, entry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_session_lists_the_role_and_the_codes_it_holds_in_catalogue_order()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(
            world.Clinic, Permissions.MoneyCollect, Permissions.BookingsView, Permissions.MoneyView);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);

        using var mine = await person.Browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var (_, _, described) = await mine.ReadAsync();

        Assert.Equal(person.RoleId, described.GetProperty("role").GetProperty("id").GetGuid());
        Assert.StartsWith("role-", described.GetProperty("role").GetProperty("name").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, described.GetProperty("role").GetProperty("readyKey").ValueKind);
        Assert.Equal(
            [Permissions.BookingsView, Permissions.MoneyView, Permissions.MoneyCollect],
            described.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!));

        using var theirs = await owner.Browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        var (_, _, ownerSession) = await theirs.ReadAsync();
        Assert.Equal("owner", ownerSession.GetProperty("role").GetProperty("readyKey").GetString());
        Assert.Equal("مالك العيادة", ownerSession.GetProperty("role").GetProperty("name").GetString());
        Assert.Equal(
            Fixtures.Access.AllCodes,
            ownerSession.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!));
    }
}
