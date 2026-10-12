namespace Scs.Operators.Domain;

public static class OperatorStates
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Suspended = "suspended";
}

/// <summary>
/// A person who runs the platform (spec 010, FR-001). Belongs to no clinic: the same email may
/// also be a clinic team member's, and nothing is shared between the two accounts (FR-002).
/// Its role arrives with spec 011.
/// </summary>
internal sealed class Operator
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int MobileMaxLength = 30;

    private Operator()
    {
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = "";

    /// <summary>Trimmed, lower case, unique, never changed after creation.</summary>
    public string Email { get; private set; } = "";

    public string? Mobile { get; private set; }

    /// <summary><c>ar</c> or <c>en</c>: the language of the console and of the account's emails.</summary>
    public string Language { get; private set; } = "ar";

    public string State { get; private set; } = OperatorStates.Invited;

    /// <summary>Empty while invited. Never returned by any read.</summary>
    public string? PasswordHash { get; private set; }

    public DateTimeOffset? PasswordChangedAtUtc { get; private set; }

    public byte FailedAttempts { get; private set; }

    public DateTimeOffset? LastFailedAtUtc { get; private set; }

    /// <summary>Codes spent in a row; the third locks the account (FR-020a).</summary>
    public byte SpentCodes { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Set while an invitation email is owed to an invited account: the deployment command asks,
    /// the platform sends (FR-003).
    /// </summary>
    public DateTimeOffset? InvitationOwedSinceUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsLocked(DateTimeOffset now) => LockedUntilUtc > now;

    public static Operator Invite(string fullName, string normalizedEmail, DateTimeOffset now)
    {
        var name = (fullName ?? "").Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new ArgumentException("A name has 1 to 200 characters.", nameof(fullName));
        }

        if (string.IsNullOrEmpty(normalizedEmail) || normalizedEmail.Length > EmailMaxLength)
        {
            throw new ArgumentException("An email has at most 254 characters.", nameof(normalizedEmail));
        }

        return new Operator
        {
            Id = Guid.CreateVersion7(),
            FullName = name,
            Email = normalizedEmail,
            State = OperatorStates.Invited,
            CreatedAtUtc = now,
        };
    }

    public void OweInvitation(DateTimeOffset now)
    {
        if (State != OperatorStates.Invited)
        {
            throw new InvalidOperationException("Only an invited account is owed an invitation.");
        }

        InvitationOwedSinceUtc = now;
    }

    public void InvitationSent() => InvitationOwedSinceUtc = null;

    /// <summary>The first password, from the invitation's link: the account becomes active.</summary>
    public void Activate(string passwordHash, DateTimeOffset now)
    {
        if (State != OperatorStates.Invited)
        {
            throw new InvalidOperationException("Only an invited account chooses a first password.");
        }

        State = OperatorStates.Active;
        InvitationOwedSinceUtc = null;
        SetPassword(passwordHash, now);
    }

    /// <summary>A new password: wrong tries, spent codes and a lock are forgotten (FR-028).</summary>
    public void SetPassword(string passwordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);
        PasswordHash = passwordHash;
        PasswordChangedAtUtc = now;
        FailedAttempts = 0;
        LastFailedAtUtc = null;
        SpentCodes = 0;
        LockedUntilUtc = null;
    }
}
