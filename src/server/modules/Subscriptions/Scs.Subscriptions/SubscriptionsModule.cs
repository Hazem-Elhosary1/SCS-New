using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Plans;
using Scs.Operators.Contracts;
using Scs.Plans.Contracts;
using Scs.Subscriptions.Api;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Infrastructure;
using Scs.Subscriptions.Infrastructure.Persistence;

namespace Scs.Subscriptions;

/// <summary>
/// A clinic's subscription (BRD Shared §4–§5, Ops module 16). With spec 014: which plan each
/// clinic is on, the one answer to what it has, and the meter of its monthly limits — the one
/// enforcement point every other module asks (constitution III). Specs 015 to 021 add the
/// subscription's states, invoices, payments, plan changes and extra units here. Migrated after
/// Tenancy, whose isolation function its tables use.
/// </summary>
public sealed class SubscriptionsModule : IModule, IMigratableModule
{
    public string Name => "Subscriptions";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SubscriptionsDbContext>((provider, builder) => SubscriptionsDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScsAudit();

        // One reader per request: every question of that request is answered from one reading.
        services.AddScoped<ClinicEntitlementsReader>();
        services.AddScoped<IClinicEntitlements>(provider => provider.GetRequiredService<ClinicEntitlementsReader>());
        services.AddScoped<IPlanAnswers>(provider => provider.GetRequiredService<ClinicEntitlementsReader>());

        services.AddScoped<ClinicPlans>();
        services.AddScoped<IClinicPlans>(provider => provider.GetRequiredService<ClinicPlans>());
        services.AddScoped<MonthlyMeter>();
        services.AddScoped<IMonthlyMeter>(provider => provider.GetRequiredService<MonthlyMeter>());
        services.AddScoped<IClinicLimits, ClinicLimits>();

        // Spec 015: the operators' clinics — the add, the list, and the figures other modules show.
        services.AddScoped<OperatorClinicPlans>();
        services.AddScoped<ClinicOnboarding>();
        services.AddScoped<ClinicsReading>();

        // Registered after Plans and Operators, so these replace their "nothing yet" answers.
        services.AddScoped<IPlanSubscribers, PlanSubscribers>();
        services.AddScoped<IManagedClinicCounts, ManagedClinicCounts>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapDevClinicPlan().MapOpsClinics();

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = SubscriptionsDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = SubscriptionsDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }
}
