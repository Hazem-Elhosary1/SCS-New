namespace Scs.Tenancy.Contracts;

/// <summary>What another module may know about the clinic the current work is for.</summary>
/// <param name="Specialty">One of the fifteen specialties (SH-013), or null when none is recorded.</param>
/// <param name="Address">Null for a clinic that predates spec 015.</param>
/// <param name="TaxNumber"><c>NNN-NNN-NNN</c>, or null.</param>
/// <param name="DefaultLanguage"><c>ar</c> or <c>en</c>.</param>
/// <param name="AccountManagerId">The operator who looks after the clinic, or null.</param>
/// <param name="CreatedBy">The operator id that added it, as text, or null.</param>
/// <param name="ShortName">Its current slug, for the public booking page.</param>
/// <param name="Version">The row's version, for an edit made from this read.</param>
public sealed record ClinicProfile(
    Guid Id,
    string NameAr,
    string NameEn,
    string State,
    string? Specialty = null,
    string? Address = null,
    string? TaxNumber = null,
    string DefaultLanguage = "ar",
    Guid? AccountManagerId = null,
    string? CreatedBy = null,
    DateTimeOffset CreatedAtUtc = default,
    string? ShortName = null,
    byte[]? Version = null);

/// <summary>
/// The established clinic's own names and state, read under isolation. Other modules use this
/// instead of reading the clinic table (spec 003 needs the state at sign-in). With no clinic
/// established it refuses with <c>CLINIC_REQUIRED</c>.
/// </summary>
public interface IClinicProfile
{
    Task<ClinicProfile> GetOwnAsync(CancellationToken cancellation);
}
