using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.Audit.Api;
using Scs.Audit.Contracts;
using Scs.Audit.Infrastructure;
using Scs.Audit.Infrastructure.Persistence;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Modules;

namespace Scs.Audit;

/// <summary>
/// The audit trail (spec 007): who did what, to what, when and from where, with the value before
/// and after wherever money, a permission or a plan changes. It owns the table and reads it;
/// every module writes to it through its own unit of work. Migrated after Tenancy, whose
/// isolation function its table uses, and before Identity, whose data migration carries the
/// sign-in events recorded before this spec into it.
/// </summary>
public sealed class AuditModule : IModule, IMigratableModule
{
    public string Name => "Audit";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>((provider, builder) => AuditDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScsAudit();
        services.AddScoped<AuditReading>();
        services.AddScoped<IClinicAuditReading>(provider => provider.GetRequiredService<AuditReading>());
        services.AddScoped<IOperatorAuditReading, OperatorAuditReading>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapAudit();

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = AuditDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = AuditDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }
}
