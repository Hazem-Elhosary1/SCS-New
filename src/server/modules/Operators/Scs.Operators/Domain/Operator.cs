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
/// It holds one role (spec 011), and what it may do is that role's permissions at each request.
/// </summary>
internal sealed class Operator
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int MobileMaxLength = 30;
    public const int JobTitleMaxLength = 150;

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

    /// <summary>The one role the account holds (spec 011, ق-53).</summary>
    public Guid RoleId { get; private set; }

    /// <summary>One of <see cref="OperatorDepartments"/>, or none.</summary>
    public string? Department { get; private set; }

    public string? JobTitle { get; private set; }

    /// <summary>Always on while the platform asks every operator for the emailed code (spec 011, FR-040).</summary>
    public bool TwoStepRequired { get; private set; } = true;

    /// <summary>Who invited the account; none when the deployment tool did.</summary>
    public Guid? InvitedById { get; private set; }

    /// <summary>The inviter's name when they invited: kept so the page can say it whatever becomes of them.</summary>
    public string? InvitedByName { get; private set; }

    /// <summary>When the first password was set.</summary>
    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    public DateTimeOffset? SuspendedAtUtc { get; private set; }

    /// <summary>
    /// Set while an invitation email is owed to an invited account: the deployment command asks,
    /// the platform sends (FR-003).
    /// </summary>
    public DateTimeOffset? InvitationOwedSinceUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsLocked(DateTimeOffset now) => LockedUntilUtc > now;

    public static Operator Invite(string fullName, string normalizedEmail, Guid roleId, DateTimeOffset now)
    {
        var name = NameOf(fullName);
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("An operator holds a role.", nameof(roleId));
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
            RoleId = roleId,
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
        ActivatedAtUtc = now;
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

    /// <summary>Who invited the account, when an operator did (spec 011).</summary>
    public void InvitedBy(Guid inviterId, string inviterName)
    {
        InvitedById = inviterId;
        InvitedByName = NameOf(inviterName);
    }

    /// <summary>
    /// The details an operator who manages operators may set. The email is not among them: it
    /// is the account's identity and never changes (ق-59).
    /// </summary>
    public void Describe(string fullName, string? mobile, string? department, string? jobTitle, string language)
    {
        var name = NameOf(fullName);
        var phone = Optional(mobile, MobileMaxLength, "A mobile number has at most 30 characters.", nameof(mobile));
        var title = Optional(jobTitle, JobTitleMaxLength, "A job title has at most 150 characters.", nameof(jobTitle));

        if (department is not null && !OperatorDepartments.All.Contains(department))
        {
            throw new ArgumentException("Not a department of the platform.", nameof(department));
        }

        if (language is not ("ar" or "en"))
        {
            throw new ArgumentException("The language is Arabic or English.", nameof(language));
        }

        FullName = name;
        Mobile = phone;
        Department = department;
        JobTitle = title;
        Language = language;
    }

    /// <summary>Another role. Whether it may be given, and to this account, is the caller's to check first.</summary>
    /// <returns>Whether the role changed.</returns>
    public bool ChangeRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("An operator holds a role.", nameof(roleId));
        }

        if (RoleId == roleId)
        {
            return false;
        }

        RoleId = roleId;
        return true;
    }

    /// <summary>Only an active account is suspended (§4-3).</summary>
    public void Suspend(DateTimeOffset now)
    {
        if (State != OperatorStates.Active)
        {
            throw new InvalidOperationException("Only an active account is suspended.");
        }

        State = OperatorStates.Suspended;
        SuspendedAtUtc = now;
    }

    /// <summary>Back to active, with wrong tries, spent codes and any lock forgotten (ق-125).</summary>
    public void Reactivate()
    {
        if (State != OperatorStates.Suspended)
        {
            throw new InvalidOperationException("Only a suspended account is reactivated.");
        }

        State = OperatorStates.Active;
        SuspendedAtUtc = null;
        ForgetTries();
    }

    /// <summary>Only an active account that is locked now is unlocked.</summary>
    public void Unlock(DateTimeOffset now)
    {
        if (State != OperatorStates.Active || !IsLocked(now))
        {
            throw new InvalidOperationException("Only an active, locked account is unlocked.");
        }

        ForgetTries();
    }

    private void ForgetTries()
    {
        FailedAttempts = 0;
        LastFailedAtUtc = null;
        SpentCodes = 0;
        LockedUntilUtc = null;
    }

    private static string NameOf(string? fullName)
    {
        var name = (fullName ?? "").Trim();
        return name.Length is 0 or > NameMaxLength
            ? throw new ArgumentException("A name has 1 to 200 characters.", nameof(fullName))
            : name;
    }

    private static string? Optional(string? value, int max, string refusal, string parameter)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return trimmed is { Length: var length } && length > max ? throw new ArgumentException(refusal, parameter) : trimmed;
    }
}
