using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// Handing an account to its owner in the three ways of SH-044, and handing it over again (spec
/// 004, FR-030 to FR-034, FR-044). Works for the established clinic, under isolation. The email
/// leaves inside the call, because the caller is a signed-in person who must be told when it did
/// not; no email ever contains a password.
/// </summary>
internal sealed class AccountHandover(
    IdentityDbContext db,
    IClinicContext clinic,
    IStaffAccounts accounts,
    ICurrentStaff staff,
    PasswordLinks links,
    PasswordSetter setter,
    IEmailSender mail,
    IClinicProfile profile,
    TimeProvider clock,
    IOptions<ScsOptions> options) : IAccountHandover
{
    private AuthOptions Auth => options.Value.Auth;

    public async Task<AccountHandoverResult> CreateWithLinkAsync(string email, CancellationToken cancellation)
    {
        var id = await accounts.CreateAsync(email, AccountStates.Invited, password: null, cancellation).ConfigureAwait(false);
        var sent = await SendInvitationAsync(id, EmailAddress.Normalize(email), cancellation).ConfigureAwait(false);
        return new AccountHandoverResult(id, sent, GeneratedPassword: null);
    }

    public async Task<AccountHandoverResult> CreateWithGeneratedPasswordAsync(string email, CancellationToken cancellation)
    {
        var password = PasswordGenerator.Generate(Auth.GeneratedPasswordLength);
        var (id, sent) = await CreateWithTemporaryPasswordAsync(email, password, cancellation).ConfigureAwait(false);
        return new AccountHandoverResult(id, sent, password);
    }

    public async Task<AccountHandoverResult> CreateWithTypedPasswordAsync(
        string email, string password, CancellationToken cancellation)
    {
        var (id, sent) = await CreateWithTemporaryPasswordAsync(email, password, cancellation).ConfigureAwait(false);
        return new AccountHandoverResult(id, sent, GeneratedPassword: null);
    }

    public async Task<AccountHandoverResult> ResendInvitationAsync(Guid accountId, CancellationToken cancellation)
    {
        var account = await FindAsync(accountId, cancellation).ConfigureAwait(false);
        if (account.State != AccountStates.Invited)
        {
            // Only an account that was never activated has an invitation to send again.
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var sent = await SendInvitationAsync(accountId, account.Email, cancellation).ConfigureAwait(false);
        return new AccountHandoverResult(accountId, sent, GeneratedPassword: null);
    }

    public async Task<AccountHandoverResult> ReissueAsync(
        Guid accountId, string method, string? password, CancellationToken cancellation)
    {
        if (method is not (HandoverMethods.Link or HandoverMethods.Generated or HandoverMethods.Typed))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        var account = await FindAsync(accountId, cancellation).ConfigureAwait(false);
        if (account.State == AccountStates.Disabled)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.AccountDisabled);
        }

        var actor = staff.IsSignedIn ? staff.AccountId : (Guid?)null;

        if (method == HandoverMethods.Link)
        {
            // The password, if there is one, keeps working until the link is used. The link is
            // not held back by the caps on what a person may ask for, but it counts toward them.
            var sent = account.State == AccountStates.Invited
                ? await SendInvitationAsync(accountId, account.Email, cancellation).ConfigureAwait(false)
                : await links.SendAsync(
                    accountId,
                    PasswordLinkKinds.Reset,
                    enforceResetCaps: false,
                    address => AccountEmails.ResetLink(account.Email, address, Auth.ResetLinkMinutes),
                    cancellation).ConfigureAwait(false) == LinkSendResult.Sent;

            db.Events.Add(SignInEvent.Record(
                accountId, SignInEventKinds.AccessReissued, clock.GetUtcNow(), RequestOrigin.None.TraceId,
                ipAddress: null, userAgent: null, detail: method, actorAccountId: actor));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            return new AccountHandoverResult(accountId, sent, GeneratedPassword: null);
        }

        var generated = method == HandoverMethods.Generated ? PasswordGenerator.Generate(Auth.GeneratedPasswordLength) : null;

        // Never compared with the current password: whoever re-issues must not be able to test
        // guesses of a colleague's password.
        var result = await setter.SetAsync(
            new SetPassword(accountId, generated ?? password, SignInEventKinds.AccessReissued, RequestOrigin.None)
            {
                Temporary = true,
                EventDetail = method,
                ActorAccountId = actor,
            },
            cancellation).ConfigureAwait(false);

        if (result != SetPasswordResult.Done)
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var told = await TrySendAsync(AccountEmails.AccessReset(account.Email, links.SignInAddress), cancellation)
            .ConfigureAwait(false);
        return new AccountHandoverResult(accountId, told, generated);
    }

    private async Task<(Guid Id, bool EmailSent)> CreateWithTemporaryPasswordAsync(
        string email, string password, CancellationToken cancellation)
    {
        Guid id;
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            id = await accounts.CreateAsync(email, AccountStates.Active, password, cancellation).ConfigureAwait(false);

            var until = clock.GetUtcNow().AddHours(Auth.TempPasswordHours);
            await db.Accounts.Where(a => a.Id == id)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(a => a.MustChangePassword, true)
                        .SetProperty(a => a.TemporaryPasswordExpiresAtUtc, until),
                    cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);
        var sent = await TrySendAsync(
            AccountEmails.AccountCreated(EmailAddress.Normalize(email), own.NameAr, own.NameEn, links.SignInAddress),
            cancellation).ConfigureAwait(false);

        return (id, sent);
    }

    private async Task<bool> SendInvitationAsync(Guid accountId, string email, CancellationToken cancellation)
    {
        var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);
        var result = await links.SendAsync(
            accountId,
            PasswordLinkKinds.NewAccount,
            enforceResetCaps: false,
            address => AccountEmails.Invitation(
                email, address, own.NameAr, own.NameEn, links.SignInAddress, Auth.InviteLinkHours),
            cancellation).ConfigureAwait(false);

        return result == LinkSendResult.Sent;
    }

    private async Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellation)
    {
        try
        {
            await mail.SendAsync(message, cancellation).ConfigureAwait(false);
            return true;
        }
        catch (EmailNotSentException)
        {
            return false;
        }
    }

    private async Task<AccountSummary> FindAsync(Guid accountId, CancellationToken cancellation)
    {
        clinic.Require();
        return await db.Accounts.AsNoTracking()
                   .Where(a => a.Id == accountId)
                   .Select(a => new AccountSummary(a.Email, a.State))
                   .SingleOrDefaultAsync(cancellation)
                   .ConfigureAwait(false)
               ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
    }

    private sealed record AccountSummary(string Email, string State)
    {
        public override string ToString() => nameof(AccountSummary);
    }
}
