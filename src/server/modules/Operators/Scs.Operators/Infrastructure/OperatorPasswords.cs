using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;

namespace Scs.Operators.Infrastructure;

/// <summary>What a link is, read before its screen is shown.</summary>
internal sealed record LinkView(string Kind, string Email)
{
    public override string ToString() => nameof(LinkView);
}

/// <summary>
/// An operator's password: the link that lets its owner choose one — the first, or a new one
/// after forgetting (FR-004, FR-025 to FR-029) — and changing it while signed in, which asks
/// for a code by email before anything changes (FR-045, FR-045a).
/// </summary>
internal sealed partial class OperatorPasswords(
    OperatorsDbContext db,
    OperatorSessions sessions,
    OperatorSignIn signIn,
    OperatorCodes codes,
    PasswordLinkTokens tokens,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<OperatorPasswords> logger)
{
    /// <summary>At most one reset email for an account in this long (FR-026).</summary>
    public static readonly TimeSpan ResetEmailGap = TimeSpan.FromMinutes(1);

    private OpsOptions Ops => options.Value.Ops;

    // ---------------------------------------------------------------- links

    /// <summary>
    /// Asks for a reset link. The caller is told nothing: the answer and the work are the same
    /// whether or not an operator has the address (FR-025). A link goes only to an active
    /// account, locked or not, and at most one a minute.
    /// </summary>
    public async Task ForgotAsync(string? email, OpsOrigin origin, CancellationToken cancellation)
    {
        var normalized = OperatorEmail.Normalize(email);
        var now = clock.GetUtcNow();
        var account = await db.Operators.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Email == normalized, cancellation)
            .ConfigureAwait(false);

        var recently = account is not null && await db.Links.AsNoTracking()
            .AnyAsync(
                l => l.OperatorId == account.Id && l.Kind == PasswordLinkKinds.Reset && l.CreatedAtUtc > now - ResetEmailGap,
                cancellation)
            .ConfigureAwait(false);

        if (account is null || account.State != OperatorStates.Active || recently)
        {
            return;
        }

        var link = await IssueAsync(account.Id, PasswordLinkKinds.Reset, TimeSpan.FromMinutes(Ops.ResetLinkMinutes), now, cancellation)
            .ConfigureAwait(false);

        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.PasswordResetRequested, account.Id, now, origin.Address, origin.UserAgent, origin.TraceId));
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        try
        {
            await mail.SendAsync(
                    renderer.Render(OperatorEmails.ResetLink(account.Email, account.Language, link, Ops.ResetLinkMinutes)), cancellation)
                .ConfigureAwait(false);
        }
        catch (EmailNotSentException)
        {
            // Said to nobody: the screen is the same either way. The log names the account only.
            LogResetNotSent(logger, account.Id);
        }
    }

    /// <summary>
    /// Makes a link of a kind for an account, ends the earlier ones of that kind, and returns
    /// the address to open. The caller saves.
    /// </summary>
    public async Task<string> IssueAsync(Guid operatorId, string kind, TimeSpan life, DateTimeOffset now, CancellationToken cancellation)
    {
        await db.Links
            .Where(l => l.OperatorId == operatorId && l.Kind == kind && l.UsedAtUtc == null && l.EndedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(l => l.EndedAtUtc, now), cancellation)
            .ConfigureAwait(false);

        var secret = PasswordLinkTokens.NewSecret();
        var link = PasswordLink.Issue(operatorId, kind, PasswordLinkTokens.Hash(secret), now, life);
        db.Links.Add(link);

        var first = kind == PasswordLinkKinds.First;
        var token = tokens.Write(new PasswordLinkToken(
            first ? PasswordLinkTokens.OperatorFirst : PasswordLinkTokens.OperatorReset, Guid.Empty, link.Id, secret));

        return $"{Ops.AppBase()}/password/{(first ? "first" : "reset")}#token={token}";
    }

    /// <summary>Reads a link without using it. Null for anything that no longer works.</summary>
    public async Task<(LinkView? View, string? Kind)> ReadAsync(string? token, CancellationToken cancellation)
    {
        var (link, kind) = await FindAsync(token, cancellation).ConfigureAwait(false);
        if (link is null)
        {
            return (null, kind);
        }

        var email = await db.Operators.AsNoTracking()
            .Where(o => o.Id == link.OperatorId)
            .Select(o => o.Email)
            .SingleAsync(cancellation)
            .ConfigureAwait(false);

        return (new LinkView(link.Kind, email), link.Kind);
    }

    /// <summary>
    /// Chooses the password a link is for. The link first, then the rules, then "differs from
    /// the current one". A reset ends every session, lifts a lock and ends every waiting code
    /// and link (FR-028); a first password makes the account active. Neither opens a session.
    /// </summary>
    public async Task<OpsOutcome> SetAsync(string? token, string? password, OpsOrigin origin, CancellationToken cancellation)
    {
        var (found, kind) = await FindAsync(token, cancellation).ConfigureAwait(false);
        if (found is null)
        {
            return LinkInvalid(kind);
        }

        var problems = PasswordRules.Check(password);
        if (problems.Count > 0)
        {
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["password"] = [.. problems] });
        }

        var now = clock.GetUtcNow();
        var account = await db.Operators.SingleAsync(o => o.Id == found.OperatorId, cancellation).ConfigureAwait(false);
        var first = found.Kind == PasswordLinkKinds.First;

        if (first ? account.State != OperatorStates.Invited : account.State != OperatorStates.Active)
        {
            return LinkInvalid(found.Kind);
        }

        if (!first && PasswordHasher.Verify(account.PasswordHash, password!).Matches)
        {
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["password"] = [PasswordRules.SameAsCurrent] });
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Used once: of two requests with the same link, one finds it already taken.
            var taken = await db.Links
                .Where(l => l.Id == found.Id && l.UsedAtUtc == null && l.EndedAtUtc == null && l.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(update => update.SetProperty(l => l.UsedAtUtc, now), cancellation)
                .ConfigureAwait(false);

            if (taken != 1)
            {
                return LinkInvalid(found.Kind);
            }

            var hash = PasswordHasher.Hash(password!);
            if (first)
            {
                account.Activate(hash, now);
            }
            else
            {
                account.SetPassword(hash, now);
            }

            db.Records.Add(SignInRecord.Of(
                first ? SignInRecordKinds.FirstPasswordSet : SignInRecordKinds.PasswordResetCompleted,
                account.Id, now, origin.Address, origin.UserAgent, origin.TraceId));

            await db.Links
                .Where(l => l.OperatorId == account.Id && l.UsedAtUtc == null && l.EndedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(l => l.EndedAtUtc, now), cancellation)
                .ConfigureAwait(false);
            await codes.EndAllAsync(account.Id, now, cancellation).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            if (!first)
            {
                await sessions.EndAllAsync(account.Id, SessionEndings.PasswordReset, origin, cancellation).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return new OpsOutcome.Done();
    }

    // ---------------------------------------------------------------- changing it while signed in

    /// <summary>
    /// Step one of a change (FR-045a): the current password is checked — a wrong one counts as
    /// a wrong try, and the fifth locks the account and ends the session — then the new one's
    /// rules; nothing changes, and a code is sent. The new password is kept only as its hash,
    /// with the code that will release it.
    /// </summary>
    public async Task<OpsOutcome> BeginChangeAsync(
        Guid operatorId, Guid sessionId, string? current, string? password, OpsOrigin origin, CancellationToken cancellation)
    {
        var account = await db.Operators.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == operatorId, cancellation)
            .ConfigureAwait(false);

        if (account is null || account.State != OperatorStates.Active)
        {
            return new OpsOutcome.Refused(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded);
        }

        if (string.IsNullOrEmpty(current) || !PasswordHasher.Verify(account.PasswordHash, current).Matches)
        {
            var locked = await signIn.CountWrongPasswordAsync(account, origin, cancellation).ConfigureAwait(false);
            if (locked)
            {
                return new OpsOutcome.Refused(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded, EndedFor: SessionEndings.Locked);
            }

            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["current"] = ["WRONG"] });
        }

        var problems = PasswordRules.Check(password);
        if (problems.Count == 0 && PasswordHasher.Verify(account.PasswordHash, password!).Matches)
        {
            problems = [PasswordRules.SameAsCurrent];
        }

        if (problems.Count > 0)
        {
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["password"] = [.. problems] });
        }

        var auth = options.Value.Auth;
        return await codes.IssueAsync(
                account, ChallengePurposes.PasswordChange, clock.GetUtcNow().AddMinutes(auth.CodeMinutes), resendsUsed: 0,
                remember: false, origin, ErrorCodes.SignInCodeNotSent, cancellation, sessionId, PasswordHasher.Hash(password!))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Step two: the right code writes the new password. The session that asked stays open;
    /// wrong codes here do not count towards the lock (FR-045a).
    /// </summary>
    public async Task<OpsOutcome> ConfirmChangeAsync(
        OperatorChallengeTicket? ticket, string? code, Guid operatorId, Guid sessionId, OpsOrigin origin,
        CancellationToken cancellation)
    {
        if (ticket is not null && ticket.OperatorId != operatorId)
        {
            return OpsOutcome.NoChallenge;
        }

        var tried = await codes.TryAsync(ticket, code, ChallengePurposes.PasswordChange, origin, countsTowardsLock: false, cancellation)
            .ConfigureAwait(false);
        if (tried.Refusal is not null)
        {
            return tried.Refusal;
        }

        if (tried.SessionId != sessionId || tried.PendingPasswordHash is null)
        {
            return OpsOutcome.NoChallenge;
        }

        var now = clock.GetUtcNow();
        var account = await db.Operators.SingleAsync(o => o.Id == operatorId, cancellation).ConfigureAwait(false);
        account.SetPassword(tried.PendingPasswordHash, now);
        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.PasswordChanged, operatorId, now, origin.Address, origin.UserAgent, origin.TraceId, sessionId));

        await db.Links
            .Where(l => l.OperatorId == operatorId && l.UsedAtUtc == null && l.EndedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(l => l.EndedAtUtc, now), cancellation)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        return new OpsOutcome.Done();
    }

    // ---------------------------------------------------------------- pieces

    private async Task<(PasswordLink? Link, string? Kind)> FindAsync(string? token, CancellationToken cancellation)
    {
        var read = tokens.Read(token);
        var kind = read?.Kind switch
        {
            PasswordLinkTokens.OperatorReset => PasswordLinkKinds.Reset,
            PasswordLinkTokens.OperatorFirst => PasswordLinkKinds.First,
            _ => null,
        };

        if (read is null || kind is null)
        {
            return (null, null);
        }

        var link = await db.Links.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == read.LinkId && l.Kind == kind, cancellation)
            .ConfigureAwait(false);

        return link is not null && link.Stands(clock.GetUtcNow()) && PasswordLinkTokens.Matches(read.Secret, link.SecretHash)
            ? (link, kind)
            : (null, kind);
    }

    private static OpsOutcome.Refused LinkInvalid(string? kind) =>
        new(StatusCodes.Status410Gone, ErrorCodes.LinkInvalid, LinkKind: kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The reset link of operator {operatorId} could not be sent")]
    private static partial void LogResetNotSent(ILogger logger, Guid operatorId);
}
