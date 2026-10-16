using Scs.SharedKernel.Access;

namespace Scs.Operators.Domain;

/// <summary>Who is granting: the codes their role holds, and whether they are «مسؤول عام».</summary>
internal sealed record Granter(Guid OperatorId, Guid? RoleId, bool IsSuperAdmin, IReadOnlySet<string> Codes);

/// <summary>Why a role may not be given by this person; null when it may.</summary>
internal static class GrantRefusals
{
    /// <summary>The role holds a permission the granter does not.</summary>
    public const string HoldsMore = "holds-more";

    /// <summary>«مسؤول عام» is given only by one who is.</summary>
    public const string SuperAdminOnly = "super-admin-only";
}

/// <summary>
/// The one rule of what may be granted in the operators console (spec 011, FR-011, FR-016 to
/// FR-018, FR-030, FR-037; BRD ق-54, ق-55, ق-63, ق-66). Nobody ends up with a permission the
/// person who granted it did not hold, by any path: inviting, editing an operator, adding,
/// editing or copying a role. Pure: every path asks here, and so does the console's drawing.
/// </summary>
internal static class RoleGrants
{
    /// <summary>May the granter give this role to someone? Null when yes; otherwise the reason.</summary>
    public static string? WhyNotGrantable(Granter granter, string? roleSystemKey, IEnumerable<string> roleCodes)
    {
        ArgumentNullException.ThrowIfNull(granter);
        ArgumentNullException.ThrowIfNull(roleCodes);

        if (granter.IsSuperAdmin)
        {
            return null;
        }

        if (roleSystemKey == OperatorPermissions.SystemRoles.SuperAdmin)
        {
            return GrantRefusals.SuperAdminOnly;
        }

        return roleCodes.All(granter.Codes.Contains) ? null : GrantRefusals.HoldsMore;
    }

    /// <summary>Nobody changes the role of their own account (ق-55).</summary>
    public static bool IsOwnAccount(Granter granter, Guid targetOperatorId) => granter.OperatorId == targetOperatorId;

    /// <summary>May the granter tick this permission in a role?</summary>
    public static bool MayAdd(Granter granter, string code) => granter.IsSuperAdmin || granter.Codes.Contains(code);

    /// <summary>
    /// The codes a save would add that the granter may not add. What the role already held
    /// stays, even where the granter does not hold it (ق-63); what a code needs is added with it
    /// and judged with it.
    /// </summary>
    public static IReadOnlyList<string> NotAddable(Granter granter, IEnumerable<string> held, IEnumerable<string> wanted)
    {
        ArgumentNullException.ThrowIfNull(granter);
        var before = held.ToHashSet(StringComparer.Ordinal);
        var after = OperatorPermissions.WithRequired(wanted);
        return [.. after.Where(code => !before.Contains(code) && !MayAdd(granter, code))];
    }

    /// <summary>Nobody but «مسؤول عام» edits the role they hold (ق-63).</summary>
    public static bool MayEdit(Granter granter, Guid roleId) => granter.IsSuperAdmin || granter.RoleId != roleId;

    /// <summary>
    /// Copying a role takes those of its permissions the creator holds, and says what was left
    /// (ق-66). A code is kept only with what it needs: a write is not copied without its view.
    /// </summary>
    public static (IReadOnlyList<string> Taken, IReadOnlyList<string> Left) Copy(Granter granter, IEnumerable<string> sourceCodes)
    {
        ArgumentNullException.ThrowIfNull(granter);
        var source = OperatorPermissions.InOrder(sourceCodes);
        var taken = source
            .Where(code => MayAdd(granter, code))
            .Where(code => OperatorPermissions.Require(code).Needs is not { } needed || MayAdd(granter, needed))
            .ToList();
        return (taken, [.. source.Except(taken, StringComparer.Ordinal)]);
    }
}
