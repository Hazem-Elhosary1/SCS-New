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
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Notifications;
using Scs.Files.Api;
using Scs.Files.Contracts;
using Scs.Files.Infrastructure;
using Scs.Files.Infrastructure.Persistence;
using Scs.Identity.Contracts;
using Scs.SharedKernel.Files;

namespace Scs.Files;

/// <summary>
/// The one place the product stores and fetches files (spec 009): uploading, the platform's own
/// checks, the malware scan, giving a file out to whoever may see its record, and not keeping
/// it longer than it should be kept. It owns the table and reads it; the module that owns a
/// record attaches its files through its own unit of work. Migrated after Tenancy, whose
/// isolation function its table uses.
/// </summary>
public sealed class FilesModule : IModule, IMigratableModule
{
    public string Name => "Files";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FilesDbContext>((provider, builder) => FilesDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScsAudit();
        services.AddScsNotifications();
        services.AddScsFiles();
        services.AddDataProtection();

        services.TryAddSingleton<IFileStore, FolderFileStore>();

        // With no daemon configured, a developer's machine scans for the test signature only;
        // the options validator has already refused that anywhere else.
        services.TryAddSingleton<IMalwareScanner>(provider =>
            provider.GetRequiredService<IOptions<ScsOptions>>().Value.Scan.HasScanner
                ? ActivatorUtilities.CreateInstance<ClamdScanner>(provider)
                : new TestSignatureScanner());

        services.AddScoped<Uploads>();
        services.AddScoped<FileDownloads>();
        services.AddScoped<PublicFiles>();
        services.AddScoped<IRecordFiles, RecordFiles>();
        services.AddScoped<IClinicFiles, ClinicFiles>();
        services.AddFileKindOwner<DevSampleOwner>();
        services.AddSingleton<IRecurringJobRegistration, FileJobs>();

        // Not tagged for readiness: files waiting for a scan do not take the platform out of service.
        services.AddSingleton<ScanBacklog>();
        services.AddHealthChecks().AddCheck<ScanBacklogHealthCheck>(ScanBacklogHealthCheck.Name);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapFiles();

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = FilesDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = FilesDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Registers the module's recurring work once the host has started.</summary>
    private sealed class FileJobs(IClinicJobScheduler perClinic) : IRecurringJobRegistration
    {
        public void Register(IJobScheduler scheduler)
        {
            perClinic.AddOrUpdateRecurringPerClinic<FileCleanupJob>(FileCleanupJob.JobName, FileCleanupJob.Cron);
            scheduler.AddOrUpdateRecurring<ScanSweepJob>(
                ScanSweepJob.JobName, job => job.RunAsync(CancellationToken.None), ScanSweepJob.Cron);
        }
    }
}

/// <summary>
/// Owns the builders' kind (research R8): any signed-in person uploads a sample, and sees what
/// is attached to their own account. The kind exists on a developer's machine only — the upload
/// address refuses it anywhere else — so this owner answers nothing in a real environment.
/// </summary>
internal sealed class DevSampleOwner(ICurrentStaff staff, IOptions<ScsOptions> options) : IFileKindOwner
{
    public string Kind => FileKinds.DevSample;

    public Task<bool> MayUploadAsync(CancellationToken cancellation) =>
        Task.FromResult(options.Value.IsLocal && staff.IsSignedIn);

    public Task<bool> MaySeeAsync(FileRecord record, CancellationToken cancellation) =>
        Task.FromResult(
            options.Value.IsLocal && staff.IsSignedIn && record.Kind == FileRecord.Account && record.Id == staff.AccountId);
}
