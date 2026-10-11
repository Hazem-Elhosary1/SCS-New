using System.Diagnostics.Metrics;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Telemetry;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Jobs;

/// <summary>A job that keeps failing: retried, counted, and finally logged at Error with its id.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FailingJobTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Job_that_always_fails_is_retried_counted_and_finally_logged_at_error()
    {
        await sql.EnsureMigratedAsync();

        long failures = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ScsTelemetry.MeterName && instrument.Name == "scs.jobs.failed")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "job" && Equals(tag.Value, nameof(AlwaysFailingJob)))
                {
                    Interlocked.Add(ref failures, value);
                }
            }
        });
        listener.Start();

        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", sql.AppConnection)
            .With("Scs:ROLE", "all");
        _ = factory.CreateClient();

        var jobId = factory.Services.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<AlwaysFailingJob>(job => job.RunAsync());

        bool GaveUp() => factory.Logs.Lines.Any(line =>
            line.Contains("will not be retried", StringComparison.Ordinal) &&
            line.Contains($"\"jobId\":\"{jobId}\"", StringComparison.Ordinal));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (!GaveUp() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(500);
        }

        Assert.True(GaveUp(), "No final Error entry for the failed job.");

        var final = factory.Logs.Lines.Single(line =>
            line.Contains("will not be retried", StringComparison.Ordinal) &&
            line.Contains($"\"jobId\":\"{jobId}\"", StringComparison.Ordinal));
        Assert.Contains("\"@l\":\"Error\"", final, StringComparison.Ordinal);
        Assert.Contains($"\"jobName\":\"{nameof(AlwaysFailingJob)}\"", final, StringComparison.Ordinal);

        // One first attempt and one retry, each logged as a failed attempt with the job's id.
        var attempts = factory.Logs.Lines.Count(line =>
            line.Contains("Background job attempt failed", StringComparison.Ordinal) &&
            line.Contains($"\"jobId\":\"{jobId}\"", StringComparison.Ordinal));
        Assert.Equal(2, attempts);
        Assert.Equal(2, Interlocked.Read(ref failures));

        // What the job threw is in the logs, scrubbed like everything else.
        Assert.DoesNotContain(Leak.Email, factory.Logs.All, StringComparison.Ordinal);
    }
}

public sealed class AlwaysFailingJob
{
    [AutomaticRetry(Attempts = 1, DelaysInSeconds = [1], LogEvents = true)]
    public Task RunAsync() => throw new InvalidOperationException($"Could not notify {Leak.Email}");
}
