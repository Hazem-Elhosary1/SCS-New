namespace Scs.Tenancy.Contracts;

/// <summary>
/// The stored states of a clinic. Spec 016 (clinic lifecycle) owns the full list and what each
/// state permits; spec 002 uses only the distinction below.
/// </summary>
public static class ClinicStates
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";

    /// <summary>
    /// States in which the patient address answers exactly as it does for an unknown clinic
    /// (spec 002, FR-033).
    /// </summary>
    public static IReadOnlyList<string> NotPubliclyAvailable { get; } = [Suspended, Cancelled];
}
