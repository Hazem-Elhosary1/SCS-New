using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;

namespace Scs.Operators.Infrastructure;

/// <summary>Where a request came from, for the record: an address, a browser, a reference.</summary>
internal sealed record OpsOrigin(string? Address, string? UserAgent, string TraceId)
{
    /// <summary>The mark of the browser a sign-in is made from; null where there is none.</summary>
    public string? DeviceMark { get; init; }

    public static OpsOrigin None { get; } = new(null, null, new string('0', 32));
}

/// <summary>A session that was just opened or renewed, and what the browser needs to carry it.</summary>
/// <param name="RenewalSecret">Null when the renewal cookie must be left as it is.</param>
internal sealed record IssuedOperatorSession(
    Guid SessionId, Guid OperatorId, byte[]? RenewalSecret, DateTimeOffset ExpiresAtUtc, bool Remember, bool NewBrowser = false);

/// <summary>What a step ended in. The endpoint turns it into cookies and an answer.</summary>
internal abstract record OpsOutcome
{
    /// <summary>A code is in the operator's email; the browser gets the ticket for the next step.</summary>
    public sealed record CodeSent(
        OperatorChallengeTicket Ticket, string Email, DateTimeOffset Now, DateTimeOffset ExpiresAtUtc,
        DateTimeOffset ResendAtUtc, int ResendsLeft) : OpsOutcome;

    public sealed record SignedIn(IssuedOperatorSession Session) : OpsOutcome;

    /// <summary>The step did what it was for and has nothing to hand back (a password was changed).</summary>
    public sealed record Done : OpsOutcome;

    /// <param name="ClearChallenge">The pending step is over; the browser should forget it.</param>
    public sealed record Refused(
        int Status,
        string Code,
        DateTimeOffset? LockedUntilUtc = null,
        int? RetryAfterSeconds = null,
        int? AttemptsLeft = null,
        bool ClearChallenge = false,
        string? LinkKind = null,
        string? EndedFor = null) : OpsOutcome;

    public static Refused InvalidCredentials { get; } = new(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);

    public static Refused NoChallenge { get; } =
        new(StatusCodes.Status401Unauthorized, ErrorCodes.StepExpired, ClearChallenge: true);
}

