using System.Text.RegularExpressions;

namespace Scs.Plans.Domain;

/// <summary>What an operator gives a feature, adding or editing it; the key is apart, given once.</summary>
internal sealed record FeatureFields(
    string? NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Group,
    string? Kind,
    string? UnitAr,
    string? UnitEn,
    int? Baseline,
    string? Specialty,
    string? Status,
    bool IsSold);

/// <summary>
/// The rules of a feature (spec 012, FR-009 to FR-012; data-model.md). <see cref="Check"/> names
/// each field that is wrong with the platform's field words; <see cref="Shape"/> says whether a
/// switch or a limit carries what it should. The database holds the same rules again.
/// </summary>
internal static partial class FeatureRules
{
    public const string Required = "REQUIRED";
    public const string TooLong = "TOO_LONG";
    public const string Invalid = "INVALID";

    /// <summary>A limit without both units.</summary>
    public const string UnitsRequired = "UNITS_REQUIRED";

    /// <summary>A switch with a unit or a baseline.</summary>
    public const string SwitchHasLimit = "SWITCH_HAS_LIMIT";

    public const int KeyMinLength = 2;
    public const int KeyMaxLength = 64;
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int UnitMaxLength = 40;
    public const int BaselineMax = 1_000_000;

    /// <summary>The gap between two features of a group: the next is the group's last plus this (FR-011).</summary>
    public const int OrderStep = 10;

    /// <summary>The limits counted each month, from a fixed list and never a field (FR-012; §3-6).</summary>
    public static IReadOnlyList<string> MonthlyKeys { get; } = ["message_credits", "ai_xray_analysis", "ai_voice_notes", "teleconsult_minutes"];

    /// <summary>The one feature the clinic's specialty decides: its type reads «يتبع التخصص».</summary>
    public const string SpecialtyDecidedKey = "specialty_exam_modules";

    public static bool IsMonthly(string key) => MonthlyKeys.Contains(key, StringComparer.Ordinal);

    [GeneratedRegex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public static bool IsKey(string? key) =>
        key is { Length: >= KeyMinLength and <= KeyMaxLength } && KeyPattern().IsMatch(key);

    /// <summary>Each field that is wrong, with its word. A key is asked only when adding.</summary>
    public static IReadOnlyDictionary<string, string[]> Check(string? key, FeatureFields fields, bool editing = false)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!editing)
        {
            if (string.IsNullOrEmpty(key))
            {
                errors["key"] = [Required];
            }
            else if (!IsKey(key))
            {
                errors["key"] = [Invalid];
            }
        }

        Text(errors, "nameAr", fields.NameAr, NameMaxLength, required: true);
        Text(errors, "nameEn", fields.NameEn, NameMaxLength, required: true);
        Text(errors, "descriptionAr", fields.DescriptionAr, DescriptionMaxLength, required: false);
        Text(errors, "descriptionEn", fields.DescriptionEn, DescriptionMaxLength, required: false);
        Text(errors, "unitAr", fields.UnitAr, UnitMaxLength, required: false);
        Text(errors, "unitEn", fields.UnitEn, UnitMaxLength, required: false);

        OneOf(errors, "group", fields.Group, FeatureGroups.All, required: true);
        OneOf(errors, "kind", fields.Kind, FeatureKinds.All, required: true);
        OneOf(errors, "status", fields.Status, FeatureStatuses.All, required: true);
        OneOf(errors, "specialty", fields.Specialty, Specialties.All, required: false);

        if (fields.Baseline is < 0 or > BaselineMax)
        {
            errors["baseline"] = [Invalid];
        }

        return errors;
    }

    /// <summary>Null when a limit has both units and a switch has neither unit nor baseline (FR-010).</summary>
    public static string? Shape(FeatureFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var hasAr = !string.IsNullOrWhiteSpace(fields.UnitAr);
        var hasEn = !string.IsNullOrWhiteSpace(fields.UnitEn);

        return fields.Kind switch
        {
            FeatureKinds.Limit when !hasAr || !hasEn => UnitsRequired,
            FeatureKinds.Switch when hasAr || hasEn || fields.Baseline is not null => SwitchHasLimit,
            _ => null,
        };
    }

    private static void Text(Dictionary<string, string[]> errors, string field, string? value, int max, bool required)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0 && required)
        {
            errors[field] = [Required];
        }
        else if (trimmed.Length > max)
        {
            errors[field] = [TooLong];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> list, bool required)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (required)
            {
                errors[field] = [Required];
            }
        }
        else if (!list.Contains(value, StringComparer.Ordinal))
        {
            errors[field] = [Invalid];
        }
    }
}

