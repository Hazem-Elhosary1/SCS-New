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
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Notifications;
using Scs.Notifications.Api;
using Scs.Notifications.Contracts;
using Scs.Notifications.Infrastructure;
using Scs.Notifications.Infrastructure.Persistence;

namespace Scs.Notifications;

/// <summary>
/// Telling people what concerns them (spec 008): the bell inside the console, the email that
/// follows an event's kind, and the clinic's email switches. It owns the tables and reads them;
/// every module raises into them through its own unit of work. Migrated after Tenancy, whose
/// isolation function its tables use, and before Identity, which raises the first event.
/// </summary>
public sealed class NotificationsModule : IModule, IMigratableModule
{
    public string Name => "Notifications";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>((provider, builder) => NotificationsDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScsMail();
        services.AddScsAudit();
        services.AddScsNotifications();
        services.AddScoped<IBell, Bell>();
        services.AddScoped<IEmailSwitches, EmailSwitches>();
        services.AddScoped<INotificationDispatch, NotificationDispatch>();
        services.AddSingleton<IRecurringJobRegistration, NotificationJobs>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapNotifications();

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = NotificationsDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = NotificationsDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Registers the module's recurring work once the host has started.</summary>
    private sealed class NotificationJobs(IClinicJobScheduler perClinic) : IRecurringJobRegistration
    {
        public void Register(IJobScheduler scheduler)
        {
            perClinic.AddOrUpdateRecurringPerClinic<NotificationCleanupJob>(NotificationCleanupJob.JobName, NotificationCleanupJob.Cron);
            scheduler.AddOrUpdateRecurring<EmailSweepJob>(
                EmailSweepJob.JobName, job => job.RunAsync(CancellationToken.None), EmailSweepJob.Cron);
        }
    }
}
