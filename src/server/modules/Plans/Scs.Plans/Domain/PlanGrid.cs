namespace Scs.Plans.Domain;

/// <summary>What the grid's check needs of a catalogue feature.</summary>
internal sealed record GridFeature(string Key, bool IsLimit, bool IsSold, bool IsRetired);

/// <summary>A row a save sends: a feature switched on, and for a limit its number or none.</summary>
internal sealed record GridRow(string? Key, int? Limit);

/// <summary>
/// The check's answer: <see cref="Errors"/> for what a field can show (a row's key or limit), or
/// one <see cref="Refusal"/> for the plan as a whole; both empty when the grid can be written.
/// </summary>
internal sealed record GridVerdict(string? Refusal, IReadOnlyDictionary<string, string[]> Errors);

/// <summary>
/// A plan's grid checked against the catalogue as it is, before anything is written — refused,
/// never corrected (spec 013, FR-017; ق-٤٢). A feature not sold or retired is taken only by a plan
/// that already has it (ق-٤٨, clarified).
/// </summary>
internal static class PlanGrid
{
    public const string FeatureUnknown = "FEATURE_UNKNOWN";
    public const string SwitchHasLimit = "SWITCH_HAS_LIMIT";
    public const string FeatureNotSold = "FEATURE_NOT_SOLD";

    public static GridVerdict Check(
        IReadOnlyList<GridRow> rows, IReadOnlyDictionary<string, GridFeature> catalogue, IReadOnlySet<string> already)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(already);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? refusal = null;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? new GridRow(null, null);
            if (string.IsNullOrEmpty(row.Key))
            {
                errors[$"features[{i}].key"] = [PlanRules.Required];
                continue;
            }

            if (!seen.Add(row.Key))
            {
                errors[$"features[{i}].key"] = [PlanRules.Invalid];
                continue;
            }

            if (!PlanRules.IsLimit(row.Limit))
            {
                errors[$"features[{i}].limit"] = [PlanRules.Invalid];
            }

            if (!catalogue.TryGetValue(row.Key, out var feature))
            {
                refusal ??= FeatureUnknown;
                continue;
            }

            if (row.Limit is not null && !feature.IsLimit)
            {
                refusal ??= SwitchHasLimit;
            }

            if ((!feature.IsSold || feature.IsRetired) && !already.Contains(row.Key))
            {
                refusal ??= FeatureNotSold;
            }
        }

        return new GridVerdict(refusal, errors);
    }
}
