using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel;

namespace Scs.IntegrationTests.Isolation;

/// <summary>A clinic-owned entity that exists only in tests, in the scratch table <c>probe.Note</c>.</summary>
public sealed class ProbeNote : IClinicOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClinicId { get; set; }

    public string Body { get; set; } = "note";
}

public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public DbSet<ProbeNote> Notes => Set<ProbeNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeNote>().ToTable("Note", "probe");
        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>
/// The application's own layer of isolation (research R3): filtering, stamping and refusing,
/// on top of what the database enforces.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class ApplicationLayerTests(SqlServerFixture sql)
{
    private static ProbeDbContext Context(TestDatabase database, Guid? clinicId)
    {
        var clinic = new ClinicContext();
        if (clinicId is { } id)
        {
            clinic.Establish(id, ClinicSource.Account);
        }

        var options = new DbContextOptionsBuilder<ProbeDbContext>().UseSqlServer(database.App).Options;
        return new ProbeDbContext(options, clinic);
    }

    private static async Task<(TestDatabase Database, Guid A, Guid B)> ArrangeAsync(SqlServerFixture sql)
    {
        var database = await PredicateBehaviourTests.CreateProbeDatabaseAsync(sql);
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        await TestDatabase.ExecuteAsync(database.Crossing, $"INSERT INTO probe.Note (Id, ClinicId, Body) VALUES (NEWID(), '{a}', N'of A')");
        await TestDatabase.ExecuteAsync(database.Crossing, $"INSERT INTO probe.Note (Id, ClinicId, Body) VALUES (NEWID(), '{b}', N'of B')");
        return (database, a, b);
    }

    [Fact]
    public async Task A_query_returns_only_the_established_clinics_rows()
    {
        var (database, a, _) = await ArrangeAsync(sql);
        await using var db = Context(database, a);

        var notes = await db.Notes.ToListAsync();

        Assert.Equal("of A", Assert.Single(notes).Body);
    }

    [Fact]
    public async Task The_database_still_isolates_when_the_application_filter_is_switched_off()
    {
        var (database, a, _) = await ArrangeAsync(sql);
        await using var db = Context(database, a);

        var notes = await db.Notes.IgnoreQueryFilters().ToListAsync();

        Assert.Equal("of A", Assert.Single(notes).Body);
    }

    [Fact]
    public async Task With_no_clinic_a_query_returns_nothing_even_unfiltered()
    {
        var (database, _, _) = await ArrangeAsync(sql);
        await using var db = Context(database, clinicId: null);

        Assert.Empty(await db.Notes.ToListAsync());
        Assert.Empty(await db.Notes.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task A_new_record_is_stamped_with_the_established_clinic()
    {
        var (database, a, _) = await ArrangeAsync(sql);
        var note = new ProbeNote { Body = "new" };

        await using (var db = Context(database, a))
        {
            db.Notes.Add(note);
            await db.SaveChangesAsync();
        }

        Assert.Equal(a, note.ClinicId);
        Assert.Equal(a, await TestDatabase.ScalarAsync<Guid>(database.Crossing, $"SELECT ClinicId FROM probe.Note WHERE Id = '{note.Id}'"));
    }

    [Fact]
    public async Task A_record_offered_for_another_clinic_is_refused_before_it_reaches_the_database()
    {
        var (database, a, b) = await ArrangeAsync(sql);
        await using var db = Context(database, a);
        db.Notes.Add(new ProbeNote { ClinicId = b });

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(() => db.SaveChangesAsync());

        Assert.Equal(ErrorCodes.ClinicMismatch, refusal.Code);
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.Crossing, $"SELECT COUNT(*) FROM probe.Note WHERE ClinicId = '{b}'"));
    }

    [Fact]
    public async Task Moving_a_record_to_another_clinic_is_refused()
    {
        var (database, a, b) = await ArrangeAsync(sql);
        await using var db = Context(database, a);
        var note = await db.Notes.SingleAsync();
        note.ClinicId = b;

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(() => db.SaveChangesAsync());

        Assert.Equal(ErrorCodes.ClinicMismatch, refusal.Code);
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.Crossing, $"SELECT COUNT(*) FROM probe.Note WHERE ClinicId = '{a}'"));
    }

    [Fact]
    public async Task Saving_a_clinic_owned_record_with_no_clinic_is_refused()
    {
        var (database, _, _) = await ArrangeAsync(sql);
        await using var db = Context(database, clinicId: null);
        db.Notes.Add(new ProbeNote());

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(() => db.SaveChangesAsync());

        Assert.Equal(ErrorCodes.ClinicRequired, refusal.Code);
    }

    [Fact]
    public async Task Changing_and_deleting_its_own_records_works()
    {
        var (database, a, b) = await ArrangeAsync(sql);

        await using (var db = Context(database, a))
        {
            var note = await db.Notes.SingleAsync();
            note.Body = "changed";
            await db.SaveChangesAsync();
            db.Notes.Remove(note);
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(database.Crossing, $"SELECT COUNT(*) FROM probe.Note WHERE ClinicId = '{a}'"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(database.Crossing, $"SELECT COUNT(*) FROM probe.Note WHERE ClinicId = '{b}'"));
    }

    [Fact]
    public async Task One_context_instance_serves_one_clinic_and_another_serves_another()
    {
        // The model, and so the filter, is built once and cached; each context must still see
        // only its own clinic.
        var (database, a, b) = await ArrangeAsync(sql);

        await using var forA = Context(database, a);
        await using var forB = Context(database, b);

        Assert.Equal("of A", (await forA.Notes.SingleAsync()).Body);
        Assert.Equal("of B", (await forB.Notes.SingleAsync()).Body);
        Assert.Equal("of A", (await forA.Notes.SingleAsync()).Body);
    }
}

