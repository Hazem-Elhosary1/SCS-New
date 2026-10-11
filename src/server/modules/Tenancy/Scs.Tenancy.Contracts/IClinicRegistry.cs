namespace Scs.Tenancy.Contracts;

/// <summary>A clinic as operators see it.</summary>
public sealed record ClinicListItem(Guid Id, string ShortName, string NameAr, string NameEn, string State, DateTimeOffset CreatedAtUtc);

/// <summary>What a patient address resolves to (contracts/tenancy-api.openapi.yaml).</summary>
public sealed record ClinicSummary(string ShortName, string NameAr, string NameEn, string State, bool Renamed);

/// <summary>
/// The operator operations on clinics. Each works across the clinic boundary through the
/// operator connection and is one of the declared crossings; register, rename and list record
/// who used them. A refused short name or display name is reported as a validation refusal with
/// field codes (<c>REQUIRED</c>, <c>TOO_SHORT</c>, <c>TOO_LONG</c>, <c>INVALID_FORMAT</c>,
/// <c>RESERVED</c>, <c>TAKEN</c>).
/// </summary>
public interface IClinicRegistry
{
    /// <summary>Creates a clinic with its first short name and returns its id.</summary>
    Task<Guid> RegisterAsync(string shortName, string nameAr, string nameEn, string operatorId, CancellationToken cancellation);

    /// <summary>
    /// Gives a clinic a new current short name. Every earlier name keeps leading to it and is
    /// never available to another clinic.
    /// </summary>
    Task RenameAsync(Guid clinicId, string newShortName, string operatorId, CancellationToken cancellation);

    Task<IReadOnlyList<ClinicListItem>> ListAsync(string operatorId, CancellationToken cancellation);

    /// <summary>Clinic ids only, so that a per-clinic job can take clinics one by one.</summary>
    Task<IReadOnlyList<Guid>> ListClinicIdsForJobsAsync(CancellationToken cancellation);
}
