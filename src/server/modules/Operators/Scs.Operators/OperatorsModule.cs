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
using Scs.BuildingBlocks.Security;
using Scs.Operators.Api;
using Scs.Operators.Contracts;
using Scs.Operators.Infrastructure;
using Scs.Operators.Infrastructure.Persistence;

namespace Scs.Operators;

/// <summary>
/// The people who run the platform (spec 010): their accounts, signing in, sessions and
/// passwords. Nothing here belongs to a clinic, and nothing here reads a clinic's data.
/// </summary>
public sealed class OperatorsModule : IModule, IMigratableModule
{
    public string Name => "Operators";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OperatorsDbContext>((provider, builder) => OperatorsDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddDataProtection();
        services.TryAddSingleton<PasswordLinkTokens>();
        services.AddScsOperatorSessions();
        services.AddOperatorRateLimits();

        services.AddScoped<IOperatorAccessSource, OperatorAccessSource>();
        services.AddScoped<OperatorSessions>();
        services.AddScoped<IOperatorSessionCheck>(provider => provider.GetRequiredService<OperatorSessions>());
        services.AddScoped<IOperatorSessions>(provider => provider.GetRequiredService<OperatorSessions>());
        services.AddScoped<OperatorCodes>();
        services.AddScoped<OperatorSignIn>();
        services.AddScoped<OperatorPasswords>();
        services.AddScoped<OperatorInvite>();
        services.AddScoped<OperatorsDirectory>();
        services.AddScoped<OperatorManagement>();
        services.AddScoped<Roles>();
        services.AddScoped<OperatorProfile>();
        services.AddScoped<IOperatorDirectory>(provider => provider.GetRequiredService<OperatorProfile>());
        services.AddScoped<Scs.BuildingBlocks.Audit.IOperatorActorNames>(provider => provider.GetRequiredService<OperatorProfile>());
        services.AddScoped<OperatorsCleanupJob>();
        services.AddScoped<OperatorInviteJob>();
        services.AddSingleton<IRecurringJobRegistration, OperatorJobs>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapOperators().MapOperatorsTeam().MapOperatorRoles();

    /// <summary>
    /// The deployment tool's <c>operator invite</c> (FR-003): notes that an invitation is owed to
    /// a new or still-invited operator. The platform sends it within a minute.
    /// </summary>
    public static async Task<InviteResult> InviteAsync(
        string appConnection, string fullName, string email, string ranBy, CancellationToken cancellation)
    {
        var db = OperatorsDbContextFactory.Create(appConnection);
        await using (db.ConfigureAwait(false))
        {
            return await new OperatorInvite(db, TimeProvider.System).InviteAsync(fullName, email, ranBy, cancellation)
                .ConfigureAwait(false);
        }
    }

    /// <summary>The deployment tool's <c>operator withdraw</c> (FR-003a).</summary>
    public static async Task<InviteResult> WithdrawAsync(
        string appConnection, string email, string ranBy, CancellationToken cancellation)
    {
        var db = OperatorsDbContextFactory.Create(appConnection);
        await using (db.ConfigureAwait(false))
        {
            return await new OperatorInvite(db, TimeProvider.System).WithdrawAsync(email, ranBy, cancellation)
                .ConfigureAwait(false);
        }
    }

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = OperatorsDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = OperatorsDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    private sealed class OperatorJobs : IRecurringJobRegistration
    {
        public void Register(IJobScheduler scheduler)
        {
            scheduler.AddOrUpdateRecurring<OperatorsCleanupJob>(
                OperatorsCleanupJob.JobName, job => job.RunAsync(CancellationToken.None), OperatorsCleanupJob.Cron);
            scheduler.AddOrUpdateRecurring<OperatorInviteJob>(
                OperatorInviteJob.JobName, job => job.RunAsync(CancellationToken.None), OperatorInviteJob.Cron);
        }
    }
}
