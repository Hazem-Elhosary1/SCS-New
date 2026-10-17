using System.Text;

namespace Scs.Plans.Domain;

/// <summary>What an operator gives a plan, adding or editing it. The code is derived once and is not a field.</summary>
internal sealed record PlanFields(
    string? NameAr,
    string? NameEn,
    string? Description,
    decimal? MonthlyEgp,
    decimal? AnnualEgp,
    decimal? MonthlyUsd,
    decimal? AnnualUsd,
    string? Badge);

/// <summary>
/// The rules of a plan (spec 013, FR-010 to FR-013; data-model.md). <see cref="Check"/> names each
/// field that is wrong with the platform's field words. The database holds the same rules again.
/// </summary>
internal static class PlanRules
{
    public const string Required = "REQUIRED";
    public const string TooLong = "TOO_LONG";
    public const string Invalid = "INVALID";

    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int ReasonMaxLength = 500;
    public const int NoteMaxLength = 500;

    /// <summary>A name's code (≤ 100) and room for its number.</summary>
    public const int CodeMaxLength = 110;

    /// <summary>The fallback code when the English name leaves nothing (research R3).</summary>
    public const string FallbackCode = "plan";

    public const decimal AmountMax = 9_999_999_999.99m;
    public const int LimitMin = 1;
    public const int LimitMax = 1_000_000;

    /// <summary>
    /// The English name lower-cased, each run of characters other than a–z and 0–9 one hyphen, no
    /// hyphen at either end; <see cref="FallbackCode"/> when nothing is left (OP-A-194).
    /// </summary>
    public static string BaseCode(string? nameEn)
    {
        var code = new StringBuilder();
        var gap = false;
        foreach (var c in (nameEn ?? "").ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (gap && code.Length > 0)
                {
                    code.Append('-');
                }

                code.Append(c);
                gap = false;
            }
            else
            {
                gap = true;
            }
        }

        var text = code.Length > NameMaxLength ? code.ToString(0, NameMaxLength).TrimEnd('-') : code.ToString();
        return text.Length == 0 ? FallbackCode : text;
    }

    /// <summary>The code itself when free, else the first free of <c>-2</c>, <c>-3</c> ….</summary>
    public static string FreeCode(string baseCode, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(baseCode);
        var used = new HashSet<string>(taken ?? [], StringComparer.Ordinal);
        if (!used.Contains(baseCode))
        {
            return baseCode;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseCode}-{n}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    public static bool IsAmount(decimal value) => value is >= 0 and <= AmountMax && decimal.Round(value, 2) == value;

    public static bool IsLimit(int? limit) => limit is null or >= LimitMin and <= LimitMax;

    public static bool IsReason(string? reason) => (reason ?? "").Trim().Length <= ReasonMaxLength;

    public static bool IsNote(string? note) => (note ?? "").Trim().Length <= NoteMaxLength;

    /// <summary>A note or reason as kept: trimmed, null when empty.</summary>
    public static string? Note(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Each field that is wrong, with its word.</summary>
    public static IReadOnlyDictionary<string, string[]> Check(PlanFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        Text(errors, "nameAr", fields.NameAr, NameMaxLength);
        Text(errors, "nameEn", fields.NameEn, NameMaxLength);
        Text(errors, "description", fields.Description, DescriptionMaxLength);
        Amount(errors, "monthlyEgp", fields.MonthlyEgp);
        Amount(errors, "annualEgp", fields.AnnualEgp);
        Amount(errors, "monthlyUsd", fields.MonthlyUsd);
        Amount(errors, "annualUsd", fields.AnnualUsd);

        if (!string.IsNullOrEmpty(fields.Badge) && !PlanBadges.All.Contains(fields.Badge, StringComparer.Ordinal))
        {
            errors["badge"] = [Invalid];
        }

        return errors;
    }

    private static void Text(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            errors[field] = [Required];
        }
        else if (trimmed.Length > max)
        {
            errors[field] = [TooLong];
        }
    }

    private static void Amount(Dictionary<string, string[]> errors, string field, decimal? value)
    {
        if (value is null)
        {
            errors[field] = [Required];
        }
        else if (!IsAmount(value.Value))
        {
            errors[field] = [Invalid];
        }
    }
}

/// <summary>A plan's row for one feature of the catalogue: included, and for a limit its number or none.</summary>
internal sealed class PlanFeature
{
    private PlanFeature()
    {
    }

    public PlanFeature(Guid planId, string featureKey, int? limit)
    {
        PlanId = planId;
        FeatureKey = featureKey;
        Limit = limit;
    }

    public Guid PlanId { get; private set; }

    public string FeatureKey { get; private set; } = "";

    /// <summary>1–1,000,000 for a limit, null for no limit; null for a switch.</summary>
    public int? Limit { get; set; }
}

