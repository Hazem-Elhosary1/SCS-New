using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Operators.Infrastructure;

/// <summary>What a form of an operator sends. Each field is checked here, whatever the console checked.</summary>
internal sealed record OperatorInput(
    [property: PersonalData] string? FullName,
    [property: PersonalData] string? Email,
    Guid? RoleId,
    [property: PersonalData] string? Mobile,
    string? Department,
    string? JobTitle,
    string? Language,
    bool SendInvitation,
    string? Note,
    string? Version)
{
    public override string ToString() => nameof(OperatorInput);
}

/// <summary>
/// What an operator who manages operators does to an account (spec 011, User Stories 2, 4 and
/// 5): inviting, editing, suspending, reactivating, sending a password link, unlocking. Each
/// change and its entry in the platform's trail are one transaction. No method returns a link
/// or a token, and none changes an email or deletes an account.
/// </summary>
internal sealed partial class OperatorManagement(
    OperatorsDbContext db,
    IOperatorAccess access,
    OperatorSessions sessions,
    OperatorPasswords passwords,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<OperatorManagement> logger)
{
    public const int NoteMaxLength = 500;

    private const string Required = PasswordRules.Required;
    private const string Invalid = "INVALID";
    private const string TooLong = PasswordRules.TooLong;

    /// <summary>At most one link for an account in this long, whoever asks (FR-044, ق-61).</summary>
    private static readonly TimeSpan LinkGap = TimeSpan.FromMinutes(1);

    // ---------------------------------------------------------------- inviting

    /// <summary>
    /// A new account in the state "invited", with no password. The invitation is owed when
    /// asked for, and the platform sends it within a minute (spec 010's job).
    /// </summary>
    public async Task<Guid> InviteAsync(OperatorInput input, Guid callerId, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = Check(input, adding: true);
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var email = OperatorEmail.Normalize(input.Email);
        if (await db.Operators.AsNoTracking().AnyAsync(o => o.Email == email, cancellation).ConfigureAwait(false))
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsEmailTaken);
        }

        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);
        var now = clock.GetUtcNow();

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await SuperAdminGuard.LockRoleHoldersAsync(db, cancellation).ConfigureAwait(false);
            var role = await GrantableRoleAsync(input.RoleId!.Value, granter, cancellation).ConfigureAwait(false);
            var inviter = await db.Operators.AsNoTracking().Where(o => o.Id == callerId).Select(o => o.FullName)
                .SingleAsync(cancellation).ConfigureAwait(false);

            var account = Operator.Invite(input.FullName!, email, role.Id, now);
            account.Describe(input.FullName!, input.Mobile, Clean(input.Department), input.JobTitle, input.Language!);
            account.InvitedBy(callerId, inviter);
            if (input.SendInvitation)
            {
                account.OweInvitation(now);
            }

            db.Operators.Add(account);
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateException taken) when (taken.IsUniqueViolation())
            {
                // Two invitations of one address at the same moment: the second is told what the first would have been.
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsEmailTaken);
            }

            var facts = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sent"] = input.SendInvitation };
            NameRole(facts, role);
            await db.RecordForPlatformAsync(new AuditRecord(AuditActions.OperatorInvited, Target(account), facts), cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return account.Id;
        }
    }

    // ---------------------------------------------------------------- editing

    /// <summary>
    /// Changes an account's details and, within the guards, its role. A changed role ends every
    /// session of the account in the same transaction (FR-039); nothing else does.
    /// </summary>
    public async Task EditAsync(Guid id, OperatorInput input, Guid callerId, OpsOrigin origin, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = Check(input, adding: false);
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var account = await FindAsync(id, cancellation).ConfigureAwait(false);
            if (Convert.ToBase64String(account.RowVersion) != input.Version)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsChangedElsewhere);
            }

            Role? before = null;
            Role? after = null;
            if (input.RoleId!.Value != account.RoleId)
            {
                if (RoleGrants.IsOwnAccount(granter, id))
                {
                    throw new ScsProblemException(StatusCodes.Status403Forbidden, ErrorCodes.OpsOwnRole);
                }

                await SuperAdminGuard.LockRoleHoldersAsync(db, cancellation).ConfigureAwait(false);
                after = await GrantableRoleAsync(input.RoleId.Value, granter, cancellation).ConfigureAwait(false);
                before = await db.Roles.AsNoTracking().SingleAsync(r => r.Id == account.RoleId, cancellation).ConfigureAwait(false);

                // Only an active super admin counts toward "one always remains".
                if (before.IsSuperAdmin && account.State == OperatorStates.Active)
                {
                    await SuperAdminGuard.EnsureAnotherRemainsAsync(db, id, cancellation).ConfigureAwait(false);
                }
            }

            var was = new { account.FullName, account.Mobile, account.Department, account.JobTitle, account.Language };
            account.Describe(input.FullName!, input.Mobile, Clean(input.Department), input.JobTitle, input.Language!);
            if (after is not null)
            {
                account.ChangeRole(after.Id);
            }

            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsChangedElsewhere);
            }

            var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            var facts = note is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["note"] = note };

            List<AuditChange> changes =
            [
                AuditChange.Of("fullName", was.FullName, account.FullName),
                AuditChange.Of("mobile", was.Mobile, account.Mobile),
                AuditChange.Of("department", was.Department, account.Department),
                AuditChange.Of("jobTitle", was.JobTitle, account.JobTitle),
                AuditChange.Of("language", was.Language, account.Language),
            ];
            changes.RemoveAll(change => change.IsEmpty);

            var target = Target(account);
            if (changes.Count > 0)
            {
                await db.RecordForPlatformAsync(new AuditRecord(AuditActions.OperatorEdited, target, facts, changes), cancellation)
                    .ConfigureAwait(false);
            }

            if (before is not null && after is not null)
            {
                await db.RecordForPlatformAsync(
                        new AuditRecord(
                            AuditActions.OperatorRoleChanged, target, facts,
                            [
                                new AuditChange(
                                    "role",
                                    before.IsSystem ? null : before.Name,
                                    after.IsSystem ? null : after.Name,
                                    BeforeKey: before.SystemKey,
                                    AfterKey: after.SystemKey),
                            ]),
                        cancellation)
                    .ConfigureAwait(false);

                await sessions.EndAllAsync(id, SessionEndings.RoleChanged, origin, cancellation).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- the account's four actions

    public async Task SuspendAsync(Guid id, string? reason, Guid callerId, OpsOrigin origin, CancellationToken cancellation)
    {
        var why = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (why is { Length: > NoteMaxLength })
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["reason"] = [TooLong] });
        }

        if (id == callerId)
        {
            throw new ScsProblemException(StatusCodes.Status403Forbidden, ErrorCodes.OpsOwnAccount);
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var account = await FindAsync(id, cancellation).ConfigureAwait(false);
            if (account.State != OperatorStates.Active)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsStateChanged);
            }

            if (account.RoleId == OperatorPermissions.SystemRoles.IdOf(OperatorPermissions.SystemRoles.SuperAdmin))
            {
                await SuperAdminGuard.EnsureAnotherRemainsAsync(db, id, cancellation).ConfigureAwait(false);
            }

            account.Suspend(clock.GetUtcNow());
            await SaveAsync(cancellation).ConfigureAwait(false);

            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.OperatorSuspended, Target(account),
                        why is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = why }),
                    cancellation)
                .ConfigureAwait(false);
            await sessions.EndAllAsync(id, SessionEndings.Suspended, origin, cancellation).ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellation) => ChangeAsync(
        id,
        account => account.State == OperatorStates.Suspended,
        (account, _) => account.Reactivate(),
        AuditActions.OperatorReactivated,
        cancellation);

    public Task UnlockAsync(Guid id, CancellationToken cancellation) => ChangeAsync(
        id,
        account => account.State == OperatorStates.Active && account.IsLocked(clock.GetUtcNow()),
        (account, now) => account.Unlock(now),
        AuditActions.OperatorUnlocked,
        cancellation);

    /// <summary>
    /// Sends the account's holder a way to choose a password: a reset link to an active
    /// account, the invitation anew to an invited one. Nothing is kept of a link that could
    /// not leave, and the link is returned to nobody.
    /// </summary>
    public async Task SendLinkAsync(Guid id, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var ops = options.Value.Ops;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var account = await FindAsync(id, cancellation).ConfigureAwait(false);
            if (account.State == OperatorStates.Suspended)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsStateChanged);
            }

            var recently = account.InvitationOwedSinceUtc > now - LinkGap
                           || await db.Links.AsNoTracking()
                               .AnyAsync(l => l.OperatorId == id && l.CreatedAtUtc > now - LinkGap, cancellation)
                               .ConfigureAwait(false);
            if (recently)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsLinkTooSoon);
            }

            var first = account.State == OperatorStates.Invited;
            EmailContent content;
            if (first)
            {
                var link = await passwords
                    .IssueAsync(id, PasswordLinkKinds.First, TimeSpan.FromHours(ops.FirstLinkHours), now, cancellation)
                    .ConfigureAwait(false);
                account.InvitationSent();
                var role = await db.Roles.AsNoTracking().Where(r => r.Id == account.RoleId)
                    .Select(r => new { r.Name, r.SystemKey }).SingleAsync(cancellation).ConfigureAwait(false);
                content = OperatorEmails.FirstPassword(
                    account.Email, account.Language, link, ops.FirstLinkHours, account.InvitedByName,
                    RoleRef.NameIn(role.SystemKey, role.Name, account.Language));
            }
            else
            {
                var link = await passwords
                    .IssueAsync(id, PasswordLinkKinds.Reset, TimeSpan.FromMinutes(ops.ResetLinkMinutes), now, cancellation)
                    .ConfigureAwait(false);
                content = OperatorEmails.ResetLink(account.Email, account.Language, link, ops.ResetLinkMinutes);
            }

            await SaveAsync(cancellation).ConfigureAwait(false);

            try
            {
                await mail.SendAsync(renderer.Render(content), cancellation).ConfigureAwait(false);
            }
            catch (Exception notLeft) when (notLeft is EmailNotSentException or EmailCompositionException)
            {
                await transaction.RollbackAsync(cancellation).ConfigureAwait(false);
                LogLinkNotSent(logger, id);
                throw new ScsProblemException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.OpsLinkNotSent);
            }

            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.OperatorLinkSent, Target(account),
                        new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = first ? "invitation" : "reset" }),
                    cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>A change that needs the account in one state, and is recorded with nothing more.</summary>
    private async Task ChangeAsync(
        Guid id, Func<Operator, bool> mayBe, Action<Operator, DateTimeOffset> change, string action, CancellationToken cancellation)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var account = await FindAsync(id, cancellation).ConfigureAwait(false);
            if (!mayBe(account))
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsStateChanged);
            }

            change(account, clock.GetUtcNow());
            await SaveAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(new AuditRecord(action, Target(account)), cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Saves; an account that someone else changed in the same moment is "not in that state any more".</summary>
    private async Task SaveAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsStateChanged);
        }
    }

    private async Task<Operator> FindAsync(Guid id, CancellationToken cancellation) =>
        await db.Operators.SingleOrDefaultAsync(o => o.Id == id, cancellation).ConfigureAwait(false)
        ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private async Task<Granter> GranterAsync(Guid callerId, CancellationToken cancellation)
    {
        var grant = await access.GetAsync(cancellation).ConfigureAwait(false);
        return new Granter(callerId, grant.RoleId, grant.IsSuperAdmin, grant.Codes);
    }

    /// <summary>A role that exists, is not deleted, and that this person may give (FR-030).</summary>
    private async Task<Role> GrantableRoleAsync(Guid roleId, Granter granter, CancellationToken cancellation)
    {
        var role = await db.Roles.AsNoTracking().Include(r => r.Permissions)
                       .SingleOrDefaultAsync(r => r.Id == roleId && r.DeletedAtUtc == null, cancellation)
                       .ConfigureAwait(false)
                   ?? throw ScsProblemException.Validation(
                       new Dictionary<string, string[]>(StringComparer.Ordinal) { ["roleId"] = [Invalid] });

        return RoleGrants.WhyNotGrantable(granter, role.SystemKey, role.Codes) is null
            ? role
            : throw new ScsProblemException(StatusCodes.Status403Forbidden, ErrorCodes.OpsRoleNotGrantable);
    }

    /// <summary>The limits of FR-028, each named by its field.</summary>
    private static Dictionary<string, string[]> Check(OperatorInput input, bool adding)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var name = (input.FullName ?? "").Trim();
        if (name.Length == 0)
        {
            errors["fullName"] = [Required];
        }
        else if (name.Length > Operator.NameMaxLength)
        {
            errors["fullName"] = [TooLong];
        }

        if (adding)
        {
            var email = (input.Email ?? "").Trim();
            if (email.Length == 0)
            {
                errors["email"] = [Required];
            }
            else if (email.Length > Operator.EmailMaxLength || !OperatorEmail.LooksValid(email))
            {
                errors["email"] = [Invalid];
            }
        }
        else if (string.IsNullOrEmpty(input.Version))
        {
            errors["version"] = [Required];
        }

        if (input.RoleId is null || input.RoleId == Guid.Empty)
        {
            errors["roleId"] = [Required];
        }

        if ((input.Mobile ?? "").Trim().Length > Operator.MobileMaxLength)
        {
            errors["mobile"] = [TooLong];
        }

        if (Clean(input.Department) is { } department && !OperatorDepartments.All.Contains(department))
        {
            errors["department"] = [Invalid];
        }

        if ((input.JobTitle ?? "").Trim().Length > Operator.JobTitleMaxLength)
        {
            errors["jobTitle"] = [TooLong];
        }

        if (input.Language is null)
        {
            errors["language"] = [Required];
        }
        else if (input.Language is not ("ar" or "en"))
        {
            errors["language"] = [Invalid];
        }

        if (!adding && (input.Note ?? "").Trim().Length > NoteMaxLength)
        {
            errors["note"] = [TooLong];
        }

        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AuditTarget Target(Operator account) => new(AuditTarget.Operator, account.Id, account.FullName);

    /// <summary>A custom role by the name it has now; one of the platform's own by its key, to be named in the reader's language.</summary>
    private static void NameRole(Dictionary<string, object?> facts, Role role)
    {
        if (role.SystemKey is { } key)
        {
            facts["systemRole"] = key;
        }
        else
        {
            facts["role"] = role.Name;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The password link of operator {operatorId} could not be sent; nothing was kept of it")]
    private static partial void LogLinkNotSent(ILogger logger, Guid operatorId);
}
