using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>What a step of sign-in ended in. The endpoint turns it into cookies and an answer.</summary>
internal abstract record SignInOutcome
{
    /// <summary>A code is in the person's email; the browser gets the ticket for the second step.</summary>
    public sealed record CodeSent(
        ChallengeTicket Ticket, int ExpiresInSeconds, int ResendAvailableInSeconds, int ResendsLeft) : SignInOutcome;

    /// <summary>A session was opened.</summary>
    public sealed record SignedIn(IssuedSession Session) : SignInOutcome;

    /// <param name="ClearChallenge">The pending sign-in is over; the browser should forget it.</param>
    public sealed record Refused(
        int Status,
        string Code,
        DateTimeOffset? LockedUntilUtc = null,
        int? ResendAvailableInSeconds = null,
        bool ClearChallenge = false) : SignInOutcome;

    public static Refused InvalidCredentials { get; } = new(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);

    public static Refused CodeInvalid { get; } = new(StatusCodes.Status401Unauthorized, ErrorCodes.CodeInvalid);
}

/// <summary>
/// Signing in, in two steps (spec 003). The order of checks is fixed (FR-009): the account
/// exists → the password → the lock → the account's state → the clinic's state → sending the
/// code. Everything wrong before a correct password gets one answer, after the same work
/// (FR-010, FR-011); only someone who has typed the correct password is told that the account is
/// locked or disabled or the clinic suspended.
/// </summary>
internal sealed class SignInService(
    IdentityDbContext db,
    AccountLookup lookup,
    IClinicContext clinic,
    IClinicProfile profile,
    IEmailSender mail,
    SessionService sessions,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    /// <summary>
    /// How long the single answer takes at the least. Checking a password costs about a tenth of
    /// a second and the paths differ by a few database calls; waiting until one fixed moment
    /// hides both, so the time says nothing about what was wrong.
    /// </summary>
    public static readonly TimeSpan FailureFloor = TimeSpan.FromMilliseconds(300);

    private AuthOptions Auth => options.Value.Auth;

    private byte[] Key => Auth.CodeKeyBytes();

    // ---------------------------------------------------------------- step one

    public async Task<SignInOutcome> SignInAsync(string? email, string password, RequestOrigin origin, CancellationToken cancellation)
    {
        var normalized = EmailAddress.Normalize(email);
        var found = await lookup.FindAsync(normalized, cancellation).ConfigureAwait(false);

        if (found is null)
        {
            PasswordHasher.VerifyDecoy(password);
            await RecordAnonymousAttemptAsync(normalized, origin, cancellation).ConfigureAwait(false);
            return SignInOutcome.InvalidCredentials;
        }

        // From here on the request works for the account's clinic, under ordinary isolation.
        clinic.Establish(found.ClinicId, ClinicSource.Account);

        var account = await db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == found.AccountId, cancellation)
            .ConfigureAwait(false);

        var verification = account?.PasswordHash is { } hash
            ? PasswordHasher.Verify(hash, password)
            : PasswordHasher.VerifyDecoy(password);

        // A temporary password nobody replaced in time is refused exactly as a wrong one:
        // same answer, same time, same count toward the lock (spec 004, FR-031).
        if (account is null || !verification.Matches || account.HasExpiredTemporaryPassword(clock.GetUtcNow()))
        {
            if (account is not null)
            {
                await CountWrongPasswordAsync(account.Id, origin, cancellation).ConfigureAwait(false);
            }

            return SignInOutcome.InvalidCredentials;
        }

        var now = clock.GetUtcNow();
        var refusal = await RefusalAfterCorrectPasswordAsync(account, now, cancellation).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        var rehashed = verification.NeedsRehash ? PasswordHasher.Hash(password) : account.PasswordHash;
        await db.Accounts.Where(a => a.Id == account.Id)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(a => a.FailedAttempts, (byte)0)
                    .SetProperty(a => a.LastFailedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(a => a.PasswordHash, rehashed),
                cancellation)
            .ConfigureAwait(false);

        if (!Auth.EmailCodeEnabled)
        {
            // The second step is switched off for the whole platform (FR-022).
            return await OpenSessionAsync(account.Id, origin, cancellation).ConfigureAwait(false);
        }

        return await IssueCodeAsync(
            account, now.AddMinutes(Auth.CodeMinutes), resendsUsed: 0, ErrorCodes.SignInCodeNotSent, origin, cancellation)
            .ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- step two

    public async Task<SignInOutcome> ConfirmCodeAsync(
        ChallengeTicket? ticket, string? code, RequestOrigin origin, CancellationToken cancellation)
    {
        if (ticket is null || code is null || code.Length != CodeHasher.CodeLength || !code.All(char.IsAsciiDigit))
        {
            return SignInOutcome.CodeInvalid;
        }

        clinic.Establish(ticket.ClinicId, ClinicSource.Account);

        var now = clock.GetUtcNow();
        var hash = CodeHasher.HashCode(Key, ticket.ChallengeId, code);
        var maxAttempts = Auth.CodeAttempts;

        // One statement counts the attempt and, only for a standing code that matches, uses it
        // up: two requests with the right code cannot both succeed, and a sixth try never does.
        var outcome = (await db.Database
            .SqlQuery<CodeAttempt>(
                $"""
                 UPDATE [identity].[SignInChallenge]
                 SET [Attempts] = [Attempts] + 1,
                     [ConsumedAtUtc] = CASE WHEN [CodeHash] = {hash} THEN {now} END
                 OUTPUT CAST(inserted.[Attempts] AS int) AS [Attempts],
                        CAST(CASE WHEN inserted.[ConsumedAtUtc] IS NULL THEN 0 ELSE 1 END AS bit) AS [Accepted]
                 WHERE [Id] = {ticket.ChallengeId} AND [AccountId] = {ticket.AccountId}
                   AND [ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL
                   AND [ExpiresAtUtc] > {now} AND [Attempts] < {maxAttempts}
                 """)
            .ToListAsync(cancellation)
            .ConfigureAwait(false)).SingleOrDefault();

        if (outcome is null)
        {
            return SignInOutcome.CodeInvalid;
        }

        if (!outcome.Accepted)
        {
            await CountWrongCodeAsync(ticket.AccountId, spent: outcome.Attempts >= maxAttempts, origin, cancellation)
                .ConfigureAwait(false);
            return SignInOutcome.CodeInvalid;
        }

        // The account and the clinic are checked again: either may have changed since step one.
        var account = await db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == ticket.AccountId, cancellation)
            .ConfigureAwait(false);

        if (account is null)
        {
            return SignInOutcome.CodeInvalid with { ClearChallenge = true };
        }

        var refusal = await RefusalAfterCorrectPasswordAsync(account, now, cancellation).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal with { ClearChallenge = true };
        }

        return await OpenSessionAsync(account.Id, origin, cancellation).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- resend

    public async Task<SignInOutcome> ResendAsync(ChallengeTicket? ticket, RequestOrigin origin, CancellationToken cancellation)
    {
        if (ticket is null)
        {
            return SignInOutcome.CodeInvalid;
        }

        clinic.Establish(ticket.ClinicId, ClinicSource.Account);

        var now = clock.GetUtcNow();
        var standing = await db.Challenges.AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Id == ticket.ChallengeId && c.AccountId == ticket.AccountId
                     && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null && c.ExpiresAtUtc > now,
                cancellation)
            .ConfigureAwait(false);

        if (standing is null)
        {
            return SignInOutcome.CodeInvalid;
        }

        if (standing.ResendsUsed >= Auth.MaxResends)
        {
            return new SignInOutcome.Refused(StatusCodes.Status409Conflict, ErrorCodes.ResendLimitReached);
        }

        if (standing.ResendAvailableAtUtc > now)
        {
            var wait = (int)Math.Ceiling((standing.ResendAvailableAtUtc - now).TotalSeconds);
            return new SignInOutcome.Refused(
                StatusCodes.Status409Conflict, ErrorCodes.ResendTooEarly, ResendAvailableInSeconds: wait);
        }

        var account = await db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == ticket.AccountId, cancellation)
            .ConfigureAwait(false);

        if (account is null)
        {
            return SignInOutcome.CodeInvalid;
        }

        // A new code for the same sign-in: it does not get ten minutes of its own.
        return await IssueCodeAsync(
            account, standing.ExpiresAtUtc, (byte)(standing.ResendsUsed + 1), ErrorCodes.SignInCodeNotResent, origin, cancellation)
            .ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>
    /// An attempt on an email that matches no account belongs to no clinic. The application may
    /// add such a row and do nothing else with the table, not even read it back, so this is a
    /// plain insert rather than a tracked entity. The email itself is never stored.
    /// </summary>
    private async Task RecordAnonymousAttemptAsync(string normalizedEmail, RequestOrigin origin, CancellationToken cancellation)
    {
        var attempt = AnonymousSignInAttempt.Record(
            CodeHasher.EmailKey(Key, normalizedEmail), clock.GetUtcNow(), origin.TraceId, origin.IpAddress, origin.UserAgent);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO [security].[AnonymousSignInAttempt] ([EmailKey], [IpAddress], [UserAgent], [TraceId], [OccurredAtUtc])
             VALUES ({attempt.EmailKey}, {attempt.IpAddress}, {attempt.UserAgent}, {attempt.TraceId}, {attempt.OccurredAtUtc})
             """,
            cancellation).ConfigureAwait(false);
    }

    /// <summary>Lock → account state → clinic state. Null when the person may go on.</summary>
    private async Task<SignInOutcome.Refused?> RefusalAfterCorrectPasswordAsync(
        StaffAccount account, DateTimeOffset now, CancellationToken cancellation)
    {
        if (account.IsLocked(now))
        {
            return new SignInOutcome.Refused(
                StatusCodes.Status403Forbidden, ErrorCodes.AccountLocked, LockedUntilUtc: account.LockedUntilUtc);
        }

        if (account.State != AccountStates.Active)
        {
            return new SignInOutcome.Refused(StatusCodes.Status403Forbidden, ErrorCodes.AccountDisabled);
        }

        var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);
        return ClinicStates.NotPubliclyAvailable.Contains(own.State)
            ? new SignInOutcome.Refused(StatusCodes.Status403Forbidden, ErrorCodes.ClinicSuspended)
            : null;
    }

    private async Task<SignInOutcome> OpenSessionAsync(Guid accountId, RequestOrigin origin, CancellationToken cancellation)
    {
        var session = sessions.Open(accountId, origin);
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        // A completed sign-in forgets the codes spent before it.
        await db.Accounts.Where(a => a.Id == accountId && a.SpentCodes != 0)
            .ExecuteUpdateAsync(update => update.SetProperty(a => a.SpentCodes, (byte)0), cancellation)
            .ConfigureAwait(false);

        return new SignInOutcome.SignedIn(session);
    }

    private async Task<SignInOutcome> IssueCodeAsync(
        StaffAccount account,
        DateTimeOffset expiresAt,
        byte resendsUsed,
        string notSentCode,
        RequestOrigin origin,
        CancellationToken cancellation)
    {
        var id = Guid.NewGuid();
        var code = CodeHasher.NewCode();

        // The email leaves first. If it cannot, nothing has changed: no code stands for step
        // one, and the earlier code stays valid for a resend (FR-014, FR-027).
        try
        {
            await mail.SendAsync(CodeEmail.Build(account.Email, code, Auth.CodeMinutes), cancellation).ConfigureAwait(false);
        }
        catch (EmailNotSentException)
        {
            return new SignInOutcome.Refused(StatusCodes.Status503ServiceUnavailable, notSentCode);
        }

        var now = clock.GetUtcNow();
        var challenge = SignInChallenge.Issue(
            id,
            account.Id,
            CodeHasher.HashCode(Key, id, code),
            now,
            expiresAt,
            TimeSpan.FromSeconds(Auth.ResendSeconds),
            resendsUsed,
            Session.Truncate(origin.IpAddress, Session.IpAddressMaxLength));

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // The newest code is the only one that stands.
            await db.Challenges
                .Where(c => c.AccountId == account.Id && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(c => c.ReplacedAtUtc, now), cancellation)
                .ConfigureAwait(false);

            db.Challenges.Add(challenge);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        var ticket = new ChallengeTicket(id, account.Id, clinic.Require(), expiresAt);
        return new SignInOutcome.CodeSent(
            ticket,
            ExpiresInSeconds: Math.Max(0, (int)(expiresAt - now).TotalSeconds),
            ResendAvailableInSeconds: Auth.ResendSeconds,
            ResendsLeft: Math.Max(0, Auth.MaxResends - resendsUsed));
    }

    /// <summary>
    /// Counts a wrong password and locks on the fifth, in one statement, so two wrong attempts
    /// at the same moment cannot both be "the fourth". An attempt made more than the attempt
    /// window after the last one counts as the first. While the account is locked nothing moves.
    /// An invited account has no password and is never counted.
    /// </summary>
    private async Task CountWrongPasswordAsync(Guid accountId, RequestOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var windowStart = now.AddMinutes(-Auth.AttemptWindowMinutes);
        var lockUntil = now.AddMinutes(Auth.LockMinutes);
        var max = Auth.MaxFailedAttempts;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var counted = (await db.Database
                .SqlQuery<WrongPasswordCount>(
                    $"""
                     UPDATE a
                     SET [FailedAttempts] = CASE WHEN n.[Locked] = 1 THEN a.[FailedAttempts]
                                                 WHEN n.[Next] >= {max} THEN 0
                                                 ELSE n.[Next] END,
                         [LastFailedAtUtc] = CASE WHEN n.[Locked] = 1 THEN a.[LastFailedAtUtc] ELSE {now} END,
                         [LockedUntilUtc] = CASE WHEN n.[Locked] = 0 AND n.[Next] >= {max} THEN {lockUntil}
                                                 ELSE a.[LockedUntilUtc] END
                     OUTPUT CAST(CASE WHEN n.[Locked] = 0 AND n.[Next] >= {max} THEN 1 ELSE 0 END AS bit) AS [Locked],
                            CAST(CASE WHEN n.[Locked] = 0 AND n.[Next] = 1 THEN 1 ELSE 0 END AS bit) AS [StartsRun]
                     FROM [identity].[StaffAccount] a
                     CROSS APPLY (
                         SELECT CASE WHEN a.[LockedUntilUtc] > {now} THEN 1 ELSE 0 END AS [Locked],
                                CASE WHEN a.[LastFailedAtUtc] IS NULL OR a.[LastFailedAtUtc] < {windowStart}
                                     THEN 1 ELSE a.[FailedAttempts] + 1 END AS [Next]
                     ) n
                     WHERE a.[Id] = {accountId} AND a.[State] <> 'invited'
                     """)
                .ToListAsync(cancellation)
                .ConfigureAwait(false)).SingleOrDefault();

            db.Events.Add(SignInEvent.Record(
                accountId, SignInEventKinds.WrongPassword, now, origin.TraceId, origin.IpAddress, origin.UserAgent));

            // A run of failed attempts is one entry of the trail, written by its first attempt (spec 007).
            if (counted?.StartsRun == true)
            {
                db.Record(TrailEntries.FailedRun(accountId, origin));
            }

            await FinishLockAsync(accountId, counted?.Locked == true, now, origin, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records a wrong code. A spent code (its fifth wrong try) is counted on the account, and
    /// the third in a row locks it exactly as wrong passwords do (FR-032).
    /// </summary>
    private async Task CountWrongCodeAsync(Guid accountId, bool spent, RequestOrigin origin, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            db.Events.Add(SignInEvent.Record(
                accountId, SignInEventKinds.WrongCode, now, origin.TraceId, origin.IpAddress, origin.UserAgent));

            var locked = false;
            if (spent)
            {
                db.Events.Add(SignInEvent.Record(
                    accountId, SignInEventKinds.CodeSpent, now, origin.TraceId, origin.IpAddress, origin.UserAgent));

                var lockUntil = now.AddMinutes(Auth.LockMinutes);
                var max = Auth.MaxSpentCodes;
                locked = (await db.Database
                    .SqlQuery<bool>(
                        $"""
                         UPDATE [identity].[StaffAccount]
                         SET [SpentCodes] = CASE WHEN [LockedUntilUtc] > {now} THEN [SpentCodes]
                                                 WHEN [SpentCodes] + 1 >= {max} THEN 0
                                                 ELSE [SpentCodes] + 1 END,
                             [LockedUntilUtc] = CASE WHEN ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= {now})
                                                          AND [SpentCodes] + 1 >= {max} THEN {lockUntil}
                                                     ELSE [LockedUntilUtc] END
                         OUTPUT CAST(CASE WHEN (deleted.[LockedUntilUtc] IS NULL OR deleted.[LockedUntilUtc] <= {now})
                                               AND deleted.[SpentCodes] + 1 >= {max} THEN 1 ELSE 0 END AS bit) AS [Value]
                         WHERE [Id] = {accountId}
                         """)
                    .ToListAsync(cancellation)
                    .ConfigureAwait(false)).SingleOrDefault();
            }

            await FinishLockAsync(accountId, locked, now, origin, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Saves the queued events; when the account was just locked, also ends its sessions.</summary>
    private async Task FinishLockAsync(
        Guid accountId, bool justLocked, DateTimeOffset now, RequestOrigin origin, CancellationToken cancellation)
    {
        if (justLocked)
        {
            db.Events.Add(SignInEvent.Record(
                accountId, SignInEventKinds.AccountLocked, now, origin.TraceId, origin.IpAddress, origin.UserAgent));
            await sessions.EndForAccountCoreAsync(accountId, SessionEndReasons.Locked, origin, cancellation)
                .ConfigureAwait(false);
        }
        else
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
    }

}

/// <summary>What the sign-in lookup returns: two identifiers.</summary>
internal sealed class AccountLocation
{
    public Guid AccountId { get; set; }

    public Guid ClinicId { get; set; }
}

/// <summary>What one wrong password did to the account's count.</summary>
internal sealed class WrongPasswordCount
{
    /// <summary>This attempt locked the account.</summary>
    public bool Locked { get; set; }

    /// <summary>This attempt is the first of a run: none stood before it, or the last was too long ago.</summary>
    public bool StartsRun { get; set; }
}

/// <summary>What one try of a code did to it.</summary>
internal sealed class CodeAttempt
{
    public int Attempts { get; set; }

    public bool Accepted { get; set; }
}
