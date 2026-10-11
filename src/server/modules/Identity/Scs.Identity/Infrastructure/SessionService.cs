using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>Where a request came from, for the record: an address, a browser, a reference.</summary>
internal sealed record RequestOrigin(string? IpAddress, string? UserAgent, string TraceId)
{
    /// <summary>The mark of the browser a sign-in is made from (spec 008); null where there is none.</summary>
    public string? DeviceMark { get; init; }

    public static RequestOrigin None { get; } = new(null, null, new string('0', 32));
}

/// <summary>A session that was just opened or renewed, and what the browser needs to carry it.</summary>
/// <param name="RenewalSecret">Null when the renewal cookie must be left as it is.</param>
internal sealed record IssuedSession(
    Guid SessionId, Guid AccountId, Guid ClinicId, byte[]? RenewalSecret, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Opens, renews and ends sessions (spec 003, FR-033 to FR-041). Every method works for the
/// established clinic, under isolation.
/// </summary>
internal sealed class SessionService(
    IdentityDbContext db,
    IClinicContext clinic,
    IClinicProfile profile,
    TimeProvider clock,
    IOptions<ScsOptions> options) : IStaffSessions, IStaffSessionCheck
{
    private AuthOptions Auth => options.Value.Auth;

    /// <summary>
    /// One read on every request: is the session alive, and does its account hold a temporary
    /// password (spec 004, FR-036)? Both facts are read together, so replacing the password
    /// frees the session on its very next request.
    /// </summary>
    public async Task<StaffSessionState> CheckAsync(Guid sessionId, Guid accountId, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var found = await (
            from session in db.Sessions.AsNoTracking()
            join account in db.Accounts.AsNoTracking() on session.AccountId equals account.Id
            where session.Id == sessionId && session.AccountId == accountId
                  && session.EndedAtUtc == null && session.ExpiresAtUtc > now
            select (bool?)account.MustChangePassword).SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

        return found switch
        {
            null => StaffSessionState.Ended,
            true => StaffSessionState.MustChangePassword,
            false => StaffSessionState.Alive,
        };
    }

    /// <summary>Adds a session and its "signed in" event to the unit of work; the caller saves.</summary>
    public IssuedSession Open(Guid accountId, RequestOrigin origin)
    {
        var now = clock.GetUtcNow();
        var secret = CodeHasher.NewRenewalSecret();
        var session = Session.Open(
            accountId,
            CodeHasher.HashRenewalSecret(secret),
            now,
            TimeSpan.FromHours(Auth.SessionHours),
            origin.IpAddress,
            origin.UserAgent);

        db.Sessions.Add(session);
        db.Events.Add(SignInEvent.Record(
            accountId, SignInEventKinds.SignedIn, now, origin.TraceId, origin.IpAddress, origin.UserAgent, session.Id));
        db.Record(TrailEntries.SignedIn(accountId, origin));

        return new IssuedSession(session.Id, accountId, clinic.Require(), secret, session.ExpiresAtUtc);
    }

    /// <summary>
    /// Renews a session, or says it is over. The secret presented is replaced; the one before is
    /// honoured for a short grace period (two tabs renewing together) and, presented after it,
    /// is treated as stolen: every session of the account is ended (FR-038).
    /// </summary>
    public async Task<IssuedSession?> RenewAsync(RenewalTicket ticket, RequestOrigin origin, CancellationToken cancellation)
    {
        clinic.Establish(ticket.ClinicId, ClinicSource.Account);

        var now = clock.GetUtcNow();
        var presented = CodeHasher.HashRenewalSecret(ticket.Secret);

        var session = await db.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == ticket.SessionId && s.AccountId == ticket.AccountId, cancellation)
            .ConfigureAwait(false);

        if (session is null || session.EndedAtUtc is not null)
        {
            return null;
        }

        if (session.ExpiresAtUtc <= now)
        {
            await EndAsync([session.Id], session.AccountId, SessionEndReasons.Expired, origin, cancellation).ConfigureAwait(false);
            return null;
        }

        var isCurrent = CryptographicOperations.FixedTimeEquals(presented, session.RenewalHash);
        var isPrevious = session.PreviousRenewalHash is { } previous
                         && CryptographicOperations.FixedTimeEquals(presented, previous);

        if (!isCurrent && !isPrevious)
        {
            return null;
        }

        if (isPrevious && !(session.PreviousValidUntilUtc > now))
        {
            // A secret that was replaced, shown again after its grace: someone holds a copy.
            db.Events.Add(SignInEvent.Record(
                session.AccountId, SignInEventKinds.RenewalReused, now, origin.TraceId,
                origin.IpAddress, origin.UserAgent, session.Id));
            await EndForAccountCoreAsync(session.AccountId, SessionEndReasons.RenewalReused, origin, cancellation)
                .ConfigureAwait(false);
            return null;
        }

        // The person behind the session must still be allowed in.
        var refusal = await WhyNotAllowedAsync(session.AccountId, now, cancellation).ConfigureAwait(false);
        if (refusal is not null)
        {
            await EndAsync([session.Id], session.AccountId, refusal, origin, cancellation).ConfigureAwait(false);
            return null;
        }

        if (isPrevious)
        {
            // Within grace: another tab already renewed. A fresh access cookie, the same secret.
            return new IssuedSession(session.Id, session.AccountId, session.ClinicId, RenewalSecret: null, session.ExpiresAtUtc);
        }

        var secret = CodeHasher.NewRenewalSecret();
        var next = CodeHasher.HashRenewalSecret(secret);
        var graceEnds = now.AddSeconds(Auth.RenewalGraceSeconds);

        var rotated = await db.Sessions
            .Where(s => s.Id == session.Id && s.EndedAtUtc == null && s.RenewalHash == presented)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(s => s.PreviousRenewalHash, s => s.RenewalHash)
                    .SetProperty(s => s.PreviousValidUntilUtc, graceEnds)
                    .SetProperty(s => s.RenewalHash, next)
                    .SetProperty(s => s.LastUsedAtUtc, now),
                cancellation)
            .ConfigureAwait(false);

        // Lost a race with another renewal of the same secret: that one rotated, this one is in grace.
        return rotated == 1
            ? new IssuedSession(session.Id, session.AccountId, session.ClinicId, secret, session.ExpiresAtUtc)
            : new IssuedSession(session.Id, session.AccountId, session.ClinicId, RenewalSecret: null, session.ExpiresAtUtc);
    }

    /// <summary>Ends one session at its owner's request. Nothing happens when it is already over.</summary>
    public async Task SignOutAsync(Guid sessionId, Guid accountId, Guid clinicId, RequestOrigin origin, CancellationToken cancellation)
    {
        clinic.Establish(clinicId, ClinicSource.Account);
        await EndAsync([sessionId], accountId, SessionEndReasons.SignedOut, origin, cancellation, byOwner: true)
            .ConfigureAwait(false);
    }

    public Task<int> EndForAccountAsync(Guid accountId, string reason, CancellationToken cancellation)
    {
        if (reason is not (SessionEndings.EndedForAccount or SessionEndings.PasswordReset or SessionEndings.RoleChanged))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return EndForAccountCoreAsync(accountId, reason, RequestOrigin.None, cancellation);
    }

    public async Task<int> EndForClinicAsync(Guid? exceptSessionId, string reason, CancellationToken cancellation)
    {
        if (reason is not (SessionEndings.EndedForClinic or SessionEndings.ClinicSuspended))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        clinic.Require();
        var alive = await db.Sessions.AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.Id != exceptSessionId)
            .Select(s => new { s.Id, s.AccountId })
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var ended = 0;
        foreach (var account in alive.GroupBy(s => s.AccountId))
        {
            ended += await EndAsync(
                    [.. account.Select(s => s.Id)], account.Key, reason, RequestOrigin.None, cancellation, TrailEntries.ScopeAll)
                .ConfigureAwait(false);
        }

        return ended;
    }

    /// <summary>
    /// Ends every live session of an account, for any reason, and saves — except one, when
    /// given: the session of the person who has just replaced their own temporary password.
    /// </summary>
    /// <param name="byOwner">The account's owner caused it, signed in or not: the entry names them (spec 007).</param>
    public async Task<int> EndForAccountCoreAsync(
        Guid accountId, string reason, RequestOrigin origin, CancellationToken cancellation, Guid? exceptSessionId = null,
        bool byOwner = false)
    {
        clinic.Require();
        var alive = await db.Sessions.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.EndedAtUtc == null && s.Id != exceptSessionId)
            .Select(s => s.Id)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return await EndAsync(alive, accountId, reason, origin, cancellation, TrailEntries.ScopeAll, byOwner)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Why the account may no longer hold a session, as an end reason; null when it may. Used at
    /// renewal, so a session of a disabled account or a suspended clinic cannot continue.
    /// </summary>
    private async Task<string?> WhyNotAllowedAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellation)
    {
        var account = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.State, a.LockedUntilUtc })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (account is null || account.State != AccountStates.Active)
        {
            return SessionEndReasons.Disabled;
        }

        if (account.LockedUntilUtc > now)
        {
            return SessionEndReasons.Locked;
        }

        var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);
        return ClinicStates.NotPubliclyAvailable.Contains(own.State) ? SessionEndReasons.ClinicSuspended : null;
    }

    private async Task<int> EndAsync(
        List<Guid> sessionIds, Guid accountId, string reason, RequestOrigin origin, CancellationToken cancellation,
        string scope = TrailEntries.ScopeOne, bool byOwner = false)
    {
        if (sessionIds.Count == 0)
        {
            // Events queued by the caller (a reused secret, a lock) must still be written.
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            return 0;
        }

        var now = clock.GetUtcNow();
        var ended = await db.Sessions
            .Where(s => sessionIds.Contains(s.Id) && s.EndedAtUtc == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(s => s.EndedAtUtc, now).SetProperty(s => s.EndReason, reason),
                cancellation)
            .ConfigureAwait(false);

        if (ended > 0)
        {
            foreach (var id in sessionIds)
            {
                db.Events.Add(SignInEvent.Record(
                    accountId, SignInEventKinds.SessionEnded, now, origin.TraceId,
                    origin.IpAddress, origin.UserAgent, id, reason));
            }

            // One entry for the account, however many of its sessions ended (spec 007, FR-013).
            db.Record(TrailEntries.SessionsEnded(accountId, reason, scope, byOwner, origin));
        }

        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        return ended;
    }
}
