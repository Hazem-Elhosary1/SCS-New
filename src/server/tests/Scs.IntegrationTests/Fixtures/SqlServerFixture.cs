using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>
/// A real SQL Server in a container, prepared exactly as a real environment is: the same
/// script creates the database and the two logins, and the same migrator applies the structure.
/// Shared by every test class in the <see cref="DatabaseCollection"/>.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Database = "scs_test";
    private const string AppPassword = "App-Login-Pw-1!";
    private const string MigratorPassword = "Migrator-Login-Pw-1!";

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly SemaphoreSlim _migrating = new(1, 1);
    private bool _migrated;

    /// <summary>Administrator, for assertions about structure.</summary>
    public string AdminConnection => For("sa", MsSqlBuilder.DefaultPassword);

    /// <summary>The application login: data only.</summary>
    public string AppConnection => For("scs_app", AppPassword);

    /// <summary>The migrator login: may alter structure.</summary>
    public string MigrationConnection => For("scs_migrator", MigratorPassword);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var script = (await File.ReadAllTextAsync(FindLoginsScript()))
            .Replace("$(SCS_DB)", Database, StringComparison.Ordinal)
            .Replace("$(SCS_APP_PASSWORD)", AppPassword, StringComparison.Ordinal)
            .Replace("$(SCS_MIGRATOR_PASSWORD)", MigratorPassword, StringComparison.Ordinal);

        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        foreach (var batch in script.Split(["\nGO"], StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The text is the project's own script, not user input.
            command.CommandText = batch;
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Applies the structure once; later calls do nothing.</summary>
    public async Task EnsureMigratedAsync()
    {
        await _migrating.WaitAsync();
        try
        {
            if (_migrated)
            {
                return;
            }

            using var output = new StringWriter();
            using var error = new StringWriter();
            var exit = await Migrator.Migrator.RunAsync(MigrationConnection, output, error, CancellationToken.None);
            Assert.True(exit == 0, $"Migrator failed: {error}");
            _migrated = true;
        }
        finally
        {
            _migrating.Release();
        }
    }

    public async Task<T?> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test code with fixed statements.
        command.CommandText = sql;
#pragma warning restore CA2100
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public Task ExecuteAsync(string connectionString, string sql) => ScalarAsync<object>(connectionString, sql);

    private string For(string user, string password) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = Database,
            UserID = user,
            Password = password,
            TrustServerCertificate = true,
        }.ConnectionString;

    private static string FindLoginsScript()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "compose", "sql", "create-logins.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("deploy/compose/sql/create-logins.sql was not found above the test directory.");
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
