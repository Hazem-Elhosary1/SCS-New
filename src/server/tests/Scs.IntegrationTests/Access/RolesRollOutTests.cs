using Scs.Identity;
using Scs.IntegrationTests.Fixtures;
using Scs.Tenancy;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, FR-036 and FR-009: clinics and accounts that exist when the roles tables arrive
/// are brought over by the same migration — three ready roles each, and an owner.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RolesRollOutTests(SqlServerFixture sql)
{
    private const string Before = "PasswordLinksAndTemporaryPasswords";

    private static async Task<Guid> AccountAsync(TestDatabase db, Guid clinic, string state, int minutesAgo)
    {
        var id = Guid.CreateVersion7();
        await TestDatabase.ExecuteAsync(
            db.Crossing,
            $"""
             INSERT INTO [identity].StaffAccount (Id, ClinicId, Email, PasswordHash, State, CreatedAtUtc)
             VALUES ('{id}', '{clinic}', '{id:N}@rollout.example', {(state == "invited" ? "NULL" : "'v1.1.c2FsdA==.aGFzaA=='")},
                     '{state}', DATEADD(MINUTE, -{minutesAgo}, SYSDATETIMEOFFSET()))
             """);
        return id;
    }

    private static Task<List<string>> RolesOfAsync(TestDatabase db, Guid clinic) => TestDatabase.ReadColumnAsync(
        db.Crossing,
        $"""
         SELECT CONCAT(r.ReadyKey, '|', r.Name, '|', r.Description, '|', r.Category, '|', r.IsRenamed, '|', r.HoldsEverything, '|',
                       (SELECT STRING_AGG(CAST(p.Code AS varchar(max)), ',') WITHIN GROUP (ORDER BY p.Code)
                        FROM [identity].RolePermission p WHERE p.RoleId = r.Id))
         FROM [identity].Role r WHERE r.ClinicId = '{clinic}' ORDER BY r.ReadyKey
         """);

    [Fact]
    public async Task Existing_clinics_get_their_ready_roles_and_their_oldest_active_account_becomes_owner()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        var tenancy = new TenancyModule();
        var identity = new IdentityModule();
        await tenancy.MigrateAsync(db.Migration, null, default);
        await identity.MigrateAsync(db.Migration, Before, default);

        // Three clinics as they are before this spec: accounts and no roles.
        var ordinary = await db.SeedClinicAsync();
        var first = await AccountAsync(db, ordinary, "active", minutesAgo: 30);
        var later = await AccountAsync(db, ordinary, "active", minutesAgo: 10);

        var oldestIsDisabled = await db.SeedClinicAsync();
        var disabled = await AccountAsync(db, oldestIsDisabled, "disabled", minutesAgo: 60);
        var invited = await AccountAsync(db, oldestIsDisabled, "invited", minutesAgo: 50);
        var firstActive = await AccountAsync(db, oldestIsDisabled, "active", minutesAgo: 5);

        var empty = await db.SeedClinicAsync();

        await identity.MigrateAsync(db.Migration, null, default);

        async Task<string?> RoleKeyOf(Guid account) => await TestDatabase.ScalarAsync<string>(
            db.Crossing,
            $"SELECT r.ReadyKey FROM [identity].StaffAccount a JOIN [identity].Role r ON r.Id = a.RoleId WHERE a.Id = '{account}'");

        Assert.Equal("owner", await RoleKeyOf(first));
        Assert.Null(await RoleKeyOf(later));
        Assert.Equal("owner", await RoleKeyOf(firstActive));
        Assert.Null(await RoleKeyOf(disabled));
        Assert.Null(await RoleKeyOf(invited));

        foreach (var clinic in new[] { ordinary, oldestIsDisabled, empty })
        {
            Assert.Equal(3, (await RolesOfAsync(db, clinic)).Count);
        }

        // What the migration wrote is what a clinic registered from now on is given.
        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", db.App)
            .With("Scs:DB:REPORT_CONNECTION", db.Report)
            .With("Scs:DB:CROSSING_CONNECTION", db.Crossing);
        var fresh = await db.SeedClinicAsync();
        await factory.ProvisionRolesAsync(fresh);

        Assert.Equal(await RolesOfAsync(db, fresh), await RolesOfAsync(db, ordinary));
        Assert.Equal(await RolesOfAsync(db, fresh), await RolesOfAsync(db, empty));

        // Running the migrations again changes nothing.
        await identity.MigrateAsync(db.Migration, null, default);
        Assert.Equal(4 * 3, await TestDatabase.ScalarAsync<int>(db.Crossing, "SELECT COUNT(*) FROM [identity].Role"));
        Assert.Null(await RoleKeyOf(later));
    }

    [Fact]
    public async Task The_migration_can_be_taken_back()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        var identity = new IdentityModule();
        await new TenancyModule().MigrateAsync(db.Migration, null, default);
        await identity.MigrateAsync(db.Migration, null, default);
        var clinic = await db.SeedClinicAsync();
        await AccountAsync(db, clinic, "active", minutesAgo: 1);

        await identity.MigrateAsync(db.Migration, Before, default);

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            db.Admin, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'identity' AND t.name IN ('Role', 'RolePermission')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            db.Admin, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('[identity].StaffAccount') AND name = 'RoleId'"));

        await identity.MigrateAsync(db.Migration, null, default);
        Assert.Equal(3, await TestDatabase.ScalarAsync<int>(db.Crossing, "SELECT COUNT(*) FROM [identity].Role"));
    }
}