/// <summary>
/// An operator signing in, in two steps (spec 010). The order of checks is fixed (FR-009): the
/// account and its password → the lock → the account's state → sending the code. Everything
/// wrong before a correct password gets one answer after the same work (FR-010, FR-011); only
/// someone who typed the correct password is told the account is locked or suspended. The code
/// cannot be switched off (FR-008).
/// </summary>
internal sealed class OperatorSignIn(
    OperatorsDbContext db,
    OperatorSessions sessions,
    OperatorCodes codes,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    /// <summary>
    /// How long the single answer takes at the least: checking a password costs about a tenth
    /// of a second and the paths differ by a few database calls; waiting until one fixed moment
    /// hides both.
    /// </summary>
    public static readonly TimeSpan FailureFloor = TimeSpan.FromMilliseconds(300);

    private AuthOptions Auth => options.Value.Auth;

    public async Task<OpsOutcome> SignInAsync(
        string? email, string password, bool remember, OpsOrigin origin, CancellationToken cancellation)
    {
        var normalized = OperatorEmail.Normalize(email);
        var account = await db.Operators.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Email == normalized, cancellation)
            .ConfigureAwait(false);

        // An invited account has no password: the same work is done against a hash nobody matches.
        var verification = account?.PasswordHash is { } hash
            ? PasswordHasher.Verify(hash, password)
            : PasswordHasher.VerifyDecoy(password);

        if (account is null || !verification.Matches)
        {
            await CountWrongPasswordAsync(account, origin, cancellation).ConfigureAwait(false);
            return OpsOutcome.InvalidCredentials;
        }

        var now = clock.GetUtcNow();
        var refusal = RefusalAfterCorrectPassword(account, now);
        if (refusal is not null)
        {
            return refusal;
        }

        var rehashed = verification.NeedsRehash ? PasswordHasher.Hash(password) : account.PasswordHash;
        await db.Operators.Where(o => o.Id == account.Id)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(o => o.FailedAttempts, (byte)0)
                    .SetProperty(o => o.LastFailedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(o => o.PasswordHash, rehashed),
                cancellation)
            .ConfigureAwait(false);

        return await codes.IssueAsync(
                account, ChallengePurposes.SignIn, now.AddMinutes(Auth.CodeMinutes), resendsUsed: 0, remember, origin,
                ErrorCodes.SignInCodeNotSent, cancellation)
            .ConfigureAwait(false);
    }

    public async Task<OpsOutcome> ConfirmCodeAsync(
        OperatorChallengeTicket? ticket, string? code, OpsOrigin origin, CancellationToken cancellation)
    {
        var tried = await codes.TryAsync(ticket, code, ChallengePurposes.SignIn, origin, countsTowardsLock: true, cancellation)
            .ConfigureAwait(false);
        if (tried.Refusal is not null)
        {
            return tried.Refusal;
        }

        // The account is checked again: it may have changed since the password.
        var now = clock.GetUtcNow();
        var account = await db.Operators.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == ticket!.OperatorId, cancellation)
            .ConfigureAwait(false);

        if (account is null)
        {
            return OpsOutcome.NoChallenge;
        }

        var refusal = RefusalAfterCorrectPassword(account, now);
        if (refusal is not null)
        {
            return refusal with { ClearChallenge = true };
        }

        var session = await sessions.OpenAsync(account, tried.Remember, origin, cancellation).ConfigureAwait(false);
        return new OpsOutcome.SignedIn(session);
    }

    /// <summary>Lock → the account's state. Null when the operator may go on.</summary>
    internal static OpsOutcome.Refused? RefusalAfterCorrectPassword(Operator account, DateTimeOffset now)
    {
        if (account.IsLocked(now))
        {
            return new OpsOutcome.Refused(
                StatusCodes.Status403Forbidden, ErrorCodes.AccountLocked, LockedUntilUtc: account.LockedUntilUtc);
        }

        return account.State == OperatorStates.Active
            ? null
            : new OpsOutcome.Refused(StatusCodes.Status403Forbidden, ErrorCodes.AccountDisabled);
    }

    /// <summary>
    /// Counts a wrong password and locks on the fifth, in one statement, so two wrong tries at
    /// one moment cannot both be "the fourth". A try made more than the attempt window after the
    /// last counts as the first; while the account is locked nothing moves; an invited account
    /// has no password and is never counted. An email that matches nobody leaves a line with no
    /// account and no address typed (FR-053).
    /// </summary>
    internal async Task<bool> CountWrongPasswordAsync(Operator? account, OpsOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var locked = false;

        if (account is not null)
        {
            var windowStart = now.AddMinutes(-Auth.AttemptWindowMinutes);
            var lockUntil = now.AddMinutes(Auth.LockMinutes);
            var max = Auth.MaxFailedAttempts;
            var id = account.Id;

            locked = (await db.Database
                .SqlQuery<bool>(
                    $"""
                     UPDATE o
                     SET [FailedAttempts] = CASE WHEN n.[Locked] = 1 THEN o.[FailedAttempts]
                                                 WHEN n.[Next] >= {max} THEN 0
                                                 ELSE n.[Next] END,
                         [LastFailedAtUtc] = CASE WHEN n.[Locked] = 1 THEN o.[LastFailedAtUtc] ELSE {now} END,
                         [LockedUntilUtc] = CASE WHEN n.[Locked] = 0 AND n.[Next] >= {max} THEN {lockUntil}
                                                 ELSE o.[LockedUntilUtc] END
                     OUTPUT CAST(CASE WHEN n.[Locked] = 0 AND n.[Next] >= {max} THEN 1 ELSE 0 END AS bit) AS [Value]
                     FROM [ops].[Operator] o
                     CROSS APPLY (
                         SELECT CASE WHEN o.[LockedUntilUtc] > {now} THEN 1 ELSE 0 END AS [Locked],
                                CASE WHEN o.[LastFailedAtUtc] IS NULL OR o.[LastFailedAtUtc] < {windowStart}
                                     THEN 1 ELSE o.[FailedAttempts] + 1 END AS [Next]
                     ) n
                     WHERE o.[Id] = {id} AND o.[State] <> 'invited'
                     """)
                .ToListAsync(cancellation)
                .ConfigureAwait(false)).SingleOrDefault();
        }

        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.LoginFailed, account?.Id, now, origin.Address, origin.UserAgent, origin.TraceId));

        if (locked)
        {
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.AccountLocked, account!.Id, now, origin.Address, origin.UserAgent, origin.TraceId));
        }

        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        if (locked)
        {
            await sessions.EndAllAsync(account!.Id, SessionEndings.Locked, origin, cancellation).ConfigureAwait(false);
        }

        return locked;
    }
}

