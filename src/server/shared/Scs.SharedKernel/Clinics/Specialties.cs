namespace Scs.SharedKernel.Clinics;

/// <summary>
/// The fifteen specialties, in the BRD's order (SH-013): a fixed list, not catalogue data. A plan's
/// feature may follow one (spec 012); a clinic has one, set at its creation (spec 015).
/// </summary>
public static class Specialties
{
    public const string Dental = "dental";

    public static IReadOnlyList<string> All { get; } =
    [
        Dental, "orthopedics", "neurology", "pediatrics", "internal_medicine", "general_surgery", "dermatology", "obgyn",
        "ophthalmology", "ent", "urology", "cardiology", "psychiatry", "nutrition", "physiotherapy",
    ];

    public static bool IsKnown(string? specialty) => specialty is not null && All.Contains(specialty, StringComparer.Ordinal);
}
