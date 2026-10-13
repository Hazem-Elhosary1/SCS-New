using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// Asking for a reset link (spec 004, FR-003 to FR-006). The request does the least possible and
/// never sends mail itself: it finds the account, notes the request, and leaves the rest to
/// <see cref="AccountEmailJob"/>. So the answer is the same, and takes the same time, whether the
/// address is registered or not, whether the account may have a link or not, and whether the
/// mail server is up or not (research R6).
/// </summary>
internal sealed class ForgotPasswordService(
    IdentityDbContext db,
    AccountLookup lookup,
    IClinicContext clinic,
    IClinicJobScheduler jobs,
    TimeProvider clock)
{
    public async Task RequestAsync(string? email, RequestOrigin origin, CancellationToken cancellation)
    {
        var found = await lookup.FindAsync(EmailAddress.Normalize(email), cancellation).ConfigureAwait(false);
        if (found is null)
        {
            return;
        }

        // From here on the request works for the account's clinic, under ordinary isolation.
        clinic.Establish(found.ClinicId, ClinicSource.Account);

        var state = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == found.AccountId)
            .Select(a => a.State)
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (state is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        db.Events.Add(SignInEvent.Record(
            found.AccountId, SignInEventKinds.ResetRequested, now, origin.TraceId, origin.IpAddress, origin.UserAgent));

        // Only an active account is sent a link: a disabled one may not sign in, and one that
        // was never activated gets its link from whoever invited it (FR-004).
        var mayHaveLink = state == AccountStates.Active;
        if (mayHaveLink)
        {
            db.EmailRequests.Add(AccountEmailRequest.For(found.AccountId, AccountEmailKinds.ResetLink, now));
        }

        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        if (mayHaveLink)
        {
            jobs.TryEnqueueForClinic<AccountEmailJob>(found.ClinicId);
        }
    }
}

/// <summary>
/// Sends the clinic's waiting account emails (research R7). For a reset link it is the only
/// place the two caps live: at most one a minute and five an hour per account. The link is made
/// here, so its secret exists only in this job's memory and in the email — the job store keeps
/// the clinic's id and nothing else. Several requests for one account are handled as one, and
/// each account by itself: one failed email does not hold up another's. Running it twice has the
/// same result as once.
/// </summary>
[RecordsNothing("sends the emails that were asked for; nothing changes until a link is used")]
internal sealed partial class AccountEmailJob(
    IdentityDbContext db,
    PasswordLinks links,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<AccountEmailJob> logger) : IClinicJob
{
    /// <summary>A request older than this is not worth an email any more.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    public async Task RunAsync(CancellationToken cancellation)
    {
        var waiting = await db.EmailRequests
            .Where(r => r.HandledAtUtc == null)
            .OrderBy(r => r.RequestedAtUtc)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var group in waiting.GroupBy(r => (r.AccountId, r.Kind)))
        {
            var outcome = await HandleAsync(group.Key.AccountId, group.Key.Kind, cancellation).ConfigureAwait(false);
            var now = clock.GetUtcNow();

            if (outcome is null)
            {
                // The mail server refused. Leave what is still fresh for the next try.
                failed++;
                foreach (var stale in group.Where(r => r.RequestedAtUtc < now - GiveUpAfter))
                {
                    stale.Handle(AccountEmailOutcomes.GaveUp, now);
                }
            }
            else
            {
                if (outcome == AccountEmailOutcomes.Sent)
                {
                    sent++;
                }
                else
                {
                    skipped++;
                }

                foreach (var request in group)
                {
                    request.Handle(outcome, now);
                }
            }

            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        LogHandled(logger, sent, skipped, failed);

        if (failed > 0 && await db.EmailRequests.AnyAsync(r => r.HandledAtUtc == null, cancellation).ConfigureAwait(false))
        {
            // Failing the job is what makes the job server try again later.
            throw new EmailNotSentException();
        }
    }

    /// <summary>The outcome, or null when the email could not be handed over.</summary>
    private async Task<string?> HandleAsync(Guid accountId, string kind, CancellationToken cancellation)
    {
        var account = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.Email, a.State })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (account is null || account.State != AccountStates.Active)
        {
            return AccountEmailOutcomes.SkippedState;
        }

        var language = await AccountLanguage.OfAsync(db, accountId, cancellation).ConfigureAwait(false);

        if (kind == AccountEmailKinds.PasswordChanged)
        {
            try
            {
                await mail.SendAsync(
                        renderer.Render(AccountEmails.PasswordChanged(account.Email, language, clock.GetUtcNow())), cancellation)
                    .ConfigureAwait(false);
                return AccountEmailOutcomes.Sent;
            }
            catch (Exception notLeft) when (notLeft is EmailNotSentException or EmailCompositionException)
            {
                return null;
            }
        }

        var minutes = options.Value.Auth.ResetLinkMinutes;
        var result = await links.SendAsync(
            accountId,
            PasswordLinkKinds.Reset,
            enforceResetCaps: true,
            address => renderer.Render(AccountEmails.PasswordReset(account.Email, language, address, minutes)),
            cancellation).ConfigureAwait(false);

        return result switch
        {
            LinkSendResult.Sent => AccountEmailOutcomes.Sent,
            LinkSendResult.SkippedLimit => AccountEmailOutcomes.SkippedLimit,
            _ => null,
        };
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Account emails: {sent} sent, {skipped} skipped, {failed} could not be sent")]
    private static partial void LogHandled(ILogger logger, int sent, int skipped, int failed);
}
