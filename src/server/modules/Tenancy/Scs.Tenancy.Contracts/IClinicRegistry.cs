namespace Scs.Tenancy.Contracts;

/// <summary>A clinic as operators see it. Clinics being set up, or discarded, are never listed (spec 015).</summary>
/// <param name="CreatedBy">The operator id that added it, as text (<c>tool:seed</c> for the seed); null for none.</param>
/// <param name="AccountManagerId">The operator who looks after it, or null.</param>
public sealed record ClinicListItem(
    Guid Id,
    string ShortName,
    string NameAr,
    string NameEn,
    string State,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy = null,
    Guid? AccountManagerId = null);

/// <summary>What a patient address resolves to (contracts/tenancy-api.openapi.yaml).</summary>
public sealed record ClinicSummary(string ShortName, string NameAr, string NameEn, string State, bool Renamed);

/// <summary>A clinic as an operator adds it (spec 015, FR-002). The slug is derived, never given.</summary>
public sealed record ClinicRegistration(
    string? NameAr, string? NameEn, string? Specialty, string? Address, string? TaxNumber, string OperatorId);

/// <summary>A clinic just registered, still being set up.</summary>
public sealed record RegisteredClinic(Guid Id, string Slug);

/// <summary>What the add did besides the clinic, for <c>clinic.created</c> (spec 015, FR-027).</summary>
public sealed record ClinicCreation(
    string? PlanCode, DateTimeOffset TrialEndsAtUtc, string OwnerName, string OwnerEmail, string OwnerMobile, string OperatorId);

/// <summary>
/// An operator's edit of a clinic's own data (spec 015, FR-020 – FR-022). The tax number not sent
/// keeps it; sent empty removes it. The specialty may be null («غير مسجّل»).
/// </summary>
public sealed record ClinicProfileChange(
    string? NameAr,
    string? NameEn,
    string? Address,
    bool TaxNumberSent,
    string? TaxNumber,
    string? Specialty,
    string? DefaultLanguage,
    string? Note,
    string OperatorId);

/// <summary>
/// The operator operations on clinics. Each works across the clinic boundary through the
/// operator connection and is one of the declared crossings; register, rename, list and update
/// record who used them. A refused short name or display name is reported as a validation refusal
/// with field codes (<c>REQUIRED</c>, <c>TOO_SHORT</c>, <c>TOO_LONG</c>, <c>INVALID_FORMAT</c>,
/// <c>RESERVED</c>, <c>TAKEN</c>).
/// </summary>
public interface IClinicRegistry
{
    /// <summary>Creates an active clinic with its first short name and returns its id (spec 002; the seed).</summary>
    Task<Guid> RegisterAsync(string shortName, string nameAr, string nameEn, string operatorId, CancellationToken cancellation);

    /// <summary>
    /// One add of a clinic at a time on the whole platform (spec 015, research R11): held until
    /// disposed. A turn not given within 15 seconds is refused with <c>LIMIT_BUSY</c>.
    /// </summary>
    Task<IAsyncDisposable> TakeAddTurnAsync(CancellationToken cancellation);

    /// <summary>
    /// Registers a clinic as an operator adds it (spec 015, FR-004): its profile, a slug derived
    /// from its names and numbered on a clash, and the ready roles — in <c>setting-up</c>, seen by
    /// nobody until <see cref="ActivateAsync"/>. Its fields are refused with field codes.
    /// </summary>
    Task<RegisteredClinic> RegisterAsync(ClinicRegistration registration, CancellationToken cancellation);

    /// <summary>
    /// Makes a clinic being set up visible and records <c>clinic.created</c> in its trail and the
    /// platform's, in one transaction (research R2, stage 3).
    /// </summary>
    Task ActivateAsync(Guid clinicId, ClinicCreation creation, CancellationToken cancellation);

    /// <summary>Gives up an add that failed part-way: the clinic is never seen and its name never reused.</summary>
    Task DiscardAsync(Guid clinicId, string operatorId, CancellationToken cancellation);

    /// <summary>
    /// Checks an operator's edit without writing anything: refuses with
    /// <c>CLINIC_CHANGED_ELSEWHERE</c> when the clinic changed since <paramref name="version"/> was
    /// read and with <c>NOT_FOUND</c> when it is not visible, and adds each field it would refuse to
    /// <paramref name="errors"/>, so that the caller can refuse the whole edit before any part of it
    /// is written (spec 015, FR-021).
    /// </summary>
    Task CheckProfileAsync(
        Guid clinicId, byte[] version, ClinicProfileChange change, Dictionary<string, string[]> errors, CancellationToken cancellation);

    /// <summary>
    /// Applies an operator's edit and records <c>clinic.updated</c> with each changed field in the
    /// clinic's trail and the platform's, in one transaction. Nothing changed records nothing; but
    /// when <paramref name="ownerChanged"/>, the clinic's version still moves, so that a form read
    /// before the owner's change is refused (FR-021). Returns the clinic's new version.
    /// </summary>
    Task<byte[]> UpdateProfileAsync(
        Guid clinicId, byte[] version, ClinicProfileChange change, bool ownerChanged, CancellationToken cancellation);

    /// <summary>
    /// Sets the clinic's account manager, or none, and records <c>clinic.account-manager-assigned</c>
    /// with the names before and after. Returns the clinic's new version.
    /// </summary>
    Task<byte[]> AssignAccountManagerAsync(
        Guid clinicId, byte[] version, Guid? accountManagerId, string? nameBefore, string? nameAfter, string operatorId,
        CancellationToken cancellation);

    /// <summary>
    /// Gives a clinic a new current short name. Every earlier name keeps leading to it and is
    /// never available to another clinic.
    /// </summary>
    Task RenameAsync(Guid clinicId, string newShortName, string operatorId, CancellationToken cancellation);

    /// <summary>Every visible clinic, oldest first: never one being set up or discarded.</summary>
    Task<IReadOnlyList<ClinicListItem>> ListAsync(string operatorId, CancellationToken cancellation);

    /// <summary>Clinic ids only, so that a per-clinic job can take clinics one by one.</summary>
    Task<IReadOnlyList<Guid>> ListClinicIdsForJobsAsync(CancellationToken cancellation);
}
