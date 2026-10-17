using Scs.IntegrationTests.Fixtures;
using Scs.Plans;
using Scs.Plans.Infrastructure.Seed;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, User Story 5 (FR-022 to FR-024, SC-006): a new platform's catalogue holds the 35
/// seeded features; applying the seed again adds nothing and leaves an operator's edits alone.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeatureSeedApplyTests(SqlServerFixture sql)
{
    private const string Rows =
        "SELECT CONCAT([Key], '|', [Group], '|', [Kind], '|', [NameAr], '|', [NameEn], '|', [UnitAr], '|', [UnitEn], '|', [Baseline], '|', [Specialty], '|', [Status], '|', [IsSold], '|', [IsRetired], '|', [SortOrder]) FROM [plans].[Feature] ORDER BY [Key]";

    // A spec that builds a feature marks it in a migration of its own after the seed's: spec 014
    // built «المستخدمون» (MaxUsersBuilt).
    private static readonly string[] BuiltSince = ["max_users"];

    private static string Of(Scs.Plans.Domain.Feature f) =>
        string.Join('|', f.Key, f.Group, f.Kind, f.NameAr, f.NameEn, f.UnitAr, f.UnitEn, f.Baseline, f.Specialty, BuiltSince.Contains(f.Key) ? "built" : f.Status, f.IsSold ? 1 : 0, 0, f.SortOrder);

    private static async Task ApplySeedAgainAsync(TestDatabase db)
    {
        foreach (var statement in FeatureSeed.Statements())
        {
            await TestDatabase.ExecuteAsync(db.Migration, statement);
        }
    }

    [Fact]
    public async Task A_new_platform_holds_the_thirty_five_features_of_the_seed()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        await new PlansModule().MigrateAsync(db.Migration, null, default);

        var stored = await TestDatabase.ReadColumnAsync(db.Admin, Rows);
        Assert.Equal(35, stored.Count);
        Assert.Equal(FeatureSeed.Features().Select(Of).Order(StringComparer.Ordinal), stored.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Applying_the_seed_again_adds_nothing_and_keeps_what_operators_changed()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        await new PlansModule().MigrateAsync(db.Migration, null, default);

        await TestDatabase.ExecuteAsync(
            db.Admin,
            """
            UPDATE [plans].[Feature] SET [NameEn] = N'Seats', [Group] = 'growth', [Status] = 'built', [Version] = 2 WHERE [Key] = 'max_users';
            UPDATE [plans].[Feature] SET [IsRetired] = 1 WHERE [Key] = 'allow_telegram';
            INSERT INTO [plans].[Feature] ([Key], [NameAr], [NameEn], [Group], [Kind], [Status], [IsSold], [IsRetired], [SortOrder], [Version])
            VALUES ('own_feature', N'ميزة المشغل', N'Operator''s feature', 'reports', 'switch', 'upcoming', 1, 0, 30, 1);
            """);
        var before = await TestDatabase.ReadColumnAsync(db.Admin, Rows);

        await ApplySeedAgainAsync(db);
        await new PlansModule().MigrateAsync(db.Migration, null, default);

        Assert.Equal(before, await TestDatabase.ReadColumnAsync(db.Admin, Rows));
        Assert.Equal(36, before.Count);
        Assert.Contains(before, row => row.StartsWith("max_users|growth|limit|المستخدمون|Seats|", StringComparison.Ordinal));
        Assert.Contains(before, row => row.StartsWith("allow_telegram|", StringComparison.Ordinal) && row.EndsWith("|1|1|20", StringComparison.Ordinal));
    }
}
