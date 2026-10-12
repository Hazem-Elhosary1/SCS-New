namespace Scs.Operators.Domain;

public static class ChallengePurposes
{
    public const string SignIn = "sign-in";
    public const string PasswordChange = "password-change";
}

/// <summary>
/// A code that is waiting to be typed (data-model §2): for a sign-in between the password and
/// the code, or for a change of password between the current password and the code (FR-045a).
/// Only a keyed hash of the code is kept.
/// </summary>
internal sealed class SignInChallenge
{
    public const int AddressMaxLength = 45;

    private SignInChallenge()
    {
    }

    public Guid Id { get; private set; }

    public Guid OperatorId { get; private set; }

    public string Purpose { get; private set; } = ChallengePurposes.SignIn;

    /// <summary>For a change of password: the session that asked.</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>For a change of password: written to the account only when the code is right.</summary>
    public string? PendingPasswordHash { get; private set; }

    public byte[] CodeHash { get; private set; } = [];

    public DateTimeOffset CodeSentAtUtc { get; private set; }

    /// <summary>The whole challenge ends here: a new code does not get ten minutes of its own.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset ResendAvailableAtUtc { get; private set; }

    public byte Attempts { get; private set; }

    public byte ResendsUsed { get; private set; }

    public bool Remember { get; private set; }

