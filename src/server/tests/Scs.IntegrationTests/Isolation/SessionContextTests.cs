using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// How the clinic is carried on a connection (spec 002, FR-008, FR-014, FR-018, FR-019).
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class SessionContextTests(SqlServerFixture sql)
{
    private const string Visible = "SELECT COUNT(*) FROM tenancy.Clinic";

    [Fact]
    public async Task Once_set_the_clinic_cannot_be_changed_on_that_connection()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();

        await using var connection = await TestDatabase.OpenAsync(sql.Main.App, a);

        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            connection, $"EXEC sp_set_session_context @key = N'ClinicId', @value = '{b}'"));
        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            connection, "EXEC sp_set_session_context @key = N'ClinicId', @value = NULL"));

        // Still clinic A, and only clinic A.
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(connection, Visible));
        Assert.Equal(a, await TestDatabase.ScalarAsync<Guid>(connection, "SELECT Id FROM tenancy.Clinic"));
    }

    [Fact]
    public async Task A_pooled_connection_keeps_nothing_of_the_previous_work()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();

        // A pool of exactly one connection, so the second piece of work must reuse the first's.
        var pooled = new SqlConnectionStringBuilder(sql.Main.App)
        {
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 1,
            ApplicationName = $"pool-test-{Guid.NewGuid():N}",
        }.ConnectionString;

        int firstSession;
        await using (var first = await TestDatabase.OpenAsync(pooled, a))
        {
            firstSession = await TestDatabase.ScalarAsync<short>(first, "SELECT @@SPID");
            Assert.Equal(1, await TestDatabase.ScalarAsync<int>(first, Visible));
        }

        await using var second = await TestDatabase.OpenAsync(pooled);

        Assert.Equal(firstSession, await TestDatabase.ScalarAsync<short>(second, "SELECT @@SPID"));
        Assert.Null(await TestDatabase.ScalarAsync<object>(second, "SELECT SESSION_CONTEXT(N'ClinicId')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(second, Visible));
    }

    [Fact]
    public async Task A_reused_connection_can_be_given_a_different_clinic_for_the_next_work()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();
        var pooled = new SqlConnectionStringBuilder(sql.Main.App)
        {
            MaxPoolSize = 1,
            ApplicationName = $"pool-test-{Guid.NewGuid():N}",
        }.ConnectionString;

        await using (var first = await TestDatabase.OpenAsync(pooled, a))
        {
            Assert.Equal(a, await TestDatabase.ScalarAsync<Guid>(first, "SELECT Id FROM tenancy.Clinic"));
        }

        await using var second = await TestDatabase.OpenAsync(pooled, b);

        Assert.Equal(b, await TestDatabase.ScalarAsync<Guid>(second, "SELECT Id FROM tenancy.Clinic"));
    }

    [Theory]
    [InlineData("N'not-a-guid'")]
    [InlineData("N''")]
    [InlineData("42")]
    [InlineData("N'00000000-0000-0000-0000-000000000000'")]
    public async Task A_value_that_is_not_a_real_clinic_shows_nothing_and_reveals_nothing(string value)
    {
        await sql.EnsureMigratedAsync();
        await sql.Main.SeedClinicAsync();

        await using var connection = await TestDatabase.OpenAsync(sql.Main.App);
        await TestDatabase.ExecuteAsync(connection, $"EXEC sp_set_session_context @key = N'ClinicId', @value = {value}");

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(connection, Visible));
    }

    [Fact]
    public async Task Any_other_session_key_does_not_open_the_door()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();

        await using var connection = await TestDatabase.OpenAsync(sql.Main.App);
        await TestDatabase.ExecuteAsync(connection, $"EXEC sp_set_session_context @key = N'clinic', @value = '{a}'");
        await TestDatabase.ExecuteAsync(connection, "EXEC sp_set_session_context @key = N'Crossing', @value = 1");
        await TestDatabase.ExecuteAsync(connection, "EXEC sp_set_session_context @key = N'IsOperator', @value = N'true'");

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(connection, Visible));
    }
}

