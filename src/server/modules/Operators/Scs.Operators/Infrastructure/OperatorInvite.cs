using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;

namespace Scs.Operators.Infrastructure;

/// <summary>What the deployment command reports: a state, never a secret.</summary>
public sealed record InviteResult(string Outcome, Guid? OperatorId);

/// <summary>
/// What the deployment tool's <c>operator invite</c> and <c>operator withdraw</c> do (FR-003,
/// FR-003a). Reachable from no address of the platform. It never shows, takes or sets a
/// password and never holds a link: it notes that an invitation is owed, and the platform
/// itself makes the link and sends it (<see cref="OperatorInviteJob"/>), so the tool needs
/// neither the mail server nor the keys that protect a link.
/// </summary>
internal sealed class OperatorInvite(OperatorsDbContext db, TimeProvider clock)
{
    public const string Invited = "invited";
    public const string InvitedAgain = "invited-again";
    public const string AlreadyActive = "already-active";
    public const string Withdrawn = "withdrawn";
    public const string NothingToWithdraw = "nothing-to-withdraw";
    public const string NotInvited = "not-invited";

    public async Task<InviteResult> InviteAsync(string fullName, string email, string ranBy, CancellationToken cancellation)
    {
        if (!OperatorEmail.LooksValid(email))
        {
            throw new ArgumentException("Not an email address.", nameof(email));
        }

        var normalized = OperatorEmail.Normalize(email);
        var now = clock.GetUtcNow();
        var account = await db.Operators.SingleOrDefaultAsync(o => o.Email == normalized, cancellation).ConfigureAwait(false);

        if (account is not null && account.State != OperatorStates.Invited)
        {
            await NothingChangedAsync(account.Id, AlreadyActive, ranBy, now, cancellation).ConfigureAwait(false);
            return new InviteResult(AlreadyActive, account.Id);
        }

        var again = account is not null;
        if (account is null)
        {
            account = Operator.Invite(fullName, normalized, now);
            db.Operators.Add(account);
        }

        account.OweInvitation(now);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            // An earlier link stops working the moment a new invitation is asked for.
            await db.Links
                .Where(l => l.OperatorId == account.Id && l.UsedAtUtc == null && l.EndedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(l => l.EndedAtUtc, now), cancellation)
                .ConfigureAwait(false);

            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.InvitedByDeployment, account.Id, now, null, null, OpsOrigin.None.TraceId, facts: ranBy));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return new InviteResult(again ? InvitedAgain : Invited, account.Id);
    }

    /// <summary>Removes an invited account that has not chosen a password; its links stop working at once.</summary>
    public async Task<InviteResult> WithdrawAsync(string email, string ranBy, CancellationToken cancellation)
    {
        var normalized = OperatorEmail.Normalize(email);
        var now = clock.GetUtcNow();
        var account = await db.Operators.SingleOrDefaultAsync(o => o.Email == normalized, cancellation).ConfigureAwait(false);

        if (account is null)
        {
            await NothingChangedAsync(null, NothingToWithdraw, ranBy, now, cancellation).ConfigureAwait(false);
            return new InviteResult(NothingToWithdraw, null);
        }

        if (account.State != OperatorStates.Invited)
        {
            await NothingChangedAsync(account.Id, NotInvited, ranBy, now, cancellation).ConfigureAwait(false);
            return new InviteResult(NotInvited, account.Id);
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.Links.Where(l => l.OperatorId == account.Id).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
            db.Operators.Remove(account);
            db.Records.Add(SignInRecord.Of(
                SignInRecordKinds.InviteWithdrawn, account.Id, now, null, null, OpsOrigin.None.TraceId, facts: ranBy));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return new InviteResult(Withdrawn, account.Id);
    }

    /// <summary>
    /// Every use of the command is recorded, also one that changes nothing (FR-003): which
    /// outcome, and who ran it — never the email it was run for (FR-053).
    /// </summary>
    private async Task NothingChangedAsync(
        Guid? operatorId, string outcome, string ranBy, DateTimeOffset now, CancellationToken cancellation)
    {
        db.Records.Add(SignInRecord.Of(
            SignInRecordKinds.CommandChangedNothing, operatorId, now, null, null, OpsOrigin.None.TraceId,
            facts: $"{outcome}; {ranBy}"));
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
    }
}

/// <summary>
/// Every minute: sends the invitation of each invited operator who is owed one — a link good
/// for seventy-two hours, to choose a first password. An email that cannot leave stays owed and
/// is tried again; a withdrawn or activated account is owed nothing.
/// </summary>
[RecordsNothing("sends an operator's invitation email; the invitation itself was recorded when it was asked for")]
internal sealed partial class OperatorInviteJob(
    OperatorsDbContext db,
    OperatorPasswords passwords,
    IEmailSender mail,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<OperatorInviteJob> logger)
{
    public const string JobName = "ops-invitations";
    public const string Cron = "* * * * *";

    public async Task<int> RunAsync(CancellationToken cancellation)
    {
        var owed = await db.Operators
            .Where(o => o.State == OperatorStates.Invited && o.InvitationOwedSinceUtc != null)
            .OrderBy(o => o.InvitationOwedSinceUtc)
            .Take(20)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var sent = 0;
        var hours = options.Value.Ops.FirstLinkHours;

        foreach (var account in owed)
        {
            var now = clock.GetUtcNow();
            var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var link = await passwords
                    .IssueAsync(account.Id, PasswordLinkKinds.First, TimeSpan.FromHours(hours), now, cancellation)
                    .ConfigureAwait(false);
                account.InvitationSent();
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

                try
                {
                    await mail.SendAsync(OperatorEmails.FirstPassword(account.Email, account.Language, link, hours), cancellation)
                        .ConfigureAwait(false);
                }
                catch (EmailNotSentException)
                {
                    // Nothing is kept of a link nobody received; the next run tries again.
                    await transaction.RollbackAsync(cancellation).ConfigureAwait(false);
                    db.ChangeTracker.Clear();
                    LogNotSent(logger, account.Id);
                    continue;
                }

                await transaction.CommitAsync(cancellation).ConfigureAwait(false);
                sent++;
            }
        }

        return sent;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The invitation of operator {operatorId} could not be sent; it stays owed")]
    private static partial void LogNotSent(ILogger logger, Guid operatorId);
}
