using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.Tenancy.Contracts;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Story 3 (SH-052, CL-AD-082, CL-AD-109, FR-008 to FR-010): every clinic has
/// «مالك العيادة», «مدير العيادة» and «طبيب» from the moment it exists.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReadyRolesTests(SqlServerFixture sql)
{
    private static async Task<Guid> RegisterAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClinicRegistry>().RegisterAsync(
            $"c{Guid.NewGuid():N}"[..20], "عيادة جديدة", "A new clinic", "tool:test", CancellationToken.None);
    }

    [Fact]
    public async Task A_clinic_has_its_three_ready_roles_the_moment_it_is_registered()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var clinic = await RegisterAsync(factory);
        var roles = await factory.RolesAsync(clinic, r => r.ListAsync(default));

        Assert.Equal(["owner", "manager", "doctor"], roles.Select(r => r.ReadyKey));
        Assert.Equal(["مالك العيادة", "مدير العيادة", "طبيب"], roles.Select(r => r.Name));
        Assert.All(roles, r => Assert.Equal("ready", r.Category));
        Assert.All(roles, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
        Assert.Equal([true, false, false], roles.Select(r => r.Fixed));
        Assert.Equal([PermissionCatalogue.Count, ReadySets.Manager.Count, ReadySets.Doctor.Count], roles.Select(r => r.PermissionCount));
        Assert.Equal([83, 59, 40], roles.Select(r => r.PermissionCount));
    }

    [Fact]
    public async Task The_ready_roles_hold_exactly_what_the_catalogue_marks_for_them()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        await world.Factory.ProvisionRolesAsync(world.Clinic);

        async Task<List<string>> CodesOf(string key) => [.. (await TestDatabase.ReadColumnAsync(
            sql.Main.Crossing,
            $"""
             SELECT p.Code FROM [identity].RolePermission p JOIN [identity].Role r ON r.Id = p.RoleId
             WHERE r.ClinicId = '{world.Clinic}' AND r.ReadyKey = '{key}'
             """)).Order(StringComparer.Ordinal)];

        Assert.Equal(ReadySets.Manager.Order(StringComparer.Ordinal), await CodesOf("manager"));
        Assert.Equal(ReadySets.Doctor.Order(StringComparer.Ordinal), await CodesOf("doctor"));

        // The owner role stores nothing: it holds whatever the catalogue holds (FR-005).
        Assert.Empty(await CodesOf("owner"));
    }

    [Fact]
    public async Task Provisioning_again_changes_nothing_and_a_removed_ready_role_does_not_come_back()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        await world.Factory.ProvisionRolesAsync(world.Clinic);
        var first = await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default));

        await world.Factory.ProvisionRolesAsync(world.Clinic);
        Assert.Equal(first, await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default)));

        var manager = first.Single(r => r.ReadyKey == "manager").Id;
        await world.Factory.RolesAsync(world.Clinic, r => r.RemoveAsync(manager, default));
        await world.Factory.ProvisionRolesAsync(world.Clinic);

        var after = await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default));
        Assert.Equal(["owner", "doctor"], after.Select(r => r.ReadyKey));
    }

    [Fact]
    public async Task When_a_provisioner_fails_the_registration_says_so_and_running_it_again_repairs_the_clinic()
    {
        await sql.EnsureMigratedAsync();
        var failing = new FailingOnce();
        using var factory = sql.CreateApi().WithServices(services => services.AddScoped<IClinicProvisioner>(_ => failing));

        var name = $"c{Guid.NewGuid():N}"[..20];
        using (var scope = factory.Services.CreateScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
                .GetRequiredService<IClinicRegistry>().RegisterAsync(name, "عيادة", "Clinic", "tool:test", default));
        }

        // The clinic was registered; what failed was one module's part, and the others are
        // either done or safe to do again.
        var clinic = await TestDatabase.ScalarAsync<Guid>(
            sql.Main.Crossing, $"SELECT ClinicId FROM tenancy.ClinicDirectory WHERE ShortName = '{name}'");
        Assert.NotEqual(Guid.Empty, clinic);

        await factory.ProvisionRolesAsync(clinic);
        var roles = await factory.RolesAsync(clinic, r => r.ListAsync(default));
        Assert.Equal(["owner", "manager", "doctor"], roles.Select(r => r.ReadyKey));
    }

    [Fact]
    public async Task Every_stored_code_is_a_permission_of_the_catalogue()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        await world.Factory.ProvisionRolesAsync(world.Clinic);

        var stored = await TestDatabase.ReadColumnAsync(sql.Main.Crossing, "SELECT DISTINCT Code FROM [identity].RolePermission");

        Assert.NotEmpty(stored);
        Assert.All(stored, code => Assert.True(PermissionCatalogue.Contains(code), code));
    }

    [Fact]
    public async Task A_code_that_left_the_catalogue_grants_nothing_and_does_not_break_its_holder()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        await world.ExecuteAsync(
            $"INSERT INTO [identity].RolePermission (RoleId, Code, ClinicId) VALUES ('{person.RoleId}', 'gone.fromCatalogue', '{world.Clinic}')");

        try
        {
            using var session = await person.Browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
            var (status, _, body) = await session.ReadAsync();

            Assert.Equal(200, status);
            Assert.Equal([Permissions.BookingsView], body.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!));
        }
        finally
        {
            await world.ExecuteAsync("DELETE FROM [identity].RolePermission WHERE Code = 'gone.fromCatalogue'");
        }
    }

    [Fact]
    public async Task The_roles_list_is_served_to_a_holder_of_its_view_and_refused_to_anyone_else()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        await world.Factory.ProvisionRolesAsync(world.Clinic);
        using var viewer = await world.Factory.SignInWithAsync(world.Clinic, Permissions.AdminRolesView);
        using var other = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        using var served = await viewer.Browser.GetAsync(new Uri("/v1/roles", UriKind.Relative));
        var (status, _, body) = await served.ReadAsync();

        Assert.Equal(200, status);
        Assert.Equal(PermissionCatalogue.Count, body.GetProperty("catalogueSize").GetInt32());
        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(["owner", "manager", "doctor"], roles.Take(3).Select(r => r.GetProperty("readyKey").GetString()));
        Assert.Equal([83, 59, 40], roles.Take(3).Select(r => r.GetProperty("permissionCount").GetInt32()));
        Assert.All(roles.Skip(3), r => Assert.Equal("custom", r.GetProperty("category").GetString()));
        Assert.Equal(5, roles.Count);

        using var refused = await other.Browser.GetAsync(new Uri("/v1/roles", UriKind.Relative));
        var (refusedStatus, code, refusal) = await refused.ReadAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (refusedStatus, code));
        Assert.Equal(Permissions.AdminRolesView, refusal.GetProperty("permission").GetString());

        using var nobody = world.Factory.Browser();
        using var unauthenticated = await nobody.GetAsync(new Uri("/v1/roles", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    private sealed class FailingOnce : IClinicProvisioner
    {
        private int _calls;

        public Task ProvisionAsync(CancellationToken cancellation) =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new InvalidOperationException("A module could not prepare the new clinic.")
                : Task.CompletedTask;
    }
}
