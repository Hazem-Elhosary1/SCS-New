namespace Scs.Subscriptions.Contracts;

/// <summary>
/// What the established clinic has (spec 014, FR-002; ق-٣٦): the one answer every check asks,
/// worked out from the clinic's plan, the feature catalogue and the clinic's specialty, once per
/// request and never kept longer — a plan saved by an operator applies to the very next check.
/// With no clinic established it refuses with <c>CLINIC_REQUIRED</c>.
/// </summary>
public interface IClinicEntitlements
{
    Task<ClinicEntitlements> CurrentAsync(CancellationToken cancellation);

    /// <summary>
    /// Refuses with <c>PLAN_FEATURE_NOT_INCLUDED</c> (the feature named) when the feature is built
    /// and the clinic's plan does not include it. An upcoming feature is never refused (ق-٤٩).
    /// </summary>
    Task EnsureIncludedAsync(string featureKey, CancellationToken cancellation);

    /// <summary>The limit: null for no limit (or a feature nothing enforces yet); 0 for a key the catalogue does not know (ق-٣٩).</summary>
    Task<int?> LimitOfAsync(string featureKey, CancellationToken cancellation);
}

/// <param name="PlanId">Null when the clinic has no plan: then it has the catalogue's baseline only.</param>
public sealed record ClinicEntitlements(
    Guid? PlanId, string? PlanNameAr, string? PlanNameEn, IReadOnlyDictionary<string, Entitlement> Features)
{
    /// <summary>The answer for a key; one the catalogue does not know is not included and has a limit of 0 (ق-٣٩).</summary>
    public Entitlement Of(string key) =>
        Features.TryGetValue(key, out var found)
            ? found
            : new Entitlement(key, "limit", Included: false, Limit: 0, Enforced: true, Monthly: false, key, key, null, null);

    /// <summary>The built switches the plan does not include: what the clinic console shows as not in the plan.</summary>
    public IReadOnlyList<string> Withheld =>
        [.. Features.Values.Where(f => f is { Kind: "switch", Enforced: true, Included: false }).Select(f => f.Key).Order(StringComparer.Ordinal)];
}

/// <param name="Kind"><c>switch</c> or <c>limit</c>.</param>
/// <param name="Included">A switch: whether the clinic has it. A limit: always true — its <paramref name="Limit"/> says how much.</param>
/// <param name="Limit">A limit's number; null for no limit. Always null for a switch.</param>
/// <param name="Enforced">Built: something refuses it. An upcoming feature is never refused nor shown as not in the plan (ق-٤٩).</param>
/// <param name="Monthly">One of the four limits counted per month (SH-018).</param>
public sealed record Entitlement(
    string Key, string Kind, bool Included, int? Limit, bool Enforced, bool Monthly,
    string NameAr, string NameEn, string? UnitAr, string? UnitEn);
