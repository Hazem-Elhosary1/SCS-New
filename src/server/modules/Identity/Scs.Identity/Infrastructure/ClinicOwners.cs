using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Clinics;
using Scs.SharedKernel.Wording;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The clinic's owner for the operators console (spec 015; contracts/ops-clinics-api.md,
/// "Identity"). Works for the established clinic, under isolation, except the email check (the
/// sign-in lookup) and the operators' list (the owners' crossing).
/// </summary>
internal sealed partial class ClinicOwners(
    IdentityDbContext db,
    IClinicContext clinic,
    IStaffAccounts accounts,
    IClinicRoles roles,
    IAccountHandover handover,
    PasswordLinks links,
    SessionService sessions,
    AccountLookup lookup,
    OperatorClinicOwners crossing,
    IClinicProfile profile,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    ILogger<ClinicOwners> logger) : IClinicOwners
{
    /// <summary>One invitation email a minute for an account (BRD Ops §3-3).</summary>
    public static readonly TimeSpan InvitationGap = TimeSpan.FromSeconds(60);

    public async Task<bool> EmailIsFreeAsync(string email, CancellationToken cancellation) =>
        await lookup.FindAsync(EmailAddress.Normalize(email), cancellation).ConfigureAwait(false) is null;

    public async Task<Guid> CreateAsync(string email, string fullName, string mobile, string language, CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var id = await accounts.CreateAsync(email, AccountStates.Invited, password: null, cancellation).ConfigureAwait(false);

            var account = await db.Accounts.SingleAsync(a => a.Id == id, cancellation).ConfigureAwait(false);
            account.SetOwnerDetails(fullName, mobile);
            var now = clock.GetUtcNow();
            var preference = new StaffPreference(id);
            preference.Choose(Words.Language(language), sidebarFolded: null, dismissedBanner: null, DateOnly.FromDateTime(now.UtcDateTime), now);
            db.Preferences.Add(preference);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            var owner = await OwnerRole.Ids(db.Roles.AsNoTracking()).SingleOrDefaultAsync(cancellation).ConfigureAwait(false);
            if (owner == Guid.Empty)
            {
                throw new InvalidOperationException($"The clinic {clinicId} has no owner role.");
            }

            await roles.AssignAsync(id, owner, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return id;
        }
    }

    public async Task DiscardAsync(Guid accountId, CancellationToken cancellation)
    {
        clinic.Require();
        if (await db.PasswordLinks.AnyAsync(l => l.AccountId == accountId, cancellation).ConfigureAwait(false))
        {
            return;
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.Preferences.Where(p => p.AccountId == accountId).ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
            await db.Accounts.Where(a => a.Id == accountId && a.State == AccountStates.Invited)
                .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task<ClinicOwner?> FindAsync(CancellationToken cancellation)
    {
        var account = await OwnerQuery().AsNoTracking().FirstOrDefaultAsync(cancellation).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        OwnerInvitation? invitation = null;
        if (account.State == AccountStates.Invited)
        {
            var latest = await db.PasswordLinks.AsNoTracking()
                .Where(l => l.AccountId == account.Id && l.Kind == PasswordLinkKinds.NewAccount)
                .OrderByDescending(l => l.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellation).ConfigureAwait(false);
            // Lapsed only when the latest link ran out; one replaced without a successor (the
            // owner's email changed) leaves no invitation to speak of (BRD Ops §4-7).
            invitation = latest is null || latest.ReplacedAtUtc is not null
                ? new OwnerInvitation(OwnerInvitationStates.None, null)
                : latest.IsLive(clock.GetUtcNow())
                    ? new OwnerInvitation(OwnerInvitationStates.Live, latest.ExpiresAtUtc)
                    : new OwnerInvitation(OwnerInvitationStates.Lapsed, latest.ExpiresAtUtc);
        }

        return new ClinicOwner(
            account.Id, account.Email, account.FullName, account.Mobile, account.State, account.LastSignInAtUtc, invitation);
    }

    public void CheckDetails(string? fullName, string? email, string? mobile, Dictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _ = Details(fullName, email, mobile, errors);
    }

    /// <summary>The owner's fields as they would be kept, and each refusal added to <paramref name="errors"/>.</summary>
    private static (string Name, EmailAddress? Address, string Phone) Details(
        string? fullName, string? email, string? mobile, Dictionary<string, string[]> errors)
    {
        var name = (fullName ?? "").Trim();
        if (name.Length == 0)
        {
            errors["ownerName"] = [ClinicFields.Required];
        }
        else if (name.Length > StaffAccount.FullNameMaxLength)
        {
            errors["ownerName"] = [ClinicFields.TooLong];
        }

        var (address, emailError) = EmailAddress.TryCreate(email);
        if (emailError is not null)
        {
            errors["email"] = [emailError];
        }

        var phone = "";
        if (string.IsNullOrWhiteSpace(mobile))
        {
            errors["mobile"] = [ClinicFields.Required];
        }
        else if (!ClinicFields.TryMobile(mobile, out phone))
        {
            errors["mobile"] = [ClinicFields.MobileCode];
        }

        return (name, address, phone);
    }

    public async Task<bool> UpdateAsync(
        Guid clinicId, string clinicNameAr, string? fullName, string? email, string? mobile, string? note, string noticeLanguage,
        CancellationToken cancellation)
    {
        clinic.Require();
        var account = await OwnerQuery().FirstOrDefaultAsync(cancellation).ConfigureAwait(false)
                      ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "The clinic has no owner.");

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (name, address, phone) = Details(fullName, email, mobile, errors);
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var changes = new List<AuditChange>
        {
            AuditChange.Of("ownerName", account.FullName, name),
            AuditChange.Of("ownerEmail", account.Email, address!.Value),
            AuditChange.Of("ownerMobile", account.Mobile, phone),
        };
        if (changes.All(c => c.IsEmpty))
        {
            return false;
        }

        var oldEmail = account.Email;
        var emailChanged = !string.Equals(oldEmail, address.Value, StringComparison.Ordinal);
        var wasActive = account.State == AccountStates.Active;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            account.SetOwnerDetails(name, phone);
            if (emailChanged)
            {
                account.ChangeEmail(address);
            }

            var record = new AuditRecord(
                AuditActions.ClinicOwnerUpdated,
                new AuditTarget(AuditTarget.Clinic, clinicId, clinicNameAr),
                Facts: new Dictionary<string, object?>(StringComparer.Ordinal) { ["note"] = (note ?? "").Trim() },
                Changes: changes);
            db.Record(record);
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateException failure) when (failure.IsUniqueViolation())
            {
                throw ScsProblemException.Validation(
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { ["email"] = [ClinicFields.Taken] });
            }

            await db.RecordForPlatformAsync(record, cancellation).ConfigureAwait(false);

            if (emailChanged && wasActive)
            {
                // The old address no longer signs in: every session it holds ends, and so does a
                // sign-in it had started or a reset link sent to it (FR-021a).
                await sessions.EndForAccountCoreAsync(account.Id, SessionEndReasons.EmailChanged, RequestOrigin.None, cancellation)
                    .ConfigureAwait(false);
                var now = clock.GetUtcNow();
                await db.Challenges
                    .Where(c => c.AccountId == account.Id && c.ConsumedAtUtc == null && c.ReplacedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(c => c.ReplacedAtUtc, now), cancellation)
                    .ConfigureAwait(false);
                await links.ReplaceAllAsync(account.Id, cancellation).ConfigureAwait(false);
            }
            else if (emailChanged)
            {
                // An invitation went to the old address: it stops working (FR-021a).
                await links.ReplaceAllAsync(account.Id, cancellation).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        if (emailChanged && wasActive)
        {
            await TellOldAddressAsync(account.Id, oldEmail, address.Value, noticeLanguage, cancellation).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<DateTimeOffset> ResendInvitationAsync(Guid clinicId, string clinicNameAr, CancellationToken cancellation)
    {
        clinic.Require();
        var owner = await OwnerQuery().AsNoTracking().Select(a => new { a.Id, a.State }).FirstOrDefaultAsync(cancellation).ConfigureAwait(false)
                    ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "The clinic has no owner.");

        if (owner.State != AccountStates.Invited)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.InvitationAlreadyAccepted);
        }

        var since = clock.GetUtcNow() - InvitationGap;
        if (await db.PasswordLinks.AnyAsync(
                // A link replaced since (the owner's email changed) went to an address no longer kept.
                l => l.AccountId == owner.Id && l.Kind == PasswordLinkKinds.NewAccount && l.CreatedAtUtc > since && l.ReplacedAtUtc == null,
                cancellation)
            .ConfigureAwait(false))
        {
            throw new ScsProblemException(StatusCodes.Status429TooManyRequests, ErrorCodes.ResetEmailCooldown);
        }

        var result = await handover.ResendInvitationAsync(owner.Id, cancellation).ConfigureAwait(false);
        if (!result.EmailSent)
        {
            throw new ScsProblemException(StatusCodes.Status502BadGateway, ErrorCodes.AccountLinkNotSent);
        }

        var expiresAt = await db.PasswordLinks.AsNoTracking()
            .Where(l => l.AccountId == owner.Id && l.Kind == PasswordLinkKinds.NewAccount)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => l.ExpiresAtUtc)
            .FirstAsync(cancellation).ConfigureAwait(false);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var record = new AuditRecord(AuditActions.ClinicInvitationResent, new AuditTarget(AuditTarget.Clinic, clinicId, clinicNameAr));
            db.Record(record);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(record, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return expiresAt;
    }

    public Task<IReadOnlyList<OwnerContact>> ListForOperatorsAsync(CancellationToken cancellation) => crossing.ListAsync(cancellation);

    /// <summary>The clinic's owner: the earliest account holding the role that holds everything.</summary>
    private IQueryable<StaffAccount> OwnerQuery()
    {
        var owners = OwnerRole.Ids(db.Roles);
        return from account in db.Accounts
               where account.RoleId != null && owners.Contains(account.RoleId.Value)
               orderby account.CreatedAtUtc
               select account;
    }

    /// <summary>The notice to the old address, in the clinic's language (spec 015, User Story 4 scenario 6).</summary>
    private async Task TellOldAddressAsync(Guid accountId, string oldEmail, string newEmail, string language, CancellationToken cancellation)
    {
        try
        {
            var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);
            await mail.SendAsync(
                renderer.Render(AccountEmails.EmailChanged(oldEmail, language, newEmail, own.NameAr, own.NameEn)), cancellation)
                .ConfigureAwait(false);
        }
        catch (Exception notLeft) when (notLeft is EmailNotSentException or EmailCompositionException)
        {
            // The change stands; the notice is a courtesy (FR-021a). Logged without the address.
            LogNoticeNotSent(logger, accountId, notLeft.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The email-changed notice of account {accountId} was not sent ({failure})")]
    private static partial void LogNoticeNotSent(ILogger logger, Guid accountId, string failure);
}
