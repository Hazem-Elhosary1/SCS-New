namespace Scs.Tenancy.Contracts;

/// <summary>
/// The stored states of a clinic. Spec 016 (clinic lifecycle) owns the full list and what each
/// state permits; spec 002 uses only the distinction below, and spec 015 adds the two states of an
/// add that nobody sees.
/// </summary>
public static class ClinicStates
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";

    /// <summary>Registered by an add that is not complete yet (spec 015, research R2).</summary>
    public const string SettingUp = "setting-up";

    /// <summary>An add that failed part-way and was given up (spec 015, research R2).</summary>
    public const string Discarded = "discarded";

    /// <summary>
    /// States in which the patient address answers exactly as it does for an unknown clinic
    /// (spec 002, FR-033), and which spec 015's operators never see.
    /// </summary>
    public static IReadOnlyList<string> NotPubliclyAvailable { get; } = [Suspended, Cancelled, SettingUp, Discarded];

    /// <summary>States an operator never sees: the clinic is not there for anyone (spec 015).</summary>
    public static IReadOnlyList<string> Invisible { get; } = [SettingUp, Discarded];
}
