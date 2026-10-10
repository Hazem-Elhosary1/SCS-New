using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Tenancy;
using Scs.Tenancy.Api;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Infrastructure;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy;

/// <summary>
/// The clinic as the owner of data. This module is migrated first: its first migration creates
/// the function every other module's isolation policies call.
/// </summary>
public sealed class TenancyModule : IModule, IMigratableModule
{
    /// <summary>Recorded as the operator while there is no operator sign-in (spec 010).</summary>
    public const string SeedOperator = "tool:seed";

    public string Name => "Tenancy";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TenancyDbContext>((provider, builder) => TenancyDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICrossClinicConnectionFactory, CrossClinicConnectionFactory>();
        services.AddScoped<CrossingDbContext>();
        services.AddScoped<IClinicRegistry, ClinicRegistry>();
        services.AddScoped<IClinicLocator, ClinicLocator>();
        services.AddScoped<IClinicProfile, ClinicProfileReader>();
        services.AddScoped<IClinicIdSource, RegistryClinicIdSource>();
        services.AddScoped<PurgeAccessRecordsJob>();
        services.AddSingleton<IRecurringJobRegistration, TenancyJobs>();
        services.AddClinicLookupRateLimit();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapClinic();
    }

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = TenancyDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = TenancyDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Registers the made-up clinics every local and test environment starts with, if they are
    /// not there. Never run against production (ops/runbooks/data-handling.md).
    /// </summary>
    public static async Task<IReadOnlyList<string>> SeedAsync(string crossingConnection, CancellationToken cancellation)
    {
        (string ShortName, string NameAr, string NameEn)[] clinics =
        [
            ("noor", "عيادة النور", "Al Noor Clinic"),
            ("shifa", "عيادة الشفاء", "Al Shifa Clinic"),
        ];

        var db = new CrossingDbContext(new CrossClinicConnectionFactory(crossingConnection));
        await using (db.ConfigureAwait(false))
        {
            var registry = new ClinicRegistry(db, TimeProvider.System, NullLogger<ClinicRegistry>.Instance);
            var added = new List<string>();

            foreach (var clinic in clinics)
            {
                if (!await db.Directory.AnyAsync(e => e.ShortName == clinic.ShortName, cancellation).ConfigureAwait(false))
                {
                    await registry.RegisterAsync(clinic.ShortName, clinic.NameAr, clinic.NameEn, SeedOperator, cancellation)
                        .ConfigureAwait(false);
                    added.Add(clinic.ShortName);
                }
            }

            return added;
        }
    }

    /// <summary>The ids of the made-up clinics, by short name, for the seeds of later modules.</summary>
    public static async Task<IReadOnlyDictionary<string, Guid>> SeededClinicIdsAsync(
        string crossingConnection, CancellationToken cancellation)
    {
        string[] names = ["noor", "shifa"];
        var db = new CrossingDbContext(new CrossClinicConnectionFactory(crossingConnection));
        await using (db.ConfigureAwait(false))
        {
            return await db.Directory.AsNoTracking()
                .Where(e => names.Contains(e.ShortName))
                .ToDictionaryAsync(e => e.ShortName, e => e.ClinicId, cancellation)
                .ConfigureAwait(false);
        }
    }

    private sealed class RegistryClinicIdSource(IClinicRegistry registry) : IClinicIdSource
    {
        public Task<IReadOnlyList<Guid>> GetClinicIdsAsync(CancellationToken cancellation) =>
            registry.ListClinicIdsForJobsAsync(cancellation);
    }

    private sealed class TenancyJobs : IRecurringJobRegistration
    {
        public void Register(IJobScheduler scheduler) =>
            scheduler.AddOrUpdateRecurring<PurgeAccessRecordsJob>(
                PurgeAccessRecordsJob.JobName,
                job => job.RunAsync(CancellationToken.None),
                PurgeAccessRecordsJob.Cron);
    }
}
