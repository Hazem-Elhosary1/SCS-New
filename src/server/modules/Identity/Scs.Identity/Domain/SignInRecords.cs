using Scs.SharedKernel;

namespace Scs.Identity.Domain;

/// <summary>
/// One six-digit code issued after a correct password. It is <em>standing</em> while it is not
/// consumed, not replaced, not expired and has attempts left; at most one stands per account.
/// The code itself is never stored: only a keyed hash of it.
/// </summary>
public sealed class SignInChallenge : IClinicOwned
{
    private SignInChallenge()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

#pragma warning disable CA1819 // A hash is a byte array.
    [Secret]
    public byte[] CodeHash { get; private set; } = [];
#pragma warning restore CA1819

    public byte Attempts { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>When the sign-in this code belongs to must be started again. A resend does not move it.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset ResendAvailableAtUtc { get; private set; }

    /// <summary>Codes already resent in this sign-in, carried from the code this one replaced.</summary>
    public byte ResendsUsed { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public DateTimeOffset? ReplacedAtUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public static SignInChallenge Issue(
        Guid id,
        Guid accountId,
        byte[] codeHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        TimeSpan resendCountdown,
        byte resendsUsed,
        string? ipAddress) => new()
        {
            Id = id,
            AccountId = accountId,
            CodeHash = codeHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt,
            ResendAvailableAtUtc = now + resendCountdown,
            ResendsUsed = resendsUsed,
            IpAddress = ipAddress,
        };

    public override string ToString() => $"{nameof(SignInChallenge)}#{Id}";
}

/// <summary>Why a session ended. Stored as text; shown to nobody at sign-in (FR-041).</summary>
public static class SessionEndReasons
{
    public const string SignedOut = "signed-out";
    public const string Expired = "expired";
    public const string Locked = "locked";
    public const string Disabled = "disabled";
    public const string EndedForAccount = "ended-for-account";
    public const string EndedForClinic = "ended-for-clinic";
    public const string ClinicSuspended = "clinic-suspended";
    public const string RenewalReused = "renewal-reused";
    public const string PasswordReset = "password-reset";
    public const string RoleChanged = "role-changed";

    /// <summary>An operator changed the clinic owner's sign-in email (spec 015, FR-021a).</summary>
    public const string EmailChanged = "email-changed";

    public static IReadOnlyList<string> All { get; } =
    [
        SignedOut, Expired, Locked, Disabled, EndedForAccount, EndedForClinic,
        ClinicSuspended, RenewalReused, PasswordReset, RoleChanged, EmailChanged,
    ];
}

/// <summary>
/// One signed-in browser. Alive while it has not been ended and has not reached its maximum
/// life. The browser proves the session is its own with a secret that changes at every renewal;
/// only hashes of the current and the previous secret are kept.
/// </summary>
public sealed class Session : IClinicOwned
{
    public const int UserAgentMaxLength = 300;
    public const int IpAddressMaxLength = 45;

    private Session()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

#pragma warning disable CA1819 // Hashes are byte arrays.
    [Secret]
    public byte[] RenewalHash { get; private set; } = [];

    [Secret]
    public byte[]? PreviousRenewalHash { get; private set; }
#pragma warning restore CA1819

    public DateTimeOffset? PreviousValidUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastUsedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public string? EndReason { get; private set; }

    [PersonalData]
    public string? IpAddress { get; private set; }

    [PersonalData]
    public string? UserAgent { get; private set; }

    public static Session Open(
        Guid accountId, byte[] renewalHash, DateTimeOffset now, TimeSpan maximumLife, string? ipAddress, string? userAgent) => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            RenewalHash = renewalHash,
            CreatedAtUtc = now,
            LastUsedAtUtc = now,
            ExpiresAtUtc = now + maximumLife,
            IpAddress = Truncate(ipAddress, IpAddressMaxLength),
            UserAgent = Truncate(userAgent, UserAgentMaxLength),
        };

    internal static string? Truncate(string? text, int length) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= length ? text : text[..length];

    public override string ToString() => $"{nameof(Session)}#{Id}";
}

public static class SignInEventKinds
{
    public const string SignedIn = "signed-in";
    public const string WrongPassword = "wrong-password";
    public const string AccountLocked = "account-locked";
    public const string WrongCode = "wrong-code";
    public const string CodeSpent = "code-spent";
    public const string RenewalReused = "renewal-reused";
    public const string SessionEnded = "session-ended";

