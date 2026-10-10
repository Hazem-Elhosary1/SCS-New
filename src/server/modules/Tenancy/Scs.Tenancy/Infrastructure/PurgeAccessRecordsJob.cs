using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Infrastructure;

/// <summary>
/// The daily job <c>tenancy-purge-access-records</c>: removes cross-clinic access records older
/// than the retention period (spec 002, FR-032). It is the only code that deletes such a record.
/// Running it twice has the same result as once.
/// </summary>
internal sealed partial class PurgeAccessRecordsJob(
    CrossingDbContext db, IOptions<ScsOptions> options, TimeProvider clock, ILogger<PurgeAccessRecordsJob> logger)
{
    public const string JobName = "tenancy-purge-access-records";
    public const string Cron = "30 1 * * *";

    public async Task RunAsync(CancellationToken cancellation)
    {
        var days = options.Value.Retention.CrossClinicAccessDays;
        var cutoff = clock.GetUtcNow().AddDays(-days);

        var removed = await db.Access.Where(a => a.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        LogPurged(logger, removed, days);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Removed {removed} cross-clinic access records older than {retentionDays} days")]
    private static partial void LogPurged(ILogger logger, int removed, int retentionDays);
}
