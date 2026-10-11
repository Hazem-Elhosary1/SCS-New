using Scs.SharedKernel;

namespace Scs.Identity.Domain;

/// <summary>Where a role came from.</summary>
public static class RoleCategories
{
    /// <summary>Created with the clinic.</summary>
    public const string Ready = "ready";

    /// <summary>Created by the clinic.</summary>
    public const string Custom = "custom";
}

/// <summary>The three roles every clinic starts with (SH-052).</summary>
public static class ReadyRoleKeys
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Doctor = "doctor";
}

/// <summary>
/// A clinic's named set of permissions (SH-050): nothing more. Every member of the team holds
/// at most one. Clinic-owned: another clinic's role does not exist here.
/// </summary>
public sealed class Role : IClinicOwned
{
    public const int NameMaxLength = 40;
    public const int DescriptionMaxLength = 200;

    private Role()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>Unique within the clinic (ق-54).</summary>
    public string Name { get; private set; } = "";

    public string? Description { get; private set; }

    public string Category { get; private set; } = RoleCategories.Custom;

    /// <summary>Which of the three ready roles this is; null for a custom role.</summary>
    public string? ReadyKey { get; private set; }

    /// <summary>
    /// False while a ready role still has the name it was created with: the screen then shows
    /// the name for its key in the page's language.
    /// </summary>
    public bool IsRenamed { get; private set; }

    /// <summary>
    /// «مالك العيادة»: holds whatever the catalogue holds, today and after any release, and is
    /// never edited, renamed or removed (ق-28). It stores no permission rows.
    /// </summary>
    public bool HoldsEverything { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null when the system created it.</summary>
    public Guid? CreatedByAccountId { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByAccountId { get; private set; }

#pragma warning disable CA1819 // The concurrency token is a byte array by the database's definition.
    public byte[] RowVersion { get; private set; } = [];
#pragma warning restore CA1819

    public List<RolePermission> Permissions { get; private set; } = [];

    public static Role Ready(string key, string name, string description, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Description = description,
        Category = RoleCategories.Ready,
        ReadyKey = key,
        HoldsEverything = key == ReadyRoleKeys.Owner,
        CreatedAtUtc = now,
    };

    public static Role Custom(string name, string? description, Guid? createdBy, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Description = description,
        Category = RoleCategories.Custom,
        IsRenamed = true,
        CreatedByAccountId = createdBy,
        CreatedAtUtc = now,
    };

    /// <summary>Replaces what the role is called and what it says about itself.</summary>
    public void Describe(string name, string? description, Guid? by, DateTimeOffset now)
    {
        IsRenamed |= !string.Equals(Name, name, StringComparison.Ordinal);
        Name = name;
        Description = description;
        UpdatedByAccountId = by;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Replaces the set the role holds and says what changed, for the record of spec 007. The
    /// caller has checked the set (the requirement rule) before this.
    /// </summary>
    public (IReadOnlyList<string> Added, IReadOnlyList<string> Removed) Hold(IReadOnlyCollection<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        if (HoldsEverything)
        {
            throw new InvalidOperationException("The owner role holds the whole catalogue; it has no set of its own.");
        }

        var wanted = codes.ToHashSet(StringComparer.Ordinal);
        var removed = Permissions.Where(p => !wanted.Contains(p.Code)).ToList();
        var added = wanted.Except(Permissions.Select(p => p.Code), StringComparer.Ordinal).ToList();

        foreach (var gone in removed)
        {
            Permissions.Remove(gone);
        }

        foreach (var code in added)
        {
            Permissions.Add(new RolePermission(Id, code));
        }

        return (added, [.. removed.Select(p => p.Code)]);
    }
}

/// <summary>One permission a role holds, by its code in the catalogue.</summary>
#pragma warning disable CA1711 // "Permission" is the BRD's word for exactly this; it is not a code-access permission.
public sealed class RolePermission : IClinicOwned
#pragma warning restore CA1711
{
    public const int CodeMaxLength = 64;

    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, string code)
    {
        RoleId = roleId;
        Code = code;
    }

    public Guid RoleId { get; private set; }

    public string Code { get; private set; } = "";

    public Guid ClinicId { get; private set; }
}
