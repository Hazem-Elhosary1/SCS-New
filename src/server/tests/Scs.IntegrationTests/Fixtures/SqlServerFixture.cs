using Microsoft.Data.SqlClient;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Tenancy;
using Testcontainers.MsSql;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>
/// A real SQL Server in a container, prepared exactly as a real environment is: the same
/// script creates the database, the logins and the cross-clinic role, and the same migrator
/// applies the structure. Shared by every test class in the <see cref="DatabaseCollection"/>.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Database = "scs_test";

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly SemaphoreSlim _migrating = new(1, 1);
    private bool _migrated;
    private int _scratch;

    /// <summary>The database most tests share, migrated on first use.</summary>
    public TestDatabase Main { get; private set; } = null!;

    /// <summary>Administrator, for assertions about structure.</summary>
    public string AdminConnection => Main.Admin;

    /// <summary>The application login: data only, under clinic isolation.</summary>
    public string AppConnection => Main.App;

    /// <summary>The migrator login: may alter structure.</summary>
    public string MigrationConnection => Main.Migration;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Main = await CreateDatabaseAsync(Database);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// A new, empty database with the logins, the role and the two administrator-owned schemas,
    /// and nothing else. For tests that must control every structure change themselves.
    /// </summary>
    public Task<TestDatabase> CreateScratchDatabaseAsync() =>
        CreateDatabaseAsync($"scs_scratch_{Interlocked.Increment(ref _scratch)}");

    /// <summary>Applies the structure to the shared database once; later calls do nothing.</summary>
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
            var exit = await Migrator.Migrator.RunAsync(Main.Migration, output, error, CancellationToken.None);
            Assert.True(exit == 0, $"Migrator failed: {error}");
            _migrated = true;
        }
        finally
        {
            _migrating.Release();
        }
    }

    /// <summary>The application, hosted in memory and connected to the shared database.</summary>
    public ApiFactory CreateApi() => new ApiFactory()
        .With("Scs:DB:CONNECTION", Main.App)
        .With("Scs:DB:REPORT_CONNECTION", Main.Report)
        .With("Scs:DB:CROSSING_CONNECTION", Main.Crossing);

    public Task<T?> ScalarAsync<T>(string connectionString, string sql) => TestDatabase.ScalarAsync<T>(connectionString, sql);

    public Task ExecuteAsync(string connectionString, string sql) => TestDatabase.ExecuteAsync(connectionString, sql);

    private async Task<TestDatabase> CreateDatabaseAsync(string name)
    {
        var database = new TestDatabase(_container.GetConnectionString(), name);

        var script = (await File.ReadAllTextAsync(FindLoginsScript()))
            .Replace("$(SCS_DB)", name, StringComparison.Ordinal)
            .Replace("$(SCS_APP_PASSWORD)", TestDatabase.AppPassword, StringComparison.Ordinal)
            .Replace("$(SCS_REPORT_PASSWORD)", TestDatabase.ReportPassword, StringComparison.Ordinal)
            .Replace("$(SCS_OPS_PASSWORD)", TestDatabase.OpsPassword, StringComparison.Ordinal)
            .Replace("$(SCS_MIGRATOR_PASSWORD)", TestDatabase.MigratorPassword, StringComparison.Ordinal);

        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        foreach (var batch in script.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
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

        return database;
    }

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

/// <summary>One database on the test server, with a connection string for each login.</summary>
public sealed class TestDatabase(string serverConnection, string name)
{
    public const string AppPassword = "App-Login-Pw-1!";
    public const string ReportPassword = "Report-Login-Pw-1!";
    public const string OpsPassword = "Ops-Login-Pw-1!";
    public const string MigratorPassword = "Migrator-Login-Pw-1!";

    public string Name { get; } = name;

    /// <summary>Administrator: for creating structure in tests and for catalog queries.</summary>
    public string Admin => For("sa", MsSqlBuilder.DefaultPassword);

    /// <summary>The application login.</summary>
    public string App => For("scs_app", AppPassword);

    /// <summary>The read-only report login.</summary>
    public string Report => For("scs_report", ReportPassword);

    /// <summary>The operator login, a member of the cross-clinic role. Tests seed rows with it.</summary>
    public string Crossing => For("scs_ops", OpsPassword);

    /// <summary>The migrator login.</summary>
    public string Migration => For("scs_migrator", MigratorPassword);

    /// <summary>Opens a connection and, when a clinic is given, sets it exactly as the application does.</summary>
    public static async Task<SqlConnection> OpenAsync(string connectionString, Guid? clinicId = null)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (clinicId is { } id)
        {
            await using var command = ClinicSession.CreateCommand(connection, id);
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }

    public static async Task<T?> ScalarAsync<T>(string connectionString, string sql, Guid? clinicId = null)
    {
        await using var connection = await OpenAsync(connectionString, clinicId);
        return await ScalarAsync<T>(connection, sql);
    }

    public static async Task<T?> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test code with fixed statements.
        command.CommandText = sql;
#pragma warning restore CA2100
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>The first column of every row, as text.</summary>
    public static async Task<List<string>> ReadColumnAsync(string connectionString, string sql)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test code with fixed statements.
        command.CommandText = sql;
#pragma warning restore CA2100
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return values;
    }

    /// <summary>Runs a statement and returns the number of rows it affected.</summary>
    public static async Task<int> ExecuteAsync(string connectionString, string sql, Guid? clinicId = null)
    {
        await using var connection = await OpenAsync(connectionString, clinicId);
        return await ExecuteAsync(connection, sql);
    }

    public static async Task<int> ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test code with fixed statements.
        command.CommandText = sql;
#pragma warning restore CA2100
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Runs a write the caller expects isolation to stop. Returns the rows affected, or -1 when
    /// the database refused the statement outright. Either way nothing was changed.
    /// </summary>
    public static async Task<int> TryExecuteAsync(string connectionString, string sql, Guid? clinicId = null)
    {
        try
        {
            return await ExecuteAsync(connectionString, sql, clinicId);
        }
        catch (SqlException)
        {
            return -1;
        }
    }

    /// <summary>Registers a made-up clinic through the operator login and returns its id.</summary>
    public async Task<Guid> SeedClinicAsync(string state = "active")
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            Crossing,
            $"""
             INSERT INTO tenancy.Clinic (Id, ClinicId, NameAr, NameEn, State, CreatedAtUtc)
             VALUES ('{id}', '{id}', N'عيادة تجريبية', N'Test clinic', N'{state}', SYSDATETIMEOFFSET())
             """);
        return id;
    }

    /// <summary>Creates the job storage schema, as the migrator does.</summary>
    public Task InstallJobStorageAsync() => JobScheduling.InstallSchemaAsync(Migration, CancellationToken.None);

    private string For(string user, string password) =>
        new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = Name,
            UserID = user,
            Password = password,
            TrustServerCertificate = true,
        }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
