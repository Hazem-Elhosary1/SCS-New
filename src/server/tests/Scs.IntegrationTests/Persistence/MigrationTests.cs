using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class MigrationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrator_creates_the_platform_structure_with_one_heartbeat_row()
    {
        await sql.EnsureMigratedAsync();

        Assert.Equal(1, await sql.ScalarAsync<int>(sql.AdminConnection,
            "SELECT COUNT(*) FROM sys.schemas WHERE name = 'platform'"));
        Assert.Equal(1, await sql.ScalarAsync<int>(sql.AdminConnection,
            "SELECT COUNT(*) FROM platform.JobHeartbeat"));
        Assert.Equal((short)1, await sql.ScalarAsync<short>(sql.AdminConnection,
            "SELECT Id FROM platform.JobHeartbeat"));
        Assert.Equal("platform-heartbeat", await sql.ScalarAsync<string>(sql.AdminConnection,
            "SELECT JobName FROM platform.JobHeartbeat"));
        Assert.True(await sql.ScalarAsync<int>(sql.AdminConnection,
            "SELECT COUNT(*) FROM platform.__EFMigrationsHistory") >= 1);
    }

    [Fact]
    public async Task Columns_have_the_types_the_data_model_gives()
    {
        await sql.EnsureMigratedAsync();

        async Task<string?> Column(string name) => await sql.ScalarAsync<string>(sql.AdminConnection,
            $"""
             SELECT DATA_TYPE + ISNULL('(' + CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)) + ')', '')
                    + CASE IS_NULLABLE WHEN 'NO' THEN ' NOT NULL' ELSE ' NULL' END
             FROM INFORMATION_SCHEMA.COLUMNS
             WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'JobHeartbeat' AND COLUMN_NAME = '{name}'
             """);

        Assert.Equal("smallint NOT NULL", await Column("Id"));
        Assert.Equal("nvarchar(100) NOT NULL", await Column("JobName"));
        Assert.Equal("datetimeoffset NOT NULL", await Column("LastRunAtUtc"));
        Assert.Equal("nvarchar(40) NOT NULL", await Column("LastRunVersion"));
    }

    [Fact]
    public async Task Running_the_migrator_again_changes_nothing()
    {
        await sql.EnsureMigratedAsync();
        var before = await sql.ScalarAsync<int>(sql.AdminConnection, "SELECT COUNT(*) FROM platform.__EFMigrationsHistory");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await Migrator.Migrator.RunAsync(sql.MigrationConnection, output, error, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(before, await sql.ScalarAsync<int>(sql.AdminConnection, "SELECT COUNT(*) FROM platform.__EFMigrationsHistory"));
        Assert.Equal(1, await sql.ScalarAsync<int>(sql.AdminConnection, "SELECT COUNT(*) FROM platform.JobHeartbeat"));
    }

    [Fact]
    public async Task A_second_heartbeat_row_is_refused()
    {
        await sql.EnsureMigratedAsync();

        var error = await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteAsync(sql.AppConnection,
            "INSERT INTO platform.JobHeartbeat (Id, JobName, LastRunAtUtc, LastRunVersion) VALUES (2, 'x', SYSDATETIMEOFFSET(), 'x')"));

        Assert.Contains("CK_JobHeartbeat_SingleRow", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CREATE TABLE platform.Intruder (Id int)")]
    [InlineData("ALTER TABLE platform.JobHeartbeat ADD Extra int NULL")]
    [InlineData("DROP TABLE platform.JobHeartbeat")]
    [InlineData("CREATE SCHEMA intruder")]
    public async Task Application_login_cannot_alter_structure(string statement)
    {
        await sql.EnsureMigratedAsync();

        await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteAsync(sql.AppConnection, statement));
    }

    [Fact]
    public async Task Application_login_can_read_and_write_data()
    {
        await sql.EnsureMigratedAsync();

        await sql.ExecuteAsync(sql.AppConnection,
            "UPDATE platform.JobHeartbeat SET LastRunVersion = LastRunVersion WHERE Id = 1");

        Assert.Equal(1, await sql.ScalarAsync<int>(sql.AppConnection, "SELECT COUNT(*) FROM platform.JobHeartbeat"));
    }

    [Fact]
    public async Task Missing_connection_is_reported_by_name_and_fails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Migrator.Migrator.RunAsync(null, output, error, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains("SCS_DB__MIGRATION_CONNECTION", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_does_not_print_the_connection()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Migrator.Migrator.RunAsync(
            "Server=localhost,1;Database=x;User Id=someone;Password=S3cret-Value!;Connect Timeout=1;TrustServerCertificate=true",
            output, error, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.DoesNotContain("S3cret-Value!", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("someone", error.ToString(), StringComparison.Ordinal);
    }
}
