using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.Platform.Application;

namespace Scs.IntegrationTests.Jobs;

[Collection(DatabaseCollection.Name)]
public sealed class HeartbeatJobTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Job_stamps_the_single_row_with_the_time_and_the_running_version()
    {
        await sql.EnsureMigratedAsync();
        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", sql.AppConnection)
            .With("Scs:VERSION", "1.2.3+direct");
        _ = factory.CreateClient();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<HeartbeatJob>().RunAsync(CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            // Twice has the same result as once.
            await scope.ServiceProvider.GetRequiredService<HeartbeatJob>().RunAsync(CancellationToken.None);
        }

        Assert.Equal(1, await sql.ScalarAsync<int>(sql.AdminConnection, "SELECT COUNT(*) FROM platform.JobHeartbeat"));
        Assert.Equal("1.2.3+direct", await sql.ScalarAsync<string>(sql.AdminConnection, "SELECT LastRunVersion FROM platform.JobHeartbeat"));
        Assert.True(await sql.ScalarAsync<DateTimeOffset>(sql.AdminConnection, "SELECT LastRunAtUtc FROM platform.JobHeartbeat") >= before);
    }

    [Fact]
    public async Task Job_runs_through_the_background_worker()
    {
        await sql.EnsureMigratedAsync();
        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", sql.AppConnection)
            .With("Scs:ROLE", "all")
            .With("Scs:VERSION", "1.2.3+worker");
        _ = factory.CreateClient();

        factory.Services.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<HeartbeatJob>(job => job.RunAsync(CancellationToken.None));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        string? version = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            version = await sql.ScalarAsync<string>(sql.AdminConnection, "SELECT LastRunVersion FROM platform.JobHeartbeat");
            if (version == "1.2.3+worker")
            {
                break;
            }

            await Task.Delay(500);
        }

        Assert.Equal("1.2.3+worker", version);

        // The completed entry is written just after the row is saved.
        var logged = false;
        for (var attempt = 0; attempt < 20 && !logged; attempt++)
        {
            logged = factory.Logs.Lines.Any(line =>
                line.Contains("\"jobId\"", StringComparison.Ordinal) &&
                line.Contains("\"jobName\"", StringComparison.Ordinal));
            if (!logged)
            {
                await Task.Delay(250);
            }
        }

        Assert.True(logged, "No log entry carried jobId and jobName.");
    }
}
