namespace Scs.Identity.Contracts;

/// <summary>
/// Who is asking, on a request made with a staff session (spec 003). What they may do is asked
/// of <c>IStaffAccess</c> (spec 005), never worked out from a role's name. Using it on a request with no session is refused with
/// <c>UNAUTHENTICATED</c>.
/// </summary>
public interface ICurrentStaff
{
    bool IsSignedIn { get; }

    Guid AccountId { get; }

    Guid SessionId { get; }

    /// <summary>The same value as the clinic context of the request.</summary>
    Guid ClinicId { get; }
}

/// <summary>The states an account can be created in or moved to.</summary>
public static class StaffAccountStates
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Disabled = "disabled";
}

/// <summary>
/// Operations on the accounts of the established clinic, for the specs that own the screens
/// (004 password and activation, 015 clinic creation, 026 users). Every operation runs under
/// clinic isolation: an account of another clinic is "not found". None has an endpoint here.
/// </summary>
public interface IStaffAccounts
{
    /// <summary>
    /// Creates an account for the established clinic. <c>invited</c> takes no password;
    /// <c>active</c> and <c>disabled</c> need one that meets the password rules. Refused with
    /// field codes on <c>email</c> (<c>REQUIRED</c>, <c>TOO_LONG</c>, <c>INVALID_FORMAT</c>,
    /// <c>TAKEN</c>) and <c>password</c> (<c>REQUIRED</c>, <c>TOO_SHORT</c>, <c>TOO_LONG</c>,
    /// <c>NO_UPPER_LOWER</c>, <c>NO_DIGIT</c>, <c>NO_SYMBOL</c>). <c>TAKEN</c> is the answer whichever
    /// clinic already holds the email.
    /// </summary>
    Task<Guid> CreateAsync(string email, string state, string? password, CancellationToken cancellation);

    /// <summary>
    /// Moves an account between <c>active</c> and <c>disabled</c>. Disabling ends the account's
    /// sessions in the same transaction (SH-045).
    /// </summary>
    Task SetStateAsync(Guid accountId, string state, CancellationToken cancellation);

    /// <summary>Lifts a lock at once (SH-049).</summary>
    Task UnlockAsync(Guid accountId, CancellationToken cancellation);

    /// <summary>The same, for the password-reset path of spec 004.</summary>
    Task ClearLockAsync(Guid accountId, CancellationToken cancellation);
}

/// <summary>Why sessions are being ended by another spec's operation.</summary>
public static class SessionEndings
{
    public const string EndedForAccount = "ended-for-account";
    public const string PasswordReset = "password-reset";
    public const string RoleChanged = "role-changed";
    public const string EndedForClinic = "ended-for-clinic";
    public const string ClinicSuspended = "clinic-suspended";
}

/// <summary>
/// Ending sessions of the established clinic. A session ended here is refused on its very next
/// request. Spec 016 must call <see cref="EndForClinicAsync"/> when it suspends or cancels a
/// clinic; without that call the clinic's sessions stop only at their next renewal.
/// </summary>
public interface IStaffSessions
{
    /// <summary>Ends every live session of the account. Returns how many were ended.</summary>
    Task<int> EndForAccountAsync(Guid accountId, string reason, CancellationToken cancellation);

    /// <summary>Ends every live session of the clinic, except one if given. Returns how many were ended.</summary>
    Task<int> EndForClinicAsync(Guid? exceptSessionId, string reason, CancellationToken cancellation);
}

/// <summary>How an account reaches its owner (SH-044).</summary>
public static class HandoverMethods
{
    /// <summary>A link by email; the owner chooses the password.</summary>
    public const string Link = "link";

    /// <summary>A password the system generates, shown once to whoever asked.</summary>
    public const string Generated = "generated";

    /// <summary>A password typed by whoever asked.</summary>
    public const string Typed = "typed";
}

/// <summary>What handing an account over ended in.</summary>
/// <param name="EmailSent">
/// False when the email could not be handed to the mail server. The account is as the operation
/// left it; for a link, nothing usable was left behind. Show it, and offer to send again.
/// </param>
/// <param name="GeneratedPassword">
/// Only for <see cref="HandoverMethods.Generated"/>, and only this once: it cannot be read again
/// by anyone. Show it once; never store it, log it or put it in an audit entry.
/// </param>
public sealed record AccountHandoverResult(Guid AccountId, bool EmailSent, string? GeneratedPassword)
{
    /// <summary>The type and id only: this may carry a password.</summary>
    public override string ToString() => $"{nameof(AccountHandoverResult)}#{AccountId}";
}

/// <summary>
/// Handing an account to its owner, and handing it over again (spec 004), for the specs that own
/// the screens (015 clinic creation, 026 users). Every operation runs for the established clinic,
/// under isolation: an account of another clinic is "not found". None has an endpoint here, and
/// whether the caller may do it is the caller's business (spec 005). No email these send ever
/// contains a password. Passwords set here are temporary: their owner must replace them at
/// first sign-in, and they stop working after 72 hours if nobody does.
/// </summary>
public interface IAccountHandover
{
    /// <summary>Creates the account <c>invited</c> and emails a link that activates it (72 hours).</summary>
    Task<AccountHandoverResult> CreateWithLinkAsync(string email, CancellationToken cancellation);

    /// <summary>Creates the account <c>active</c> with a generated temporary password, returned once.</summary>
    Task<AccountHandoverResult> CreateWithGeneratedPasswordAsync(string email, CancellationToken cancellation);

