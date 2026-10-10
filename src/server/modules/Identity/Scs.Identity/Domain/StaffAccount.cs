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
/// A member of a clinic's team, as far as signing in is concerned. Name, role and branch arrive
/// with specs 005 and 026. Clinic-owned: with no clinic established it is invisible, which is
/// why finding it before sign-in goes through the one declared lookup.
/// </summary>
public sealed class StaffAccount : IClinicOwned
{
    public const int EmailMaxLength = EmailAddress.MaxLength;
    public const int PasswordHashMaxLength = 200;
    public const int StateMaxLength = 16;

    private StaffAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>Lower case, trimmed, unique on the whole platform. Never changes.</summary>
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

    public DateTimeOffset CreatedAtUtc { get; private set; }

#pragma warning disable CA1819 // The concurrency token is a byte array by the database's definition.
    public byte[] RowVersion { get; private set; } = [];
#pragma warning restore CA1819

    public bool IsLocked(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

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
