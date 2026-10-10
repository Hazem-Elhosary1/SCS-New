using System.Linq.Expressions;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Jobs;

/// <summary>
/// How modules register background work. Modules use this interface, never Hangfire directly
/// (contracts/conventions.md §6). A job must be idempotent.
/// </summary>
public interface IJobScheduler
{
    void AddOrUpdateRecurring<TJob>(string jobName, Expression<Func<TJob, Task>> run, string cron)
        where TJob : notnull;
}

internal sealed class HangfireJobScheduler(IRecurringJobManager recurring) : IJobScheduler
{
    public void AddOrUpdateRecurring<TJob>(string jobName, Expression<Func<TJob, Task>> run, string cron)
        where TJob : notnull =>
        recurring.AddOrUpdate(jobName, run, cron, new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

/// <summary>Runs once the host has started, to register a module's recurring jobs.</summary>
public interface IRecurringJobRegistration
{
    void Register(IJobScheduler scheduler);
}

public static class JobScheduling
{
    public const string Schema = "hangfire";

    /// <summary>
    /// Background work on SQL Server (FR-035). Jobs are stored in schema <c>hangfire</c>, which
    /// the migrator creates; the application login cannot. A worker runs here only when
    /// <c>SCS_ROLE</c> is <c>worker</c> or <c>all</c>. Failures retry with back-off, up to five
    /// attempts, and are logged with the job id and name.
    /// </summary>
    public static IServiceCollection AddScsJobs(this IServiceCollection services, ScsOptions options)
    {
        services.AddHangfire((provider, configuration) => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseFilter(new AutomaticRetryAttribute { Attempts = 5, LogEvents = true })
            .UseFilter(new JobLoggingFilter(provider.GetRequiredService<ILogger<JobLoggingFilter>>()))
            .UseSqlServerStorage(
                () => new SqlConnection(options.Db.Connection),
                new SqlServerStorageOptions
                {
                    SchemaName = Schema,
                    PrepareSchemaIfNecessary = false,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                }));

        services.AddSingleton<IJobScheduler, HangfireJobScheduler>();
        services.AddHostedService<RecurringJobRegistrar>();

        if (options.RunsWorker)
        {
            services.AddHangfireServer(server => server.ServerName = $"scs-{Environment.MachineName}");
        }

        return services;
    }

    /// <summary>Creates or upgrades the <c>hangfire</c> schema. Called by the migrator only.</summary>
    public static async Task InstallSchemaAsync(string migrationConnection, CancellationToken cancellation)
    {
        var connection = new SqlConnection(migrationConnection);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellation).ConfigureAwait(false);
            SqlServerObjectsInstaller.Install(connection, Schema, enableHeavyMigrations: true);
        }
    }
}