/// <summary>
/// A capability a plan can be made of (spec 012; BRD module 16 §3-6): a switch or a limit, in one
/// of seven groups, for every specialty or one, built or upcoming, sold in plans or not. Its key
/// is its name everywhere — plans, the server, the audit trail — and never changes once made
/// (ق-٤٦). A retired feature stays: plans that hold it keep it (ق-٤٨), and it can be sold again.
/// </summary>
internal sealed class Feature
{
    private Feature()
    {
    }

    public string Key { get; private set; } = "";

    public string NameAr { get; private set; } = "";

    public string NameEn { get; private set; } = "";

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    public string Group { get; private set; } = "";

    public string Kind { get; private set; } = "";

    public string? UnitAr { get; private set; }

    public string? UnitEn { get; private set; }

    /// <summary>What a clinic has when its plan leaves the limit out; null for no limit.</summary>
    public int? Baseline { get; private set; }

    /// <summary>One of the fifteen, or null for every specialty.</summary>
    public string? Specialty { get; private set; }

    public string Status { get; private set; } = FeatureStatuses.Upcoming;

    public bool IsSold { get; private set; } = true;

    public bool IsRetired { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Raised by every edit, not by retiring or selling again: an edit open across a retire still saves.</summary>
    public int Version { get; private set; } = 1;

    public bool IsMonthly => FeatureRules.IsMonthly(Key);

    public static Feature Create(string key, FeatureFields fields, int sortOrder)
    {
        if (!FeatureRules.IsKey(key))
        {
            throw new ArgumentException("A feature's key has 2 to 64 lower-case letters, digits and underscores, from a letter.", nameof(key));
        }

        var feature = new Feature { Key = key };
        feature.Take(fields, sortOrder);
        return feature;
    }

    /// <param name="sortOrder">The new order when the group changes; null keeps the one it has.</param>
    public void Edit(FeatureFields fields, int? sortOrder)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (IsRetired && !fields.IsSold)
        {
            throw new InvalidOperationException("A retired feature stays sold: sell it again, or leave it retired.");
        }

        Take(fields, sortOrder ?? SortOrder);
        Version++;
    }

    /// <summary>No new plan can take it; plans that hold it keep it (FR-021).</summary>
    public void Retire()
    {
        if (!IsSold || IsRetired)
        {
            throw new InvalidOperationException("Only a sold feature that is not retired is retired.");
        }

        IsRetired = true;
    }

    /// <summary>Offered to every plan again (FR-021a).</summary>
    public void Resume()
    {
        if (!IsRetired)
        {
            throw new InvalidOperationException("Only a retired feature is sold again.");
        }

        IsRetired = false;
    }

    private void Take(FeatureFields fields, int sortOrder)
    {
        if (FeatureRules.Check(Key, fields).Count > 0 || FeatureRules.Shape(fields) is not null)
        {
            throw new ArgumentException("A feature's fields break its rules.", nameof(fields));
        }

        NameAr = fields.NameAr!.Trim();
        NameEn = fields.NameEn!.Trim();
        DescriptionAr = Words(fields.DescriptionAr);
        DescriptionEn = Words(fields.DescriptionEn);
        Group = fields.Group!;
        Kind = fields.Kind!;
        UnitAr = Words(fields.UnitAr);
        UnitEn = Words(fields.UnitEn);
        Baseline = fields.Baseline;
        Specialty = string.IsNullOrEmpty(fields.Specialty) ? null : fields.Specialty;
        Status = fields.Status!;
        IsSold = fields.IsSold;
        SortOrder = sortOrder;
    }

    private static string? Words(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