    public string? Address { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    /// <summary>A newer code took its place, or the challenge was cancelled.</summary>
    public DateTimeOffset? ReplacedAtUtc { get; private set; }

    public static SignInChallenge Issue(
        Guid id, Guid operatorId, string purpose, byte[] codeHash, DateTimeOffset now, DateTimeOffset expiresAt,
        TimeSpan resendWait, byte resendsUsed, bool remember, string? address, Guid? sessionId = null,
        string? pendingPasswordHash = null) => new()
    {
        Id = id,
        OperatorId = operatorId,
        Purpose = purpose,
        SessionId = sessionId,
        PendingPasswordHash = pendingPasswordHash,
        CodeHash = codeHash,
        CodeSentAtUtc = now,
        ExpiresAtUtc = expiresAt,
        ResendAvailableAtUtc = now + resendWait,
        ResendsUsed = resendsUsed,
        Remember = remember,
        Address = Cut(address, AddressMaxLength),
    };

    internal static string? Cut(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}

public static class SessionEndings
{
    public const string SignedOut = "signed-out";
    public const string Replaced = "replaced";
    public const string Idle = "idle";
    public const string Expired = "expired";
    public const string PasswordReset = "password-reset";
    public const string Suspended = "suspended";
    public const string SignedOutEverywhere = "signed-out-everywhere";
    public const string Reuse = "reuse";
    public const string RoleChanged = "role-changed";
    public const string Locked = "locked";

    public static IReadOnlyList<string> All { get; } =
        [SignedOut, Replaced, Idle, Expired, PasswordReset, Suspended, SignedOutEverywhere, Reuse, RoleChanged, Locked];

    /// <summary>What the operator did themselves: the sign-in screen says nothing about it.</summary>
    public static bool ByTheOperator(string reason) => reason is SignedOut or SignedOutEverywhere;
}

/// <summary>One signed-in browser (data-model §3). At most one is open for an operator (FR-030).</summary>
internal sealed class OperatorSession
{
    public const int UserAgentMaxLength = 256;

    private OperatorSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid OperatorId { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    /// <summary>Its longest life, however active (FR-031).</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Moved by what the operator does; not by a renewal or a background request.</summary>
    public DateTimeOffset LastActivityAtUtc { get; private set; }

    /// <summary>Whether its cookies outlive the browser being closed (FR-033).</summary>
    public bool Remember { get; private set; }

    public byte[] RenewalHash { get; private set; } = [];

    public byte[]? PreviousRenewalHash { get; private set; }

    public DateTimeOffset? PreviousValidUntilUtc { get; private set; }

    public string? Address { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public string? EndedFor { get; private set; }

    public static OperatorSession Open(
        Guid operatorId, byte[] renewalHash, DateTimeOffset now, TimeSpan life, bool remember, string? address,
        string? userAgent) => new()
    {
        Id = Guid.CreateVersion7(),
        OperatorId = operatorId,
        StartedAtUtc = now,
        ExpiresAtUtc = now + life,
        LastActivityAtUtc = now,
        Remember = remember,
        RenewalHash = renewalHash,
        Address = SignInChallenge.Cut(address, SignInChallenge.AddressMaxLength),
        UserAgent = SignInChallenge.Cut(userAgent, UserAgentMaxLength),
    };
}

public static class PasswordLinkKinds
{
    public const string Reset = "reset";
    public const string First = "first";
}

/// <summary>What lets the owner of an account choose a password (data-model §4): once, for a while.</summary>
internal sealed class PasswordLink
{
    private PasswordLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid OperatorId { get; private set; }

    public string Kind { get; private set; } = PasswordLinkKinds.Reset;

    public byte[] SecretHash { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public bool Stands(DateTimeOffset now) => UsedAtUtc is null && EndedAtUtc is null && ExpiresAtUtc > now;

    public static PasswordLink Issue(Guid operatorId, string kind, byte[] secretHash, DateTimeOffset now, TimeSpan life) => new()
    {
        Id = Guid.CreateVersion7(),
        OperatorId = operatorId,
        Kind = kind,
        SecretHash = secretHash,
        CreatedAtUtc = now,
        ExpiresAtUtc = now + life,
    };
}

/// <summary>A browser an account has completed a sign-in from (data-model §5): a hash of its mark.</summary>
internal sealed class KnownBrowser
{
    private KnownBrowser()
    {
    }

    public Guid OperatorId { get; private set; }

    public byte[] MarkHash { get; private set; } = [];

    public DateTimeOffset FirstSeenAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public static KnownBrowser See(Guid operatorId, byte[] markHash, DateTimeOffset now) =>
        new() { OperatorId = operatorId, MarkHash = markHash, FirstSeenAtUtc = now, LastSeenAtUtc = now };
}

/// <summary>
/// The kinds of line in the operators' sign-in record (data-model §6). The names the operators'
/// chapter gives its security events are used where it has one, so specs 023 and 025 read them
/// without translating.
/// </summary>
public static class SignInRecordKinds
{
    public const string LoginSucceeded = "login_succeeded";
    public const string LoginFailed = "login_failed";
    public const string OtpFailed = "otp_failed";
    public const string AccountLocked = "account_locked";
    public const string SignedOut = "signed_out";
    public const string SessionEnded = "session_ended";
    public const string SessionTokenReuse = "session_token_reuse";
    public const string PasswordResetRequested = "password_reset_requested";
    public const string PasswordResetCompleted = "password_reset_completed";
    public const string PasswordChanged = "password_changed";
    public const string FirstPasswordSet = "first_password_set";
    public const string NewBrowser = "new_browser";
    public const string InvitedByDeployment = "operator_invited_by_deployment";
    public const string InviteWithdrawn = "operator_invite_withdrawn";

    /// <summary>The deployment command was run and changed nothing: the fact says who ran it and why not.</summary>
    public const string CommandChangedNothing = "operator_command_changed_nothing";

    public static IReadOnlyList<string> All { get; } =
    [
        LoginSucceeded, LoginFailed, OtpFailed, AccountLocked, SignedOut, SessionEnded, SessionTokenReuse,
        PasswordResetRequested, PasswordResetCompleted, PasswordChanged, FirstPasswordSet, NewBrowser,
        InvitedByDeployment, InviteWithdrawn, CommandChangedNothing,
    ];
}

/// <summary>
/// One event of signing in (FR-052). Insert-only for the application; removed by age alone
/// (FR-052a). Never holds a password, a code, a token, a link or an email (FR-053).
/// </summary>
internal sealed class SignInRecord
{
    public const int FactsMaxLength = 400;

    private SignInRecord()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string Kind { get; private set; } = "";

    /// <summary>Empty for an email that matches nobody.</summary>
    public Guid? OperatorId { get; private set; }

    public Guid? SessionId { get; private set; }

    public string? Address { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>A short note by kind: the reason a session ended, the user who ran a command.</summary>
    public string? Facts { get; private set; }

    public string TraceId { get; private set; } = "";

    public static SignInRecord Of(
        string kind, Guid? operatorId, DateTimeOffset now, string? address, string? userAgent, string traceId,
        Guid? sessionId = null, string? facts = null)
    {
        if (!SignInRecordKinds.All.Contains(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of sign-in record.");
        }

        return new SignInRecord
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = now,
            Kind = kind,
            OperatorId = operatorId,
            SessionId = sessionId,
            Address = SignInChallenge.Cut(address, SignInChallenge.AddressMaxLength),
            UserAgent = SignInChallenge.Cut(userAgent, OperatorSession.UserAgentMaxLength),
            Facts = SignInChallenge.Cut(facts, FactsMaxLength),
            TraceId = SignInChallenge.Cut(traceId, 64) ?? "",
        };
    }
}
