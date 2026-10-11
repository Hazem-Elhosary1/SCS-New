using Scs.SharedKernel;

namespace Scs.Identity.Domain;

public static class PasswordLinkKinds
{
    /// <summary>For an active account whose owner forgot the password, or was sent a link by someone at the clinic.</summary>
    public const string Reset = "reset";

    /// <summary>For an invited account: choosing the first password activates it.</summary>
    public const string NewAccount = "new-account";

    public static IReadOnlyList<string> All { get; } = [Reset, NewAccount];
}

/// <summary>
/// One emailed link that lets its holder set a password (spec 004). It is <em>live</em> while it
/// has not been used, has not been replaced and has not run out; an account has at most one
/// live link. The link's secret is never stored: only a hash of it.
/// </summary>
public sealed class PasswordLink : IClinicOwned
{
    public const int KindMaxLength = 12;

    private PasswordLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Kind { get; private set; } = "";

#pragma warning disable CA1819 // A hash is a byte array.
    [Secret]
    public byte[] SecretHash { get; private set; } = [];
#pragma warning restore CA1819

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    /// <summary>Set when a newer link is issued, the password is set another way, or the account is disabled.</summary>
    public DateTimeOffset? ReplacedAtUtc { get; private set; }

    public bool IsLive(DateTimeOffset now) => UsedAtUtc is null && ReplacedAtUtc is null && ExpiresAtUtc > now;

    public static PasswordLink Issue(
        Guid id, Guid accountId, string kind, byte[] secretHash, DateTimeOffset now, TimeSpan life)
    {
        if (!PasswordLinkKinds.All.Contains(kind, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new PasswordLink
        {
            Id = id,
            AccountId = accountId,
            Kind = kind,
            SecretHash = secretHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + life,
        };
    }

    public override string ToString() => $"{nameof(PasswordLink)}#{Id}";
}

public static class AccountEmailKinds
{
    public const string ResetLink = "reset-link";
    public const string PasswordChanged = "password-changed";
}

public static class AccountEmailOutcomes
{
    public const string Sent = "sent";

    /// <summary>One was sent to this account too recently, or too many in the last hour.</summary>
    public const string SkippedLimit = "skipped-limit";

    /// <summary>The account is no longer active.</summary>
    public const string SkippedState = "skipped-state";

    /// <summary>The mail server kept refusing until the request was too old to be worth sending.</summary>
    public const string GaveUp = "gave-up";
}

/// <summary>
/// An email waiting to be sent in the background (spec 004, research R6). Asking for a reset
/// link only writes one of these and answers at once, so the answer takes the same time whether
/// or not anything will be sent. It holds no address and no secret.
/// </summary>
public sealed class AccountEmailRequest : IClinicOwned
{
    public const int KindMaxLength = 20;
    public const int OutcomeMaxLength = 16;

    private AccountEmailRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Kind { get; private set; } = "";

    public DateTimeOffset RequestedAtUtc { get; private set; }

    /// <summary>Null while waiting.</summary>
    public DateTimeOffset? HandledAtUtc { get; private set; }

    public string? Outcome { get; private set; }

    public static AccountEmailRequest For(Guid accountId, string kind, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        AccountId = accountId,
        Kind = kind,
        RequestedAtUtc = now,
    };

    public void Handle(string outcome, DateTimeOffset now)
    {
        Outcome = outcome;
        HandledAtUtc = now;
    }

    public override string ToString() => $"{nameof(AccountEmailRequest)}#{Id}";
}
