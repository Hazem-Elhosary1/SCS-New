using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Contracts;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// Opens, renews and ends an operator's sessions (spec 010, FR-030 to FR-038). An operator has
/// one session: a new sign-in ends the earlier one in the same transaction that opens the new.
/// </summary>
internal sealed partial class OperatorSessions(
    OperatorsDbContext db,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<OperatorSessions> logger) : IOperatorSessionCheck, IOperatorSessions
{
    private AuthOptions Auth => options.Value.Auth;

    private TimeSpan Idle => TimeSpan.FromMinutes(options.Value.Ops.IdleMinutes);

    /// <summary>
    /// One statement on every request: the session is alive — open, not past its longest life,
    /// used within the idle period — and, when the operator caused the request, its last
    /// activity moves. When it is not alive, the row says why, and an idle or outlived session
    /// is ended here so that its reason is kept.
    /// </summary>
    public async Task<OperatorSessionVerdict> CheckAsync(
        Guid sessionId, Guid operatorId, bool background, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var idleSince = now - Idle;

        var alive = background
            ? await db.Sessions.AsNoTracking()
                .AnyAsync(
                    s => s.Id == sessionId && s.OperatorId == operatorId && s.EndedAtUtc == null
                         && s.ExpiresAtUtc > now && s.LastActivityAtUtc > idleSince,
                    cancellation)
                .ConfigureAwait(false)
            : await db.Sessions
                .Where(s => s.Id == sessionId && s.OperatorId == operatorId && s.EndedAtUtc == null
                            && s.ExpiresAtUtc > now && s.LastActivityAtUtc > idleSince)
                .ExecuteUpdateAsync(update => update.SetProperty(s => s.LastActivityAtUtc, now), cancellation)
                .ConfigureAwait(false) == 1;

        if (alive)
        {
            return OperatorSessionVerdict.Yes;
        }

        return new OperatorSessionVerdict(false, await WhyOverAsync(sessionId, operatorId, now, cancellation).ConfigureAwait(false));
    }

    /// <summary>
    /// Opens the session of a completed sign-in: every earlier one of the account ends as
    /// "replaced", the count of spent codes is forgotten, and a browser the account never used
    /// is told to its owner by email (FR-030, FR-033a).
    /// </summary>
    public async Task<IssuedOperatorSession> OpenAsync(Operator account, bool remember, OpsOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var secret = CodeHasher.NewRenewalSecret();
        var session = OperatorSession.Open(
            account.Id, CodeHasher.HashRenewalSecret(secret), now, TimeSpan.FromHours(Auth.SessionHours), remember,
            origin.Address, origin.UserAgent);

        var newBrowser = false;
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // First, and so holding the account's row until the end: of two sign-ins that finish
            // at the same moment the second waits here, then ends the first one's session like
            // any earlier one. Whichever completes last holds the session (FR-030).
            await db.Operators.Where(o => o.Id == account.Id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(o => o.SpentCodes, (byte)0).SetProperty(o => o.LastSignInAtUtc, now),
                    cancellation)
                .ConfigureAwait(false);

            await EndCoreAsync(account.Id, exceptSessionId: null, SessionEndings.Replaced, origin, now, cancellation)
                .ConfigureAwait(false);

            db.Sessions.Add(session);
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.LoginSucceeded, account.Id, now, origin.Address, origin.UserAgent, origin.TraceId, session.Id));

            newBrowser = await SeeBrowserAsync(account.Id, origin.DeviceMark, now, cancellation).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        if (newBrowser)
        {
            await TellOfNewBrowserAsync(account, session, origin, now, cancellation).ConfigureAwait(false);
        }

        return new IssuedOperatorSession(session.Id, account.Id, secret, session.ExpiresAtUtc, remember, newBrowser);
    }

    /// <summary>
    /// Renews a session, or says it is over. The secret presented is replaced; the one before
    /// is honoured for a short grace (two tabs renewing together) and, presented after it, is
    /// treated as stolen: the session is ended and the reuse recorded (FR-034).
    /// </summary>
    public async Task<(IssuedOperatorSession? Session, string? EndedFor)> RenewAsync(
        OperatorRenewalTicket ticket, OpsOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var presented = CodeHasher.HashRenewalSecret(ticket.Secret);

        var session = await db.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == ticket.SessionId && s.OperatorId == ticket.OperatorId, cancellation)
            .ConfigureAwait(false);

        if (session is null)
        {
            return (null, null);
        }

        if (session.EndedAtUtc is not null)
        {
            return (null, Told(session.EndedFor));
        }

        var isCurrent = CryptographicOperations.FixedTimeEquals(presented, session.RenewalHash);
        var isPrevious = session.PreviousRenewalHash is { } previous
                         && CryptographicOperations.FixedTimeEquals(presented, previous);

        if (!isCurrent && !isPrevious)
        {
            return (null, null);
        }

        if (isPrevious && !(session.PreviousValidUntilUtc > now))
        {
            // A secret that was replaced, shown again after its grace: someone holds a copy.
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.SessionTokenReuse, session.OperatorId, now, origin.Address, origin.UserAgent,
                origin.TraceId, session.Id));
            await EndAllAsync(session.OperatorId, SessionEndings.Reuse, origin, cancellation).ConfigureAwait(false);
            return (null, SessionEndings.Reuse);
        }

        // Renewing is not activity: an idle or outlived session is over, whoever asks.
        var over = await WhyOverAsync(session.Id, session.OperatorId, now, cancellation).ConfigureAwait(false);
        if (over is not null)
        {
            return (null, over);
        }

        // The operator behind the session must still be allowed in.
        var state = await db.Operators.AsNoTracking()
            .Where(o => o.Id == session.OperatorId)
            .Select(o => new { o.State, o.LockedUntilUtc })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        var refusal = state is null || state.State != OperatorStates.Active
            ? SessionEndings.Suspended
            : state.LockedUntilUtc > now ? SessionEndings.Locked : null;

        if (refusal is not null)
        {
            await EndAllAsync(session.OperatorId, refusal, origin, cancellation).ConfigureAwait(false);
            return (null, refusal);
        }

        if (isPrevious)
        {
            // Within grace: another tab already renewed. A fresh access cookie, the same secret.
            return (new IssuedOperatorSession(session.Id, session.OperatorId, null, session.ExpiresAtUtc, session.Remember), null);
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
                    .SetProperty(s => s.RenewalHash, next),
                cancellation)
            .ConfigureAwait(false);

        // Lost a race with another renewal of the same secret: that one rotated, this one is in grace.
        return (new IssuedOperatorSession(
            session.Id, session.OperatorId, rotated == 1 ? secret : null, session.ExpiresAtUtc, session.Remember), null);
    }

    /// <summary>Ends one session at its owner's request. Nothing happens when it is already over.</summary>
    public async Task SignOutAsync(Guid sessionId, Guid operatorId, OpsOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var ended = await db.Sessions
            .Where(s => s.Id == sessionId && s.OperatorId == operatorId && s.EndedAtUtc == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(s => s.EndedAtUtc, now).SetProperty(s => s.EndedFor, SessionEndings.SignedOut),
                cancellation)
            .ConfigureAwait(false);

        if (ended == 1)
        {
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.SignedOut, operatorId, now, origin.Address, origin.UserAgent, origin.TraceId, sessionId));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Ends every open session of an account, with its reason, and records it.</summary>
    public async Task<int> EndAllAsync(
        Guid operatorId, string reason, OpsOrigin origin, CancellationToken cancellation, Guid? exceptSessionId = null)
    {
        var ended = await EndCoreAsync(operatorId, exceptSessionId, reason, origin, clock.GetUtcNow(), cancellation)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        return ended;
    }

    Task<int> IOperatorSessions.EndAllAsync(Guid operatorId, string reason, CancellationToken cancellation)
    {
        if (reason is not (OperatorSessionEndings.Suspended or OperatorSessionEndings.RoleChanged))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return EndAllAsync(operatorId, reason, OpsOrigin.None, cancellation);
    }

    /// <summary>Ends and queues the record; the caller saves, in its own transaction when it has one.</summary>
    private async Task<int> EndCoreAsync(
        Guid operatorId, Guid? exceptSessionId, string reason, OpsOrigin origin, DateTimeOffset now,
        CancellationToken cancellation)
    {
        var open = await db.Sessions.AsNoTracking()
            .Where(s => s.OperatorId == operatorId && s.EndedAtUtc == null && s.Id != exceptSessionId)
            .Select(s => s.Id)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        if (open.Count == 0)
        {
            return 0;
        }

        var ended = await db.Sessions
            .Where(s => open.Contains(s.Id) && s.EndedAtUtc == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(s => s.EndedAtUtc, now).SetProperty(s => s.EndedFor, reason),
                cancellation)
            .ConfigureAwait(false);

        foreach (var id in open)
        {
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.SessionEnded, operatorId, now, origin.Address, origin.UserAgent, origin.TraceId, id, reason));
        }

        return ended;
    }

    /// <summary>
    /// Why a session is not alive, as the console is told it; null when it is alive, or when the
    /// operator ended it themselves. An open session found idle or outlived is ended here.
    /// </summary>
    private async Task<string?> WhyOverAsync(Guid sessionId, Guid operatorId, DateTimeOffset now, CancellationToken cancellation)
    {
        var row = await db.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.OperatorId == operatorId)
            .Select(s => new { s.EndedAtUtc, s.EndedFor, s.ExpiresAtUtc, s.LastActivityAtUtc })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        if (row.EndedAtUtc is not null)
        {
            return Told(row.EndedFor);
        }

        var reason = row.ExpiresAtUtc <= now
            ? SessionEndings.Expired
            : row.LastActivityAtUtc <= now - Idle ? SessionEndings.Idle : null;

        if (reason is null)
        {
            return null;
        }

        var ended = await db.Sessions
            .Where(s => s.Id == sessionId && s.EndedAtUtc == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(s => s.EndedAtUtc, now).SetProperty(s => s.EndedFor, reason),
                cancellation)
            .ConfigureAwait(false);

        if (ended == 1)
        {
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.SessionEnded, operatorId, now, null, null, OpsOrigin.None.TraceId, sessionId, reason));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        return reason;
    }

    private static string? Told(string? endedFor) =>
        endedFor is null || SessionEndings.ByTheOperator(endedFor) ? null : endedFor;

    /// <summary>True when this account has not completed a sign-in from this browser before.</summary>
    private async Task<bool> SeeBrowserAsync(Guid operatorId, string? mark, DateTimeOffset now, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(mark))
        {
            return true;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(mark));
        var seen = await db.Browsers
            .Where(b => b.OperatorId == operatorId && b.MarkHash == hash)
            .ExecuteUpdateAsync(update => update.SetProperty(b => b.LastSeenAtUtc, now), cancellation)
            .ConfigureAwait(false);

        if (seen == 1)
        {
            return false;
        }

        db.Browsers.Add(KnownBrowser.See(operatorId, hash, now));
        return true;
    }

    /// <summary>The email about a new browser never fails a sign-in; whether it left is recorded (FR-033a).</summary>
    private async Task TellOfNewBrowserAsync(
        Operator account, OperatorSession session, OpsOrigin origin, DateTimeOffset now, CancellationToken cancellation)
    {
        var sent = true;
        try
        {
            await mail.SendAsync(
                    renderer.Render(OperatorEmails.NewBrowser(account.Email, account.Language, origin.UserAgent, origin.Address, now)), cancellation)
                .ConfigureAwait(false);
        }
        catch (EmailNotSentException)
        {
            sent = false;
            LogNoticeNotSent(logger, account.Id);
        }

        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.NewBrowser, account.Id, now, origin.Address, origin.UserAgent, origin.TraceId, session.Id,
            sent ? "sent" : "not sent"));
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The new-browser notice of operator {operatorId} could not be sent")]
    private static partial void LogNoticeNotSent(ILogger logger, Guid operatorId);
}
