using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.Tenancy.Infrastructure;

namespace Scs.IntegrationTests.Tenancy;

/// <summary>What a test job saw, keyed by the clinic it ran for.</summary>
public static class JobObservations
{
    public static ConcurrentDictionary<Guid, (int ClinicsVisible, Guid? Established)> Seen { get; } = new();

    public static ConcurrentBag<Guid> FailFor { get; } = [];
}

/// <summary>A per-clinic job that records what the clinic's isolation let it see.</summary>
public sealed partial class ObservingJob(IClinicContext clinic, IClinicConnection connection, ILogger<ObservingJob> logger) : IClinicJob
{
    public async Task RunAsync(CancellationToken cancellation)
    {
        var id = clinic.Require();
        LogRunning(logger);

        if (JobObservations.FailFor.Contains(id))
        {
            throw new InvalidOperationException("This clinic's turn fails.");
        }

        await using var open = await connection.OpenAsync(cancellation);
        await using var command = open.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM tenancy.Clinic";
        JobObservations.Seen[id] = ((int)(await command.ExecuteScalarAsync(cancellation))!, clinic.ClinicId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Observing job running")]
    private static partial void LogRunning(ILogger logger);
}

/// <summary>Spec 002, User Story 6: background work keeps to one clinic at a time.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicJobTests(SqlServerFixture sql)
{
    private static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
        }

        Assert.True(condition(), what);
    }

    [Fact]
    public async Task A_job_for_a_clinic_sees_only_that_clinic_and_its_log_entries_carry_the_clinic()
    {
        await sql.EnsureMigratedAsync();
        var a = await sql.Main.SeedClinicAsync();
        await sql.Main.SeedClinicAsync();

        using var factory = sql.CreateApi().With("Scs:ROLE", "all");
        _ = factory.CreateClient();
        var jobId = factory.Services.GetRequiredService<IClinicJobScheduler>().EnqueueForClinic<ObservingJob>(a);

        await WaitForAsync(() => JobObservations.Seen.ContainsKey(a), "The job did not run.");

        Assert.Equal((1, a), JobObservations.Seen[a]);
        Assert.Contains(factory.Logs.Lines, line =>
            line.Contains("Observing job running", StringComparison.Ordinal)
            && line.Contains($"\"clinicId\":\"{a}\"", StringComparison.OrdinalIgnoreCase)
            && line.Contains($"\"jobId\":\"{jobId}\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_job_cannot_be_enqueued_without_a_clinic()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        _ = factory.CreateClient();

        Assert.Throws<ArgumentException>(() =>
            factory.Services.GetRequiredService<IClinicJobScheduler>().EnqueueForClinic<ObservingJob>(Guid.Empty));
    }

    [Fact]
    public async Task A_job_covering_all_clinics_takes_them_one_by_one_and_one_failure_does_not_stop_the_rest()
    {
        await sql.EnsureMigratedAsync();
        var first = await sql.Main.SeedClinicAsync();
        var failing = await sql.Main.SeedClinicAsync();
        var last = await sql.Main.SeedClinicAsync();
        JobObservations.FailFor.Add(failing);

        using var factory = sql.CreateApi().With("Scs:ROLE", "all");
        _ = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ClinicJobFanOut<ObservingJob>>().RunAsync(CancellationToken.None);
        }

        await WaitForAsync(
            () => JobObservations.Seen.ContainsKey(first) && JobObservations.Seen.ContainsKey(last),
            "The clinics before and after the failing one did not both complete.");

        // Each clinic's turn saw exactly its own clinic.
        Assert.Equal((1, first), JobObservations.Seen[first]);
        Assert.Equal((1, last), JobObservations.Seen[last]);
        Assert.False(JobObservations.Seen.ContainsKey(failing));

        // The failure is recorded against the clinic it happened in.
        await WaitForAsync(
            () => factory.Logs.Lines.Any(line =>
                line.Contains("Background job attempt failed", StringComparison.Ordinal)
                && line.Contains($"\"clinicId\":\"{failing}\"", StringComparison.OrdinalIgnoreCase)),
            "The failing clinic's failure was not logged with its clinic id.");
    }

    [Fact]
    public async Task Work_with_no_clinic_sees_no_clinic_records()
    {
        await sql.EnsureMigratedAsync();
        await sql.Main.SeedClinicAsync();
        using var factory = sql.CreateApi();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<Scs.Tenancy.Infrastructure.Persistence.TenancyDbContext>();

        Assert.Empty(db.Clinics.ToList());
    }
}

/// <summary>Spec 002, FR-032: access records are kept for the retention period and then removed.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccessRecordRetentionTests(SqlServerFixture sql)
{
    private async Task<long> InsertAsync(string marker, int daysOld) =>
        (long)(await TestDatabase.ScalarAsync<decimal>(
            sql.Main.Crossing,
            $"""
             INSERT INTO crossing.CrossClinicAccess (Operation, OperatorId, OccurredAtUtc)
             VALUES ('list-clinics', '{marker}', DATEADD(DAY, -{daysOld}, SYSDATETIMEOFFSET()));
             SELECT SCOPE_IDENTITY();
             """));

    private Task<int> CountAsync(long id) =>
        TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM crossing.CrossClinicAccess WHERE Id = {id}");

    private async Task RunPurgeAsync(string? retentionDays = null)
    {
        using var factory = sql.CreateApi().With("Scs:RETENTION:CROSS_CLINIC_ACCESS_DAYS", retentionDays);
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PurgeAccessRecordsJob>().RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Records_older_than_five_years_are_removed_and_younger_ones_kept_by_default()
    {
        await sql.EnsureMigratedAsync();
        var marker = $"retention:{Guid.NewGuid():N}";
        var old = await InsertAsync(marker, daysOld: 1826);
        var young = await InsertAsync(marker, daysOld: 1824);

        await RunPurgeAsync();
        await RunPurgeAsync();

        Assert.Equal(0, await CountAsync(old));
        Assert.Equal(1, await CountAsync(young));
    }

    [Fact]
    public async Task The_period_is_a_setting()
    {
        await sql.EnsureMigratedAsync();
        var marker = $"retention:{Guid.NewGuid():N}";
        var old = await InsertAsync(marker, daysOld: 91);
        var young = await InsertAsync(marker, daysOld: 89);

        await RunPurgeAsync(retentionDays: "90");

        Assert.Equal(0, await CountAsync(old));
        Assert.Equal(1, await CountAsync(young));
    }

    [Theory]
    [InlineData("29")]
    [InlineData("0")]
    [InlineData("-1")]
    public void A_period_under_thirty_days_stops_startup(string days)
    {
        using var factory = sql.CreateApi().With("Scs:RETENTION:CROSS_CLINIC_ACCESS_DAYS", days);

        var error = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("SCS_RETENTION__CROSS_CLINIC_ACCESS_DAYS", error.Message, StringComparison.Ordinal);
    }
}