/// <summary>
/// What each login may and may not do (spec 002, FR-005, FR-018): the application cannot widen
/// its own access, switch isolation off, or write the clinic records.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class LoginPermissionTests(SqlServerFixture sql)
{
    [Theory]
    [InlineData("ALTER SECURITY POLICY tenancy.ClinicIsolation WITH (STATE = OFF)")]
    [InlineData("DROP SECURITY POLICY tenancy.ClinicIsolation")]
    [InlineData("DROP FUNCTION tenancy.fn_ClinicAccess")]
    [InlineData("ALTER ROLE scs_cross_clinic ADD MEMBER scs_app")]
    [InlineData("ALTER ROLE db_owner ADD MEMBER scs_app")]
    [InlineData("EXECUTE AS USER = 'scs_ops'")]
    [InlineData("EXECUTE AS USER = 'dbo'")]
    public async Task The_application_login_cannot_switch_isolation_off_or_widen_its_access(string statement)
    {
        await sql.EnsureMigratedAsync();

        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(sql.Main.App, statement));

        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(
            sql.Main.Admin, "SELECT CAST(is_enabled AS int) FROM sys.security_policies WHERE name = 'ClinicIsolation'"));
    }

    [Theory]
    [InlineData("GRANT ALTER ANY SECURITY POLICY TO scs_app")]
    [InlineData("GRANT CONTROL TO scs_app")]
    [InlineData("GRANT ALTER ON SCHEMA::tenancy TO scs_app")]
    [InlineData("REVOKE INSERT ON SCHEMA::tenancy FROM scs_app")]
    public async Task The_application_login_cannot_grant_itself_anything(string statement)
    {
        await sql.EnsureMigratedAsync();

        // Granting to oneself is not always an error in SQL Server, so the outcome is checked.
        await TestDatabase.TryExecuteAsync(sql.Main.App, statement);

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            sql.Main.App, "SELECT HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY SECURITY POLICY')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            sql.Main.App, "SELECT HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            sql.Main.App, "SELECT HAS_PERMS_BY_NAME(N'tenancy', N'SCHEMA', N'ALTER')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            sql.Main.App, "SELECT HAS_PERMS_BY_NAME(N'tenancy', N'SCHEMA', N'INSERT')"));
    }

    [Fact]
    public async Task The_application_login_is_not_in_the_cross_clinic_role()
    {
        await sql.EnsureMigratedAsync();

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT IS_ROLEMEMBER(N'scs_cross_clinic')"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.Report, "SELECT IS_ROLEMEMBER(N'scs_cross_clinic')"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, "SELECT IS_ROLEMEMBER(N'scs_cross_clinic')"));
    }

    [Theory]
    [InlineData("UPDATE tenancy.Clinic SET State = N'active' WHERE ClinicId = '{0}'")]
    [InlineData("UPDATE tenancy.Clinic SET NameEn = N'Renamed by the clinic' WHERE ClinicId = '{0}'")]
    [InlineData("DELETE FROM tenancy.Clinic WHERE ClinicId = '{0}'")]
    public async Task A_clinic_cannot_change_its_own_record(string statement)
    {
        await sql.EnsureMigratedAsync();
        var suspended = await sql.Main.SeedClinicAsync(state: "suspended");

        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            sql.Main.App, string.Format(System.Globalization.CultureInfo.InvariantCulture, statement, suspended), suspended));

        Assert.Equal("suspended", await TestDatabase.ScalarAsync<string>(
            sql.Main.Crossing, $"SELECT State FROM tenancy.Clinic WHERE Id = '{suspended}'"));
    }

    [Fact]
    public async Task The_application_login_has_no_rights_on_the_access_records()
    {
        await sql.EnsureMigratedAsync();

        foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE", "DELETE" })
        {
            Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
                sql.Main.App, $"SELECT HAS_PERMS_BY_NAME(N'crossing', N'SCHEMA', N'{permission}')"));
            Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
                sql.Main.Report, $"SELECT HAS_PERMS_BY_NAME(N'crossing', N'SCHEMA', N'{permission}')"));
        }
    }

    [Theory]
    [InlineData("UPDATE platform.JobHeartbeat SET JobName = JobName")]
    [InlineData("INSERT INTO tenancy.Clinic (Id, ClinicId, NameAr, NameEn, CreatedAtUtc) VALUES (NEWID(), NEWID(), N'x', N'x', SYSDATETIMEOFFSET())")]
    [InlineData("DELETE FROM platform.JobHeartbeat")]
    [InlineData("CREATE TABLE platform.Intruder (Id int)")]
    public async Task The_report_login_cannot_write_anything(string statement)
    {
        await sql.EnsureMigratedAsync();

        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(sql.Main.Report, statement));
    }
}
