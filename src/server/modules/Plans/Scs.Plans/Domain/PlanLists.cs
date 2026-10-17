namespace Scs.Plans.Domain;

/// <summary>
/// The badges a plan may carry on its card for clinics (spec 013, ق-٤٥): at most one plan holds
/// one at any time, and a disabled plan none. The operator picks it; nothing computes it (ب-١٠).
/// </summary>
internal static class PlanBadges
{
    /// <summary>«الأفضل» · Best.</summary>
    public const string Best = "best";

    /// <summary>«الأكثر انتشارًا» · Most popular.</summary>
    public const string MostPopular = "most_popular";

    /// <summary>«الأكثر مبيعًا» · Best selling.</summary>
    public const string BestSelling = "best_selling";

    public static IReadOnlyList<string> All { get; } = [Best, MostPopular, BestSelling];
}

/// <summary>An active plan is given to clinics; a disabled one keeps its clinics and takes no new one (§4-2).</summary>
internal static class PlanStates
{
    public const string Active = "active";
    public const string Disabled = "disabled";

    public static IReadOnlyList<string> All { get; } = [Active, Disabled];
}
