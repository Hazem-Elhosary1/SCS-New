using Microsoft.Extensions.DependencyInjection;
using Scs.Identity.Contracts;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>A signed-in person and the role they were given.</summary>
public sealed record SignedInStaff(HttpClient Browser, Guid AccountId, string Email, Guid RoleId) : IDisposable
{
    public void Dispose() => Browser.Dispose();
}

/// <summary>Helpers for roles and permissions (spec 005), through the module's own operations.</summary>
public static class Access
{
    public static string[] AllCodes => [.. PermissionCatalogue.All.Select(p => p.Code)];

    /// <summary>
    /// Every code except these and whatever needs them, directly or further down: the largest
    /// valid set that does not hold them.
    /// </summary>
    public static string[] AllCodesExcept(params string[] without)
    {
        var gone = without.ToHashSet(StringComparer.Ordinal);
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var permission in PermissionCatalogue.All)
            {
                if (permission.Requires is { } needed && gone.Contains(needed) && gone.Add(permission.Code))
                {
                    grew = true;
                }
            }
        }

        return [.. PermissionCatalogue.All.Select(p => p.Code).Where(code => !gone.Contains(code))];
    }

    public static Task<T> RolesAsync<T>(this ApiFactory factory, Guid clinicId, Func<IClinicRoles, Task<T>> work) =>
        factory.ForClinicAsync(clinicId, services => work(services.GetRequiredService<IClinicRoles>()));

    public static Task RolesAsync(this ApiFactory factory, Guid clinicId, Func<IClinicRoles, Task> work) =>
        factory.ForClinicAsync(clinicId, services => work(services.GetRequiredService<IClinicRoles>()));

    /// <summary>Gives the clinic its three ready roles, as registering it does.</summary>
    public static Task ProvisionRolesAsync(this ApiFactory factory, Guid clinicId) =>
        factory.RolesAsync(clinicId, roles => roles.ProvisionAsync(CancellationToken.None));

    public static async Task<Guid> ReadyRoleAsync(this ApiFactory factory, Guid clinicId, string key)
    {
        await factory.ProvisionRolesAsync(clinicId);
        var roles = await factory.RolesAsync(clinicId, r => r.ListAsync(CancellationToken.None));
        return roles.Single(role => role.ReadyKey == key).Id;
    }

    /// <summary>A custom role with a name of its own holding exactly these codes.</summary>
    public static Task<Guid> CreateRoleAsync(this ApiFactory factory, Guid clinicId, params string[] codes) =>
        factory.RolesAsync(clinicId, roles => roles.CreateAsync($"role-{Guid.NewGuid():N}", null, codes, CancellationToken.None));

    /// <summary>A new account holding the role, signed in with both steps.</summary>
    public static async Task<SignedInStaff> SignInWithRoleAsync(this ApiFactory factory, Guid clinicId, Guid roleId)
    {
        var (accountId, email) = await factory.CreateAccountAsync(clinicId);
        await factory.RolesAsync(clinicId, roles => roles.AssignAsync(accountId, roleId, CancellationToken.None));
        return new SignedInStaff(await factory.SignInAsAsync(email), accountId, email, roleId);
    }

    /// <summary>A new account holding a new custom role with exactly these codes, signed in.</summary>
    public static async Task<SignedInStaff> SignInWithAsync(this ApiFactory factory, Guid clinicId, params string[] codes) =>
        await factory.SignInWithRoleAsync(clinicId, await factory.CreateRoleAsync(clinicId, codes));

    /// <summary>A new account holding «مالك العيادة», signed in.</summary>
    public static async Task<SignedInStaff> SignInAsOwnerAsync(this ApiFactory factory, Guid clinicId) =>
        await factory.SignInWithRoleAsync(clinicId, await factory.ReadyRoleAsync(clinicId, "owner"));

    public static Task<HttpResponseMessage> NeedsAsync(this HttpClient browser, string code) =>
        browser.GetAsync(new Uri($"/probe/needs/{code}", UriKind.Relative));

    public static int RunsOf(string code) => ProbeEndpoints.Runs.GetValueOrDefault(code);
}
