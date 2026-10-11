using System.Net;
using Scs.BuildingBlocks.Errors;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Stories 4 and 6 (SH-053, SH-054, ق-28, ق-29, ق-31, ق-54; FR-012 to FR-018): a
/// role is stored only as a valid set, the owner role is fixed, and a role in use and the last
/// owner are protected.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicRolesTests(SqlServerFixture sql)
{
    private static async Task<ScsProblemException> RefusedAsync(Func<Task> work) =>
        await Assert.ThrowsAsync<ScsProblemException>(work);

    [Fact]
    public async Task A_valid_set_is_stored_as_a_custom_role()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        var id = await world.Factory.RolesAsync(world.Clinic, r => r.CreateAsync(
            "  الاستقبال  ", "  مكتب المواعيد  ", [Permissions.BookingsView, Permissions.BookingsManage], default));
        var role = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == id);

        Assert.Equal(new RoleSummary(id, "الاستقبال", null, "مكتب المواعيد", "custom", false, 2), role);
    }

    [Fact]
    public async Task A_set_without_what_one_of_its_permissions_requires_is_refused_naming_both()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var requirements = PermissionCatalogue.All.Where(p => p.Requires is not null).ToList();

        Assert.Equal(61, requirements.Count);

        foreach (var permission in requirements)
        {
            var set = Fixtures.Access.AllCodes.Where(code => code != permission.Requires).ToArray();

            var refusal = await RefusedAsync(() => world.Factory.RolesAsync(
                world.Clinic, r => r.CreateAsync($"r-{Guid.NewGuid():N}", null, set, default)));

            Assert.Equal((400, "VALIDATION_FAILED"), (refusal.Status, refusal.Code));
            Assert.Equal([PermissionSets.MissingRequirement], refusal.Errors!["permissions"]);

            var missing = System.Text.Json.JsonSerializer.SerializeToElement(refusal.Extensions!["missing"]);
            Assert.Contains(
                missing.EnumerateArray(),
                pair => pair.GetProperty("permission").GetString() == permission.Code
                        && pair.GetProperty("requires").GetString() == permission.Requires);
        }

        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{world.Clinic}'"));
    }

    [Fact]
    public async Task What_is_refused_as_a_set_is_refused_as_an_answer_in_the_uniform_shape()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);

        using var response = await owner.Browser.PostAsync(new Uri("/probe/role-without-view", UriKind.Relative), null);
        var (status, code, body) = await response.ReadAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("MISSING_REQUIREMENT", body.GetProperty("errors").GetProperty("permissions")[0].GetString());
        var pair = body.GetProperty("missing")[0];
        Assert.Equal(Permissions.AdminUsersManage, pair.GetProperty("permission").GetString());
        Assert.Equal(Permissions.AdminUsersView, pair.GetProperty("requires").GetString());
    }

    [Fact]
    public async Task An_empty_set_an_unknown_code_and_a_repeated_code_are_refused()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        async Task<string[]> CodesFor(params string[] set) => (await RefusedAsync(() => world.Factory.RolesAsync(
            world.Clinic, r => r.CreateAsync($"r-{Guid.NewGuid():N}", null, set, default)))).Errors!["permissions"];

        Assert.Equal(["REQUIRED"], await CodesFor());
        Assert.Equal(["UNKNOWN"], await CodesFor(Permissions.BookingsView, "clinic.own.permission"));
        Assert.Equal(["DUPLICATE"], await CodesFor(Permissions.BookingsView, Permissions.BookingsView));
    }

    [Fact]
    public async Task A_refused_set_names_the_codes_that_are_wrong()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        string[] set = [Permissions.BookingsView, "clinic.own.permission", Permissions.BookingsView, Permissions.MoneyCollect];

        var refusal = await RefusedAsync(() => world.Factory.RolesAsync(
            world.Clinic, r => r.CreateAsync("فريق", null, set, default)));

        Assert.Equal(["UNKNOWN", "DUPLICATE", "MISSING_REQUIREMENT"], refusal.Errors!["permissions"]);
        Assert.Equal(["clinic.own.permission"], Assert.IsAssignableFrom<IEnumerable<string>>(refusal.Extensions!["unknown"]));
        Assert.Equal([Permissions.BookingsView], Assert.IsAssignableFrom<IEnumerable<string>>(refusal.Extensions!["duplicate"]));
        Assert.True(refusal.Extensions.ContainsKey("missing"));

        // A set with only one kind of fault names only that.
        var onlyUnknown = await RefusedAsync(() => world.Factory.RolesAsync(
            world.Clinic, r => r.CreateAsync("فريق", null, [Permissions.BookingsView, "no.such.code"], default)));
        Assert.Equal(["no.such.code"], Assert.IsAssignableFrom<IEnumerable<string>>(onlyUnknown.Extensions!["unknown"]));
        Assert.False(onlyUnknown.Extensions.ContainsKey("duplicate"));
        Assert.False(onlyUnknown.Extensions.ContainsKey("missing"));
    }

    [Fact]
    public async Task A_name_is_required_at_most_forty_characters_and_unique_within_the_clinic_only()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        string[] set = [Permissions.BookingsView];

        async Task<string[]> NameCodes(Guid clinic, string name) => (await RefusedAsync(() => world.Factory.RolesAsync(
            clinic, r => r.CreateAsync(name, null, set, default)))).Errors!["name"];

        Assert.Equal(["REQUIRED"], await NameCodes(world.Clinic, "   "));
        Assert.Equal(["TOO_LONG"], await NameCodes(world.Clinic, new string('ف', 41)));

        await world.Factory.RolesAsync(world.Clinic, r => r.CreateAsync(new string('ف', 40), null, set, default));
        await world.Factory.RolesAsync(world.Clinic, r => r.CreateAsync("فريق العيادة", null, set, default));
        Assert.Equal(["TAKEN"], await NameCodes(world.Clinic, "فريق العيادة"));

        // Another clinic may use the same name.
        await world.Factory.RolesAsync(other, r => r.CreateAsync("فريق العيادة", null, set, default));
    }

    [Fact]
    public async Task The_owner_role_is_never_edited_renamed_or_removed()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var owner = await world.Factory.ReadyRoleAsync(world.Clinic, "owner");

        var edited = await RefusedAsync(() => world.Factory.RolesAsync(
            world.Clinic, r => r.UpdateAsync(owner, "المالك", null, [Permissions.BookingsView], default)));
        var removed = await RefusedAsync(() => world.Factory.RolesAsync(world.Clinic, r => r.RemoveAsync(owner, default)));

        Assert.Equal((409, "ROLE_FIXED"), (edited.Status, edited.Code));
        Assert.Equal((409, "ROLE_FIXED"), (removed.Status, removed.Code));
        Assert.Equal("مالك العيادة", (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default)))[0].Name);
    }

    [Fact]
    public async Task The_other_two_ready_roles_are_edited_like_any_role_and_the_edit_says_what_changed()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        var wanted = ReadySets.Doctor
            .Where(code => code != Permissions.AdminRolesView)
            .Append(Permissions.MoneyCollect)
            .ToArray();

        var change = await world.Factory.RolesAsync(world.Clinic, r => r.UpdateAsync(doctor, "طبيب", "وصف جديد", wanted, default));

        Assert.Equal([Permissions.MoneyCollect], change.Added);
        Assert.Equal([Permissions.AdminRolesView], change.Removed);

        var same = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == doctor);
        Assert.Equal("doctor", same.ReadyKey);
        Assert.Equal(40, same.PermissionCount);

        // Renamed, it is shown as the clinic wrote it from then on.
        await world.Factory.RolesAsync(world.Clinic, r => r.UpdateAsync(doctor, "طبيب أسنان", null, wanted, default));
        var renamed = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == doctor);
        Assert.Null(renamed.ReadyKey);
        Assert.Equal("ready", renamed.Category);
        Assert.Equal("طبيب أسنان", renamed.Name);
    }

    [Fact]
    public async Task An_edit_that_breaks_the_rule_changes_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var role = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.MoneyView, Permissions.MoneyCollect);

        var refusal = await RefusedAsync(() => world.Factory.RolesAsync(
            world.Clinic, r => r.UpdateAsync(role, "تحصيل", null, [Permissions.MoneyCollect], default)));

        Assert.Equal([PermissionSets.MissingRequirement], refusal.Errors!["permissions"]);
        var stored = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == role);
        Assert.StartsWith("role-", stored.Name, StringComparison.Ordinal);
        Assert.Equal(2, stored.PermissionCount);
    }

    [Fact]
    public async Task A_role_that_an_account_holds_is_not_removed_and_an_unused_one_is()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var used = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);
        var unused = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);
        var (first, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        var (second, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(first, used, default));
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(second, used, default));

        var refusal = await RefusedAsync(() => world.Factory.RolesAsync(world.Clinic, r => r.RemoveAsync(used, default)));
        await world.Factory.RolesAsync(world.Clinic, r => r.RemoveAsync(unused, default));

        Assert.Equal((409, "ROLE_IN_USE"), (refusal.Status, refusal.Code));
        Assert.Equal(2, refusal.Extensions!["holders"]);
        var left = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Select(r => r.Id).ToList();
        Assert.Contains(used, left);
        Assert.DoesNotContain(unused, left);
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].RolePermission WHERE RoleId = '{unused}'"));
    }

    [Fact]
    public async Task A_clinic_is_never_left_without_an_owner_even_when_two_changes_race()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var owner = await world.Factory.ReadyRoleAsync(world.Clinic, "owner");
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        var (only, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(only, owner, default));

        var refusal = await RefusedAsync(() => world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(only, doctor, default)));
        Assert.Equal((409, "LAST_OWNER"), (refusal.Status, refusal.Code));

        // With two owners, one may step down — and only one, however the two requests interleave.
        var (second, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(second, owner, default));

        var attempts = await Task.WhenAll(new[] { only, second }.Select(async account =>
        {
            try
            {
                await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(account, doctor, default));
                return "done";
            }
            catch (ScsProblemException failure)
            {
                return failure.Code;
            }
        }));

        Assert.Equal(["LAST_OWNER", "done"], attempts.Order(StringComparer.Ordinal));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].StaffAccount WHERE RoleId = '{owner}'"));
    }

    [Fact]
    public async Task Nobody_changes_the_role_of_their_own_account()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var (other, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");

        // The probe runs the operation as the signed-in person, as the users page of spec 026 will.
        using var own = await owner.Browser.PostAsync(new Uri($"/probe/assign/{owner.AccountId}/{doctor}", UriKind.Relative), null);
        using var someoneElse = await owner.Browser.PostAsync(new Uri($"/probe/assign/{other}/{doctor}", UriKind.Relative), null);

        var (status, code, _) = await own.ReadAsync();
        Assert.Equal((409, "OWN_ROLE"), (status, code));
        Assert.Equal(HttpStatusCode.NoContent, someoneElse.StatusCode);
        Assert.Equal(doctor, await world.ScalarAsync<Guid>($"SELECT RoleId FROM [identity].StaffAccount WHERE Id = '{other}'"));
    }
}
