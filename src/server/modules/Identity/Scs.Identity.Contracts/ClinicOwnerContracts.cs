namespace Scs.Identity.Contracts;

/// <summary>The three states of an invited owner's invitation (BRD Ops §4-7).</summary>
public static class OwnerInvitationStates
{
    /// <summary>A link that still works: <see cref="OwnerInvitation.At"/> is when it expires.</summary>
    public const string Live = "live";

    /// <summary>The latest link ran out: <see cref="OwnerInvitation.At"/> is when it expired.</summary>
    public const string Lapsed = "lapsed";

    /// <summary>No link was ever sent, or none is left to speak of.</summary>
    public const string None = "none";
}

/// <param name="State">One of <see cref="OwnerInvitationStates"/>.</param>
/// <param name="At">When the live link expires, or when the lapsed one expired; null for none.</param>
public sealed record OwnerInvitation(string State, DateTimeOffset? At);

/// <summary>The established clinic's owner, as an operator reads it (spec 015, FR-014, FR-017).</summary>
/// <param name="State"><c>invited</c>, <c>active</c> or <c>disabled</c>.</param>
/// <param name="Invitation">Only while the owner is invited.</param>
public sealed record ClinicOwner(
    Guid AccountId,
    string Email,
    string? FullName,
    string? Mobile,
    string State,
    DateTimeOffset? LastSignInAtUtc,
    OwnerInvitation? Invitation)
{
    /// <summary>The type and id only: this is personal data.</summary>
    public override string ToString() => $"{nameof(ClinicOwner)}#{AccountId}";
}

/// <summary>A clinic's owner as the operators' list searches it (spec 015, FR-009).</summary>
public sealed record OwnerContact(Guid ClinicId, string Email, string? FullName, string? Mobile)
{
    /// <summary>The type and clinic only: this is personal data.</summary>
    public override string ToString() => $"{nameof(OwnerContact)}#{ClinicId}";
}

/// <summary>
/// The clinic's owner, for the operators console (spec 015): made with the clinic, read on its
/// detail, corrected in its edit, and invited again. Every operation but the first and the last
/// runs for the established clinic, under isolation. The owner is the clinic's earliest account
/// holding the ready role «مالك العيادة».
/// </summary>
public interface IClinicOwners
{
    /// <summary>
    /// Whether no account on the platform has this email (spec 003: one email, one account). Answers
    /// yes or no only, through the lookup sign-in uses; for a signed-in operator (SH-043 untouched).
    /// </summary>
    Task<bool> EmailIsFreeAsync(string email, CancellationToken cancellation);

    /// <summary>
    /// Creates the owner of the established clinic: invited, with its name and mobile, holding the
    /// owner role, its language the clinic's — in one transaction, with the seat check. Sends
    /// nothing. A taken email is refused with <c>email: TAKEN</c>.
    /// </summary>
    Task<Guid> CreateAsync(string email, string fullName, string mobile, string language, CancellationToken cancellation);

    /// <summary>Removes an owner made by an add that failed part-way: only an invited account no link was ever sent to.</summary>
    Task DiscardAsync(Guid accountId, CancellationToken cancellation);

    /// <summary>The established clinic's owner, or null when it has none.</summary>
    Task<ClinicOwner?> FindAsync(CancellationToken cancellation);

    /// <summary>
    /// Adds to <paramref name="errors"/> each of the owner's fields an update would refuse, writing
    /// nothing, so that an edit can be refused whole before any part of it is written (FR-021).
    /// </summary>
    void CheckDetails(string? fullName, string? email, string? mobile, Dictionary<string, string[]> errors);

    /// <summary>
    /// Corrects the owner's name, email and mobile and records <c>clinic.owner-updated</c> with each
    /// changed field, in one transaction. A changed email ends the owner's sessions (an active
    /// owner, with a notice to the old address after the save, in <paramref name="noticeLanguage"/>:
    /// the clinic's) or its links (an invited owner). Nothing changed records nothing. Returns whether
    /// anything changed.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid clinicId, string clinicNameAr, string? fullName, string? email, string? mobile, string? note, string noticeLanguage,
        CancellationToken cancellation);

    /// <summary>
    /// Sends the invited owner a new 72-hour link; the earlier one stops working. Refused with
    /// <c>INVITATION_ALREADY_ACCEPTED</c>, <c>RESET_EMAIL_COOLDOWN</c> (a link less than a minute
    /// old) or <c>ACCOUNT_LINK_NOT_SENT</c>. Returns when the new link expires.
    /// </summary>
    Task<DateTimeOffset> ResendInvitationAsync(Guid clinicId, string clinicNameAr, CancellationToken cancellation);

    /// <summary>Every clinic's owner, across clinics, for the operators' list (crossing <c>read-clinic-owners-as-operator</c>).</summary>
    Task<IReadOnlyList<OwnerContact>> ListForOperatorsAsync(CancellationToken cancellation);
}
