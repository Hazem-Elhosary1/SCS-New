using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Modules;
using Scs.Plans.Api;
using Scs.Plans.Contracts;
using Scs.Plans.Infrastructure;
using Scs.Plans.Infrastructure.Persistence;

namespace Scs.Plans;

/// <summary>
/// What clinics can subscribe to (BRD module 16): the catalogue of features plans are made of
/// (spec 012), and — with spec 013 — the plans themselves. Nothing here belongs to a clinic.
/// </summary>
public sealed class PlansModule : IModule, IMigratableModule
{
    public string Name => "Plans";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PlansDbContext>((provider, builder) => PlansDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.AddScoped<FeatureCatalogue>();
        services.AddScoped<IFeatureCatalogue>(provider => provider.GetRequiredService<FeatureCatalogue>());
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapFeatures();

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = PlansDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = PlansDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }
}