    /// <summary>Creates the account <c>active</c> with the caller's temporary password, which must meet the rules.</summary>
    Task<AccountHandoverResult> CreateWithTypedPasswordAsync(string email, string password, CancellationToken cancellation);

    /// <summary>For an account that was never activated: a new link; the earlier one stops working.</summary>
    Task<AccountHandoverResult> ResendInvitationAsync(Guid accountId, CancellationToken cancellation);

    /// <summary>
    /// Hands an existing account over again. <see cref="HandoverMethods.Link"/> emails a reset
    /// link (an invitation, if the account was never activated) and leaves the password working
    /// until the link is used. The two password methods replace the password at once, end the
    /// account's sessions, cancel its links and lift a lock. A disabled account is refused with
    /// <c>ACCOUNT_DISABLED</c>. The new password is never compared with the current one.
    /// </summary>
    Task<AccountHandoverResult> ReissueAsync(Guid accountId, string method, string? password, CancellationToken cancellation);
}

/// <summary>A clinic's role, as a list shows it.</summary>
/// <param name="ReadyKey">
/// <c>owner</c>, <c>manager</c> or <c>doctor</c> for a ready role that still has the name it was
/// created with: show the name for the key in the page's language. Null otherwise.
/// </param>
/// <param name="Fixed">«مالك العيادة»: holds every permission and is never edited or removed.</param>
/// <param name="PermissionCount">Counted from the catalogue for the fixed role, from the role's own set otherwise.</param>
/// <param name="Holders">How many accounts of the clinic hold the role, in any state.</param>
public sealed record RoleSummary(
    Guid Id, string Name, string? ReadyKey, string? Description, string Category, bool Fixed, int PermissionCount, int Holders);

/// <summary>What an edit changed in a role's set, for the record of spec 007.</summary>
public sealed record RoleChange(Guid RoleId, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

/// <summary>
/// The roles of the established clinic and who holds them (spec 005), for the specs that own the
/// screens (015 clinic creation, 026 users, 027 roles). Every operation runs under isolation, in
/// one transaction: a role or an account of another clinic is "not found".
///
/// <para>
/// These enforce what cannot depend on who is asking: a set that keeps the requirement rule, the
/// fixed role, the role in use, the last owner, and one's own account. Whether the caller may do
/// it at all — and "nobody grants a permission they do not hold", "nobody edits the role they
/// hold" — is the caller's business.
/// </para>
/// </summary>
public interface IClinicRoles
{
    /// <summary>
    /// Creates «مالك العيادة», «مدير العيادة» and «طبيب» if the clinic has no roles yet. Safe to
    /// repeat: a clinic that removed one of the two editable ready roles does not get it back.
    /// </summary>
    Task ProvisionAsync(CancellationToken cancellation);

    /// <summary>Ready roles first (owner, manager, doctor), then the clinic's own by name.</summary>
    Task<IReadOnlyList<RoleSummary>> ListAsync(CancellationToken cancellation);

    /// <summary>
    /// Creates a custom role. Refused with field codes on <c>name</c> (<c>REQUIRED</c>,
    /// <c>TOO_LONG</c>, <c>TAKEN</c>) and <c>permissions</c> (<c>REQUIRED</c>, <c>UNKNOWN</c>,
    /// <c>DUPLICATE</c>, <c>MISSING_REQUIREMENT</c> — the last with <c>missing</c>, the pairs).
    /// </summary>
    Task<Guid> CreateAsync(string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken cancellation);

    /// <summary>
    /// Replaces a role's name, description and set. The same refusals, and <c>ROLE_FIXED</c> for
    /// «مالك العيادة». Its holders follow the new set on their next request; their sessions go on.
    /// </summary>
    Task<RoleChange> UpdateAsync(
        Guid roleId, string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken cancellation);

    /// <summary>Removes a role nobody holds. <c>ROLE_FIXED</c>; <c>ROLE_IN_USE</c> with <c>holders</c>.</summary>
    Task RemoveAsync(Guid roleId, CancellationToken cancellation);

    /// <summary>
    /// Gives the account that role. When the role is a different one the account's sessions end
    /// in the same transaction (SH-045). <c>OWN_ROLE</c> when the signed-in person is the
    /// account; <c>LAST_OWNER</c> when it would leave the clinic without an owner.
    /// </summary>
    Task AssignAsync(Guid accountId, Guid roleId, CancellationToken cancellation);
}

/// <summary>A run of failed sign-in attempts, named by the request that began it.</summary>
/// <param name="TraceId">The trace of the request whose wrong password began the run.</param>
/// <param name="NextTraceId">The trace of the request that began the account's next run, when there is one.</param>
public sealed record FailedRunRef(Guid AccountId, string TraceId, string? NextTraceId);

/// <param name="Attempts">Wrong passwords from the run's first to its lock, or to the next run.</param>
/// <param name="Locked">The run ended in a lock.</param>
/// <param name="LockMinutes">How long a lock lasts.</param>
public sealed record FailedRun(Guid AccountId, string TraceId, int Attempts, bool Locked, int LockMinutes);

/// <summary>
/// What a reader of the audit trail needs to know about the accounts of the established clinic
/// (spec 007): how each is called today, and how a run of failed attempts went. Under isolation:
/// an account of another clinic is "not there".
/// </summary>
public interface IStaffDirectory
{
    /// <summary>The display name of each account that exists: the email address until accounts have names (spec 026).</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellation);

    /// <summary>One answer for each run asked about; a run nothing is known of is one attempt and no lock.</summary>
    Task<IReadOnlyList<FailedRun>> FailedRunsAsync(IReadOnlyCollection<FailedRunRef> runs, CancellationToken cancellation);
}
