using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, FR-001 to FR-004: creating accounts for a clinic, and one email having one account
/// on the whole platform.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class StaffAccountsTests(SqlServerFixture sql)
{
    private static async Task<IReadOnlyDictionary<string, string[]>> RefusedAsync(Func<Task> work)
    {
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(work);
        Assert.Equal("VALIDATION_FAILED", refusal.Code);
        return refusal.Errors!;
    }

    [Fact]
    public async Task An_active_account_is_created_with_a_password_and_an_invited_one_without()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        var active = await world.Factory.CreateAccountAsync(world.Clinic, email: $"  Mixed.Case-{Guid.NewGuid():N}@Clinic.Example ");
        var invited = await world.Factory.CreateAccountAsync(world.Clinic, StaffAccountStates.Invited);

        Assert.Equal(
            active.Email.Trim().ToLowerInvariant(),
            await world.ScalarAsync<string>($"SELECT Email FROM [identity].StaffAccount WHERE Id = '{active.Id}'"));
        Assert.StartsWith(
            "v1.210000.",
            await world.ScalarAsync<string>($"SELECT PasswordHash FROM [identity].StaffAccount WHERE Id = '{active.Id}'"),
            StringComparison.Ordinal);
        Assert.Equal(world.Clinic, await world.ScalarAsync<Guid>($"SELECT ClinicId FROM [identity].StaffAccount WHERE Id = '{active.Id}'"));
        Assert.Null(await world.ScalarAsync<object>($"SELECT PasswordHash FROM [identity].StaffAccount WHERE Id = '{invited.Id}'"));
        Assert.Equal("invited", await world.ScalarAsync<string>($"SELECT State FROM [identity].StaffAccount WHERE Id = '{invited.Id}'"));
    }

    [Theory]
    [InlineData("", "REQUIRED")]
    [InlineData("no-at-sign", "INVALID_FORMAT")]
    [InlineData("two@@signs.example", "INVALID_FORMAT")]
    [InlineData("nodot@domain", "INVALID_FORMAT")]
    public async Task A_bad_email_is_refused_by_field_code(string email, string code)
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        var errors = await RefusedAsync(() => world.Factory.CreateAccountAsync(world.Clinic, email: email));

        Assert.Equal([code], errors["email"]);
    }

    [Fact]
    public async Task An_active_account_needs_a_password_that_meets_every_rule()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        var none = await RefusedAsync(() => world.Factory.CreateAccountAsync(world.Clinic, password: null));
        var weak = await RefusedAsync(() => world.Factory.CreateAccountAsync(world.Clinic, password: "short"));

        Assert.Equal(["REQUIRED"], none["password"]);
        Assert.Equal(["TOO_SHORT", "NO_UPPER_LOWER", "NO_DIGIT", "NO_SYMBOL"], weak["password"]);
    }

    [Fact]
    public async Task One_email_has_one_account_and_the_refusal_does_not_say_where_it_is()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var (_, email) = await world.Factory.CreateAccountAsync(world.Clinic);

        var sameClinic = await RefusedAsync(() => world.Factory.CreateAccountAsync(world.Clinic, email: email));
        var otherClinic = await RefusedAsync(() => world.Factory.CreateAccountAsync(other, email: email.ToUpperInvariant()));

        Assert.Equal(["TAKEN"], sameClinic["email"]);
        Assert.Equal(["TAKEN"], otherClinic["email"]);
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].StaffAccount WHERE Email = '{email}'"));
    }

    [Fact]
    public async Task No_operation_changes_an_accounts_email()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.Factory.CreateAccountAsync(world.Clinic);

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var accounts = services.GetRequiredService<IStaffAccounts>();
            await accounts.SetStateAsync(id, StaffAccountStates.Disabled, default);
            await accounts.SetStateAsync(id, StaffAccountStates.Active, default);
            await accounts.UnlockAsync(id, default);
            await accounts.ClearLockAsync(id, default);
        });

        Assert.Equal(email, await world.ScalarAsync<string>($"SELECT Email FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.DoesNotContain(
            typeof(IStaffAccounts).GetMethods(),
            method => method.Name != nameof(IStaffAccounts.CreateAsync)
                      && method.GetParameters().Any(p => p.Name!.Contains("email", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task The_seed_adds_eight_made_up_accounts_once_and_one_holds_a_temporary_password()
    {
        await sql.EnsureMigratedAsync();
        var noor = await sql.Main.SeedClinicAsync();
        var shifa = await sql.Main.SeedClinicAsync();
        var clinics = new Dictionary<string, Guid> { ["noor"] = noor, ["shifa"] = shifa };

        // Emails are unique on the platform, so a second run in the same test database adds none.
        var first = await Scs.Identity.IdentityModule.SeedAsync(sql.Main.App, clinics, SignIn.Password, default);
        var second = await Scs.Identity.IdentityModule.SeedAsync(sql.Main.App, clinics, SignIn.Password, default);

        Assert.True(first.Count is 0 or 8, $"The first run added {first.Count}.");
        Assert.Empty(second);
        Assert.Equal(
            8,
            await TestDatabase.ScalarAsync<int>(
                sql.Main.Crossing, "SELECT COUNT(*) FROM [identity].StaffAccount WHERE Email LIKE '%@noor.example' OR Email LIKE '%@shifa.example'"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => Scs.Identity.IdentityModule.SeedAsync(sql.Main.App, clinics, "weak", default));

        if (first.Count == 0)
        {
            // Seeded earlier in this database, for other clinics: nothing here is this run's.
            return;
        }

        // Spec 005: every seeded account holds a role, and one in each clinic holds a custom role
        // without the view of the roles page — the person who sees the no-permission state.
        Assert.Equal(
            [
                "doctor@shifa.example=doctor", "former@noor.example=doctor", "new@shifa.example=manager",
                "owner@noor.example=owner", "owner@shifa.example=owner", "reception@noor.example=فريق العيادة",
                "reception@shifa.example=فريق العيادة", "temp@noor.example=manager",
            ],
            await TestDatabase.ReadColumnAsync(
                sql.Main.Crossing,
                """
                SELECT CONCAT(a.Email, '=', COALESCE(r.ReadyKey, r.Name))
                FROM [identity].StaffAccount a JOIN [identity].Role r ON r.Id = a.RoleId
                WHERE a.Email LIKE '%@noor.example' OR a.Email LIKE '%@shifa.example'
                ORDER BY a.Email
                """));
        var custom = await TestDatabase.ReadColumnAsync(
            sql.Main.Crossing,
            $"SELECT p.Code FROM [identity].RolePermission p JOIN [identity].Role r ON r.Id = p.RoleId WHERE r.ClinicId = '{noor}' AND r.Name = N'فريق العيادة'");
        Assert.Equal(6, custom.Count);
        Assert.DoesNotContain("admin.roles.view", custom);
        Assert.Equal(4, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{noor}'"));
        Assert.Equal(4, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{shifa}'"));

        // Spec 004: one of them holds a temporary password, kept usable by every run of the seed
        // until somebody replaces it — after which the seed leaves that account alone.
        const string Temporary = "SELECT CONCAT(MustChangePassword, '|', IIF(TemporaryPasswordExpiresAtUtc > DATEADD(HOUR, 71, SYSDATETIMEOFFSET()), 'fresh', 'stale')) FROM [identity].StaffAccount WHERE Email = 'temp@noor.example'";
        Assert.Equal(
            1,
            await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM [identity].StaffAccount WHERE MustChangePassword = 1 AND (Email LIKE '%@noor.example' OR Email LIKE '%@shifa.example')"));
        await sql.ExecuteAsync(
            sql.Main.Crossing,
            "UPDATE [identity].StaffAccount SET TemporaryPasswordExpiresAtUtc = DATEADD(HOUR, 1, SYSDATETIMEOFFSET()) WHERE Email = 'temp@noor.example'");
        await Scs.Identity.IdentityModule.SeedAsync(sql.Main.App, clinics, SignIn.Password, default);
        Assert.Equal("1|fresh", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, Temporary));

        await sql.ExecuteAsync(
            sql.Main.Crossing,
            "UPDATE [identity].StaffAccount SET MustChangePassword = 0, TemporaryPasswordExpiresAtUtc = NULL WHERE Email = 'temp@noor.example'");
        await Scs.Identity.IdentityModule.SeedAsync(sql.Main.App, clinics, SignIn.Password, default);
        Assert.Equal("0|stale", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, Temporary));

        // Leave it as the seed first made it, for whichever test runs this next.
        await sql.ExecuteAsync(
            sql.Main.Crossing,
            "UPDATE [identity].StaffAccount SET MustChangePassword = 1, TemporaryPasswordExpiresAtUtc = DATEADD(HOUR, 72, SYSDATETIMEOFFSET()) WHERE Email = 'temp@noor.example'");
    }
}

/// <summary>Spec 003, FR-050: what the help window shows.</summary>
public sealed class SupportEndpointTests
{
    [Fact]
    public async Task The_support_channels_are_public_and_come_from_configuration()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/meta/support", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("من السبت إلى الخميس", body.RootElement.GetProperty("hoursAr").GetString());
        Assert.Equal("Saturday to Thursday", body.RootElement.GetProperty("hoursEn").GetString());
        Assert.Equal("19999", body.RootElement.GetProperty("hotline").GetString());
        Assert.Equal("201000000000", body.RootElement.GetProperty("whatsapp").GetString());
        Assert.Equal("support@scs.test", body.RootElement.GetProperty("email").GetString());
    }
}
