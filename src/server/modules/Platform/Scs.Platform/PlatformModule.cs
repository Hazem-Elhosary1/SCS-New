using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Modules;
using Scs.Platform.Api;
using Scs.Platform.Application;
using Scs.Platform.Domain;
using Scs.Platform.Infrastructure.Persistence;

namespace Scs.Platform;

/// <summary>
/// The platform's own module: release information, the background-work heartbeat and the
/// intake of front-end failures. It is also the template later modules copy.
/// </summary>
public sealed class PlatformModule : IModule, IMigratableModule
{
    public string Name => "Platform";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PlatformDbContext>((provider, builder) => PlatformDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IHeartbeatStore, HeartbeatStore>();
        services.AddScoped<HeartbeatJob>();
        services.AddSingleton<IRecurringJobRegistration, PlatformJobs>();
        services.AddClientErrorRateLimit();

        // Session cookies are protected with one key ring for the whole platform, kept in the
        // database so every server instance can read what another wrote (spec 003, research R9).
        services.AddDataProtection()
            .SetApplicationName("scs")
            .PersistKeysToDbContext<PlatformDbContext>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapVersion();
        v1.MapClientErrors();
        v1.MapSupport();
    }

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = CreateForMigration(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = CreateForMigration(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    private static PlatformDbContext CreateForMigration(string migrationConnection)
    {
        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        PlatformDbContext.Configure(builder, migrationConnection);
        return new PlatformDbContext((DbContextOptions<PlatformDbContext>)builder.Options);
    }

    private sealed class PlatformJobs : IRecurringJobRegistration
    {
        public void Register(IJobScheduler scheduler) =>
            scheduler.AddOrUpdateRecurring<HeartbeatJob>(
                JobHeartbeat.HeartbeatJobName,
                job => job.RunAsync(CancellationToken.None),
                HeartbeatJob.Cron);
    }
}