/// <summary>
/// A plan clinics subscribe to (spec 013; BRD module 16 §3-5): its names and description, its
/// prices before tax in Egyptian pounds and in US dollars, an optional badge, active or disabled,
/// and what it includes from the catalogue. Its code is derived once and never changes (ق-٤٦). A
/// plan is never deleted (FR-026): disabling replaces it, and its clinics keep it (ق-٤٧).
/// </summary>
internal sealed class Plan
{
    private readonly List<PlanFeature> features = [];

    private Plan()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = "";

    public string NameAr { get; private set; } = "";

    public string NameEn { get; private set; } = "";

    public string Description { get; private set; } = "";

    public decimal MonthlyEgp { get; private set; }

    /// <summary>0 means no annual price: a year is twelve monthly prices (Q9).</summary>
    public decimal AnnualEgp { get; private set; }

    /// <summary>Kept and shown; no clinic is billed in dollars (OP-A-207).</summary>
    public decimal MonthlyUsd { get; private set; }

    public decimal AnnualUsd { get; private set; }

    /// <summary>One of <see cref="PlanBadges"/>, or null.</summary>
    public string? Badge { get; private set; }

    public string State { get; private set; } = PlanStates.Active;

    public string? DisabledReason { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Raised by a save and when the badge is taken away; not by disabling or enabling (research R5).</summary>
    public int Version { get; private set; } = 1;

    public IReadOnlyCollection<PlanFeature> Features => features;

    public bool IsActive => State == PlanStates.Active;

    public static Plan Create(Guid id, string code, PlanFields fields, DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A plan's id is made by the server.", nameof(id));
        }

        if (string.IsNullOrEmpty(code) || code.Length > PlanRules.CodeMaxLength)
        {
            throw new ArgumentException("A plan's code is derived from its English name.", nameof(code));
        }

        var plan = new Plan { Id = id, Code = code, CreatedAtUtc = now };
        plan.Take(fields, now);
        return plan;
    }

    public void Edit(PlanFields fields, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (!IsActive && !string.IsNullOrEmpty(fields.Badge))
        {
            throw new InvalidOperationException("A disabled plan holds no badge.");
        }

        Take(fields, now);
        Version++;
    }

    /// <summary>Another plan's save took the badge: this plan's editors must see it (research R5).</summary>
    public void LoseBadge(DateTimeOffset now)
    {
        Badge = null;
        UpdatedAtUtc = now;
        Version++;
    }

    /// <summary>Makes the grid exactly the rows given: updates kept rows, removes the rest, adds the new (ق-٤٣).</summary>
    public void ReplaceFeatures(IEnumerable<(string Key, int? Limit)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var wanted = rows.ToDictionary(r => r.Key, r => r.Limit, StringComparer.Ordinal);

        features.RemoveAll(f => !wanted.ContainsKey(f.FeatureKey));
        foreach (var row in features)
        {
            row.Limit = wanted[row.FeatureKey];
        }

        foreach (var (key, limit) in wanted)
        {
            if (!features.Exists(f => f.FeatureKey == key))
            {
                features.Add(new PlanFeature(Id, key, limit));
            }
        }
    }

    /// <summary>True when it changed; a disabled plan keeps its first reason and time (§4-2).</summary>
    public bool Disable(string? reason, DateTimeOffset now)
    {
        if (!PlanRules.IsReason(reason))
        {
            throw new ArgumentException("A reason has up to 500 characters.", nameof(reason));
        }

        if (!IsActive)
        {
            return false;
        }

        State = PlanStates.Disabled;
        DisabledReason = PlanRules.Note(reason);
        DisabledAtUtc = now;
        Badge = null;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>True when it changed; the badge does not come back (ق-٤٥).</summary>
    public bool Enable(DateTimeOffset now)
    {
        if (IsActive)
        {
            return false;
        }

        State = PlanStates.Active;
        DisabledReason = null;
        DisabledAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }

    private void Take(PlanFields fields, DateTimeOffset now)
    {
        if (PlanRules.Check(fields).Count > 0)
        {
            throw new ArgumentException("A plan's fields break its rules.", nameof(fields));
        }

        NameAr = fields.NameAr!.Trim();
        NameEn = fields.NameEn!.Trim();
        Description = fields.Description!.Trim();
        MonthlyEgp = fields.MonthlyEgp!.Value;
        AnnualEgp = fields.AnnualEgp!.Value;
        MonthlyUsd = fields.MonthlyUsd!.Value;
        AnnualUsd = fields.AnnualUsd!.Value;
        Badge = string.IsNullOrEmpty(fields.Badge) ? null : fields.Badge;
        UpdatedAtUtc = now;
    }
}
