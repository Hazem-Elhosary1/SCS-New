namespace Scs.SharedKernel.Access;

/// <summary>A permission held without the one it requires.</summary>
public sealed record MissingRequirement(string Permission, string Requires);

/// <summary>What is wrong with a set of permissions; nothing, when it may be stored.</summary>
/// <param name="Codes">The kinds of fault found, as field codes.</param>
/// <param name="Missing">Each permission held without the one it requires.</param>
/// <param name="Unknown">The codes given that are not in the catalogue, once each, in the order given.</param>
/// <param name="Repeated">The codes given more than once, once each, in the order they were first repeated.</param>
public sealed record PermissionSetProblems(
    IReadOnlyList<string> Codes,
    IReadOnlyList<MissingRequirement> Missing,
    IReadOnlyList<string> Unknown,
    IReadOnlyList<string> Repeated)
{
    public bool None => Codes.Count == 0;
}

/// <summary>
/// The one rule a set of permissions must keep to be a role (SH-054, ق-31): every permission in
/// it has the permission it directly requires beside it. One step only — a requirement that
/// itself requires another is covered because that permission is in the set too and is checked
/// the same way (CL-AD-089).
/// </summary>
public static class PermissionSets
{
    public const string Required = "REQUIRED";
    public const string Unknown = "UNKNOWN";
    public const string Duplicate = "DUPLICATE";
    public const string MissingRequirement = "MISSING_REQUIREMENT";

    public static PermissionSetProblems Check(IEnumerable<string>? codes)
    {
        var given = codes?.ToList() ?? [];
        var problems = new List<string>();
        var missing = new List<MissingRequirement>();

        if (given.Count == 0)
        {
            return new PermissionSetProblems([Required], missing, [], []);
        }

        var held = new HashSet<string>(StringComparer.Ordinal);
        var repeated = new List<string>();
        var unknown = new List<string>();

        foreach (var code in given)
        {
            if (!held.Add(code))
            {
                if (!repeated.Contains(code, StringComparer.Ordinal))
                {
                    repeated.Add(code);
                }
            }
            else if (!PermissionCatalogue.Contains(code))
            {
                unknown.Add(code);
            }
        }

        if (unknown.Count > 0)
        {
            problems.Add(Unknown);
        }

        if (repeated.Count > 0)
        {
            problems.Add(Duplicate);
        }

        foreach (var permission in PermissionCatalogue.All)
        {
            if (held.Contains(permission.Code) && permission.Requires is { } needed && !held.Contains(needed))
            {
                missing.Add(new MissingRequirement(permission.Code, needed));
            }
        }

        if (missing.Count > 0)
        {
            problems.Add(MissingRequirement);
        }

        return new PermissionSetProblems(problems, missing, unknown, repeated);
    }
}
