using Scs.IntegrationTests.Fixtures;
using Scs.Operators;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, FR-010 and FR-027b: the migration that brings roles creates the platform's own
/// three, and gives everyone who was an operator before it «مسؤول عام» — what they could do
/// until then.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorsRolesMigrationTests(SqlServerFixture sql)
{
    private const string Before = "InitialOperators";

    private static async Task<Guid> OperatorAsync(TestDatabase db, string state, int minutesAgo)
    {
        var id = Guid.CreateVersion7();
        await TestDatabase.ExecuteAsync(
            db.Admin,
            $"""
             INSERT INTO [ops].[Operator] (Id, FullName, Email, Language, State, PasswordHash, FailedAttempts, SpentCodes, CreatedAtUtc)
             VALUES ('{id}', N'مشغل', '{id:N}@rollout.example', 'ar', '{state}',
                     {(state == "invited" ? "NULL" : "'v1.1.c2FsdA==.aGFzaA=='")}, 0, 0, DATEADD(MINUTE, -{minutesAgo}, SYSDATETIMEOFFSET()))
             """);
        return id;
    }

    [Fact]
    public async Task Existing_operators_become_super_admins_and_the_three_system_roles_exist()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        var operators = new OperatorsModule();
        await operators.MigrateAsync(db.Migration, Before, default);

        var active = await OperatorAsync(db, "active", minutesAgo: 600);
        var invited = await OperatorAsync(db, "invited", minutesAgo: 30);
        var suspended = await OperatorAsync(db, "suspended", minutesAgo: 900);

        // One of them set a first password an hour ago: that is when it became active.
        await TestDatabase.ExecuteAsync(
            db.Admin,
            $"""
             INSERT INTO [ops].[SignInRecord] (Id, OccurredAtUtc, Kind, OperatorId, TraceId)
             VALUES (NEWID(), DATEADD(MINUTE, -60, SYSDATETIMEOFFSET()), 'first_password_set', '{active}', '{new string('0', 32)}')
             """);

        await operators.MigrateAsync(db.Migration, null, default);

        var roles = await TestDatabase.ReadColumnAsync(
            db.Admin,
            """
            SELECT CONCAT(r.SystemKey, '|', r.Name, '|', (SELECT COUNT(*) FROM [ops].[RolePermission] p WHERE p.RoleId = r.Id))
            FROM [ops].[Role] r ORDER BY r.SystemKey
            """);
        Assert.Equal(["billing-admin||11", "super-admin||28", "viewer||11"], roles);

        var held = await TestDatabase.ReadColumnAsync(
            db.Admin,
            "SELECT CONCAT(o.State, '|', r.SystemKey) FROM [ops].[Operator] o JOIN [ops].[Role] r ON r.Id = o.RoleId ORDER BY o.State");
        Assert.Equal(["active|super-admin", "invited|super-admin", "suspended|super-admin"], held);

        // When each became active, as far as it can be known; an invited account never did.
        var activated = await TestDatabase.ReadColumnAsync(
            db.Admin,
            $"""
             SELECT CONCAT(o.State, '|',
                 CASE WHEN o.ActivatedAtUtc IS NULL THEN 'never'
                      WHEN DATEDIFF(MINUTE, o.ActivatedAtUtc, SYSDATETIMEOFFSET()) BETWEEN 55 AND 65 THEN 'an hour ago'
                      WHEN o.ActivatedAtUtc = o.CreatedAtUtc THEN 'when created' ELSE 'other' END, '|',
                 IIF(o.SuspendedAtUtc IS NULL, 'not suspended', 'suspended'), '|', o.TwoStepRequired)
             FROM [ops].[Operator] o ORDER BY o.State
             """);
        Assert.Equal(
            ["active|an hour ago|not suspended|1", "invited|never|not suspended|1", "suspended|when created|suspended|1"],
            activated);

        // A role is required from now on, and a held role cannot be removed from under its holders.
        await Assert.ThrowsAnyAsync<Exception>(() => TestDatabase.ExecuteAsync(
            db.Admin,
            "INSERT INTO [ops].[Operator] (Id, FullName, Email, Language, State, FailedAttempts, SpentCodes, CreatedAtUtc) VALUES (NEWID(), N'x', 'x@rollout.example', 'ar', 'invited', 0, 0, SYSDATETIMEOFFSET())"));
        await Assert.ThrowsAnyAsync<Exception>(() => TestDatabase.ExecuteAsync(
            db.Admin, "DELETE FROM [ops].[Role] WHERE SystemKey = 'super-admin'"));

        _ = (invited, suspended);
    }
}
