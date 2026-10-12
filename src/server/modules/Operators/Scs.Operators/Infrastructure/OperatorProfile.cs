using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Contracts;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Wording;

namespace Scs.Operators.Infrastructure;

/// <summary>Who is signed in, as the console's frame shows them.</summary>
internal sealed record SessionOperator(Guid Id, string FullName, string Language, string RoleName);

/// <param name="IdleMinutes">The idle period, for the console's own clock (FR-032).</param>
/// <param name="NewBrowser">True on the answer that opened the session when the browser was new.</param>
internal sealed record OperatorSessionView(SessionOperator Operator, DateTimeOffset ExpiresAt, int IdleMinutes, bool NewBrowser);

internal sealed record TwoFactorView(bool On, bool Locked);

internal sealed record RecentSessionView(
    string Device, string? Address, bool Current, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

/// <summary>The signed-in operator's own account (FR-041 to FR-043). Personal data: its own, shown back to it.</summary>
internal sealed record ProfileView(
    string FullName,
    string RoleName,
    [property: PersonalData] string Email,
    [property: PersonalData] string? Mobile,
    TwoFactorView TwoFactor,
    IReadOnlyList<RecentSessionView> Sessions)
{
    public override string ToString() => nameof(ProfileView);
}

/// <summary>
/// What the console reads about the signed-in operator: only ever their own account — no
/// address of the console takes an account's identifier (FR-039).
/// </summary>
internal sealed class OperatorProfile(OperatorsDbContext db, IOptions<ScsOptions> options) : IOperatorDirectory
{
    /// <summary>How many sessions the profile lists (FR-043).</summary>
    public const int RecentSessions = 10;

    /// <summary>
    /// Until operators have roles (spec 011) every operator is shown as the first one is: a
    /// general administrator.
    /// </summary>
    public static string RoleName(string language) => language == "en" ? "Super Admin" : "مسؤول عام";

    public async Task<OperatorSessionView?> DescribeSessionAsync(
        Guid operatorId, Guid sessionId, string language, bool newBrowser, CancellationToken cancellation)
    {
        var found = await (
            from session in db.Sessions.AsNoTracking()
            join account in db.Operators.AsNoTracking() on session.OperatorId equals account.Id
            where session.Id == sessionId && account.Id == operatorId
            select new { account.FullName, account.Language, session.ExpiresAtUtc }).SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        _ = language;
        return found is null
            ? null
            : new OperatorSessionView(
                new SessionOperator(operatorId, found.FullName, found.Language, RoleName(found.Language)),
                found.ExpiresAtUtc,
                options.Value.Ops.IdleMinutes,
                newBrowser);
    }

    public async Task<ProfileView> ReadAsync(Guid operatorId, Guid sessionId, string language, CancellationToken cancellation)
    {
        var account = await db.Operators.AsNoTracking()
            .Where(o => o.Id == operatorId)
            .Select(o => new { o.FullName, o.Email, o.Mobile, o.Language })
            .SingleAsync(cancellation)
            .ConfigureAwait(false);

        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.OperatorId == operatorId)
            .OrderByDescending(s => s.StartedAtUtc)
            .Take(RecentSessions)
            .Select(s => new { s.Id, s.UserAgent, s.Address, s.StartedAtUtc, s.EndedAtUtc })
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        _ = language;
        return new ProfileView(
            account.FullName,
            RoleName(account.Language),
            account.Email,
            account.Mobile,
            new TwoFactorView(On: true, Locked: true),
            [
                .. sessions.Select(s => new RecentSessionView(
                    DeviceText.Describe(s.UserAgent, account.Language), s.Address, s.Id == sessionId && s.EndedAtUtc == null,
                    s.StartedAtUtc, s.EndedAtUtc)),
            ]);
    }

    public async Task<OperatorSummary?> FindAsync(Guid operatorId, CancellationToken cancellation) =>
        await db.Operators.AsNoTracking()
            .Where(o => o.Id == operatorId)
            .Select(o => new OperatorSummary(o.Id, o.FullName, o.Language, o.State))
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);
}

/// <summary>
/// Daily: removes what is over (research R13) — codes and links a day after they ended,
/// sessions ninety days after theirs, browsers not seen for four hundred days, and records of
/// signing in older than the retention period (FR-052a). It crosses no clinic boundary: these
/// tables have none.
/// </summary>
[RecordsNothing("removes spent codes, used links, old sessions and sign-in records past their retention; no clinic's data")]
internal sealed partial class OperatorsCleanupJob(
    OperatorsDbContext db, TimeProvider clock, IOptions<ScsOptions> options, ILogger<OperatorsCleanupJob> logger)
{
    public const string JobName = "ops-cleanup";
    public const string Cron = "20 3 * * *";

    public static readonly TimeSpan KeepEndedSteps = TimeSpan.FromDays(1);
    public static readonly TimeSpan KeepEndedSessions = TimeSpan.FromDays(90);

    public async Task<int> RunAsync(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var steps = now - KeepEndedSteps;
        var sessions = now - KeepEndedSessions;
        var browsers = now - OperatorCookies.DeviceLife;
        var records = now.AddMonths(-options.Value.Ops.RecordMonths);

        var removed = await db.Challenges.Where(c => c.ExpiresAtUtc < steps).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
        removed += await db.Links
            .Where(l => l.ExpiresAtUtc < steps || l.UsedAtUtc < steps || l.EndedAtUtc < steps)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
        removed += await db.Sessions.Where(s => s.EndedAtUtc < sessions).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
        removed += await db.Browsers.Where(b => b.LastSeenAtUtc < browsers).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
        removed += await db.Records.Where(r => r.OccurredAtUtc < records).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        if (removed > 0)
        {
            LogCleaned(logger, removed);
        }

        return removed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Operators' sign-in clean-up removed {rows} rows")]
    private static partial void LogCleaned(ILogger logger, int rows);
}
