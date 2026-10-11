using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The daily job <c>identity-cleanup</c>, run for each clinic in turn: removes sign-in codes
/// older than a day (every one of them long expired) and sessions that ended more than ninety
/// days ago; password links long past any life they could have had, and account email requests
/// handled more than a day ago (spec 004). Sign-in events are not touched; their retention is decided with the audit trail
/// (spec 007). Running it twice has the same result as once.
/// </summary>
[RecordsNothing("removes expired codes, ended sessions and spent links; no record a person would trace")]
internal sealed partial class CleanupJob(IdentityDbContext db, TimeProvider clock, ILogger<CleanupJob> logger) : IClinicJob
{
    public const string JobName = "identity-cleanup";
    public const string Cron = "45 1 * * *";

    public static readonly TimeSpan KeepCodesFor = TimeSpan.FromDays(1);
    public static readonly TimeSpan KeepEndedSessionsFor = TimeSpan.FromDays(90);

    /// <summary>Longer than the longest life a link can be given, so only dead links are removed.</summary>
    public static readonly TimeSpan KeepLinksFor = TimeSpan.FromDays(21);

    public static readonly TimeSpan KeepHandledEmailRequestsFor = TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var codesBefore = now - KeepCodesFor;
        var sessionsBefore = now - KeepEndedSessionsFor;

        var codes = await db.Challenges.Where(c => c.CreatedAtUtc < codesBefore)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        var sessions = await db.Sessions.Where(s => s.EndedAtUtc != null && s.EndedAtUtc < sessionsBefore)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        var linksBefore = now - KeepLinksFor;
        var links = await db.PasswordLinks.Where(l => l.CreatedAtUtc < linksBefore)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        var requestsBefore = now - KeepHandledEmailRequestsFor;
        var requests = await db.EmailRequests.Where(r => r.HandledAtUtc != null && r.HandledAtUtc < requestsBefore)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        LogCleaned(logger, codes, sessions);
        LogCleanedPasswords(logger, links, requests);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Removed {links} old password links and {requests} handled account email requests")]
    private static partial void LogCleanedPasswords(ILogger logger, int links, int requests);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Removed {codes} old sign-in codes and {sessions} sessions that ended long ago")]
    private static partial void LogCleaned(ILogger logger, int codes, int sessions);
}

/// <summary>Registers the module's recurring work once the host has started.</summary>
internal sealed class IdentityJobs(IClinicJobScheduler perClinic) : IRecurringJobRegistration
{
    public void Register(IJobScheduler scheduler) =>
        perClinic.AddOrUpdateRecurringPerClinic<CleanupJob>(CleanupJob.JobName, CleanupJob.Cron);
}
