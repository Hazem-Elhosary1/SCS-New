using Scs.SharedKernel;

namespace Scs.Identity.Domain;

/// <summary>The states an account can be in. Locked is not one: see <see cref="StaffAccount.IsLocked"/>.</summary>
public static class AccountStates
{
    /// <summary>Delivered by a link; its owner has not chosen a password yet.</summary>
    public const string Invited = "invited";

    public const string Active = "active";

    public const string Disabled = "disabled";

    public static IReadOnlyList<string> All { get; } = [Invited, Active, Disabled];
}

/// <summary>
/// A member of a clinic's team, as far as signing in is concerned, and the role they hold. The
/// owner's name and mobile arrive with spec 015, every account's name and branch with spec 026. Clinic-owned: with no clinic established it is invisible, which is
/// why finding it before sign-in goes through the one declared lookup.
/// </summary>
public sealed class StaffAccount : IClinicOwned
{
    public const int EmailMaxLength = EmailAddress.MaxLength;
    public const int PasswordHashMaxLength = 200;
    public const int StateMaxLength = 16;
    public const int FullNameMaxLength = 200;
    public const int MobileMaxLength = 20;

    private StaffAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>
    /// Lower case, trimmed, unique on the whole platform. Changed only by an operator correcting
    /// the clinic owner's address (spec 015, FR-021a).
    /// </summary>
    [PersonalData]
    public string Email { get; private set; } = "";

    /// <summary>Null while the account is invited.</summary>
    [Secret]
    public string? PasswordHash { get; private set; }

    public string State { get; private set; } = AccountStates.Invited;

    /// <summary>Wrong passwords in a row, each less than the attempt window after the one before.</summary>
    public byte FailedAttempts { get; private set; }

    public DateTimeOffset? LastFailedAtUtc { get; private set; }

    /// <summary>Sign-in codes spent in a row with no successful sign-in between them.</summary>
    public byte SpentCodes { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <summary>
    /// The password was chosen by someone else — generated, or typed by whoever created the
    /// account — and must be replaced by its owner before anything else is allowed (spec 004).
    /// </summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Until when a temporary password is accepted at sign-in. Null unless <see cref="MustChangePassword"/>.</summary>
    public DateTimeOffset? TemporaryPasswordExpiresAtUtc { get; private set; }

    /// <summary>
    /// The one role the account holds (spec 005). Null is "no role": the account then holds no
    /// permission and reaches only what any signed-in person reaches.
    /// </summary>
    public Guid? RoleId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>The person's full name: the clinic owner's since spec 015 («جهة الاتصال»); 026 gives every account one.</summary>
    [PersonalData]
    public string? FullName { get; private set; }

    /// <summary>A phone number from any country, kept without spaces and dashes (spec 015, the owner's answer).</summary>
    [PersonalData]
    public string? Mobile { get; private set; }

    /// <summary>When the latest session was opened, even if it has ended since (BRD Ops §3-2).</summary>
    public DateTimeOffset? LastSignInAtUtc { get; private set; }

#pragma warning disable CA1819 // The concurrency token is a byte array by the database's definition.
    public byte[] RowVersion { get; private set; } = [];
#pragma warning restore CA1819

    public bool IsLocked(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

    /// <summary>A temporary password that nobody replaced in time opens nothing.</summary>
    public bool HasExpiredTemporaryPassword(DateTimeOffset now) =>
        MustChangePassword && TemporaryPasswordExpiresAtUtc is { } until && until <= now;

    public static StaffAccount Create(EmailAddress email, string state, string? passwordHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (!AccountStates.All.Contains(state, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state != AccountStates.Invited && passwordHash is null)
        {
            throw new ArgumentException("An account that is not invited has a password.", nameof(passwordHash));
        }

        return new StaffAccount
        {
            Id = Guid.CreateVersion7(),
            Email = email.Value,
            State = state,
            PasswordHash = state == AccountStates.Invited ? null : passwordHash,
            CreatedAtUtc = now,
        };
    }

    public void SetState(string state)
    {
        if (state is not (AccountStates.Active or AccountStates.Disabled))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (PasswordHash is null)
        {
            throw new InvalidOperationException("An invited account becomes active by choosing a password (spec 004).");
        }

        State = state;
    }

    public void ReplacePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>The clinic owner's name and mobile, as an operator gives them (spec 015).</summary>
    public void SetOwnerDetails(string fullName, string mobile)
    {
        FullName = fullName.Trim();
        Mobile = mobile;
    }

    /// <summary>A new sign-in address; ending sessions and links is the caller's, in the same transaction.</summary>
    public void ChangeEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        Email = email.Value;
    }

    public void SignedIn(DateTimeOffset now) => LastSignInAtUtc = now;

    /// <summary>Gives the account a role. Ending its sessions is the caller's to do, in the same transaction (SH-045).</summary>
    public void AssignRole(Guid roleId) => RoleId = roleId;

    /// <summary>
    /// Sets a password by any of the routes of spec 004. An invited account becomes active; a
    /// lock is lifted. With <paramref name="temporaryUntil"/> the password is someone else's
    /// choice and must be changed at first sign-in; without it, it is the owner's own.
    /// </summary>
    public void SetPassword(string passwordHash, DateTimeOffset? temporaryUntil)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        if (State == AccountStates.Disabled)
        {
            throw new InvalidOperationException("A disabled account's password is not set.");
        }

        PasswordHash = passwordHash;
        State = AccountStates.Active;
        MustChangePassword = temporaryUntil is not null;
        TemporaryPasswordExpiresAtUtc = temporaryUntil;
        ClearLock();
    }

    /// <summary>Lifts a lock and forgets what led to it.</summary>
    public void ClearLock()
    {
        LockedUntilUtc = null;
        FailedAttempts = 0;
        LastFailedAtUtc = null;
        SpentCodes = 0;
    }

    /// <summary>The type and id only, so an account logged by mistake shows no email.</summary>
    public override string ToString() => $"{nameof(StaffAccount)}#{Id}";
}
