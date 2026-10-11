using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// What the audit trail asks about accounts (spec 007): how one is called, and how a run of
/// failed sign-in attempts went. A name is the email address until accounts have names (spec
/// 026). Everything is read under the established clinic's isolation.
/// </summary>
internal sealed class StaffDirectory(IdentityDbContext db, IClinicContext clinic, IOptions<ScsOptions> options)
    : IStaffDirectory, IAuditActorNames
{
    public async Task<string?> NameOfAsync(Guid accountId, CancellationToken cancellation) =>
        await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.Email)
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        clinic.Require();

        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = accountIds.Distinct().ToList();
        return await db.Accounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email, cancellation)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, StaffContact>> ContactsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        clinic.Require();

        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, StaffContact>();
        }

        var ids = accountIds.Distinct().ToList();
        var rows = await (
            from account in db.Accounts.AsNoTracking()
            where ids.Contains(account.Id)
            select new
            {
                account.Id,
                account.Email,
                account.State,
                Language = db.Preferences.Where(p => p.AccountId == account.Id).Select(p => p.Language).FirstOrDefault(),
            }).ToListAsync(cancellation).ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.Id, row => new StaffContact(row.Email, row.Language, row.State == AccountStates.Active));
    }

    public async Task<IReadOnlyList<FailedRun>> FailedRunsAsync(
        IReadOnlyCollection<FailedRunRef> runs, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(runs);
        clinic.Require();

        var minutes = options.Value.Auth.LockMinutes;
        var answers = new List<FailedRun>(runs.Count);

        foreach (var run in runs)
        {
            var start = await FirstWrongPasswordAsync(run.AccountId, run.TraceId, cancellation).ConfigureAwait(false);
            if (start is not { } from)
            {
                answers.Add(new FailedRun(run.AccountId, run.TraceId, 1, false, minutes));
                continue;
            }

            var until = run.NextTraceId is null
                ? null
                : await FirstWrongPasswordAsync(run.AccountId, run.NextTraceId, cancellation).ConfigureAwait(false);

            var events = db.Events.AsNoTracking()
                .Where(e => e.AccountId == run.AccountId && e.OccurredAtUtc >= from && (until == null || e.OccurredAtUtc < until));

            // A wrong password typed while the account is locked moves nothing, and is not of the run.
            var lockedAt = await events
                .Where(e => e.Kind == SignInEventKinds.AccountLocked)
                .Select(e => (DateTimeOffset?)e.OccurredAtUtc)
                .MinAsync(cancellation)
                .ConfigureAwait(false);

            var attempts = await events
                .CountAsync(
                    e => e.Kind == SignInEventKinds.WrongPassword && (lockedAt == null || e.OccurredAtUtc <= lockedAt),
                    cancellation)
                .ConfigureAwait(false);

            answers.Add(new FailedRun(run.AccountId, run.TraceId, Math.Max(1, attempts), lockedAt is not null, minutes));
        }

        return answers;
    }

    private async Task<DateTimeOffset?> FirstWrongPasswordAsync(Guid accountId, string traceId, CancellationToken cancellation) =>
        await db.Events.AsNoTracking()
            .Where(e => e.AccountId == accountId && e.Kind == SignInEventKinds.WrongPassword && e.TraceId == traceId)
            .Select(e => (DateTimeOffset?)e.OccurredAtUtc)
            .MinAsync(cancellation)
            .ConfigureAwait(false);
}
