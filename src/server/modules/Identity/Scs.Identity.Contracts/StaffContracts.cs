namespace Scs.Identity.Contracts;

/// <summary>
/// Who is asking, on a request made with a staff session (spec 003). Roles and permissions are
/// not here; they arrive with spec 005. Using it on a request with no session is refused with
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
