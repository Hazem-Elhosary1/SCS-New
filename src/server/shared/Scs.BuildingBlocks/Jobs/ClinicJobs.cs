using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Tenancy;
using Serilog.Context;

namespace Scs.BuildingBlocks.Jobs;

/// <summary>
/// Background work done for one clinic. It runs with that clinic established, under exactly the
/// same isolation as a request (spec 002, FR-029). A job must be idempotent.
/// </summary>
public interface IClinicJob
{
    Task RunAsync(CancellationToken cancellation);
}

/// <summary>
/// Supplies the ids of all clinics so that a job covering every clinic can take them one by one.
/// Implemented by the Tenancy module through a declared crossing.
/// </summary>
public interface IClinicIdSource
{
    Task<IReadOnlyList<Guid>> GetClinicIdsAsync(CancellationToken cancellation);
}

/// <summary>Scheduling of per-clinic work. Modules use this, never Hangfire directly.</summary>
public interface IClinicJobScheduler
{
    /// <summary>Runs <typeparamref name="TJob"/> once, for one clinic.</summary>
    string EnqueueForClinic<TJob>(Guid clinicId)
        where TJob : IClinicJob;

    /// <summary>
    /// Runs <typeparamref name="TJob"/> on a schedule for every clinic, one clinic at a time:
    /// each clinic's turn is its own job, with its own retries and its own failure.
    /// </summary>
    void AddOrUpdateRecurringPerClinic<TJob>(string jobName, string cron)
        where TJob : IClinicJob;
}

internal sealed class ClinicJobScheduler(IBackgroundJobClient jobs, IRecurringJobManager recurring) : IClinicJobScheduler
{
    public string EnqueueForClinic<TJob>(Guid clinicId)
        where TJob : IClinicJob
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("A clinic is required.", nameof(clinicId));
        }

        return jobs.Enqueue<ClinicJobRunner<TJob>>(runner => runner.RunAsync(clinicId, CancellationToken.None));
    }

    public void AddOrUpdateRecurringPerClinic<TJob>(string jobName, string cron)
        where TJob : IClinicJob =>
        recurring.AddOrUpdate<ClinicJobFanOut<TJob>>(
            jobName,
            fanOut => fanOut.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

/// <summary>
/// What the job server actually runs for one clinic: it establishes the clinic in the job's own
/// scope and only then creates the job, so nothing the job touches can see another clinic.
/// </summary>
public sealed class ClinicJobRunner<TJob>(IServiceProvider services, IClinicContext clinic)
    where TJob : IClinicJob
{
    public async Task RunAsync(Guid clinicId, CancellationToken cancellation)
    {
        clinic.Establish(clinicId, ClinicSource.Job);

        using (LogContext.PushProperty("clinicId", clinicId))
        {
            var job = ActivatorUtilities.GetServiceOrCreateInstance<TJob>(services);
            await job.RunAsync(cancellation).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// The scheduled part of a per-clinic job: it does no clinic work itself, it only enqueues one
/// job per clinic, so one clinic's failure cannot stop or touch another's turn (FR-030).
/// </summary>
public sealed partial class ClinicJobFanOut<TJob>(
    IClinicIdSource clinics, IClinicJobScheduler scheduler, ILogger<ClinicJobFanOut<TJob>> logger)
    where TJob : IClinicJob
{
    public async Task RunAsync(CancellationToken cancellation)
    {
        var ids = await clinics.GetClinicIdsAsync(cancellation).ConfigureAwait(false);
        foreach (var id in ids)
        {
            scheduler.EnqueueForClinic<TJob>(id);
        }

        LogFannedOut(logger, typeof(TJob).Name, ids.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued {job} for {clinics} clinics")]
    private static partial void LogFannedOut(ILogger logger, string job, int clinics);
}
