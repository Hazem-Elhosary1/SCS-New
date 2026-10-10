using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Isolation;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, FR-056 and research R4: before sign-in the application can find which clinic an
/// account belongs to, and nothing more. This is the one place the application's own login sees
/// across clinics, so it is proven here against the database itself.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class SignInLookupTests(SqlServerFixture sql)
{
    private const string Lookup = "EXEC [identity].usp_FindAccountForSignIn @Email = @email";

    private async Task<(Guid Clinic, Guid Account, string Email)> SeedAsync()
    {
        await sql.EnsureMigratedAsync();
        var clinic = await sql.Main.SeedClinicAsync();
        var email = $"lookup-{Guid.NewGuid():N}@lookup.example";
        await using var connection = await TestDatabase.OpenAsync(sql.Main.Crossing);
        var account = await IdentityRows.InsertAccountAsync(connection, clinic, email);
        return (clinic, account, email);
    }

    private static async Task<List<object?[]>> RowsAsync(string connectionString, string email, Guid? clinic = null)
    {
        await using var connection = await TestDatabase.OpenAsync(connectionString, clinic);
        await using var command = connection.CreateCommand();
        command.CommandText = Lookup;
        command.Parameters.AddWithValue("@email", email);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            Assert.Equal(["AccountId", "ClinicId"], names);
            rows.Add([reader.GetGuid(0), reader.GetGuid(1)]);
        }

        return rows;
    }

    [Fact]
    public async Task With_no_clinic_the_application_sees_no_account()
    {
        var (_, _, email) = await SeedAsync();

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT COUNT(*) FROM [identity].StaffAccount"));
        Assert.Equal(
            0,
            await TestDatabase.ScalarAsync<int>(sql.Main.App, $"SELECT COUNT(*) FROM [identity].StaffAccount WHERE Email = '{email}'"));
    }

    [Fact]
    public async Task The_lookup_returns_the_account_and_its_clinic_and_nothing_else()
    {
        var (clinic, account, email) = await SeedAsync();

        var row = Assert.Single(await RowsAsync(sql.Main.App, email));

        Assert.Equal(account, row[0]);
        Assert.Equal(clinic, row[1]);
    }

    [Fact]
    public async Task The_lookup_works_the_same_whatever_clinic_the_connection_is_for()
    {
        var (clinic, account, email) = await SeedAsync();
        var another = await sql.Main.SeedClinicAsync();

        var row = Assert.Single(await RowsAsync(sql.Main.App, email, another));

        Assert.Equal(account, row[0]);
        Assert.Equal(clinic, row[1]);
    }

    [Fact]
    public async Task An_unknown_email_returns_no_row()
    {
        await SeedAsync();

        Assert.Empty(await RowsAsync(sql.Main.App, $"nobody-{Guid.NewGuid():N}@lookup.example"));
    }

    [Fact]
    public async Task After_the_lookup_the_same_connection_still_sees_no_account()
    {
        var (_, _, email) = await SeedAsync();
        await using var connection = await TestDatabase.OpenAsync(sql.Main.App);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = Lookup;
            command.Parameters.AddWithValue("@email", email);
            await command.ExecuteNonQueryAsync();
        }

        // Running as the lookup user ended with the procedure.
        Assert.Equal("scs_app", await TestDatabase.ScalarAsync<string>(connection, "SELECT USER_NAME()"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(connection, "SELECT COUNT(*) FROM [identity].StaffAccount"));
    }

    [Fact]
    public async Task The_application_cannot_become_the_lookup_user()
    {
        await SeedAsync();

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "EXECUTE AS USER = 'scs_signin_lookup'"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Report, "EXECUTE AS USER = 'scs_signin_lookup'"));
        Assert.Equal(
            -1,
            await TestDatabase.TryExecuteAsync(
                sql.Main.App,
                "ALTER PROCEDURE [identity].usp_FindAccountForSignIn @Email nvarchar(254) WITH EXECUTE AS 'scs_signin_lookup' AS SELECT * FROM [identity].StaffAccount"));
    }

    [Fact]
    public async Task The_lookup_user_holds_no_permission_of_its_own()
    {
        await SeedAsync();

        // As the administrator, become the lookup user and try to read directly.
        await using var connection = await TestDatabase.OpenAsync(sql.Main.Admin);
        await TestDatabase.ExecuteAsync(connection, "EXECUTE AS USER = 'scs_signin_lookup'");

        foreach (var statement in new[]
                 {
                     "SELECT COUNT(*) FROM [identity].StaffAccount",
                     "SELECT TOP (1) PasswordHash FROM [identity].StaffAccount",
                     "SELECT COUNT(*) FROM tenancy.Clinic",
                     "UPDATE [identity].StaffAccount SET FailedAttempts = 0",
                 })
        {
            await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(connection, statement));
        }

        await TestDatabase.ExecuteAsync(connection, "REVERT");
    }

    [Fact]
    public async Task The_report_login_cannot_use_the_lookup()
    {
        var (_, _, email) = await SeedAsync();

        await Assert.ThrowsAsync<SqlException>(() => RowsAsync(sql.Main.Report, email));
    }

    [Fact]
    public async Task One_email_has_one_account_on_the_whole_platform()
    {
        var (_, _, email) = await SeedAsync();
        var another = await sql.Main.SeedClinicAsync();

        // Even the operator login, and even for a different clinic, is refused by the database.
        await using var connection = await TestDatabase.OpenAsync(sql.Main.Crossing);
        var refusal = await Assert.ThrowsAsync<SqlException>(() => IdentityRows.InsertAccountAsync(connection, another, email));
        Assert.Contains("UX_StaffAccount_Email", refusal.Message, StringComparison.Ordinal);
    }
}
