using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Modules;
using Scs.ClinicData.Contracts;
using Scs.ClinicData.Infrastructure;
using Scs.ClinicData.Infrastructure.Persistence;

namespace Scs.ClinicData;

/// <summary>
/// The clinic's own data, the clinic volume's chapter 11 «إدارة البيانات». With spec 015: the
/// clinic's main branch, made with the clinic. Specs 028 (branches) and 029 (doctors) add the rest
/// and its screens. Migrated after Tenancy, whose isolation function its tables use.
/// </summary>
public sealed class ClinicDataModule : IModule, IMigratableModule
{
    public string Name => "ClinicData";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ClinicDataDbContext>((provider, builder) => ClinicDataDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IBranches, Branches>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        // No routes yet: spec 028 adds the branches' screens.
    }

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = ClinicDataDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = ClinicDataDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }
}