/// <summary>Hand-written report queries (spec 002, FR-016; constitution: read-only, carrying the clinic).</summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class ClinicConnectionTests(SqlServerFixture sql)
{
    private ApiFactory Factory() => new ApiFactory()
        .With("Scs:DB:CONNECTION", sql.Main.App)
        .With("Scs:DB:REPORT_CONNECTION", sql.Main.Report)
        .With("Scs:DB:CROSSING_CONNECTION", sql.Main.Crossing);

    [Fact]
    public async Task A_hand_written_query_sees_only_the_established_clinic()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        await sql.Main.SeedClinicAsync();

        using var factory = Factory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(a, ClinicSource.Account);

        await using var connection = await scope.ServiceProvider.GetRequiredService<IClinicConnection>().OpenAsync(CancellationToken.None);
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM tenancy.Clinic";
        await using var which = connection.CreateCommand();
        which.CommandText = "SELECT MIN(CAST(Id AS char(36))) FROM tenancy.Clinic";

        Assert.Equal(1, (int)(await count.ExecuteScalarAsync())!);
        Assert.Equal(a, Guid.Parse((string)(await which.ExecuteScalarAsync())!));
    }

    [Theory]
    [InlineData("UPDATE platform.JobHeartbeat SET JobName = JobName")]
    [InlineData("UPDATE tenancy.Clinic SET NameEn = NameEn")]
    [InlineData("CREATE TABLE platform.Intruder (Id int)")]
    public async Task The_report_connection_cannot_write(string statement)
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();

        using var factory = Factory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(a, ClinicSource.Account);

        await using var connection = await scope.ServiceProvider.GetRequiredService<IClinicConnection>().OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed test statements.
        command.CommandText = statement;
#pragma warning restore CA2100

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task The_report_connection_refuses_to_open_without_a_clinic()
    {
        using var factory = Factory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => scope.ServiceProvider.GetRequiredService<IClinicConnection>().OpenAsync(CancellationToken.None));

        Assert.Equal(ErrorCodes.ClinicRequired, refusal.Code);
    }

    [Fact]
    public async Task The_module_context_reads_the_clinics_own_record_and_no_other()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        await sql.Main.SeedClinicAsync();

        using var factory = Factory();
        _ = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(a, ClinicSource.Account);
            var db = scope.ServiceProvider.GetRequiredService<Scs.Tenancy.Infrastructure.Persistence.TenancyDbContext>();

            Assert.Equal(a, (await db.Clinics.SingleAsync()).Id);
            Assert.Equal(a, (await db.Clinics.IgnoreQueryFilters().SingleAsync()).Id);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Scs.Tenancy.Infrastructure.Persistence.TenancyDbContext>();

            Assert.Empty(await db.Clinics.ToListAsync());
            Assert.Empty(await db.Clinics.IgnoreQueryFilters().ToListAsync());
        }
    }
}
