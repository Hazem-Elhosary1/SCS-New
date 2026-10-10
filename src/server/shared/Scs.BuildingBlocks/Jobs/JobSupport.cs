using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Serilog.Context;

namespace Scs.BuildingBlocks.Jobs;

/// <summary>Registers every module's recurring jobs once the host is up.</summary>
internal sealed partial class RecurringJobRegistrar(
    IServiceProvider services, ILogger<RecurringJobRegistrar> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Job storage may not be reachable yet (the database can start after the API). Keep
        // trying; readiness reports the platform as not ready in the meantime.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scheduler = services.GetRequiredService<IJobScheduler>();
                foreach (var registration in services.GetServices<IRecurringJobRegistration>())
                {
                    registration.Register(scheduler);
                }

                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogNotRegistered(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recurring jobs could not be registered yet; retrying")]
    private static partial void LogNotRegistered(ILogger logger, Exception exception);
}

/// <summary>
/// Puts <c>jobId</c> and <c>jobName</c> on every log entry a job causes
/// (contracts/conventions.md §5). A job that has used up its retries is logged at Error by the
/// job server itself.
/// </summary>
internal sealed partial class JobLoggingFilter(ILogger<JobLoggingFilter> logger)
    : JobFilterAttribute, IServerFilter, IApplyStateFilter
{
    /// <summary>A job that has used up its retries: one Error entry carrying its id and name.</summary>
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NewState is not FailedState failed)
        {
            return;
        }

        var job = context.BackgroundJob.Job;
        var scope = new Dictionary<string, object>
        {
            ["jobId"] = context.BackgroundJob.Id,
            ["jobName"] = context.GetJobParameter<string>("RecurringJobId") ?? DisplayName(job.Type),
        };

        if (IsClinicJob(job.Type) && job.Args.Count > 0 && job.Args[0] is Guid clinicId)
        {
            scope["clinicId"] = clinicId;
        }

        using (logger.BeginScope(scope))
        {
            LogGaveUp(logger, failed.Exception);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Background job failed and will not be retried")]
    private static partial void LogGaveUp(ILogger logger, Exception exception);

    private const string ScopeKey = "scs.logScope";
    private const string StartedKey = "scs.started";

    public void OnPerforming(PerformingContext context)
    {
        var job = context.BackgroundJob.Job;
        var jobName = context.GetJobParameter<string>("RecurringJobId") ?? DisplayName(job.Type);
        context.Items[StartedKey] = System.Diagnostics.Stopwatch.GetTimestamp();

        // A per-clinic job carries its clinic as its first argument. Putting it on the scope
        // here means every entry of the attempt has it, including the one written when it fails.
        var clinicId = IsClinicJob(job.Type) && job.Args.Count > 0 && job.Args[0] is Guid id ? id : (Guid?)null;

        context.Items[ScopeKey] = new Scope(
            LogContext.PushProperty("jobId", context.BackgroundJob.Id),
            LogContext.PushProperty("jobName", jobName),
            clinicId is null ? null : LogContext.PushProperty("clinicId", clinicId.Value));
    }

    public void OnPerformed(PerformedContext context)
    {
        var durationMs = context.Items.TryGetValue(StartedKey, out var started)
            ? (long)System.Diagnostics.Stopwatch.GetElapsedTime((long)started).TotalMilliseconds
            : 0;

        if (context.Exception is null)
        {
            LogCompleted(logger, durationMs);
        }
        else
        {
            LogFailed(logger, context.Exception, durationMs);
            Telemetry.ScsTelemetry.JobsFailed.Add(
                1, new KeyValuePair<string, object?>("job", context.BackgroundJob.Job.Type.Name));
        }

        if (context.Items.TryGetValue(ScopeKey, out var scope))
        {
            ((Scope)scope).Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Background job completed in {durationMs} ms")]
    private static partial void LogCompleted(ILogger logger, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background job attempt failed after {durationMs} ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, long durationMs);

    private static bool IsClinicJob(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ClinicJobRunner<>);

    /// <summary>The job a person would recognise: the clinic job itself, not the runner around it.</summary>
    private static string DisplayName(Type type) =>
        type.IsGenericType && type.GetGenericArguments() is [var inner] ? inner.Name : type.Name;

    private sealed class Scope(IDisposable first, IDisposable second, IDisposable? third) : IDisposable
    {
        public void Dispose()
        {
            third?.Dispose();
            second.Dispose();
            first.Dispose();
        }
    }
}

/// <summary>Readiness check <c>database</c>: the application login can open a connection.</summary>
internal sealed class DatabaseHealthCheck(IOptions<ScsOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        SqlProbe.RunAsync(options.Value.Db.Connection, "SELECT 1", cancellationToken);
}

/// <summary>Readiness check <c>jobs</c>: job storage exists and can be read.</summary>
internal sealed class JobStorageHealthCheck(IOptions<ScsOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        SqlProbe.RunAsync(
            options.Value.Db.Connection,
            $"SELECT TOP (1) [Version] FROM [{JobScheduling.Schema}].[Schema]",
            cancellationToken);
}

internal static class SqlProbe
{
    public static async Task<HealthCheckResult> RunAsync(string connectionString, string sql, CancellationToken cancellation)
    {
        try
        {
            // A probe must answer well inside a load balancer's health-check timeout.
            var probe = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 2, ConnectRetryCount = 0 };
            var connection = new SqlConnection(probe.ConnectionString);
            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = sql;
                    command.CommandTimeout = 3;
                    await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false);
                }
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException or ArgumentException)
        {
            // The reason goes to the logs through the health check publisher, never to the caller.
            return HealthCheckResult.Unhealthy();
        }
    }
}

public static class ReadinessExtensions
{
    /// <summary>Adds the two readiness checks named in contracts/platform-api.openapi.yaml.</summary>
    public static IHealthChecksBuilder AddScsReadiness(this IHealthChecksBuilder builder) => builder
        .AddCheck<DatabaseHealthCheck>("database", tags: [Health.HealthEndpoints.ReadyTag])
        .AddCheck<JobStorageHealthCheck>("jobs", tags: [Health.HealthEndpoints.ReadyTag]);
}
