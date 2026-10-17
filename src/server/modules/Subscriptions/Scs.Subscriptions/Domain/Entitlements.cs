namespace Scs.Subscriptions.Domain;

/// <summary>What a plan holds of one feature (spec 013): included, with a number or none.</summary>
public sealed record PlanRow(string Key, int? Limit);

/// <summary>A clinic's plan, as the rules need it; null for no plan.</summary>
public sealed record PlanGrid(Guid Id, string NameAr, string NameEn, IReadOnlyList<PlanRow> Rows);

/// <summary>One feature of the catalogue (spec 012), as the rules need it.</summary>
public sealed record CatalogueRow(
    string Key, string Kind, string NameAr, string NameEn, string? UnitAr, string? UnitEn,
    int? Baseline, string? Specialty, bool Built, bool Monthly);

/// <summary>What the clinic has of one feature.</summary>
public sealed record Entitled(
    string Key, string Kind, bool Included, int? Limit, bool Enforced, bool Monthly,
    string NameAr, string NameEn, string? UnitAr, string? UnitEn);

/// <summary>
/// The BRD's rules for what a clinic has (spec 014, research R4), in one place every check uses:
/// ق-٣٧ a specialty narrows a plan, never widens it; ق-٣٨ an included limit with no number has
/// no limit, a limit left out takes the feature's baseline; ق-٤٩ an upcoming feature is enforced
/// by nothing; a disabled plan (ق-٤٧) and a retired feature (ق-٤٨) keep applying, so neither is
/// looked at here. A key the catalogue does not know is answered by the caller (ق-٣٩).
/// </summary>
public static class Entitlements
{
    public const string CoreClinic = "core_clinic";
    public const string SpecialtyDecided = "specialty_exam_modules";
    public const string Switch = "switch";
    public const string Limit = "limit";

    public static IReadOnlyDictionary<string, Entitled> Work(
        PlanGrid? plan, IReadOnlyList<CatalogueRow> catalogue, string? specialty)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        var inPlan = (plan?.Rows ?? []).ToDictionary(r => r.Key, StringComparer.Ordinal);
        var answer = new Dictionary<string, Entitled>(StringComparer.Ordinal);

        foreach (var feature in catalogue)
        {
            var held = inPlan.TryGetValue(feature.Key, out var row);
            var narrowedOut = feature.Specialty is not null
                              && !string.Equals(feature.Specialty, specialty, StringComparison.Ordinal);

            bool included;
            int? limit;
            if (feature.Kind == Switch)
            {
                // The core is every clinic's; the exam modules follow a specialty the clinic must have.
                included = feature.Key == CoreClinic
                           || (held && !narrowedOut && (feature.Key != SpecialtyDecided || specialty is not null));
                limit = null;
            }
            else
            {
                // A limit is always "included": its number says how much. A switch the plan once
                // gave a number to, now a limit, reads as no limit until the plan is saved (013).
                included = true;
                limit = narrowedOut ? 0 : held ? row!.Limit : feature.Baseline;
            }

            answer[feature.Key] = new Entitled(
                feature.Key, feature.Kind, included, limit, Enforced: feature.Built, feature.Monthly,
                feature.NameAr, feature.NameEn, feature.UnitAr, feature.UnitEn);
        }

        return answer;
    }
}
