using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// Builds one valid row of a clinic-owned table. Every <c>IClinicOwned</c> entity needs one in
/// this project; an entity without a sample fails <see cref="IsolationBehaviourTests"/> by name.
/// </summary>
public interface IIsolationSample
{
    /// <summary>The entity this sample is for.</summary>
    Type EntityType { get; }

    /// <summary><c>schema.Table</c>.</summary>
    string Table { get; }

    /// <summary>A <c>SET</c> clause that changes a row without changing its meaning.</summary>
    string HarmlessChange { get; }

    /// <summary>Inserts one valid row for the clinic on the given connection.</summary>
    Task InsertAsync(SqlConnection connection, Guid clinicId);
}

/// <summary>
/// The isolation assertions themselves (spec 002, User Story 1), run with raw SQL as the
/// application login so nothing in the application can be what makes them pass.
/// </summary>
public static class IsolationProbe
{
    /// <param name="database">The database to test in.</param>
    /// <param name="sample">The table under test; clinics <paramref name="a"/> and <paramref name="b"/> each already hold one row.</param>
    /// <param name="a">The clinic the work is done for.</param>
    /// <param name="b">The other clinic.</param>
    public static async Task AssertIsolatedAsync(TestDatabase database, IIsolationSample sample, Guid a, Guid b)
    {
        var table = sample.Table;
        var rowOfB = $"SELECT CHECKSUM_AGG(CHECKSUM(*)) FROM {table} WHERE ClinicId = '{b}'";
        var rowOfA = $"SELECT CHECKSUM_AGG(CHECKSUM(*)) FROM {table} WHERE ClinicId = '{a}'";
        var before = await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfB);
        var beforeA = await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfA);

        // Reading as A.
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.App, $"SELECT COUNT(*) FROM {table}", a));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(database.App, $"SELECT COUNT(*) FROM {table} WHERE ClinicId = '{b}'", a));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.App, $"SELECT COUNT(*) FROM {table} WHERE ClinicId = '{a}'", a));

        // Changing or deleting B's row as A: nothing happens, and B's row is exactly as it was.
        Assert.True(await TestDatabase.TryExecuteAsync(database.App, $"UPDATE {table} SET {sample.HarmlessChange} WHERE ClinicId = '{b}'", a) <= 0);
        Assert.True(await TestDatabase.TryExecuteAsync(database.App, $"DELETE FROM {table} WHERE ClinicId = '{b}'", a) <= 0);
        Assert.Equal(before, await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfB));

        // Creating a row for B as A is refused.
        await using (var asA = await TestDatabase.OpenAsync(database.App, a))
        {
            await Assert.ThrowsAsync<SqlException>(() => sample.InsertAsync(asA, b));
        }

        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.Crossing, $"SELECT COUNT(*) FROM {table} WHERE ClinicId = '{b}'"));

        // Moving A's own row to B is refused.
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(database.App, $"UPDATE {table} SET ClinicId = '{b}' WHERE ClinicId = '{a}'", a));
        Assert.Equal(beforeA, await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfA));

        // With no clinic established: nothing is visible and nothing can be written.
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(database.App, $"SELECT COUNT(*) FROM {table}"));
        Assert.True(await TestDatabase.TryExecuteAsync(database.App, $"UPDATE {table} SET {sample.HarmlessChange}") <= 0);
        Assert.True(await TestDatabase.TryExecuteAsync(database.App, $"DELETE FROM {table}") <= 0);
        await using (var noClinic = await TestDatabase.OpenAsync(database.App))
        {
            await Assert.ThrowsAsync<SqlException>(() => sample.InsertAsync(noClinic, a));
        }

        Assert.Equal(before, await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfB));
        Assert.Equal(beforeA, await TestDatabase.ScalarAsync<int>(database.Crossing, rowOfA));

        // The report login is isolated in the same way.
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.Report, $"SELECT COUNT(*) FROM {table}", a));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(database.Report, $"SELECT COUNT(*) FROM {table}"));
    }
}
