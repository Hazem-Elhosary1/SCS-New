using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// The guardrail of constitution Principle II: the build fails when any table is neither
/// isolated nor declared platform-wide. Migrations are applied one at a time and the catalog is
/// checked after each, so a table created in one migration and protected in the next fails on
/// the first (spec 002, FR-020 and FR-021).
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class IsolationGuardrailTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Every_table_is_isolated_or_declared_platform_wide_after_every_migration()
    {
        var database = await sql.CreateScratchDatabaseAsync();
        await database.InstallJobStorageAsync();
        Assert.Empty(await IsolationCatalog.FindViolationsAsync(database.Admin));

        var applied = 0;
        foreach (var module in Migrator.Migrator.Modules)
        {
            foreach (var migration in module.GetMigrations(database.Migration))
            {
                await module.MigrateAsync(database.Migration, migration, CancellationToken.None);
                applied++;

                var violations = await IsolationCatalog.FindViolationsAsync(database.Admin);
                Assert.True(
                    violations.Count == 0,
                    $"After migration {module.Name}/{migration}:\n  " + string.Join("\n  ", violations));
            }
        }

        Assert.True(applied >= 2, "Expected at least the Tenancy and Platform migrations to be applied.");
    }

    [Fact]
    public async Task The_shared_database_has_no_unprotected_table()
    {
        await sql.EnsureMigratedAsync();

        var violations = await IsolationCatalog.FindViolationsAsync(sql.AdminConnection);

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

/// <summary>Proves the guardrail notices each way a table can be left unprotected.</summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class GuardrailCatchesTests(SqlServerFixture sql)
{
    private const string Columns = "(Id int NOT NULL PRIMARY KEY, ClinicId uniqueidentifier NOT NULL)";

    private static async Task<TestDatabase> PrepareAsync(SqlServerFixture sql)
    {
        var database = await sql.CreateScratchDatabaseAsync();
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreateAccessFunction());
        await TestDatabase.ExecuteAsync(database.Admin, "CREATE SCHEMA probe");
        return database;
    }

    private static async Task<string> SingleViolationAsync(TestDatabase database)
    {
        var violations = await IsolationCatalog.FindViolationsAsync(database.Admin);
        return string.Join(" | ", violations);
    }

    [Fact]
    public async Task A_correctly_isolated_table_passes()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.Good {Columns}");
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreatePolicy("probe", "Good"));

        Assert.Empty(await IsolationCatalog.FindViolationsAsync(database.Admin));
    }

    [Fact]
    public async Task A_module_that_runs_as_another_user_must_be_declared()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.Good {Columns}");
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreatePolicy("probe", "Good"));
        await TestDatabase.ExecuteAsync(
            database.Admin,
            "CREATE PROCEDURE probe.usp_SeeEverything WITH EXECUTE AS 'scs_signin_lookup' AS SELECT Id FROM probe.Good");
        await TestDatabase.ExecuteAsync(
            database.Admin,
            "CREATE PROCEDURE probe.usp_AsOwner WITH EXECUTE AS OWNER AS SELECT Id FROM probe.Good");
        await TestDatabase.ExecuteAsync(database.Admin, "CREATE PROCEDURE probe.usp_AsCaller AS SELECT Id FROM probe.Good");

        var violations = await IsolationCatalog.FindViolationsAsync(database.Admin);

        Assert.Equal(
            [
                "probe.usp_AsOwner: runs as OWNER and is not declared in DeclaredDatabaseCrossings",
                "probe.usp_SeeEverything: runs as scs_signin_lookup and is not declared in DeclaredDatabaseCrossings",
            ],
            violations);
    }

    [Fact]
    public async Task A_table_with_no_policy_is_reported_by_name()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.NoPolicy {Columns}");

        Assert.Equal("probe.NoPolicy: has no isolation policy", await SingleViolationAsync(database));
    }

    [Fact]
    public async Task A_policy_missing_one_block_is_reported()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.Partial {Columns}");
        await TestDatabase.ExecuteAsync(
            database.Admin,
            """
            CREATE SECURITY POLICY probe.PartialIsolation
                ADD FILTER PREDICATE tenancy.fn_ClinicAccess(ClinicId) ON probe.Partial,
                ADD BLOCK PREDICATE tenancy.fn_ClinicAccess(ClinicId) ON probe.Partial AFTER UPDATE,
                ADD BLOCK PREDICATE tenancy.fn_ClinicAccess(ClinicId) ON probe.Partial BEFORE UPDATE,
                ADD BLOCK PREDICATE tenancy.fn_ClinicAccess(ClinicId) ON probe.Partial BEFORE DELETE
                WITH (STATE = ON, SCHEMABINDING = ON);
            """);

        Assert.Equal(
            "probe.Partial: has no AFTER INSERT block predicate on ClinicId", await SingleViolationAsync(database));
    }

    [Fact]
    public async Task A_filter_only_policy_is_reported_for_each_missing_block()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.FilterOnly {Columns}");
        await TestDatabase.ExecuteAsync(
            database.Admin,
            """
            CREATE SECURITY POLICY probe.FilterOnlyIsolation
                ADD FILTER PREDICATE tenancy.fn_ClinicAccess(ClinicId) ON probe.FilterOnly
                WITH (STATE = ON, SCHEMABINDING = ON);
            """);

        var violations = await IsolationCatalog.FindViolationsAsync(database.Admin);

        Assert.Equal(4, violations.Count);
        Assert.All(violations, v => Assert.StartsWith("probe.FilterOnly: has no ", v, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_nullable_clinic_column_is_reported()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(
            database.Admin, "CREATE TABLE probe.Nullable (Id int NOT NULL PRIMARY KEY, ClinicId uniqueidentifier NULL)");
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreatePolicy("probe", "Nullable"));

        Assert.Equal("probe.Nullable: ClinicId is nullable", await SingleViolationAsync(database));
    }

    [Fact]
    public async Task A_policy_that_is_switched_off_is_reported()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, $"CREATE TABLE probe.Disabled {Columns}");
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreatePolicy("probe", "Disabled"));
        await TestDatabase.ExecuteAsync(database.Admin, "ALTER SECURITY POLICY probe.DisabledIsolation WITH (STATE = OFF)");

        var violations = await SingleViolationAsync(database);

        Assert.Contains("probe.Disabled: its isolation policy is switched off", violations, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_table_with_no_clinic_column_must_be_declared()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(database.Admin, "CREATE TABLE probe.Lookup (Id int NOT NULL PRIMARY KEY, Name nvarchar(50))");

        Assert.Equal(
            "probe.Lookup: has no ClinicId column and is not declared in PlatformWideTables",
            await SingleViolationAsync(database));
    }

    [Fact]
    public async Task A_clinic_column_of_the_wrong_type_is_reported()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(
            database.Admin, "CREATE TABLE probe.WrongType (Id int NOT NULL PRIMARY KEY, ClinicId int NOT NULL)");

        var violations = await SingleViolationAsync(database);

        Assert.Contains("probe.WrongType: ClinicId is int, not uniqueidentifier", violations, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_declared_platform_wide_table_passes_without_isolation()
    {
        var database = await PrepareAsync(sql);
        await TestDatabase.ExecuteAsync(
            database.Admin, "CREATE TABLE probe.__EFMigrationsHistory (MigrationId nvarchar(150) NOT NULL PRIMARY KEY)");

        Assert.Empty(await IsolationCatalog.FindViolationsAsync(database.Admin));
    }
}
