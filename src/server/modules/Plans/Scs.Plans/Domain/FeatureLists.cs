namespace Scs.Plans.Domain;

/// <summary>The seven groups of the catalogue, in their order (spec 012, FR-009; BRD §3-6).</summary>
internal static class FeatureGroups
{
    public const string Core = "core";
    public const string Channels = "channels";
    public const string Reports = "reports";
    public const string Money = "money";
    public const string Clinical = "clinical";
    public const string Ai = "ai";
    public const string Growth = "growth";

    public static IReadOnlyList<string> All { get; } = [Core, Channels, Reports, Money, Clinical, Ai, Growth];

    public static bool IsKnown(string? group) => group is not null && All.Contains(group, StringComparer.Ordinal);

    /// <summary>Where the group stands in the catalogue's order.</summary>
    public static int IndexOf(string group) => All.ToList().IndexOf(group);
}

/// <summary>A switch is included or not; a limit counts something, in its unit.</summary>
internal static class FeatureKinds
{
    public const string Switch = "switch";
    public const string Limit = "limit";

    public static IReadOnlyList<string> All { get; } = [Switch, Limit];
}

/// <summary>Whether the capability is in the product yet. A spec that builds one marks it built (FR-024).</summary>
internal static class FeatureStatuses
{
    public const string Built = "built";
    public const string Upcoming = "upcoming";

    public static IReadOnlyList<string> All { get; } = [Built, Upcoming];
}

/// <summary>The fifteen specialties, in their order (SH-013, FR-013). The clinic's own is spec 015's.</summary>
internal static class Specialties
{
    public const string Dental = "dental";

    public static IReadOnlyList<string> All { get; } =
    [
        Dental, "orthopedics", "neurology", "pediatrics", "internal_medicine", "general_surgery", "dermatology", "obgyn",
        "ophthalmology", "ent", "urology", "cardiology", "psychiatry", "nutrition", "physiotherapy",
    ];

    public static bool IsKnown(string? specialty) => specialty is not null && All.Contains(specialty, StringComparer.Ordinal);
}