/// <summary>An email as it is compared: trimmed and in lower case.</summary>
internal static class OperatorEmail
{
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>Enough of a check to refuse what is plainly not an address; the mail server is the judge.</summary>
    public static bool LooksValid(string? email)
    {
        var value = Normalize(email);
        var at = value.IndexOf('@', StringComparison.Ordinal);
        return value.Length <= Operator.EmailMaxLength
               && at > 0
               && at == value.LastIndexOf('@')
               && value.IndexOf('.', at) > at + 1
               && !value.EndsWith('.')
               && !value.Any(char.IsWhiteSpace);
    }
}

/// <summary>
/// The code of a step: issuing it, trying it, sending it again (FR-020 to FR-024). One
/// implementation serves signing in and changing a password (FR-045a).
/// </summary>
internal sealed class OperatorCodes(
    OperatorsDbContext db,
    OperatorSessions sessions,
    IEmailSender mail,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    private AuthOptions Auth => options.Value.Auth;

    private byte[] Key => Auth.CodeKeyBytes();

    internal sealed record Tried(OpsOutcome.Refused? Refusal, bool Remember, string? PendingPasswordHash, Guid? SessionId);

    public async Task<OpsOutcome> IssueAsync(
        Operator account, string purpose, DateTimeOffset expiresAt, byte resendsUsed, bool remember, OpsOrigin origin,
        string notSentCode, CancellationToken cancellation, Guid? sessionId = null, string? pendingPasswordHash = null,
        Guid? replaces = null)
    {
        var id = Guid.NewGuid();
        var code = CodeHasher.NewCode();

        // The email leaves first. If it cannot, nothing has changed: no code stands for a first
        // step, and the earlier code stays good for a resend (FR-013).
        try
        {
            await mail.SendAsync(
                    OperatorEmails.Code(account.Email, account.Language, code, Auth.CodeMinutes, purpose), cancellation)
                .ConfigureAwait(false);
        }
        catch (EmailNotSentException)
        {
            return new OpsOutcome.Refused(StatusCodes.Status503ServiceUnavailable, notSentCode);
        }

        var now = clock.GetUtcNow();
        var challenge = SignInChallenge.Issue(
            id, account.Id, purpose, CodeHasher.HashCode(Key, id, code), now, expiresAt,
            TimeSpan.FromSeconds(Auth.ResendSeconds), resendsUsed, remember, origin.Address, sessionId, pendingPasswordHash);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // A new code ends the one it is sent in place of, and any other of the same session
            // (a change of password begun again). A sign-in begun in another browser keeps its
            // own code: the first may still finish, and whichever completes last holds the
            // session (spec 010, edge cases).
            await db.Challenges
                .Where(c => c.OperatorId == account.Id && c.Purpose == purpose && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null
                    && (c.Id == replaces || (sessionId != null && c.SessionId == sessionId)))
                .ExecuteUpdateAsync(update => update.SetProperty(c => c.ReplacedAtUtc, now), cancellation)
                .ConfigureAwait(false);

            db.Challenges.Add(challenge);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return new OpsOutcome.CodeSent(
            new OperatorChallengeTicket(id, account.Id, expiresAt),
            account.Email,
            now,
            expiresAt,
            challenge.ResendAvailableAtUtc,
            ResendsLeft: Math.Max(0, Auth.MaxResends - resendsUsed));
    }

    /// <summary>
    /// One statement counts the try and, only for a standing code that matches, uses it up: two
    /// requests with the right code cannot both succeed, and a sixth try never does.
    /// </summary>
    public async Task<Tried> TryAsync(
        OperatorChallengeTicket? ticket, string? code, string purpose, OpsOrigin origin, bool countsTowardsLock,
        CancellationToken cancellation)
    {
        if (ticket is null)
        {
            return new Tried(OpsOutcome.NoChallenge, false, null, null);
        }

        var wrong = new OpsOutcome.Refused(StatusCodes.Status401Unauthorized, ErrorCodes.CodeInvalid);
        if (code is null || code.Length != CodeHasher.CodeLength || !code.All(char.IsAsciiDigit))
        {
            return new Tried(wrong, false, null, null);
        }

        var now = clock.GetUtcNow();
        var hash = CodeHasher.HashCode(Key, ticket.ChallengeId, code);
        var max = Auth.CodeAttempts;
        var challengeId = ticket.ChallengeId;
        var operatorId = ticket.OperatorId;

        var outcome = (await db.Database
            .SqlQuery<CodeAttempt>(
                $"""
                 UPDATE [ops].[SignInChallenge]
                 SET [Attempts] = [Attempts] + 1,
                     [ConsumedAtUtc] = CASE WHEN [CodeHash] = {hash} THEN {now} END
                 OUTPUT CAST(inserted.[Attempts] AS int) AS [Attempts],
                        CAST(CASE WHEN inserted.[ConsumedAtUtc] IS NULL THEN 0 ELSE 1 END AS bit) AS [Accepted],
                        inserted.[Remember] AS [Remember],
                        inserted.[PendingPasswordHash] AS [PendingPasswordHash],
                        inserted.[SessionId] AS [SessionId]
                 WHERE [Id] = {challengeId} AND [OperatorId] = {operatorId} AND [Purpose] = {purpose}
                   AND [ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL
                   AND [ExpiresAtUtc] > {now} AND [Attempts] < {max}
                 """)
            .ToListAsync(cancellation)
            .ConfigureAwait(false)).SingleOrDefault();

        if (outcome is null)
        {
            // Expired, used, replaced, cancelled, or its tries are spent: start again.
            return new Tried(OpsOutcome.NoChallenge, false, null, null);
        }

        if (outcome.Accepted)
        {
            return new Tried(null, outcome.Remember, outcome.PendingPasswordHash, outcome.SessionId);
        }

        var spent = outcome.Attempts >= max;
        await CountWrongCodeAsync(operatorId, spent && countsTowardsLock, origin, cancellation).ConfigureAwait(false);

        return new Tried(
            spent ? OpsOutcome.NoChallenge : wrong with { AttemptsLeft = max - outcome.Attempts },
            false, null, null);
    }

    public async Task<OpsOutcome> ResendAsync(
        OperatorChallengeTicket? ticket, string purpose, OpsOrigin origin, CancellationToken cancellation)
    {
        if (ticket is null)
        {
            return OpsOutcome.NoChallenge;
        }

        var now = clock.GetUtcNow();
        var standing = await db.Challenges.AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Id == ticket.ChallengeId && c.OperatorId == ticket.OperatorId && c.Purpose == purpose
                     && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null && c.ExpiresAtUtc > now,
                cancellation)
            .ConfigureAwait(false);

        if (standing is null)
        {
            return OpsOutcome.NoChallenge;
        }

        if (standing.ResendsUsed >= Auth.MaxResends)
        {
            return new OpsOutcome.Refused(StatusCodes.Status409Conflict, ErrorCodes.ResendLimitReached);
        }

        if (standing.ResendAvailableAtUtc > now)
        {
            var wait = (int)Math.Ceiling((standing.ResendAvailableAtUtc - now).TotalSeconds);
            return new OpsOutcome.Refused(StatusCodes.Status409Conflict, ErrorCodes.ResendTooEarly, RetryAfterSeconds: wait);
        }

        var account = await db.Operators.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == ticket.OperatorId, cancellation)
            .ConfigureAwait(false);

        if (account is null)
        {
            return OpsOutcome.NoChallenge;
        }

        // A new code for the same step: it does not get ten minutes of its own.
        return await IssueAsync(
                account, purpose, standing.ExpiresAtUtc, (byte)(standing.ResendsUsed + 1), standing.Remember, origin,
                ErrorCodes.SignInCodeNotResent, cancellation, standing.SessionId, standing.PendingPasswordHash, replaces: standing.Id)
            .ConfigureAwait(false);
    }

    /// <summary>The step is left: its code stops working.</summary>
    public async Task CancelAsync(OperatorChallengeTicket? ticket, string purpose, CancellationToken cancellation)
    {
        if (ticket is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await db.Challenges
            .Where(c => c.Id == ticket.ChallengeId && c.OperatorId == ticket.OperatorId && c.Purpose == purpose
                        && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.ReplacedAtUtc, now), cancellation)
            .ConfigureAwait(false);
    }

    /// <summary>Every waiting code of the account stops working: its password was just replaced (FR-028).</summary>
    public Task EndAllAsync(Guid operatorId, DateTimeOffset now, CancellationToken cancellation) =>
        db.Challenges
            .Where(c => c.OperatorId == operatorId && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.ReplacedAtUtc, now), cancellation);

    /// <summary>
    /// Records a wrong code. A code spent while signing in is counted on the account, and the
    /// third in a row locks it exactly as wrong passwords do (FR-020a).
    /// </summary>
    private async Task CountWrongCodeAsync(Guid operatorId, bool spentTowardsLock, OpsOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.OtpFailed, operatorId, now, origin.Address, origin.UserAgent, origin.TraceId));

        var locked = false;
        if (spentTowardsLock)
        {
            var lockUntil = now.AddMinutes(Auth.LockMinutes);
            var max = Auth.MaxSpentCodes;
            locked = (await db.Database
                .SqlQuery<bool>(
                    $"""
                     UPDATE [ops].[Operator]
                     SET [SpentCodes] = CASE WHEN [LockedUntilUtc] > {now} THEN [SpentCodes]
                                             WHEN [SpentCodes] + 1 >= {max} THEN 0
                                             ELSE [SpentCodes] + 1 END,
                         [LockedUntilUtc] = CASE WHEN ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= {now})
                                                      AND [SpentCodes] + 1 >= {max} THEN {lockUntil}
                                                 ELSE [LockedUntilUtc] END
                     OUTPUT CAST(CASE WHEN (deleted.[LockedUntilUtc] IS NULL OR deleted.[LockedUntilUtc] <= {now})
                                           AND deleted.[SpentCodes] + 1 >= {max} THEN 1 ELSE 0 END AS bit) AS [Value]
                     WHERE [Id] = {operatorId}
                     """)
                .ToListAsync(cancellation)
                .ConfigureAwait(false)).SingleOrDefault();
        }

        if (locked)
        {
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.AccountLocked, operatorId, now, origin.Address, origin.UserAgent, origin.TraceId));
        }

        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        if (locked)
        {
            await sessions.EndAllAsync(operatorId, SessionEndings.Locked, origin, cancellation).ConfigureAwait(false);
        }
    }
}

/// <summary>What one try of a code did to it.</summary>
internal sealed class CodeAttempt
{
    public int Attempts { get; set; }

    public bool Accepted { get; set; }

    public bool Remember { get; set; }

    public string? PendingPasswordHash { get; set; }

    public Guid? SessionId { get; set; }
}