    // Passwords and activation (spec 004).
    public const string ResetRequested = "reset-requested";
    public const string ResetCompleted = "reset-completed";
    public const string InvitationAccepted = "invitation-accepted";
    public const string TemporaryPasswordChanged = "temp-password-changed";
    public const string AccessReissued = "access-reissued";

    public const string Activity = "activity";
    public const string Security = "security";

    public static string CategoryOf(string kind) =>
        kind is SignedIn or ResetRequested or InvitationAccepted or TemporaryPasswordChanged ? Activity : Security;
}

/// <summary>
/// What happened to an account at sign-in or to its password: identifiers, where from, and when. No email, no name,
/// no free text. Append-only: the database refuses to change or remove a row. Read later by the
/// clinic's activity and security log (spec 059) and the operators' security page (spec 025).
/// </summary>
public sealed class SignInEvent : IClinicOwned
{
    private SignInEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Kind { get; private set; } = "";

    public string Category { get; private set; } = "";

    /// <summary>For <c>session-ended</c>: the end reason. For <c>access-reissued</c>: the way.</summary>
    public string? Detail { get; private set; }

    /// <summary>Who did it, when it was not the account's owner.</summary>
    public Guid? ActorAccountId { get; private set; }

    public Guid? SessionId { get; private set; }

    [PersonalData]
    public string? IpAddress { get; private set; }

    [PersonalData]
    public string? UserAgent { get; private set; }

    public string TraceId { get; private set; } = "";

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static SignInEvent Record(
        Guid accountId, string kind, DateTimeOffset now, string traceId,
        string? ipAddress, string? userAgent, Guid? sessionId = null, string? detail = null,
        Guid? actorAccountId = null) => new()
        {
            Id = Guid.CreateVersion7(),
            AccountId = accountId,
            Kind = kind,
            Category = SignInEventKinds.CategoryOf(kind),
            Detail = detail,
            ActorAccountId = actorAccountId,
            SessionId = sessionId,
            IpAddress = Session.Truncate(ipAddress, Session.IpAddressMaxLength),
            UserAgent = Session.Truncate(userAgent, Session.UserAgentMaxLength),
            TraceId = traceId,
            OccurredAtUtc = now,
        };

    public override string ToString() => $"{nameof(SignInEvent)}#{Id}";
}

/// <summary>
/// A sign-in attempt on an email that matches no account. It belongs to no clinic, so it is kept
/// outside every clinic's data, where the application may add a row and do nothing else. It holds
/// a keyed hash of what was typed, never the text: people type passwords into the wrong box.
/// </summary>
public sealed class AnonymousSignInAttempt
{
    private AnonymousSignInAttempt()
    {
    }

    public long Id { get; private set; }

#pragma warning disable CA1819 // A hash is a byte array.
    public byte[] EmailKey { get; private set; } = [];
#pragma warning restore CA1819

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string TraceId { get; private set; } = "";

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AnonymousSignInAttempt Record(
        byte[] emailKey, DateTimeOffset now, string traceId, string? ipAddress, string? userAgent) => new()
        {
            EmailKey = emailKey,
            IpAddress = Session.Truncate(ipAddress, Session.IpAddressMaxLength),
            UserAgent = Session.Truncate(userAgent, Session.UserAgentMaxLength),
            TraceId = traceId,
            OccurredAtUtc = now,
        };
}
