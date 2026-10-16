namespace Scs.SharedKernel;

/// <summary>
/// Stable error codes returned in the <c>code</c> field of every failure
/// (specs/001-platform-foundation/contracts/conventions.md §4). Clients branch on these.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>Work that needs a clinic was started without one (spec 002).</summary>
    public const string ClinicRequired = "CLINIC_REQUIRED";

    /// <summary>The address names no clinic; the same answer for every unknown name (spec 002).</summary>
    public const string ClinicNotAvailable = "CLINIC_NOT_AVAILABLE";

    /// <summary>The work carries two different clinics, or tries to change clinic (spec 002).</summary>
    public const string ClinicMismatch = "CLINIC_MISMATCH";

    /// <summary>The single answer before a correct password (spec 003, SH-043).</summary>
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    /// <summary>The single answer for a wrong, expired, spent, used or replaced sign-in code.</summary>
    public const string CodeInvalid = "CODE_INVALID";

    /// <summary>The access cookie has run out: renew the session, then repeat the request.</summary>
    public const string AccessExpired = "ACCESS_EXPIRED";

    /// <summary>The session is over; sign in again.</summary>
    public const string SessionEnded = "SESSION_ENDED";

    /// <summary>Told only after a correct password; carries <c>lockedUntil</c>.</summary>
    public const string AccountLocked = "ACCOUNT_LOCKED";

    /// <summary>Told only after a correct password.</summary>
    public const string AccountDisabled = "ACCOUNT_DISABLED";

    /// <summary>Told only after a correct password, and at renewal. Covers a cancelled clinic.</summary>
    public const string ClinicSuspended = "CLINIC_SUSPENDED";

    /// <summary>Refused before anything about the account is checked.</summary>
    public const string NetworkBlocked = "NETWORK_BLOCKED";

    /// <summary>The resend countdown has not ended; carries <c>resendAvailableInSeconds</c>.</summary>
    public const string ResendTooEarly = "RESEND_TOO_EARLY";

    /// <summary>No more codes may be resent in this sign-in; the last one is still valid.</summary>
    public const string ResendLimitReached = "RESEND_LIMIT_REACHED";

    /// <summary>The password was correct and the code email could not be sent.</summary>
    public const string SignInCodeNotSent = "SIGN_IN_CODE_NOT_SENT";

    /// <summary>A new code could not be sent; the earlier one is still valid.</summary>
    public const string SignInCodeNotResent = "SIGN_IN_CODE_NOT_RESENT";

    /// <summary>
    /// A password link that is expired, used, replaced, altered or never issued (spec 004).
    /// Carries <c>kind</c>; never says which of those it was.
    /// </summary>
    public const string LinkInvalid = "LINK_INVALID";

    /// <summary>The session holds a temporary password: only changing it is allowed (spec 004).</summary>
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";

    /// <summary>The account holds no temporary password, so there is nothing to replace here.</summary>
    public const string PasswordChangeNotRequired = "PASSWORD_CHANGE_NOT_REQUIRED";

    /// <summary>
    /// The person's role does not hold the permission the route needs (spec 005). Carries
    /// <c>permission</c>, the code needed; says nothing about any record.
    /// </summary>
    public const string PermissionRequired = "PERMISSION_REQUIRED";

    /// <summary>«مالك العيادة» is never edited, renamed or removed.</summary>
    public const string RoleFixed = "ROLE_FIXED";

    /// <summary>A role an account holds is not removed; carries <c>holders</c>.</summary>
    public const string RoleInUse = "ROLE_IN_USE";

    /// <summary>A clinic is never left without an owner (SH-053).</summary>
    public const string LastOwner = "LAST_OWNER";

    /// <summary>Nobody changes the role of their own account (SH-053).</summary>
    public const string OwnRole = "OWN_ROLE";

    /// <summary>A file of no bytes (spec 009).</summary>
    public const string FileEmpty = "FILE_EMPTY";

    /// <summary>Larger than its kind allows; carries <c>sizeBytes</c> and <c>maxBytes</c>.</summary>
    public const string FileTooLarge = "FILE_TOO_LARGE";

    /// <summary>Its content is none of the types its kind takes; carries <c>accepted</c>.</summary>
    public const string FileTypeNotAccepted = "FILE_TYPE_NOT_ACCEPTED";

    /// <summary>The store could not keep it; nothing of it was kept. Try again.</summary>
    public const string FileNotStored = "FILE_NOT_STORED";

    /// <summary>Too many files waiting unsaved, or too many uploads at once; carries <c>retryAfterSeconds</c>.</summary>
    public const string FileLimitReached = "FILE_LIMIT_REACHED";

    /// <summary>The record already holds as many files as its kind allows; carries <c>max</c>.</summary>
    public const string FileCountReached = "FILE_COUNT_REACHED";

    /// <summary>A file named for a record cannot be attached to it; never says which or why.</summary>
    public const string FileNotAttachable = "FILE_NOT_ATTACHABLE";

    /// <summary>Not scanned yet: it cannot be given out.</summary>
    public const string FileBeingChecked = "FILE_BEING_CHECKED";

    /// <summary>Refused by the check; carries <c>reason</c>: <c>infected</c> or <c>unreadable</c>.</summary>
    public const string FileRefused = "FILE_REFUSED";

    /// <summary>Removed, or past the time it is kept for.</summary>
    public const string FileNotKept = "FILE_NOT_KEPT";

    /// <summary>
    /// Spec 010: the step in progress — a sign-in between the password and the code, or a change
    /// of password waiting for its code — is over: expired, cancelled, or its tries are spent.
    /// </summary>
    public const string StepExpired = "STEP_EXPIRED";

    /// <summary>The email already belongs to an operator (spec 011).</summary>
    public const string OpsEmailTaken = "OPS_EMAIL_TAKEN";

    /// <summary>The role holds a permission the caller does not, or is the super admin's and the caller is not one.</summary>
    public const string OpsRoleNotGrantable = "OPS_ROLE_NOT_GRANTABLE";

    /// <summary>An operator does not change the role of their own account.</summary>
    public const string OpsOwnRole = "OPS_OWN_ROLE";

    /// <summary>The change would leave the platform with no active super admin.</summary>
    public const string OpsLastSuperAdmin = "OPS_LAST_SUPER_ADMIN";

    /// <summary>An operator does not suspend their own account.</summary>
    public const string OpsOwnAccount = "OPS_OWN_ACCOUNT";

    /// <summary>The account is not in the state the action needs; nothing changed.</summary>
    public const string OpsStateChanged = "OPS_STATE_CHANGED";

    /// <summary>A link was sent to the account less than a minute ago; nothing was sent.</summary>
    public const string OpsLinkTooSoon = "OPS_LINK_TOO_SOON";

    /// <summary>The record changed since it was read; nothing was overwritten.</summary>
    public const string OpsChangedElsewhere = "OPS_CHANGED_ELSEWHERE";

    /// <summary>The link could not be handed to the mail server; nothing was kept.</summary>
    public const string OpsLinkNotSent = "OPS_LINK_NOT_SENT";

    /// <summary>Another role has the name.</summary>
    public const string OpsRoleNameTaken = "OPS_ROLE_NAME_TAKEN";

    /// <summary>The body adds permissions the caller does not hold; carries <c>permissions</c>.</summary>
    public const string OpsPermissionsNotHeld = "OPS_PERMISSIONS_NOT_HELD";

    /// <summary>Nobody but a super admin edits the role they hold.</summary>
    public const string OpsOwnRoleEdit = "OPS_OWN_ROLE_EDIT";

    /// <summary>One of the platform's own roles: never edited or deleted.</summary>
    public const string OpsSystemRole = "OPS_SYSTEM_ROLE";

    /// <summary>A role that operators hold is not deleted; carries <c>operators</c>.</summary>
    public const string OpsRoleInUse = "OPS_ROLE_IN_USE";

    public static IReadOnlyList<string> All { get; } =
    [
        ValidationFailed, MalformedRequest, Unauthenticated, Forbidden, NotFound,
        PayloadTooLarge, RateLimited, InternalError, ServiceUnavailable,
        ClinicRequired, ClinicNotAvailable, ClinicMismatch,
        InvalidCredentials, CodeInvalid, AccessExpired, SessionEnded,
        AccountLocked, AccountDisabled, ClinicSuspended, NetworkBlocked,
        ResendTooEarly, ResendLimitReached, SignInCodeNotSent, SignInCodeNotResent,
        LinkInvalid, PasswordChangeRequired, PasswordChangeNotRequired,
        PermissionRequired, RoleFixed, RoleInUse, LastOwner, OwnRole,
        FileEmpty, FileTooLarge, FileTypeNotAccepted, FileNotStored, FileLimitReached,
        FileCountReached, FileNotAttachable, FileBeingChecked, FileRefused, FileNotKept,
        StepExpired,
        OpsEmailTaken, OpsRoleNotGrantable, OpsOwnRole, OpsLastSuperAdmin, OpsOwnAccount, OpsStateChanged, OpsLinkTooSoon,
        OpsChangedElsewhere, OpsLinkNotSent, OpsRoleNameTaken, OpsPermissionsNotHeld, OpsOwnRoleEdit, OpsSystemRole, OpsRoleInUse,
    ];
}
