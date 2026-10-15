using Scs.SharedKernel.Access;

namespace Scs.Operators.Domain;

/// <summary>One permission a role holds: a code of the operators' catalogue.</summary>
internal sealed class RolePermission
{
    public const int CodeMaxLength = 40;

    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, string code)
    {
        RoleId = roleId;
        Code = OperatorPermissions.Require(code).Code;
    }

    public Guid RoleId { get; private set; }

    public string Code { get; private set; } = "";
}

/// <summary>
/// A role of the operators console (spec 011, FR-010 to FR-012; BRD chapter 17 §3-10): a name,
/// a description and a set of permissions from the closed catalogue. Three are the platform's
/// own and never change; the others are made by operators. A deleted role is hidden, not
/// erased: what was recorded about it keeps its name.
/// </summary>
internal sealed class Role
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>As written, shown in both languages. Empty for a system role: its name is the console's.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The name trimmed and lower-cased: unique among roles that are not deleted.</summary>
    public string NameKey { get; private set; } = "";

    public string? Description { get; private set; }

    /// <summary><c>super-admin</c>, <c>billing-admin</c>, <c>viewer</c>, or null for a custom role.</summary>
    public string? SystemKey { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<RolePermission> Permissions => _permissions;

    public bool IsSystem => SystemKey is not null;

    public bool IsSuperAdmin => SystemKey == OperatorPermissions.SystemRoles.SuperAdmin;

    public bool IsDeleted => DeletedAtUtc is not null;

    public IReadOnlyList<string> Codes => OperatorPermissions.InOrder(_permissions.Select(p => p.Code));

    public static string KeyOf(string? name) => (name ?? "").Trim().ToLowerInvariant();

    /// <summary>A custom role: at least one permission, kept with what its permissions need.</summary>
    public static Role Create(string name, string? description, IEnumerable<string> codes, Guid createdBy, DateTimeOffset now)
    {
        var role = new Role { Id = Guid.CreateVersion7(), CreatedAtUtc = now, CreatedById = createdBy };
        role.Describe(name, description);
        role.Hold(codes);
        return role;
    }

    /// <summary>The name and description of a custom role.</summary>
    public void Describe(string name, string? description)
    {
        RefuseForSystem();

        var trimmed = (name ?? "").Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw new ArgumentException("A role's name has 1 to 100 characters.", nameof(name));
        }

        var words = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (words is { Length: > DescriptionMaxLength })
        {
            throw new ArgumentException("A role's description has at most 500 characters.", nameof(description));
        }

        Name = trimmed;
        NameKey = KeyOf(trimmed);
        Description = words;
    }

    /// <summary>Replaces what a custom role holds with the codes given and what they need.</summary>
    public void Hold(IEnumerable<string> codes)
    {
        RefuseForSystem();

        var kept = OperatorPermissions.WithRequired(codes);
        if (kept.Count == 0)
        {
            throw new ArgumentException("A role holds at least one permission.", nameof(codes));
        }

        _permissions.RemoveAll(permission => !kept.Contains(permission.Code));
        foreach (var code in kept.Where(code => _permissions.TrueForAll(p => p.Code != code)))
        {
            _permissions.Add(new RolePermission(Id, code));
        }
    }

    /// <summary>Hides a custom role. Whether anyone holds it is the caller's to check first.</summary>
    public void Delete(DateTimeOffset now)
    {
        RefuseForSystem();
        DeletedAtUtc ??= now;

        // The name is free again for a new role; this one keeps what it was called.
        NameKey = $"{NameKey}|{Id:N}";
    }

    private void RefuseForSystem()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("A system role is not edited or deleted.");
        }
    }
}

/// <summary>The departments an operator may be in (BRD chapter 17 §3-9): a closed list.</summary>
public static class OperatorDepartments
{
    public const string Support = "support";
    public const string Billing = "billing";
    public const string Success = "success";
    public const string Sales = "sales";
    public const string Management = "management";

    public static IReadOnlyList<string> All { get; } = [Support, Billing, Success, Sales, Management];
}
