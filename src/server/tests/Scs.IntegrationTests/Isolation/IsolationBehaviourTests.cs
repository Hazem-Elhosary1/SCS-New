using Microsoft.Data.SqlClient;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel;
using Scs.Tenancy.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>The sample for the clinic record itself.</summary>
public sealed class ClinicSample : IIsolationSample
{
    public Type EntityType => typeof(Clinic);

    public string Table => "tenancy.Clinic";

    public string HarmlessChange => "NameEn = NameEn";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO tenancy.Clinic (Id, ClinicId, NameAr, NameEn, State, CreatedAtUtc)
            VALUES (@new, @clinic, N'عيادة', N'Clinic', N'active', SYSDATETIMEOFFSET())
            """;

        // The clinic record is the one table whose key is the clinic id itself; a second row
        // for an existing clinic is impossible, so a new id is offered and the clinic claimed.
        command.Parameters.AddWithValue("@new", Guid.CreateVersion7());
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Spec 002, User Story 1 (SC-001, SC-002): for every clinic-owned entity in every module, one
/// clinic can neither see nor change another's rows, and with no clinic nothing is reachable.
/// Entities are discovered by reflection, so there is no list to keep up to date: adding an
/// entity without a sample fails here, by name.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class IsolationBehaviourTests(SqlServerFixture sql)
{
    private static readonly IReadOnlyList<Type> ClinicOwnedEntities =
    [
        .. Migrator.Migrator.Modules
            .Select(module => module.GetType().Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IClinicOwned).IsAssignableFrom(type)),
    ];

    private static readonly IReadOnlyList<IIsolationSample> Samples =
    [
        .. typeof(IsolationBehaviourTests).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && typeof(IIsolationSample).IsAssignableFrom(type)
                           && type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IIsolationSample)Activator.CreateInstance(type)!),
    ];

    public static TheoryData<string> EntityNames => new(ClinicOwnedEntities.Select(type => type.FullName!));

    [Fact]
    public void Every_clinic_owned_entity_has_an_isolation_sample()
    {
        Assert.NotEmpty(ClinicOwnedEntities);

        var missing = ClinicOwnedEntities
            .Where(entity => Samples.All(sample => sample.EntityType != entity))
            .Select(entity => entity.FullName);

        Assert.True(
            !missing.Any(),
            "Add an IIsolationSample in the integration test project for: " + string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(EntityNames))]
    public async Task One_clinic_cannot_reach_anothers_rows(string entityName)
    {
        await sql.EnsureMigratedAsync();
        var sample = Samples.Single(candidate => candidate.EntityType.FullName == entityName);

        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();

        if (sample.EntityType != typeof(Clinic))
        {
            await using var operatorConnection = await TestDatabase.OpenAsync(sql.Main.Crossing);
            await sample.InsertAsync(operatorConnection, a);
            await sample.InsertAsync(operatorConnection, b);
        }

        await IsolationProbe.AssertIsolatedAsync(sql.Main, sample, a, b);
    }

    [Fact]
    public async Task The_operator_login_sees_every_clinic_and_the_application_login_only_its_own()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();
        var both = $"SELECT COUNT(*) FROM tenancy.Clinic WHERE Id IN ('{a}', '{b}')";

        Assert.Equal(2, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, both));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.App, both, a));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, both));
    }
}

/// <summary>
/// The same assertions against a table the application login is allowed to write, so the block
/// predicates themselves are what refuses the writes (the clinic record is also protected by a
/// plain denial, which would hide a missing predicate).
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class PredicateBehaviourTests(SqlServerFixture sql)
{
    internal sealed class NoteSample : IIsolationSample
    {
        // Not an entity of any module: this sample is used only by this class.
        public Type EntityType => typeof(NoteSample);

        public string Table => "probe.Note";

        public string HarmlessChange => "Body = Body + N'!'";

        public async Task InsertAsync(SqlConnection connection, Guid clinicId)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO probe.Note (Id, ClinicId, Body) VALUES (NEWID(), @clinic, N'note')";
            command.Parameters.AddWithValue("@clinic", clinicId);
            await command.ExecuteNonQueryAsync();
        }
    }

    public static async Task<TestDatabase> CreateProbeDatabaseAsync(SqlServerFixture sql)
    {
        var database = await sql.CreateScratchDatabaseAsync();
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreateAccessFunction());
        await TestDatabase.ExecuteAsync(database.Admin, "CREATE SCHEMA probe");
        await TestDatabase.ExecuteAsync(
            database.Admin,
            "CREATE TABLE probe.Note (Id uniqueidentifier NOT NULL PRIMARY KEY, ClinicId uniqueidentifier NOT NULL, Body nvarchar(100) NOT NULL)");
        await TestDatabase.ExecuteAsync(database.Admin, ClinicIsolationSql.CreatePolicy("probe", "Note"));
        return database;
    }

    [Fact]
    public async Task The_predicates_alone_isolate_a_table_the_application_may_write()
    {
        var database = await CreateProbeDatabaseAsync(sql);
        var sample = new NoteSample();
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        await using (var operatorConnection = await TestDatabase.OpenAsync(database.Crossing))
        {
            await sample.InsertAsync(operatorConnection, a);
            await sample.InsertAsync(operatorConnection, b);
        }

        await IsolationProbe.AssertIsolatedAsync(database, sample, a, b);
    }

    [Fact]
    public async Task The_application_writes_its_own_clinics_rows_normally()
    {
        var database = await CreateProbeDatabaseAsync(sql);
        var sample = new NoteSample();
        var a = Guid.CreateVersion7();

        await using (var asA = await TestDatabase.OpenAsync(database.App, a))
        {
            await sample.InsertAsync(asA, a);
            await sample.InsertAsync(asA, a);
        }

        Assert.Equal(2, await TestDatabase.ExecuteAsync(database.App, "UPDATE probe.Note SET Body = N'changed'", a));
        Assert.Equal(2, await TestDatabase.ExecuteAsync(database.App, "DELETE FROM probe.Note", a));
    }

    [Fact]
    public async Task The_migrator_login_can_reach_every_clinics_rows_for_data_migrations()
    {
        var database = await CreateProbeDatabaseAsync(sql);
        var sample = new NoteSample();

        await using (var migrator = await TestDatabase.OpenAsync(database.Migration))
        {
            await sample.InsertAsync(migrator, Guid.CreateVersion7());
            await sample.InsertAsync(migrator, Guid.CreateVersion7());
        }

        Assert.Equal(2, await TestDatabase.ScalarAsync<int>(database.Migration, "SELECT COUNT(*) FROM probe.Note"));
    }
}
