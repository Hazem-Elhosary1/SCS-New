namespace Scs.Tenancy.Contracts;

/// <summary>What another module may know about the clinic the current work is for.</summary>
/// <param name="Specialty">One of the fifteen specialties (SH-013), or null until clinic creation sets it (spec 014).</param>
public sealed record ClinicProfile(Guid Id, string NameAr, string NameEn, string State, string? Specialty = null);

/// <summary>
/// The established clinic's own names and state, read under isolation. Other modules use this
/// instead of reading the clinic table (spec 003 needs the state at sign-in). With no clinic
/// established it refuses with <c>CLINIC_REQUIRED</c>.
/// </summary>
public interface IClinicProfile
{
    Task<ClinicProfile> GetOwnAsync(CancellationToken cancellation);
}
